// Online play: friends in one room through Unity Relay, everyone in the same seeded ocean.
//
// One player hosts: he signs in to Unity Gaming Services (anonymously), asks Relay for a room and
// gets a six-letter code to tell his friends. They type it in, Relay puts them through to him, and
// from then on the host passes everything along -- each surfer sends where he is about fifteen
// times a second and the host hands it to the rest. Everyone rides his own board on his own phone;
// the others are drawn from what they send (Multi, CrewView), as the bots are.
//
// Unity's own Relay calls are made over the web (UnityWebRequest), so the only package this needs
// is Unity Transport. The project needs a Unity Cloud project with Relay switched on: link it in
// Edit > Project Settings > Services, or put the project's id in Resources/online_project.txt.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using UnityEngine;
using UnityEngine.Networking;

/// one player in the room, as the room knows him
public class NetPlayer
{
    public int id; public string name = "", rider = "pug", board = "astro", eyes = "", head = "", neck = "", body = "";
}

public class Net : MonoBehaviour
{
    public Multi mp; public BoardController rider;
    public System.Action<MpMode, int> onStart;     // the host has started a round: (mode, ocean seed)
    public System.Action onChanged;                // the room changed: who is in, the code, the status
    public System.Action<string> onLost;           // the room is gone (the host left, the connection dropped)

    public const int MaxPlayers = 4;
    public readonly List<NetPlayer> players = new List<NetPlayer>();
    public int MyId { get; private set; }
    public bool IsHost { get; private set; }
    public bool Connected => driver.IsCreated && MyId > 0;
    public bool Busy { get; private set; }         // signing in, asking Relay, connecting
    public string Code { get; private set; } = "";
    public string Status { get; private set; } = "";

    NetworkDriver driver; NetworkPipeline reliable;
    readonly List<NetworkConnection> conns = new List<NetworkConnection>();   // host: everyone connected to him
    readonly Dictionary<int, int> connOf = new Dictionary<int, int>();         // host: player id -> index in conns
    NetworkConnection toHost;                                                  // a client: his one connection
    string token; float sendT;
    readonly byte[] buf = new byte[1024];

    // message types
    const byte Hello = 1, Welcome = 2, Roster = 3, Start = 4, State = 5, Hit = 6, KO = 7, LeftRound = 8;

    const string AuthUrl = "https://player-auth.services.api.unity.com/v1/authentication/anonymous";
    const string RelayUrl = "https://relay-allocations.services.api.unity.com/v1/";

    // =====================================================================================
    // ---- the room ----

    /// host a room: sign in, ask Relay for an allocation and its join code, and wait for friends
    public void Host() { if (!Busy && !driver.IsCreated) StartCoroutine(HostRoutine()); }

    /// join a friend's room by its code
    public void Join(string code)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        if (code.Length < 4) { Say("Type the room code your friend sees"); return; }
        if (!Busy && !driver.IsCreated) StartCoroutine(JoinRoutine(code));
    }

    /// leave the room (and close it, for the host)
    public void Leave()
    {
        StopAllCoroutines(); Busy = false;
        if (driver.IsCreated)
        {
            if (IsHost) { foreach (var c in conns) if (c.IsCreated) driver.Disconnect(c); }
            else if (toHost.IsCreated) driver.Disconnect(toHost);
            driver.ScheduleUpdate().Complete();
            driver.Dispose();
        }
        driver = default; conns.Clear(); connOf.Clear(); toHost = default;
        players.Clear(); MyId = 0; IsHost = false; Code = ""; Status = "";
        onChanged?.Invoke();
    }

    /// the host starts a round for everyone in the room
    public void StartRound(MpMode mode)
    {
        if (!IsHost || !Connected) return;
        int seed = Random.Range(1, 1 << 30);
        var w = Begin(Start); w.Write((byte)mode); w.Write(seed);
        SendAll(w, true);
        onStart?.Invoke(mode, seed);
    }

    /// everyone in the room but you, for the round (Multi.PrepareOnline)
    public List<NetPlayer> Others() { var l = new List<NetPlayer>(); foreach (var p in players) if (p.id != MyId) l.Add(p); return l; }

    void OnDestroy() { if (driver.IsCreated) { driver.ScheduleUpdate().Complete(); driver.Dispose(); } }

    void Say(string s) { Status = s; onChanged?.Invoke(); }

    NetPlayer Me()
    {
        var s = SaveData.S;
        return new NetPlayer { name = SaveData.NameOr, rider = s.rider, board = s.equipped, eyes = s.gearEyes ?? "", head = s.gearHead ?? "", neck = s.gearNeck ?? "", body = s.gearBody ?? "" };
    }

    // =====================================================================================
    // ---- signing in and Relay, over the web ----

    [System.Serializable] class AuthResp { public string idToken; public string userId; }
    [System.Serializable] class Endpoint { public string connectionType; public string host; public int port; public bool secure; }
    [System.Serializable] class Alloc { public string allocationId, allocationIdBytes, key, connectionData, hostConnectionData; public Endpoint[] serverEndpoints; }
    [System.Serializable] class Data { public Alloc allocation; public string joinCode; }
    [System.Serializable] class Resp { public Data data; }

    // the Unity Cloud project: a file in Resources first (easy to set), else the linked project
    static string ProjectId()
    {
        var t = Resources.Load<TextAsset>("online_project");
        if (t != null && !string.IsNullOrWhiteSpace(t.text)) return t.text.Trim();
        return Application.cloudProjectId;
    }

    // a phone's browser build reaches Relay over secure web sockets; everything else over DTLS
#if UNITY_WEBGL && !UNITY_EDITOR
    const string Kind = "wss";
#else
    const string Kind = "dtls";
#endif

    string err;
    IEnumerator Post(string url, string json, System.Action<string> done, bool auth)
    {
        using (var r = new UnityWebRequest(url, "POST"))
        {
            r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            r.downloadHandler = new DownloadHandlerBuffer();
            r.SetRequestHeader("Content-Type", "application/json");
            if (auth) r.SetRequestHeader("Authorization", "Bearer " + token);
            else r.SetRequestHeader("ProjectId", ProjectId());
            r.timeout = 15;
            yield return r.SendWebRequest();
            if (r.result != UnityWebRequest.Result.Success)
            {
                string body = r.downloadHandler != null ? r.downloadHandler.text : "";
                err = r.responseCode + " " + r.error + (string.IsNullOrEmpty(body) ? "" : ": " + (body.Length > 160 ? body.Substring(0, 160) : body));
                Debug.LogWarning("Online: " + url + " failed: " + err);
                done(null);
            }
            else done(r.downloadHandler.text);
        }
    }

    IEnumerator SignIn()
    {
        if (!string.IsNullOrEmpty(token)) yield break;
        if (string.IsNullOrEmpty(ProjectId())) { err = "no Unity Cloud project linked"; yield break; }
        string res = null;
        yield return Post(AuthUrl, "{}", t => res = t, false);
        if (res == null) yield break;
        var a = JsonUtility.FromJson<AuthResp>(res);
        token = a != null ? a.idToken : null;
        if (string.IsNullOrEmpty(token)) err = "sign-in gave no token";
    }

    IEnumerator HostRoutine()
    {
        Busy = true; err = null; Say("Signing in...");
        yield return SignIn();
        if (string.IsNullOrEmpty(token)) { Fail("Couldn't sign in (" + err + ")"); yield break; }
        Say("Opening a room...");
        string res = null;
        yield return Post(RelayUrl + "allocate", "{\"maxConnections\":" + (MaxPlayers - 1) + "}", t => res = t, true);
        var alloc = res != null ? JsonUtility.FromJson<Resp>(res)?.data?.allocation : null;
        if (alloc == null) { Fail("Couldn't open a room (" + err + ")"); yield break; }
        res = null;
        yield return Post(RelayUrl + "joincode", "{\"allocationId\":\"" + alloc.allocationId + "\"}", t => res = t, true);
        var code = res != null ? JsonUtility.FromJson<Resp>(res)?.data?.joinCode : null;
        if (string.IsNullOrEmpty(code)) { Fail("Couldn't get a room code (" + err + ")"); yield break; }
        if (!MakeDriver(alloc, false)) yield break;
        if (driver.Bind(NetworkEndpoint.AnyIpv4) < 0 || driver.Listen() < 0) { Fail("Couldn't open the connection"); yield break; }
        IsHost = true; MyId = 1; Code = code;
        var me = Me(); me.id = 1; players.Clear(); players.Add(me);
        Busy = false; Say("Tell your friends the code");
    }

    IEnumerator JoinRoutine(string code)
    {
        Busy = true; err = null; Say("Signing in...");
        yield return SignIn();
        if (string.IsNullOrEmpty(token)) { Fail("Couldn't sign in (" + err + ")"); yield break; }
        Say("Finding room " + code + "...");
        string res = null;
        yield return Post(RelayUrl + "join", "{\"joinCode\":\"" + code + "\"}", t => res = t, true);
        var alloc = res != null ? JsonUtility.FromJson<Resp>(res)?.data?.allocation : null;
        if (alloc == null) { Fail("No room with that code (" + err + ")"); yield break; }
        if (!MakeDriver(alloc, true)) yield break;
        toHost = driver.Connect();
        if (!toHost.IsCreated) { Fail("Couldn't connect to the room"); yield break; }
        IsHost = false; Code = code;
        Say("Connecting...");
        // Busy until the host welcomes him (Receive)
        float t0 = Time.unscaledTime;
        while (MyId == 0 && driver.IsCreated)
        {
            if (Time.unscaledTime - t0 > 15f) { Fail("The room didn't answer"); yield break; }
            yield return null;
        }
        Busy = false;
    }

    bool MakeDriver(Alloc a, bool joining)
    {
        Endpoint ep = null;
        if (a.serverEndpoints != null) foreach (var e in a.serverEndpoints) if (e.connectionType == Kind) ep = e;
        if (ep == null) { Fail("Relay offered no " + Kind + " connection"); return false; }
        try
        {
            var data = new RelayServerData(ep.host, (ushort)ep.port,
                System.Convert.FromBase64String(a.allocationIdBytes), System.Convert.FromBase64String(a.connectionData),
                System.Convert.FromBase64String(joining ? a.hostConnectionData : a.connectionData), System.Convert.FromBase64String(a.key),
                ep.secure, Kind == "wss");
            var settings = new NetworkSettings(Allocator.Temp);
            settings.WithRelayParameters(ref data);
#if UNITY_WEBGL && !UNITY_EDITOR
            driver = NetworkDriver.Create(new WebSocketNetworkInterface(), settings);
#else
            driver = NetworkDriver.Create(settings);
#endif
            reliable = driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
            return true;
        }
        catch (System.Exception ex) { Fail("Couldn't set up the connection: " + ex.Message); return false; }
    }

    void Fail(string why)
    {
        Debug.LogWarning("Online: " + why);
        Leave(); Busy = false; Status = why; onChanged?.Invoke();
    }

    // =====================================================================================
    // ---- each frame ----
    void Update()
    {
        if (!driver.IsCreated) return;
        driver.ScheduleUpdate().Complete();
        if (IsHost)
        {
            NetworkConnection c;
            while ((c = driver.Accept()).IsCreated) conns.Add(c);
            for (int i = 0; i < conns.Count; i++)
            {
                var cn = conns[i]; if (!cn.IsCreated) continue;
                NetworkEvent.Type ev;
                while ((ev = driver.PopEventForConnection(cn, out var reader)) != NetworkEvent.Type.Empty)
                {
                    if (ev == NetworkEvent.Type.Data) { int n = Read(ref reader); Receive(n, i); }
                    else if (ev == NetworkEvent.Type.Disconnect) { Dropped(i); break; }
                }
            }
        }
        else if (toHost.IsCreated)
        {
            NetworkEvent.Type ev;
            while ((ev = driver.PopEventForConnection(toHost, out var reader)) != NetworkEvent.Type.Empty)
            {
                if (ev == NetworkEvent.Type.Connect)
                {
                    var me = Me(); var w = Begin(Hello); WritePlayer(w, me); SendHost(w, true);
                }
                else if (ev == NetworkEvent.Type.Data) { int n = Read(ref reader); Receive(n, -1); }
                else if (ev == NetworkEvent.Type.Disconnect)
                {
                    bool wasIn = MyId > 0;
                    if (mp != null) mp.NetAllLeft();
                    Leave();
                    Status = wasIn ? "The host closed the room" : "Couldn't reach the room";
                    onLost?.Invoke(Status); onChanged?.Invoke();
                    return;
                }
            }
        }
        // where you are, about fifteen times a second, while a round is on
        if (Connected && mp != null && mp.Running && mp.Online)
        {
            sendT -= Time.unscaledDeltaTime;
            if (sendT <= 0f) { sendT = 1f / 15f; SendState(); }
        }
    }

    int Read(ref DataStreamReader r)
    {
        int n = Mathf.Min(r.Length, buf.Length);
        for (int i = 0; i < n; i++) buf[i] = r.ReadByte();
        return n;
    }

    // a client gone: out of the room and out of the round
    void Dropped(int i)
    {
        conns[i] = default;
        int gone = 0; foreach (var kv in connOf) if (kv.Value == i) gone = kv.Key;
        if (gone == 0) return;
        connOf.Remove(gone);
        players.RemoveAll(p => p.id == gone);
        var w = Begin(LeftRound); w.Write((byte)gone); SendAll(w, true);
        if (mp != null) mp.NetLeft(gone);
        BroadcastRoster();
        onChanged?.Invoke();
    }

    // =====================================================================================
    // ---- messages ----
    BinaryWriter Begin(byte type) { var w = new BinaryWriter(new MemoryStream(64)); w.Write(type); return w; }

    static void WriteStr(BinaryWriter w, string s)
    {
        var b = Encoding.UTF8.GetBytes(s ?? ""); int n = Mathf.Min(b.Length, 60);
        w.Write((byte)n); w.Write(b, 0, n);
    }
    static string ReadStr(BinaryReader r) { int n = r.ReadByte(); return Encoding.UTF8.GetString(r.ReadBytes(n)); }

    static void WritePlayer(BinaryWriter w, NetPlayer p)
    {
        w.Write((byte)p.id); WriteStr(w, p.name); WriteStr(w, p.rider); WriteStr(w, p.board);
        WriteStr(w, p.eyes); WriteStr(w, p.head); WriteStr(w, p.neck); WriteStr(w, p.body);
    }
    static NetPlayer ReadPlayer(BinaryReader r) =>
        new NetPlayer { id = r.ReadByte(), name = ReadStr(r), rider = ReadStr(r), board = ReadStr(r), eyes = ReadStr(r), head = ReadStr(r), neck = ReadStr(r), body = ReadStr(r) };

    void Send(NetworkConnection c, byte[] b, int n, bool rel)
    {
        if (!c.IsCreated) return;
        if (driver.BeginSend(rel ? reliable : NetworkPipeline.Null, c, out var w) < 0) return;
        for (int i = 0; i < n; i++) w.WriteByte(b[i]);
        driver.EndSend(w);
    }

    static byte[] Bytes(BinaryWriter w, out int n) { var ms = (MemoryStream)w.BaseStream; n = (int)ms.Length; return ms.GetBuffer(); }

    void SendAll(BinaryWriter w, bool rel, int except = -1)
    {
        var b = Bytes(w, out int n);
        if (IsHost) { for (int i = 0; i < conns.Count; i++) if (i != except) Send(conns[i], b, n, rel); }
        else Send(toHost, b, n, rel);
    }
    void SendHost(BinaryWriter w, bool rel) { var b = Bytes(w, out int n); Send(toHost, b, n, rel); }

    void BroadcastRoster()
    {
        var w = Begin(Roster); w.Write((byte)players.Count);
        foreach (var p in players) WritePlayer(w, p);
        SendAll(w, true);
    }

    // a message in: `from` is the sender's connection on the host, -1 on a client
    void Receive(int n, int from)
    {
        if (n < 1) return;
        BinaryReader r;
        try { r = new BinaryReader(new MemoryStream(buf, 0, n, false)); } catch { return; }
        byte type = r.ReadByte();
        try
        {
            switch (type)
            {
                case Hello:                                   // host: someone new
                    {
                        if (!IsHost || from < 0) return;
                        var p = ReadPlayer(r);
                        int id = 0; for (int k = 2; k <= MaxPlayers && id == 0; k++) if (!connOf.ContainsKey(k)) id = k;
                        if (id == 0 || (mp != null && mp.Running && mp.Online)) { driver.Disconnect(conns[from]); conns[from] = default; return; }   // full, or mid-round
                        p.id = id; connOf[id] = from; players.Add(p);
                        var w = Begin(Welcome); w.Write((byte)id); var b = Bytes(w, out int bn); Send(conns[from], b, bn, true);
                        BroadcastRoster();
                        onChanged?.Invoke();
                        break;
                    }
                case Welcome: MyId = r.ReadByte(); Say("In the room. Waiting for the host to start..."); break;
                case Roster:
                    {
                        int c = r.ReadByte(); players.Clear();
                        for (int k = 0; k < c; k++) players.Add(ReadPlayer(r));
                        onChanged?.Invoke();
                        break;
                    }
                case Start:
                    {
                        var mode = (MpMode)r.ReadByte(); int seed = r.ReadInt32();
                        onStart?.Invoke(mode, seed);
                        break;
                    }
                case State:
                    {
                        if (IsHost) Forward(n, from, false);
                        int id = r.ReadByte();
                        if (mp != null && id != MyId) mp.NetState(id, r);
                        break;
                    }
                case Hit:
                    {
                        if (IsHost) Forward(n, from, true);
                        int by = r.ReadByte(), victim = r.ReadByte(); bool kick = r.ReadByte() != 0; float dir = r.ReadSingle();
                        if (mp != null && victim == MyId) mp.NetHit(by, kick, dir);
                        break;
                    }
                case KO:
                    {
                        if (IsHost) Forward(n, from, true);
                        int victim = r.ReadByte(), by = r.ReadByte();
                        if (mp != null) mp.NetKO(victim, by);
                        break;
                    }
                case LeftRound:
                    {
                        if (IsHost) Forward(n, from, true);
                        int id = r.ReadByte();
                        if (mp != null && id != MyId) mp.NetLeft(id);
                        break;
                    }
            }
        }
        catch (EndOfStreamException) { }
    }

    // the host passes a client's message on to everyone else
    void Forward(int n, int from, bool rel)
    {
        if (from < 0) return;
        for (int i = 0; i < conns.Count; i++) if (i != from) Send(conns[i], buf, n, rel);
    }

    // ---- what Multi sends ----
    void SendState()
    {
        var w = Begin(State); w.Write((byte)MyId);
        mp.WriteLocalState(w);
        SendAll(w, false);
    }

    public void SendHit(int victim, bool kick, float dir)
    {
        if (!Connected) return;
        var w = Begin(Hit); w.Write((byte)MyId); w.Write((byte)victim); w.Write((byte)(kick ? 1 : 0)); w.Write(dir);
        SendAll(w, true);
    }

    public void SendKO(int victim, int by)
    {
        if (!Connected) return;
        var w = Begin(KO); w.Write((byte)victim); w.Write((byte)by);
        SendAll(w, true);
    }

    public void SendLeftRound()
    {
        if (!Connected) return;
        var w = Begin(LeftRound); w.Write((byte)MyId);
        SendAll(w, true);
    }
}

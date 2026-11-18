// The online wire on Unity Transport through Relay (see NetApi/Pipe.cs). Built only when the
// com.unity.transport package is installed (this assembly's define constraint).
using System.Collections.Generic;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using UnityEngine;

namespace SurfNet
{
    public class TransportPipe : IPipe
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { Pipes.Make = () => new TransportPipe(); }

        NetworkDriver driver; NetworkPipeline reliable;
        bool host;
        readonly List<NetworkConnection> conns = new List<NetworkConnection>();
        NetworkConnection toHost;
        readonly byte[] buf = new byte[1024];

        public bool IsCreated => driver.IsCreated;

        public string Open(string hostName, int port, byte[] allocationId, byte[] connectionData, byte[] hostConnectionData,
                           byte[] key, bool secure, bool webSocket, bool asHost)
        {
            try
            {
                var data = new RelayServerData(hostName, (ushort)port, allocationId, connectionData, hostConnectionData, key, secure, webSocket);
                var settings = new NetworkSettings(Allocator.Temp);
                settings.WithRelayParameters(ref data);
#if UNITY_WEBGL && !UNITY_EDITOR
                driver = NetworkDriver.Create(new WebSocketNetworkInterface(), settings);
#else
                driver = NetworkDriver.Create(settings);
#endif
                reliable = driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
                host = asHost;
                if (asHost)
                {
                    if (driver.Bind(NetworkEndpoint.AnyIpv4) < 0 || driver.Listen() < 0) { Close(); return "couldn't open the connection"; }
                }
                else
                {
                    toHost = driver.Connect();
                    if (!toHost.IsCreated) { Close(); return "couldn't connect to the room"; }
                }
                return null;
            }
            catch (System.Exception ex) { Close(); return ex.Message; }
        }

        public void Close()
        {
            if (driver.IsCreated)
            {
                foreach (var c in conns) if (c.IsCreated) driver.Disconnect(c);
                if (toHost.IsCreated) driver.Disconnect(toHost);
                driver.ScheduleUpdate().Complete();
                driver.Dispose();
            }
            driver = default; conns.Clear(); toHost = default;
        }

        public void Pump(System.Action<int> connected, System.Action<int, byte[], int> data, System.Action<int> disconnected)
        {
            if (!driver.IsCreated) return;
            driver.ScheduleUpdate().Complete();
            if (host)
            {
                NetworkConnection c;
                while ((c = driver.Accept()).IsCreated) { conns.Add(c); connected(conns.Count - 1); }
                for (int i = 0; i < conns.Count; i++)
                {
                    if (!conns[i].IsCreated) continue;
                    NetworkEvent.Type ev;
                    while (driver.IsCreated && conns[i].IsCreated && (ev = driver.PopEventForConnection(conns[i], out var reader)) != NetworkEvent.Type.Empty)
                    {
                        if (ev == NetworkEvent.Type.Data) data(i, buf, Read(ref reader));
                        else if (ev == NetworkEvent.Type.Disconnect) { conns[i] = default; disconnected(i); break; }
                    }
                }
            }
            else if (toHost.IsCreated)
            {
                NetworkEvent.Type ev;
                while (driver.IsCreated && toHost.IsCreated && (ev = driver.PopEventForConnection(toHost, out var reader)) != NetworkEvent.Type.Empty)
                {
                    if (ev == NetworkEvent.Type.Connect) connected(-1);
                    else if (ev == NetworkEvent.Type.Data) data(-1, buf, Read(ref reader));
                    else if (ev == NetworkEvent.Type.Disconnect) { toHost = default; disconnected(-1); break; }
                }
            }
        }

        int Read(ref DataStreamReader r)
        {
            int n = Mathf.Min(r.Length, buf.Length);
            for (int i = 0; i < n; i++) buf[i] = r.ReadByte();
            return n;
        }

        NetworkConnection Peer(int peer) => peer < 0 ? toHost : (peer < conns.Count ? conns[peer] : default);

        public void Send(int peer, byte[] bytes, int count, bool rel)
        {
            if (!driver.IsCreated) return;
            var c = Peer(peer); if (!c.IsCreated) return;
            if (driver.BeginSend(rel ? reliable : NetworkPipeline.Null, c, out var w) < 0) return;
            for (int i = 0; i < count; i++) w.WriteByte(bytes[i]);
            driver.EndSend(w);
        }

        public void Disconnect(int peer)
        {
            if (!driver.IsCreated) return;
            var c = Peer(peer); if (!c.IsCreated) return;
            driver.Disconnect(c);
            if (peer >= 0 && peer < conns.Count) conns[peer] = default; else toHost = default;
        }
    }
}

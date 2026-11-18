// The wire under online play, as the game sees it: open a Relay connection, send bytes to a peer,
// hear what comes back. The game (Net) only ever talks to this; the Unity Transport side of it
// (NetTransport) lives in its own assembly and is built only when that package is installed, so a
// project without it still compiles and runs, with online play saying what is missing.
namespace SurfNet
{
    public interface IPipe
    {
        bool IsCreated { get; }
        /// open a Relay connection: as the host (listening) or a client (connecting to the host).
        /// Returns null when it is open, else what went wrong.
        string Open(string host, int port, byte[] allocationId, byte[] connectionData, byte[] hostConnectionData,
                    byte[] key, bool secure, bool webSocket, bool asHost);
        void Close();
        /// one frame of the connection: who connected, what arrived, who left. A peer is a number
        /// on the host; on a client the host is peer -1.
        void Pump(System.Action<int> connected, System.Action<int, byte[], int> data, System.Action<int> disconnected);
        void Send(int peer, byte[] bytes, int count, bool reliable);
        void Disconnect(int peer);
    }

    public static class Pipes
    {
        /// set by the Unity Transport side when it is built in; null without it
        public static System.Func<IPipe> Make;
    }
}

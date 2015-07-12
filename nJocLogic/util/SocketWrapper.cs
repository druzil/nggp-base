using System.Net.Sockets;
using System.IO;

namespace nJocLogic.util
{
    class SocketWrapper : ISocketWrapper
    {
        readonly Socket _socket;

        public SocketWrapper(Socket socket)
        {
            _socket = socket;
        }

        public Stream GetStream()
        {
            return new NetworkStream(_socket);
        }

        public Socket Socket { get { return _socket; } }

        public void Close()
        {
            _socket.Close();
        }

        public TextWriter GetWriter()
        {
            return new StreamWriter(new NetworkStream(_socket));
        }
    }
}

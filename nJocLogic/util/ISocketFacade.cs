using System.IO;

namespace nJocLogic.util
{
    public interface ISocketWrapper
    {
        //private System.Net.Sockets.AddressFamily addressFamily;
        //private System.Net.Sockets.SocketType socketType;
        //private System.Net.Sockets.ProtocolType protocolType;

        //public ISocketFacade(System.Net.Sockets.AddressFamily addressFamily, System.Net.Sockets.SocketType socketType, System.Net.Sockets.ProtocolType protocolType)
        //{
        //    // TODO: Complete member initialization
        //    this.addressFamily = addressFamily;
        //    this.socketType = socketType;
        //    this.protocolType = protocolType;
        //}

        Stream GetStream();

        void Close();

        TextWriter GetWriter();
    }
}

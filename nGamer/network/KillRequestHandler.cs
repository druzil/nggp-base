namespace nGamer.network
{
    using nJocLogic.gdl;
    using nJocLogic.network;
    using nJocLogic.util;

    public class KillRequestHandler : RequestHandler
    {
        internal KillRequestHandler(ISocketWrapper socket, HttpHeader header, GdlList content, string matchId)
            : base(socket, header, matchId)
        {
        }

        protected override void Execute()
        {
            Manager.Shutdown();
        }
    }
}

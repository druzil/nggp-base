namespace nGamer.network
{
    using System;
    using nJocLogic.game;
    using nJocLogic.gdl;
    using nJocLogic.network;
    using nJocLogic.util;
    using NLog;

    public sealed class StartRequestHandler : RequestHandler
    {
        private readonly GdlList _content;

        private static readonly Logger Logger = LogManager.GetLogger("logic.network");

        internal StartRequestHandler(ISocketWrapper socket, HttpHeader header, GdlList content, string matchId)
            : base(socket, header, matchId)
        {
            _content = content;
        }

        protected override void Execute()
        {
            if (_content.Size != 6)
                throw new Exception("START request should have exactly six arguments, not " + _content.Size);

            var role = (GdlAtom)_content[2];
            var description = (GdlList)_content[3];

            int start = int.Parse(_content[4].ToString());
            int play = int.Parse(_content[5].ToString());

            IGamer gamer = GameManager.NewGame(GameId, role, description, start, play);

            if (gamer != null)
                Logger.Info(GameId + ": Game successfully created.");
            else
                Logger.Error(GameId + ": Could not create gamer from start message!");

            SendAnswer("READY");
            Finish();
        }
    }
}

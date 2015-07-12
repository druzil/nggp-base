namespace nGamer.network
{
    using System;
    using System.Text;
    using nJocLogic.game;
    using nJocLogic.gameContainer;
    using nJocLogic.gdl;
    using nJocLogic.network;
    using nJocLogic.util;
    using NLog;

    public sealed class PlayRequestHandler : RequestHandler
    {
        private readonly GdlList _content;

        private static readonly Logger Logger = LogManager.GetLogger("logic.network");

        internal PlayRequestHandler(ISocketWrapper socket, HttpHeader header, GdlList content, string matchId)
            : base(socket, header, matchId)
        {
            _content = content;
        }

        protected override void Execute()
        {
            if (_content.Size != 3)
                throw new Exception("PLAY request should have exactly three arguments, not " + _content.Size);

            GdlExpression prevExp = _content[2];
            GdlList prevMoves;

            var prevMovesStr = new StringBuilder();

            if (prevExp is GdlList)
            {
                prevMoves = (GdlList)_content[2];
                prevMovesStr.Append(" Previous moves: ");

                foreach (GdlExpression exp in prevMoves)
                {
                    prevMovesStr.Append(exp);
                    prevMovesStr.Append("  ");
                }
            }
            else
            {
                // make sure it's an atom containing NIL
                var prevAtom = prevExp as GdlAtom;
                if (prevAtom == null || prevAtom.GetToken() != GameContainer.Parser.TokNil)
                    throw new Exception("PLAY request doesn't have LIST and doesn't have NIL atom as prev-moves!");
                prevMoves = null; // empty prev moves
            }

            IGamer game = GameManager.GetGame(GameId);
            if (game == null)
            {
                Logger.Error("No game found for play request ID: " + GameId);
                Finish();
                return;
            }

            Logger.Info(GameId + ": Beginning move think." + prevMovesStr);

            Tuple<GdlExpression, String, String> next;

            try
            {
                next = game.Play(prevMoves);
            }
            catch (Exception e)
            {
                Logger.Debug(GameId + ": Exception while processing 'game.play':" + e.Message);

                Logger.Debug(GameId + ": " + e.StackTrace);

                var nil = new GdlAtom(GameContainer.SymbolTable, GameContainer.Parser.TokNil);
                next = new Tuple<GdlExpression, string, string>(nil, "exception", "Something bad happened");
            }

            String moveStr = next.Item1.ToString();
            Logger.Info(GameId + ": End of move think. Making move: " + moveStr);

            var answer = new StringBuilder(128);

            answer.Append(moveStr);

            // is there an explanation?
            if (next.Item2 != null)
            {
                answer.Append(" (explanation \"");
                answer.Append(Util.EscapeChars(next.Item2));
                answer.Append("\")");
            }

            SendAnswer(answer.ToString());
            Finish();
        }

    }

}

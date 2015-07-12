using nJocLogic.gameContainer;

namespace nGamer.network
{
    using nJocLogic.game;
    using nJocLogic.gdl;
    using nJocLogic.network;
    using nJocLogic.util;
    using NLog;

    public class StopRequestHandler : RequestHandler
{
    private readonly GdlList _content;
    
    private static readonly Logger Logger = LogManager.GetLogger("logic.network");

    internal StopRequestHandler(ISocketWrapper socket, HttpHeader header, GdlList content, string matchId)
        : base(socket, header, matchId)
    {        
        _content = content;
    }

    protected override void Execute()
    {
        // Tell the game manager that the game ended, passing in the previous moves
        GdlExpression prevMoves = _content.Arity>2 ? _content[2] : null;
        
        if (prevMoves is GdlList == false) {
            Logger.Error(GameId + ": Previous move list in STOP message was not a GDL list!");
            Finish();
            //GameContainer.Parser.Reset();
            return;
        }
        
        GameManager.EndGame(GameId, (GdlList) prevMoves);
        
        SendAnswer("DONE");
        Finish();
        //GameContainer.Parser.Reset();
    }
}
}

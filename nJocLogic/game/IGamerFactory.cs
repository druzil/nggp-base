namespace nJocLogic.game
{
    public interface IGamerFactory
    {
        IGamer MakeGamer(string gameId, gdl.GdlAtom role, gdl.GdlList description, int startClock, int playClock);
    }
}

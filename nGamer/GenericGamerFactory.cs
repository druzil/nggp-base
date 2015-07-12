using System;
using nJocLogic.data;
using nJocLogic.game;
using nJocLogic.gameContainer;
using nJocLogic.gdl;
using nJocLogic.knowledge;

namespace nGamer
{
    public class GenericGamerFactory<TGamerType> : IGamerFactory where TGamerType : IGamer
    {
        private static IGamer MakeGamer(string gameId, Parser parser)
        {
            try
            {
                return (IGamer)Activator.CreateInstance(typeof(TGamerType), gameId, parser);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                return null;
            }
        }

        public IGamer MakeGamer(String gameId, GdlAtom role, GdlList description, int startClock, int playClock)
        {
            GameContainer.Initialise(description);

            var parser = GameContainer.Parser;
            IGamer gamer = MakeGamer(gameId, parser);

            var myRole = (TermObject)Term.BuildFromGdl(role);

            GameInformation gameInfo = GameContainer.GameInformation;
            gamer.InitializeGame(myRole, playClock, gameInfo);

            return gamer;
        }
    }

}

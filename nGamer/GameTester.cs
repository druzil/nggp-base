using nJocLogic.gameContainer;
using nJocLogic.gdl;
using nJocLogic.game;
using System.Text;
using System;
using System.IO;
using nJocLogic.util;

namespace nGamer
{
    using System.Linq;

    public class GameTester
    {
        public GameTester(string kifFile, string role, int startClock, int playClock)
        {
            Parser parser = GameContainer.Parser;

            // Load up the kif
            GdlList gameDesc = parser.Parse(new StreamReader(kifFile));

            // if there is no role, find the first 'role' expression and use that
            if (role.Length == 0)
                foreach (GdlAtom roleAtom in from l in gameDesc.OfType<GdlList>()
                                             where l[0].Equals(new GdlAtom(parser.SymbolTable, parser.TokRole))
                                             select (GdlAtom)l[1])
                {
                    role = roleAtom.ToString();
                    break;
                }

            // Create the start message
            string msg = MakeMessage("(start foo " + role + " " + gameDesc + " " + startClock + " " + playClock + ")");

            // Send the play message
            SendMessage(msg);
        }

        public void SendMessage(string msg)
        {
            // THINK: do we need a dummy connection manager instead of null?
            nJocLogic.network.RequestHandler req = new network.RequestHandlerFactory().CreateRequestHandler(null, new StringSocket(msg, Console.Out));

            GameManager.NewRequest(req);
        }

        public string MakeMessage(string content)
        {
            var sb = new StringBuilder();

            sb.Append("POST / HTTP/1.0\n");
            sb.Append("Accept: text/delim\n");
            sb.Append("Sender: GAMEMASTER\n");
            sb.Append("Receiver: GAMEPLAYER\n");
            sb.Append("Content-type: text/acl\n");
            sb.Append("Content-length: " + content.Length + "\n");

            sb.Append("\n");

            sb.Append(content);

            return sb.ToString();
        }
    }
}

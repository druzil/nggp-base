using nJocLogic.game;
using System.IO;
using System;
using System.Collections.Generic;
using nJocLogic.network;
using System.Threading;
using System.Linq;

namespace nGamer
{
    class Program
    {
        public const int DefaultPort = 4001;

        public static void Server(String[] args)
        {
            bool daemonMode = false;
            int port = DefaultPort;

            try
            {
                for (int i = 1; i < args.Length; i++)
                {
                    if (args[i] == "--daemon")
                        daemonMode = true;
                    else if (args[i] == "--port")
                        port = int.Parse(args[++i]);
                    else
                        throw new Exception();
                }
            }
            catch
            {
                Console.WriteLine("usage: <--daemon> <--port='portNum'>");
                return;
            }

            if (!daemonMode)
            {
                Console.WriteLine(" ########################################");
                Console.WriteLine(" # Press Enter to shut the player down. #");
                Console.WriteLine(" ########################################");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine(" ############################################################");
                Console.WriteLine(" # Send \"(KILL abc)\" on port " + port + " to shut the player down. #");
                Console.WriteLine(" ############################################################");
                Console.WriteLine();
            }

            // TODO: read gamer config from file

            IGamerFactory factory = GetGamerFactory(); 
            GameManager.SetGamerFactory(factory);

            try
            {
                var manager = new ConnectionManager(port, new network.RequestHandlerFactory());
                var managerThread = new Thread(manager.Run);
                managerThread.Start();

                if (!daemonMode)
                {
                    // Wait for input to kill the program
                    Console.Read();
                    manager.Shutdown();
                    managerThread.Abort();
                }
                else
                    managerThread.Join();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
        }

        static void Main(string[] args)
        {
            if (args.Contains("--daemon") || args.Contains("--port") )
                Server(args);
            else
                RunTest(args);      // game-defs/alquerque.kif red log 10 10
            //RunTest(args);      // game-defs/9tictactoe.kif xPlayer log 10 10
            //game-defs/checkerBarrel.kif black log 10 10
            //game-defs/connectFour.kif white log 10 10
            //game-defs/hunter.kif robot log 10 10
            //game-defs/tictactoe.kif xPlayer log 10 10
            //game-defs/breakthrough.kif white log 10 10
            //game-defs/checkers.kif red log 10 10

            //Server(args);     // --daemon
        }

        static IGamerFactory GetGamerFactory()
        {          
            return new GenericGamerFactory<MinimaxGamer>();
        }

        static void RunTest(string[] args)
        {
            //Main.setupLoggerProperties();

            IGamerFactory factory = GetGamerFactory();
            GameManager.SetGamerFactory(factory);

            //String logFile;

            TextReader input = Console.In;

            if (args.Length == 0)
                args = GetArgs(input);

            string kif = args[0];
            string role = args[1];
            //logFile = args[2];
            int startClock = int.Parse(args[3]);    //=100
            int playClock = int.Parse(args[4]);     //=60

            // Set up debug file
            //PrintStream debugStream = new PrintStream(logFile);
            //GameManager.debugStream_ = debugStream;

            var tester = new GameTester(kif, role, startClock, playClock);

            // Go into main loop.
            while (true)
            {
                string line = input.ReadLine() ?? string.Empty;

                if (line.Equals("quit"))
                    break;

                if (line.Equals("stats"))
                    GameManager.PrintTimeStats(Console.Out);

                else if (line.Equals("play"))
                    tester.SendMessage(tester.MakeMessage("(play foo nil)"));

                else
                {
                    string msg = tester.MakeMessage(line);
                    tester.SendMessage(msg);
                }
            }

            tester.SendMessage(tester.MakeMessage("(stop foo)"));
        }

        private static string[] GetArgs(TextReader input)
        {
            var args = new List<string>();

            // Get the game's .kif file
            Console.Write("Game file (game-defs/*.kif): ");
            string game = input.ReadLine();
            args.Add("game-defs/" + game + ".kif");

            Console.Write("Role (default: 1st role): ");
            args.Add(input.ReadLine());

            args.Add("");

            Console.Write("Start clock (default: 100): ");
            String start = input.ReadLine() ?? string.Empty;

            args.Add(start.Length == 0 ? "100" : start);

            Console.Write("Play clock (default: 60): ");
            String play = input.ReadLine() ?? string.Empty;

            args.Add(play.Length == 0 ? "60" : play);

            return args.ToArray();
        }
    }
}

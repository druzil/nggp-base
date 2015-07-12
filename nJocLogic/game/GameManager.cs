using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using C5;
using nJocLogic.gdl;
using nJocLogic.network;

namespace nJocLogic.game
{
    public static class GameManager
    {
        /// <summary>
        /// The factory used to create games from descriptions.
        /// </summary>
        private static IGamerFactory _gamerFactory;

        /// <summary>
        /// Maps a game ID to a gamer for currently active games.
        /// </summary>        
        private static readonly TreeDictionary<string, IGamer> Games = new TreeDictionary<string, IGamer>();

        private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger("logic.game");

        // These get automatically initialized to zero.
        readonly private static long[] StatisticsTime = new long[5];
        readonly private static int[] StatisticsNumber = new int[5];

        public const int TimeMetagdl = 0;
        public const int TimeGetAnAnswer = 1;
        public const int TimeGetAllAnswers = 2;

        public static void AddTime(int category, long howMuch)
        {
            StatisticsTime[category] += howMuch;
            StatisticsNumber[category]++;
        }

        public static double GetAverageTime(int category)
        {
            if (StatisticsNumber[category] == 0)
                return 0;
            return ((double)StatisticsTime[category]) / StatisticsNumber[category];
        }

        public static long GetTotalTime(int category)
        {
            return StatisticsTime[category];
        }

        public static int GetNumTime(int category)
        {
            return StatisticsNumber[category];
        }

        public static string GetCategoryName(int category)
        {
            switch (category)
            {
                case TimeMetagdl:
                    return "MetaGDL";
                case TimeGetAnAnswer:
                    return "GetAnAnswer";
                case TimeGetAllAnswers:
                    return "GetAllAnswers";
                default:
                    return "Unknown!!";
            }
        }

        public static void PrintTimeStats(TextWriter output)
        {
            for (int i = 0; i <= TimeGetAllAnswers; i++)
            {
                output.Write(GetCategoryName(i));
                output.Write(": ");
                output.Write(((double)GetTotalTime(i)) / 1000000);
                output.Write(" ms (n = ");
                output.Write(GetNumTime(i));
                output.Write("; average = ");
                output.Write(GetAverageTime(i) / 1000000);
                output.WriteLine(" ms)");
            }
        }

        public static IGamerFactory GetGamerFactory()
        {
            return _gamerFactory;
        }

        public static void SetGamerFactory(IGamerFactory gamerFactory)
        {
            _gamerFactory = gamerFactory;
        }

        public static void NewRequest(RequestHandler handler)
        {
            try
            {
                var handlerThread = new Thread(handler.Run);
                handlerThread.Start();
            }
            catch (Exception e)
            {
                Logger.Fatal("Network failure: " + e.Message);
            }
        }

        /// <summary>
        /// Starts a new game. Takes the information from the start message
        /// and gives it to the gamer factory, which creates an appropriate
        /// gamer for the game.
        /// see stanfordlogic.Gamer
        /// </summary>
        /// <param name="gameId">The identifier of the game being started.</param>
        /// <param name="role">The role I am playing in the new game.</param>
        /// <param name="description">The description (rules) of the game.</param>
        /// <param name="startClock">The time given to think about the game.</param>
        /// <param name="playClock">The time given to make a move.</param>
        /// <returns>The Gamer instance created to play the game.</returns>
        public static IGamer NewGame(String gameId, GdlAtom role, GdlList description, int startClock, int playClock)
        {
            lock (typeof(GameManager))
            {
                if (_gamerFactory == null)
                {
                    Logger.Error("No gamer factory set!");
                    return null;
                }

                // Make sure this game isn't already active:
                if (Games.Contains(gameId))
                {
                    Logger.Error("Game already active: " + gameId);
                    return null;
                }

                Logger.Info("");
                Logger.Info("-----------------------------------------------");
                Logger.Info("NEW GAME! " + gameId);
                Logger.Info("");
                Logger.Info("    My role : " + role);
                Logger.Info("Start clock : " + startClock);
                Logger.Info(" Play clock : " + playClock);
                Logger.Info("");

                // put in a temporary game:
                Games[gameId] = null;
            }

            IGamer g = _gamerFactory.MakeGamer(gameId, role, description, startClock, playClock);

            lock (typeof(GameManager))
            {
                Games[gameId] = g;
            }

            return g;
        }

        /// <summary>
        /// Ends the game specified by <tt>gameId</tt>. The final moves are
        /// processed and the payoffs are computed and printed to the logger.
        /// </summary>
        /// <param name="gameId">Name of the game to terminate.</param>
        /// <param name="prevMoves">The last set of moves made in the game.</param>
        public static void EndGame(String gameId, GdlList prevMoves)
        {
            IGamer gamer = Games[gameId];

            if (gamer == null)
            {
                Logger.Error(gameId + ": WARNING: Attempting to terminate game [" + gameId + "], but no such game");
                return;
            }

            try
            {
                var prevMovesStr = new StringBuilder();
                prevMovesStr.Append(" Previous moves: ");
                foreach (GdlExpression exp in prevMoves)
                {
                    prevMovesStr.Append(exp);
                    prevMovesStr.Append("  ");
                }

                Logger.Info(gameId + ": Beginning payoff computation." + prevMovesStr);

                // Get the list of payoffs: <Role, Payoff, IsMe>
                List<PayOff> results = gamer.GetPayoffs(prevMoves);

                // Figure out what the longest role is
                int maxRoleLength = results.Select(res => res.PlayerName.Length).Concat(new[] { 0 }).Max();

                // Print out the payoffs
                foreach (PayOff res in results)
                {
                    // print the right amount of spaces (so that things line up right)
                    var spacing = new StringBuilder();
                    for (int i = 0; i < maxRoleLength - res.PlayerName.Length; i++)
                        spacing.Append(" ");

                    Logger.Info("       " + (res.PlayedRole ? "->" : "  ") + " " + res.PlayerName + spacing + " " + res.Payoff + " " + (res.PlayedRole ? "<-" : "  "));
                }
            }
            catch (Exception e)
            {
                Logger.Fatal(gameId + ": Error computing payoff: " + e.GetType().Name + " - " + e.Message);
            }

            // tell the game it's time to die.
            gamer.StopIt();
            Games.Remove(gameId);
        }

        public static IGamer GetGame(string gameId)
        {
            IGamer game;
            Games.Find(ref gameId, out game);
            return game;
        }

    }
}

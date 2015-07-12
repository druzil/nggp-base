using System;
using System.Collections.Generic;
using System.Linq;
using nJocLogic.data;
using nJocLogic.game;
using nJocLogic.gdl;
using nJocLogic.util;
using NLog;

namespace nGamer
{
    public class MinimaxGamer : Gamer
    {
        private static readonly Logger Logger = LogManager.GetLogger("logic.game");
        private static readonly Logger SearchLogger = LogManager.GetLogger("logic.game.search");

        private readonly System.Timers.Timer _searchTimer;

        private bool _stopSearch;

        /** The best move so far at the root search node. */
        private Term _bestMoveSoFar;
        /** The best score so far at the root search node. */
        private int _bestScoreSoFar;

        private class TimeoutException : Exception { }

        public MinimaxGamer(string gameId, Parser p)
            : base(gameId, p)
        {
            Random = new Random();
            _searchTimer = new System.Timers.Timer();
            _searchTimer.Elapsed += searchTimer__Elapsed;
        }

        void searchTimer__Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            StopIt();
            _searchTimer.Stop();
        }

        protected override Tuple<Term, string, string> MoveThink()
        {
            _searchTimer.Stop();
            _stopSearch = false;
            _bestMoveSoFar = null;
            _bestScoreSoFar = int.MinValue;

            // Set up the play clock:
            long time = (PlayClock - 2) * 1000; // convert to ms, giving ourselves a 2 second margin
            _searchTimer.Interval = time;
            _searchTimer.Start();

            // We now have playClock-2 seconds to finish our search.
            Pair<Term, int> searchResult;

            HashSet<Fact> rootContext = new HashSet<Fact>();
            foreach (Fact ground in CurrentContext)
                rootContext.Add(ground);

            // Run the minimax search.

            try
            {
                searchResult = MinimaxSearch(rootContext, 0);
            }
            catch (TimeoutException)
            {
                // We timed out -- default to whatever was registered as the best move so far
                Logger.Debug(GameId + ": Search clock expired, using fallback move.");
                searchResult = new Pair<Term, int>(_bestMoveSoFar, -1);
            }

            if (searchResult == null || searchResult.first == null)
            {
                // This is really really bad.
                Logger.Error(GameId + ": No move returned by minimax search");
                return null;
            }

            Term action = searchResult.first;

            string explanation = getExplanation(searchResult);

            const string taunt = "is good move"; // getTaunt(searchResult.second);

            return new Tuple<Term, string, string>(action, explanation, taunt);
        }

        private String getExplanation(Pair<Term, int> searchResult)
        {
            if (searchResult.second >= 0)
                return "Minimax score is " + searchResult.second;
            return "Timed out; using fallback move.";
        }

        private Pair<Term, int> MinimaxSearch(HashSet<Fact> context, int currentDepth)
        {
            // is this a terminal node?
            Fact isTerminal = Prover.AskOne(QueryTerminal, context);

            if (isTerminal != null)
                return CalculateTerminal(context, currentDepth);

            // Not terminal, so do the minimax search.
            // Build a list of everybody's moves.

            var otherMoves = new List<List<GroundFact>>();
            List<GroundFact> myMoves = null;

            for (int i = 0; i < Roles.Count; i++)
            {
                TermObject role = Roles[i];
                List<GroundFact> roleMoves = GetAllAnswers(context, "legal", role.ToString(), "?x").Cast<GroundFact>().ToList();

                if (roleMoves.Count == 0)
                    Logger.Debug(GameId + ": role " + role + " had no legal moves!");

                if (i == MyRoleIndex)
                {
                    myMoves = roleMoves;

                    if (currentDepth == 0 && _bestMoveSoFar == null)
                        // pick a random first move if we don't have one yet
                        _bestMoveSoFar = myMoves[Random.Next(myMoves.Count)].GetTerm(1);
                }
                else
                    otherMoves.Add(roleMoves.ToList());
            }

            // Pick my move that maximizes my score, assuming all other players
            // are trying to minimize it.
            Pair<Term, int> move =
                    FindMaximalMove(context, myMoves, otherMoves, currentDepth);

            return move;
        }

        private Pair<Term, int> CalculateTerminal(HashSet<Fact> context, int currentDepth)
        {
            // figure out my score in this outcome
            Fact myGoal = GetAnAnswer(context, "goal", MyRoleStr, "?x");

            int myScore = int.MinValue;

            if (myGoal == null)
                Logger.Error(GameId + ": No goal for me (" + MyRoleStr + "); using Integer.MIN_VALUE");
            else
            {
                try
                {
                    myScore = int.Parse(myGoal.GetTerm(1).ToString());
                }
                catch (Exception)
                {
                    Logger.Error(GameId + ": My goal (" + MyRoleStr + ") was not a number; was: " + myGoal.GetTerm(1));
                }
            }

            ReportTerminal(myScore, currentDepth);

            return new Pair<Term, int>(null, myScore);
        }

        private Pair<Term, int> FindMaximalMove(HashSet<Fact> context,
                                                    IEnumerable<GroundFact> myMoves,
                                                    List<List<GroundFact>> otherMoves,
                                                    int currentDepth)
        {
            Term bestMove = null;
            int bestScore = int.MinValue;

            foreach (GroundFact myMove in myMoves)
            {
                var it = new FactCombinationIterator(myMove, otherMoves, DoesProcessor);

                int minScore = it.Select(moveSet => GetScore(context, moveSet, currentDepth)).Concat(new[] { int.MaxValue }).Min();

                if (minScore > bestScore)
                {
                    bestScore = minScore;
                    bestMove = myMove.GetTerm(1);

                    if (currentDepth == 0 && bestScore > _bestScoreSoFar)
                    {
                        _bestMoveSoFar = bestMove;
                        _bestScoreSoFar = bestScore;
                    }
                }

                if (bestScore == 100)
                    // might as well stop now!
                    break;

                // is it time to stop the search?
                if (_stopSearch)
                    throw new TimeoutException();
            }

            if (bestMove == null || bestScore == int.MinValue)
                SearchLogger.Error(GameId + ": Failed to find best move or best score!!");

            return new Pair<Term, int>(bestMove, bestScore);
        }

        private int GetScore(HashSet<Fact> context, GroundFact[] moves,
                             int currentDepth)
        {
            // Create a new state, based on state and context

            // First, add the moves
            foreach (GroundFact move in moves)
                context.Add(move);

            // Figure out what is true in the new state
            var nexts = Prover.AskAll(QueryNext, context);

            var newContext = new HashSet<Fact>();

            foreach (Fact next in nexts)
                newContext.Add(TrueProcessor.ProcessFact((GroundFact) next));

            // Run the recursive search
            Pair<Term, int> result = MinimaxSearch(newContext, currentDepth + 1);

            // Remove the moves
            foreach (GroundFact move in moves)
                context.Remove(move);

            return result.second;
        }

        private void ReportTerminal(int myScore, int currentDepth)
        {
            if (SearchLogger.IsDebugEnabled)
            {
                String indent = Util.MakeIndent(currentDepth);
                SearchLogger.Debug(indent + GameId + ": Terminal. My score: " + myScore);
            }
        }

        public override void StopIt()
        {
            _stopSearch = true;
        }

    }

}

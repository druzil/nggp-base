using System.Collections.Generic;
using nJocLogic.data;
using nJocLogic.knowledge;
using NLog;
using nJocLogic.gdl;
using System;
using nJocLogic.gameContainer;

namespace nJocLogic.game
{
    public abstract class Gamer : ReasoningEntity, IGamer
    {
        /** Play clock: how much time I have to make a move */
        protected int PlayClock;

        /** Match ID */
        protected string GameId;

        /** Ordered list of roles. */
        protected List<TermObject> Roles;

        /** The role assigned to "me" in the game. */
        protected TermObject MyRole;
        /** The role assigned to "me", in string form. */
        protected string MyRoleStr;
        /** My role number (the index into the roles list). */
        protected int MyRoleIndex;

        /** The proof context in which reasoning about this state is performed. */
        protected HashSet<Fact> CurrentContext;

        private static readonly Logger Logger = LogManager.GetLogger("logic.game");

        protected readonly RelationNameProcessor DoesProcessor;
        protected readonly RelationNameProcessor TrueProcessor;

        protected Gamer(string gameId, Parser p)
            : base(p)
        {
            GameId = gameId;

            DoesProcessor = new RelationNameProcessor("does", SymbolTable);
            TrueProcessor = new RelationNameProcessor(Parser.TokTrue);
        }

        public void InitializeGame(TermObject assignedRole, int playClock, GameInformation gameInformation)
        {
            MyRole = assignedRole;
            MyRoleStr = assignedRole.ToString();
            PlayClock = playClock;
            Roles = gameInformation.GetRoles();

            Prover = GameContainer.Prover;

            MyRoleIndex = FindRoleIndex();

            SetupInitialState();
        }

        private int FindRoleIndex()
        {
            for (int i = 0; i < Roles.Count; i++)
                if (Roles[i].Equals(MyRole))
                    return i;

            Logger.Error("Could not find my role index");
            return 0;
        }

        protected void SetupInitialState()
        {
            // Now, find all answers to the question: "init ?x"
            HashSet<Fact> inits = GetAllAnswers(new HashSet<Fact>(), "init", "?x");

            CurrentContext = new HashSet<Fact>();
            foreach (var init in inits)
                CurrentContext.Add(TrueProcessor.ProcessFact((GroundFact)init));
        }

        public abstract void StopIt();

        /**
         * Think about the next move and return it.
         * 
         * @param prevMoves The moves just made, or nil if none (first move).
         * @return A triple containing: the move, an explanation (or null) and a taunt (or null).
         */
        public Tuple<GdlExpression, string, string> Play(GdlList prevMoves)
        {
            // Construct list of previous moves
            GroundFact[] previousMoves = ParsePreviousMoves(prevMoves);

            if (previousMoves.Length > 0)
                UpdateCurrentState(previousMoves);

            Tuple<Term, string, string> move = MoveThink();

            // Convert the Term to a GdlExpression

            GdlExpression moveGdl;

            if (move == null || move.Item1 == null)
            {
                Logger.Error(GameId + ": move returned by moveThink was null");
                moveGdl = new GdlAtom(SymbolTable, Parser.TokNil);
                move = new Tuple<Term, string, string>(null, "", "");
            }
            else
            {
                // the top-level element returned is list of all elements in the parse;
                // in this case, we have just one, the move.
                moveGdl = Parser.Parse(move.Item1.ToString())[0];
            }

            var m = new Tuple<GdlExpression, string, string>(moveGdl, move.Item2, move.Item3);

            return m;
        }

        protected virtual void UpdateCurrentState(GroundFact[] previousMoves)
        {
            foreach (GroundFact prevMove in previousMoves)
                CurrentContext.Add(prevMove);

            HashSet<Fact> newFacts = Prover.AskAll(QueryNext, CurrentContext);

            CurrentContext = new HashSet<Fact>();

            foreach (Fact newFact in newFacts)
                CurrentContext.Add(TrueProcessor.ProcessFact((GroundFact)newFact));
        }

        protected abstract Tuple<Term, string, string> MoveThink();

        protected GroundFact[] ParsePreviousMoves(GdlList prevMoves)
        {
            if (prevMoves == null)
                return new GroundFact[0];

            if (prevMoves.Size != Roles.Count)
                Logger.Error(GameId + ": Previous move list is not the same size as number of roles!");

            var previousMoves = new GroundFact[prevMoves.Size];

            for (int i = 0; i < prevMoves.Size; i++)
            {
                if (i >= Roles.Count)
                    break;

                previousMoves[i] = new GroundFact(Parser.TokDoes, Roles[i], Term.BuildFromGdl(prevMoves[i]));
            }

            return previousMoves;
        }


        /**
         * Compute the payoffs of the game.
         * 
         * @param prevMoves The list of the last moves.
         * @return List of triplets with player's name, payoff, and a flag telling whether it's the played role.
         */
        public List<PayOff> GetPayoffs(GdlList prevMoves)
        {
            // (role name, payoff, my role?) list
            var payoffs = new List<PayOff>(Roles.Count);

            // Construct list of previous moves
            GroundFact[] previousMoves = ParsePreviousMoves(prevMoves);
            UpdateCurrentState(previousMoves);

            foreach (TermObject role in Roles)
            {
                int payoff;
                try
                {
                    Fact goal = GetAnAnswer(CurrentContext, "goal", role.ToString(), "?x");
                    var score = (TermObject)goal.GetTerm(1);
                    payoff = int.Parse(score.ToString());
                }
                catch (Exception)
                {
                    payoff = -1;
                }
                payoffs.Add(new PayOff(role.ToString(), payoff, role.Equals(MyRole)));
            }

            return payoffs;
        }
    }
}

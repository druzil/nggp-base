using System;
using System.Collections.Generic;
using System.Linq;
using nJocLogic.data;
using nJocLogic.game;
using nJocLogic.gameContainer;
using nJocLogic.gdl;
using nJocLogic.knowledge;
using nJocLogic.propNet.architecture;
using nJocLogic.propNet.architecture.backComponents;
using nJocLogic.propNet.factory;
using nJocLogic.statemachine;
using nJocLogic.util;

namespace nGamer
{
    class PropNetGamer : Gamer
    {
        private bool _stopSearch;
        private readonly IPropNetStateMachine _stateMachine;

        public PropNetGamer(string gameId, Parser p)
            : base(gameId, p)
        {
            _stateMachine = new PropNetStateMachine();
        }

        #region Overrides of BaseGamer

        public override void InitializeGame(TermObject assignedRole, int playClock, GameInformation gameInformation)
        {
            base.InitializeGame(assignedRole, playClock, gameInformation);
            GameInformation info = GameContainer.GameInformation;
            IEnumerable<Expression> expressions = info.GetRules().Concat<Expression>(info.GetAllGrounds());
            _stateMachine.Initialize(expressions.ToList());
        }

        internal bool IsTerminal()
        {
            return _stateMachine.IsTerminal();
        }

        internal List<IEnumerable<GroundFact>> GetAllMoves()
        {
            List<List<GroundFact>> sequences = Roles.Select(r => _stateMachine.GetLegalMoves(r).Cast<GroundFact>().ToList()).ToList();
            return sequences.CartesianProduct().ToList();
        }

        internal void GetNextState(GroundFact[] moveFacts)
        {
            _stateMachine.GetNextState(moveFacts);
        }

        internal double[] GetScore()
        {
            return Roles.Select(r => (double)_stateMachine.GetGoal(r)).ToArray();
        }

        #endregion

        public override void StopIt()
        {
            _stopSearch = true;
        }

        protected override Tuple<Term, string, string> MoveThink()
        {
            //TODO: This is where you implement your search
            // Typically this could be a monte-carlo or an adversarial search (min-max)
            // Your search will use your state machine (for this class the _stateMachine propnet)
            // to generate furture states and evaluation positions

            throw new NotImplementedException("Implement me!");

            Term myMove = null;
            double evaluationScore = 0;
            string explanation = String.Format("Score was: {0}", evaluationScore);            
            return new Tuple<Term, string, string>(myMove, explanation, string.Empty);
        }
    }

    internal class PropNetStateMachine : IPropNetStateMachine
    {
        private PropNet _propNet;
        private List<TermObject> _roles;

        public void Initialize(List<Expression> description)
        {
            _propNet = OptimizingPropNetFactory.Create(description, new BackComponentFactory());
            _roles = _propNet.Roles;

            //TODO: You need to ensure that propositions in your propnet are set to the correct initial state
            // i.e init nodes set to true and propogated through. constant nodes set to there correct true/false value
        }

        public bool IsTerminal()
        {
            return _propNet.TerminalProposition.Value;
        }

        public int GetGoal(TermObject role)
        {
            HashSet<IProposition> goals = _propNet.GoalPropositions[role];
            IProposition goal = goals.FirstOrDefault(g => g.Value);

            return goal == null ? 0 : GetGoalValue(goal);
        }

        public IEnumerable<Fact> GetLegalMoves(TermObject role)
        {
            HashSet<IProposition> legals = _propNet.LegalPropositions[role];
            return legals.Where(l => l.Value).Select(legal => legal.Name);
        }

        public void GetNextState(GroundFact[] moves)
        {
            //TODO: This is where you must move the propnet into its next state. 
            //Depending on the type of propnet is can be forward or backwards propagation
            throw new NotImplementedException();
        }

        public List<TermObject> GetRoles()
        {
            return _roles;
        }

        private static int GetGoalValue(IProposition goalProposition)
        {
            Fact relation = goalProposition.Name;
            Term constant = relation.GetTerm(1);
            return int.Parse(constant.ToString());
        }
    }
}

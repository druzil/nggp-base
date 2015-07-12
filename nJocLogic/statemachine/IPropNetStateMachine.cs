namespace nJocLogic.statemachine
{
    using System.Collections.Generic;
    using data;

    public interface IPropNetStateMachine {
        /// <summary>
        /// Initializes the PropNetStateMachine. Computes the topological ordering
        /// Additionally may compute the initial state here
        /// </summary>
        /// <param name="description"></param>
        void Initialize(List<Expression> description);

        /// <summary>
        /// Computes if the state is terminal. Should return the value
        /// of the terminal proposition for the state.
        /// </summary>
        /// <returns></returns>
        bool IsTerminal();

        /// <summary>
        /// Computes the goal for a role in the current state.
        /// Should return the value of the goal proposition that
        /// is true for that role. If there is not exactly one goal
        /// proposition true for that role, then you should throw a
        /// GoalDefinitionException because the goal is ill-defined.
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        int GetGoal(TermObject role);

        ///// <summary>
        ///// Returns the initial state. The initial state can be computed
        ///// by only setting the truth value of the INIT proposition to true,
        ///// and then computing the resulting state.
        ///// </summary>
        ///// <returns></returns>
        //void GetInitialState();

        /// <summary>
        /// Computes the legal moves for role in state.
        /// </summary>
        /// <param name="role"></param>
        /// <returns></returns>
        IEnumerable<Fact> GetLegalMoves(TermObject role);

        /// <summary>
        /// Transitions to the next state from the given moves
        /// </summary>
        /// <param name="moves"></param>
        /// <returns></returns>
        void GetNextState(GroundFact[] moves);

        List<TermObject> GetRoles();
    }
}
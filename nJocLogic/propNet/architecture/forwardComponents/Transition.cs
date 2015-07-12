namespace nJocLogic.propNet.architecture.forwardComponents
{
    using System;

    /// <summary>
    /// The Transition class is designed to represent pass-through gates.
    /// </summary>
    public sealed class Transition : ForwardComponent, ITransition
    {
        public int TransitionID { get; set; }

        /// <summary>
        /// Derives the components value from its inputs
        /// </summary>
        /// <returns></returns>
        public override bool DeriveValue()
        {
            return GetSingleInput().Value;
        }

        public override String ToDot()
        {
            return ToDot("box", "grey", "TRANSITION");
        }
    }
}
namespace nJocLogic.propNet.architecture.backComponents
{
    using System;

    /// <summary>
    /// The Transition class is designed to represent pass-through gates.
    /// </summary>
    public sealed class Transition : BackComponent, ITransition
    {
        /// <summary>
        /// Returns the value of the input to the transition.
        /// </summary>
        /// <returns></returns>
        public override bool Value
        {
            get { return GetSingleInput().Value; }
            set { throw new Exception("Not valid for back propogation"); }
        }

        public override String ToDot()
        {
            return ToDot("box", "grey", "TRANSITION");
        }
    }
}
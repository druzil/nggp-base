namespace nJocLogic.propNet.architecture.components
{
    using System;
    using architecture;

    /// <summary>
    /// The Transition class is designed to represent pass-through gates.
    /// </summary>
    public sealed class Transition : Component
    {
        /// <summary>
        /// Returns the value of the input to the transition.
        /// </summary>
        /// <returns></returns>
        public override bool Value{ get { return GetSingleInput().Value; } }

        public override String ToDot()
        {
            return ToDot("box", "grey", "TRANSITION");
        }

        #region Overrides of Object

        public override string ToString()
        {
            return string.Format("Transition {0} {1}", Value, CachedValue);
        }

        #endregion
    }
}
namespace nJocLogic.propNet.architecture.components
{
    using System;
    using System.Linq;
    using architecture;

    /// <summary>
    /// The Or class is designed to represent logical OR gates.
    /// </summary>
    public sealed class Or : Component
    {
        /// <summary>
        /// Returns true if and only if at least one of the inputs to the or is true.
        /// </summary>
        public override bool Value
        {
            get{ return Inputs.Any(component => component.Value); }
        }

        public override String ToDot()
        {
            return ToDot("ellipse", "grey", "OR");
        }

        public override string ToString()
        {
            return string.Format("Or {0} {1}", Value, CachedValue);
        }
    }
}
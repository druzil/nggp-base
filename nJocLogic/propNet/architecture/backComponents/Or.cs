namespace nJocLogic.propNet.architecture.backComponents
{
    using System;
    using System.Linq;

    /// <summary>
    /// The Or class is designed to represent logical OR gates.
    /// </summary>
    public sealed class Or : BackComponent, IOr
    {
        /// <summary>
        /// Returns true if and only if at least one of the inputs to the or is true.
        /// </summary>
        public override bool Value
        {
            get{ return Inputs.Any(component => component.Value); }
            set { throw new Exception("Not valid for back propogation"); }
        }

        public override String ToDot()
        {
            return ToDot("ellipse", "grey", "OR");
        }
    }
}
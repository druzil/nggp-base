using nJocLogic.statemachine;

namespace nJocLogic.propNet.architecture.forwardComponents
{
    using System;
    using System.Linq;

    /// <summary>
    /// The Or class is designed to represent logical OR gates.
    /// </summary>
    public sealed class Or : ForwardComponent, IOr, ICountableInputs
    {
        /// <summary>
        /// Returns true if and only if at least one of the inputs to the or is true.
        /// </summary>
        public override bool DeriveValue()
        {
             return Inputs.Any(component => component.Value); 
        }

        public override String ToDot()
        {
            return ToDot("ellipse", "grey", "OR");
        }

        public int CountableID { get; set; }
    }
}
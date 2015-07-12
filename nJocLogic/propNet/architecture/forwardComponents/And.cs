using nJocLogic.statemachine;

namespace nJocLogic.propNet.architecture.forwardComponents
{
    using System.Linq;

    /// <summary>
    /// The And class is designed to represent logical AND gates.
    /// </summary>
    public sealed class And : ForwardComponent, IAnd, ICountableInputs
    {
        /// <summary>
        /// Derives the components value from its inputs
        /// </summary>
        /// <returns></returns>
        public override bool DeriveValue()
        {
            return Inputs.All(component => component.Value);
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        public override string ToDot()
        {
            return ToDot("invhouse", "grey", "AND");
        }

        public int CountableID { get; set; }
    }
}

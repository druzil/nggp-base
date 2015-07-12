namespace nJocLogic.propNet.architecture.backComponents
{
    using System;
    using System.Linq;

    /// <summary>
    /// The And class is designed to represent logical AND gates.
    /// </summary>
    public sealed class And : BackComponent, IAnd
    {
        /// <summary>
        /// Returns true if and only if every input to the and is true. 
        /// </summary>
        public override bool Value
        {
            get { return Inputs.All(component => component.Value); }
            set { throw new Exception("Not valid for back propogation");}
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
    }
}

namespace nJocLogic.propNet.architecture.components
{
    using System.Linq;
    using architecture;

    /// <summary>
    /// The And class is designed to represent logical AND gates.
    /// </summary>
    public sealed class And : Component
    {
        /// <summary>
        /// Returns true if and only if every input to the and is true. 
        /// </summary>
        public override bool Value
        {
            get { return Inputs.All(component => component.Value); }
        }

        #region Overrides of Object

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

        public override string ToString()
        {
            return string.Format("And {0} {1}", Value, CachedValue);
        }
        #endregion
    }
}

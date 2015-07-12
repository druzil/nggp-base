namespace nJocLogic.propNet.architecture.components
{
    using architecture;

    /// <summary>
    /// The Not class is designed to represent logical NOT gates.
    /// </summary>
    public sealed class Not : Component
    {
        /// <summary>
        /// Returns the inverse of the input to the not.
        /// </summary>
        public override bool Value { get { return !GetSingleInput().Value; } }

        #region Overrides of Object

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        public override string ToDot()
        {
            return ToDot("invtriangle", "grey", "NOT");
        }

        public override string ToString()
        {
            return string.Format("Not {0} {1}", Value, CachedValue);
        }
        #endregion
    }
}
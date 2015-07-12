namespace nJocLogic.propNet.architecture.forwardComponents
{
    /// <summary>
    /// The Not class is designed to represent logical NOT gates.
    /// </summary>
    public sealed class Not : ForwardComponent, INot
    {
        /// <summary>
        /// Derives the components value from its inputs
        /// </summary>
        /// <returns></returns>
        public override bool DeriveValue()
        {
            return !GetSingleInput().Value;
        }

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
    }
}
namespace nJocLogic.propNet.architecture.backComponents
{
    using System;

    /// <summary>
    /// The Not class is designed to represent logical NOT gates.
    /// </summary>
    public sealed class Not : BackComponent, INot
    {
        /// <summary>
        /// Returns the inverse of the input to the not.
        /// </summary>
        public override bool Value
        {
            get { return !GetSingleInput().Value; }
            set { throw new Exception("Not valid for back propogation"); }
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
            return ToDot("invtriangle", "grey", "NOT");
        }
        #endregion
    }
}
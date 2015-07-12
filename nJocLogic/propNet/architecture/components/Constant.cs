namespace nJocLogic.propNet.architecture.components
{
    using System;
    using architecture;

    /// <summary>
    /// The Constant class is designed to represent nodes with fixed logical values.
    /// </summary>
    public sealed class Constant : Component
    {	
        private readonly bool value;     /** The value of the constant. */

        /// <summary>
        /// Creates a new Constant with value <tt>value</tt>.
        /// </summary>
        /// <param name="value">The value of the Constant.</param>
        public Constant(bool value)
        {
            this.value = value;
        }

        /// <summary>
        /// Returns the value that the constant was initialized to.
        /// </summary>
        public override bool Value{ get { return value; } }

        public override String ToDot()
        {
            return ToDot("doublecircle", "grey", value.ToString().ToUpper());
        }

        public override string ToString()
        {
            return string.Format("Constant {0}", Value);
        }
    }
}
namespace nJocLogic.propNet.architecture.forwardComponents
{
    public abstract class ForwardComponent : Component
    {
        /// <summary>
        /// Returns the value of the Component.
        /// </summary>
        /// <returns>The value of the Component.</returns>
        public override bool Value { get; set; }

        /// <summary>
        /// Derives the components value from its inputs and sets the value to this new value
        /// </summary>
        /// <returns>Returns true if the derived value is different to the stored value, else false</returns>
        public bool DeriveValueAndIsChanged()
        {
            bool newValue = DeriveValue();
            bool oldValue = Value;
            Value = newValue;
            return newValue != oldValue;
        }

        /// <summary>
        /// Derives the components value from its inputs
        /// </summary>
        /// <returns></returns>
        public abstract bool DeriveValue();

        public override string ToString()
        {
            return string.Format("{0} current:{1} Derived:{2}", GetType().Name, Value, DeriveValue());
        }
    }
}

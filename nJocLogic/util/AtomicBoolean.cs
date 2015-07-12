using System.Threading;

namespace nJocLogic.util
{
    /// <summary>
    /// Provides non-blocking, thread-safe access to a boolean valueB.
    /// </summary>
    public class AtomicBoolean
    {
        #region Member Variables

        private const int ValueTrue = 1;
        private const int ValueFalse = 0;

        private int _currentValue;

        #endregion

        #region Constructor

        public AtomicBoolean(bool initialValue)
        {
            _currentValue = BoolToInt(initialValue);
        }

        #endregion

        #region Private Methods

        private int BoolToInt(bool value)
        {
            return value ? ValueTrue : ValueFalse;
        }

        private bool IntToBool(int value)
        {
            return value == ValueTrue;
        }

        #endregion

        #region Public Properties and Methods

        public bool Value
        {
            get
            {
                return IntToBool(Interlocked.Add(
                ref _currentValue, 0));
            }
        }

        /// <summary>
        /// Sets the boolean value.
        /// </summary>
        /// <param name="newValue"></param>
        /// <returns>The original value.</returns>
        public bool SetValue(bool newValue)
        {
            return IntToBool(
            Interlocked.Exchange(ref _currentValue,
            BoolToInt(newValue)));
        }

        /// <summary>
        /// Compares with expected value and if same, assigns the new value.
        /// </summary>
        /// <param name="expectedValue"></param>
        /// <param name="newValue"></param>
        /// <returns>True if able to compare and set, otherwise false.</returns>
        public bool CompareAndSet(bool expectedValue,
            bool newValue)
        {
            int expectedVal = BoolToInt(expectedValue);
            int newVal = BoolToInt(newValue);
            return Interlocked.CompareExchange(
            ref _currentValue, newVal, expectedVal) == expectedVal;
        }

        #endregion
    }
    
}

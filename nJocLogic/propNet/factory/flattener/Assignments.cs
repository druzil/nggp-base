namespace nJocLogic.propNet.factory.flattener
{
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// A unique set of possible combination of assignments to an Expression
    /// </summary>
    public class Assignments : HashSet<Assignment>
    {
        #region Overrides of Object

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var item in this)
                sb.Append(string.Format("({0}) ", item));
            return sb.ToString();
        }

        #endregion
    }
}
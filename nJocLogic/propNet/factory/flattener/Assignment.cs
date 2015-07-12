using System.Collections.Generic;
using nJocLogic.data;
using System.Linq;

namespace nJocLogic.propNet.factory.flattener
{
    /// <summary>
    /// Represents a list of concrete assignments (TermObjects).  Each element is a replacement for a different variable in a fact
    /// </summary>
    public class Assignment : IEnumerable<TermObject>
    {
        readonly List<TermObject> _termObjects = new List<TermObject>();
        private int _hashCode = 1;

        public override int GetHashCode()
        {
            return _hashCode;
        }

        void ResetHashCode()
        {
            _hashCode = 1;
            foreach (var e in _termObjects)
                _hashCode = 31 * _hashCode + (e == null ? 0 : e.GetHashCode());
        }
        #region Overrides of Object

        public override bool Equals(object obj)
        {
            var ass = obj as Assignment;
            if (ass == null)
                return false;
            //return ass.Count == Count && _hashCode == ass._hashCode && ass.SequenceEqual(this);
            return ass.Count == Count && _hashCode == ass._hashCode;        //INFO: this is wrong
        }

        #endregion

        public IEnumerator<TermObject> GetEnumerator()
        {
            return _termObjects.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _termObjects.GetEnumerator();
        }

        public void Add(TermObject termObject)
        {
            _termObjects.Add(termObject);
            ResetHashCode();
        }

        public void AddRange(IEnumerable<TermObject> selectMany)
        {
            foreach (TermObject termObject in selectMany)
                _termObjects.Add(termObject);
            ResetHashCode();

        }

        public int Count { get { return _termObjects.Count; } }

        public TermObject this[int i]
        {
            get { return _termObjects[i]; }
        }

        #region Overrides of Object

        public override string ToString()
        {
            return string.Join(",", this);
        }

        #endregion
    }
}
using System.Collections.Generic;
using System.Linq;

namespace nJocLogic.util
{
    //TODO: this is quick and dirty, should be immutable and override the methods that can modify they collection
    public class JavaHashSet<T> : HashSet<T>
    {
        public static readonly JavaHashSet<T> Empty = new JavaHashSet<T>();
        private readonly int _hashcode = 0;

        public JavaHashSet(IEnumerable<T> hashSet) : base(hashSet)
        {
            foreach (T item in hashSet)
                _hashcode ^= item.GetHashCode();
        }

        private JavaHashSet() : base()
        {
            
        }

        public override int GetHashCode()
        {
            return _hashcode;
        }

        public override bool Equals(object obj)
        {
            var set = obj as ISet<T>;
            if (set == null)
                return false;

            if (set.Count != Count)
                return false;

            return set.All(Contains);
        }        
    }
}

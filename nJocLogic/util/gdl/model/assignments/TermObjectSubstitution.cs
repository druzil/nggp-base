namespace nJocLogic.util.gdl.model.assignments
{
    using System;
    using System.Linq;
    using data;

    /// <summary>
    /// A substitution class that only maps to TermObjects
    /// </summary>
    public class TermObjectSubstitution : Substitution
    {
        // the hashcode is a sum of the keyvaluepair hashcodes
        // the keyvaluepair hashcode is the hascode of the key ^ value
        private int _hashcode;

        protected bool Equals(TermObjectSubstitution other)
        {
            return other.NumMappings() == NumMappings() && Substitutions.All(kv => kv.Value.Equals(other.GetMapping(kv.Key)));			
        }

        public override int GetHashCode()
        {
            return _hashcode;
        }

        public TermObjectSubstitution()
        {
            _hashcode = 1;
        }

        public override void AddMapping(TermVariable from, Term to)
        {
            if (to is TermObject == false)
                throw new Exception("TermObjectSubstitution is optimised for TermObjects");
            int delta = from.GetHashCode() ^ to.GetHashCode();
            _hashcode += delta;
            Substitutions[from] = to.Clone();
        }

        public override Term GetMapping(TermVariable var)
        {
            Term result;
            Substitutions.TryGetValue(var, out result);
            return result;
        }

        public TermObjectSubstitution Copy(TermObjectSubstitution add)
        {
            var result = new TermObjectSubstitution();

            foreach (var kv in Substitutions)
                result.Substitutions[kv.Key] = kv.Value.Clone();
            result._hashcode = _hashcode;

            result.Add(add);

            return result;
        }

        public void Add(TermObjectSubstitution add)
        {
            foreach (var kv in add.Substitutions)
                AddMapping(kv.Key, kv.Value);
        }

        /**
         * Gets the mapping for a function.
         * Iterates over all the Term of the functions and recursively tries to map them.
         * 
         * @param func The function whose mappings to compute.
         * @return The function after all Substitution2s have been made.
         */
        public override TermFunction GetMapping(TermFunction func)
        {
            throw new NotImplementedException();
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;

            if (obj is TermObjectSubstitution == false)
                return false;

            var sub = (TermObjectSubstitution)obj;

            return Equals(sub);
        }
    }

}

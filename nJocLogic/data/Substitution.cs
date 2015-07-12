using System.Linq;
using nJocLogic.gameContainer;
using nJocLogic.gdl;
using System.Text;
using System.Collections.Generic;

namespace nJocLogic.data
{
    public class Substitution
    {
        protected bool Equals(Substitution other)
        {
            if (other.NumMappings() != NumMappings())
                return false;

            List<TermVariable> subs = Substitutions.Keys.ToList();

            //this cannot be a foreach as GetMapping possibly modifies the collection
            for (int i = 0; i < subs.Count; i++)
            {
                TermVariable currrentKey = subs[i];
                if (!GetMapping(currrentKey).Equals(other.GetMapping(currrentKey)))
                    return false;
            }
            return true;
            //return other.NumMappings() == NumMappings() && _substitutions.All(kv => GetMapping(kv.Key).Equals(other.GetMapping(kv.Key)));			
        }

        public override int GetHashCode()
        {
            return 1;
        }

        protected readonly Dictionary<TermVariable, Term> Substitutions;

        public Substitution()
        {
            Substitutions = new Dictionary<TermVariable, Term>();
        }

        public virtual void AddMapping(TermVariable from, Term to)
        {
            //TODO: is there a reason to clone?
            Substitutions[from] = to.Clone();
        }

        public virtual Term GetMapping(TermVariable var)
        {
            Term result;

            if (Substitutions.TryGetValue(var, out result))
            {
                if (result is TermVariable)
                {
                    var sub = GetMapping((TermVariable)result);
                    if (sub != null)
                    {
                        Substitutions[var] = sub;
                        result = sub;
                    }
                }
                else
                {
                    var func = result as TermFunction;
                    if (func != null && func.HasVariables)
                        result = func.ApplySubstitution(this);
                }
            }

            return result;
        }

        private Substitution Copy()
        {
            var result = new Substitution();

            foreach (var kv in Substitutions)
                result.Substitutions[kv.Key] = kv.Value.Clone();

            return result;
        }

        public Substitution Copy(Substitution add)
        {
            Substitution result = Copy();
            result.Add(add);

            return result;
        }

        public void Add(Substitution add)
        {
            foreach (var kv in add.Substitutions)
                AddMapping(kv.Key, kv.Value);
        }

        /**
         * Gets the mapping for a function.
         * Iterates over all the Term of the functions and recursively tries to map them.
         * 
         * @param func The function whose mappings to compute.
         * @return The function after all substitutions have been made.
         */
        public virtual TermFunction GetMapping(TermFunction func)
        {
            var result = new TermFunction(true, func.FunctionName, func.Arguments);
            for (int i = 0; i < result.Arity; i++)
            {
                if (result.Arguments[i] is TermVariable)
                {
                    Term replacement = GetMapping((TermVariable)result.Arguments[i]);
                    if (replacement != null)
                        result.Arguments[i] = replacement;
                }
                else if (result.Arguments[i] is TermFunction)
                {
                    Term replacement = GetMapping((TermFunction)result.Arguments[i]);
                    if (replacement != null)
                        result.Arguments[i] = replacement;
                }
            }
            return result;
        }

        public int NumMappings()
        {
            return Substitutions.Count;
        }

        public override string ToString()
        {
            return ToString(GameContainer.SymbolTable);
        }

        public string ToString(SymbolTable symtab)
        {
            var sb = new StringBuilder();
            sb.Append("{ ");

            var keys = Substitutions.Keys;

            foreach (TermVariable tv in keys)
            {
                sb.Append(tv.ToString(symtab));
                sb.Append(" <- ");
                sb.Append(GetMapping(tv).ToString(symtab));
                sb.Append(". ");
            }

            sb.Append("}");
            return sb.ToString();
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;

            if (obj is Substitution == false)
                return false;

            var sub = (Substitution)obj;

            return Equals(sub);
        }

        public bool ContainsObjectMapping()
        {
            return Substitutions.Any(sub => sub.Value is TermObject);
        }

        internal bool IsMappedTo(TermVariable a)
        {
            return Substitutions.ContainsValue(a);
        }

        internal List<Term> GetReplacers()
        {
            List<TermVariable> keys = Substitutions.Keys.ToList();

            return keys.Select(GetMapping).ToList();
        }

        internal List<TermVariable> GetReplacees()
        {
            return Substitutions.Keys.ToList();
        }

        internal Substitution Reverse()
        {
            var result = new Substitution();

            foreach (var kv in Substitutions)
            {
                var newKey = kv.Value as TermVariable;
                if (newKey == null)
                    return null;
                result.Substitutions[newKey] = kv.Key;
            }
            return result;
        }
    }

}

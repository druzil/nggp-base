using System.Collections.Generic;
using System.IO;
using System.Linq;
using nJocLogic.gameContainer;
using nJocLogic.gdl;

namespace nJocLogic.data
{
    public abstract class Fact : Expression
    {
        public int RelationName { get; protected set; }
        readonly protected Term[] Terms;
        private readonly int _hashcode;

        protected Fact(int relName, Term[] terms)
        {
            RelationName = relName;
            Terms = terms;

            _hashcode = RelationName;
            foreach (var term in Terms)
                _hashcode = (_hashcode << 1) ^ term.GetHashCode();
        }

        /// <summary>
        /// Number of columns excluding relation name.
        /// </summary>
        public int Arity
        {
            get { return Terms.Length; }
        }

        public Term GetTerm(int whichOne)
        {
            return Terms[whichOne];
        }

        public List<Term> GetTerms()
        {
            return Terms.ToList();
        }

        public abstract bool HasOnlyTermObjects();

        public abstract override Expression ApplySubstitution(Substitution sigma);

        public override abstract Expression Uniquefy(Dictionary<TermVariable, TermVariable> varMap);

        public override bool HasTermFunction(int functionName)
        {
            return Terms.Any(t => t.HasTermFunction(functionName));
        }

        public override bool HasTermVariable(int varName)
        {
            return Terms.Any(t => t.HasVariable(varName));
        }

        /// <summary>
        /// Attempt to unify this fact with <paramref name="fact"/>. If the unification
        /// succeeds, return the substitution used -- but does not actually change
        /// the facts. If the unification fails, return null.
        /// 
        /// Note that before attempting to unify with <paramref name="fact"/>, you probably
        /// want to make sure that the facts have unique variable names.</summary>
        /// <param name="fact">The fact to unify with.</param>
        /// <returns>The substitution used to unify these, or null.</returns>
        public virtual Substitution Unify(Fact fact)
        {
            return Unifier.Mgu(this, fact);
        }

        /// <summary>
        /// Construct a fact, ground or variable, from a GdlExpression. Returns a
        /// GroundFact if there were no variables in the GdlExpression, and a
        /// VariableFact otherwise.
        /// </summary>
        /// <param name="exp">The expression from which to construct the fact.</param>
        /// <returns>The fact constructed from GdlExpression <paramref name="exp"/>.</returns>
        static public Fact FromExpression(GdlExpression exp)
        {
            // When in doubt, it's probably a variable fact.
            // Besides, the variable fact factory takes care of turning
            // things into ground facts if there are no variables.
            return VariableFact.FromExpression(exp);
        }

        public override Expression[] Constituents { get { return new Expression[] { this }; } }

        public override bool IsEquivalent(Expression target)
        {
            return Equals(target);
        }

        public override string Output()
        {
            SymbolTable symtab = GameContainer.SymbolTable;
            string result = string.Format("({0}", symtab[RelationName]);

            if (Terms.Length > 0)
            {
                result += ' ';
                int i;
                for (i = 0; i < Terms.Length - 1; i++)
                    result += Terms[i].ToString(symtab) + ' ';
                result += Terms[i].ToString(symtab);
            }

            result += ')';

            return result;
        }

        public override void PrintToStream(StreamWriter target, SymbolTable symtab)
        {
            target.Write('(');
            target.Write(symtab[RelationName]);

            if (Terms.Length > 0)
            {
                target.Write(' ');
                int i;
                for (i = 0; i < Terms.Length - 1; i++)
                {
                    target.Write(Terms[i].ToString(symtab));
                    target.Write(' ');
                }
                target.Write(Terms[i].ToString(symtab));
            }

            target.Write(')');
        }

        protected static Term[] CreateTerms(bool clone, params Term[] cols)
        {
            if (cols == null)
                return EmptyTerms;

            if (clone)
                return (Term[])cols.Clone();

            return cols;
        }

        public override int GetHashCode()
        {
            return _hashcode;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;

            var fact = obj as Fact;
            if (fact != null)
            {
                if (RelationName != fact.RelationName)
                    return false;

                if (Terms.Length != fact.Terms.Length)
                    return false;

                for (int i = 0; i < Terms.Length; i++)
                    if (!Terms[i].Equals(fact.Terms[i]))
                        return false;

                return true;
            }

            return false;
        }

        public IEnumerable<Term> NestedTerms { get { return GetNestedTerms(Terms); } }

        private static IEnumerable<Term> GetNestedTerms(IEnumerable<Term> terms)
        {
            foreach (Term t in terms)
            {
                var function = t as TermFunction;
                if (function != null)
                    foreach (Term result in GetNestedTerms(function.Arguments))
                        yield return result;
                else
                    yield return t;
            }
        }

        public override IEnumerable<TermObject> TermObjects { get { return GetTermObjects(Terms); } }

        static IEnumerable<TermObject> GetTermObjects(IEnumerable<Term> terms)
        {
            foreach (Term t in terms)
            {
                var item = t as TermObject;
                if (item != null)
                    yield return item;
                else
                {
                    var function = t as TermFunction;
                    if (function != null)
                        foreach (TermObject result in GetTermObjects(function.Arguments))
                            yield return result;
                }
            }
        }

        internal Term ToTerm()
        {
            return new TermFunction(false, RelationName, Terms);
        }
    }
}

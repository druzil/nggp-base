using System;
using nJocLogic.gdl;
using System.Collections.Generic;

namespace nJocLogic.data
{
    public class GroundFact : Fact, IComparable<GroundFact>
    {
        readonly private bool _onlyTermObjects;

        public GroundFact(int relationName, params Term[] cols) : this(true, relationName, cols) { }

        public GroundFact(bool clone, int relationName, params Term[] cols)
            : base(relationName, CreateTerms(clone, cols))
        {
            bool onlyTermObjects = true;

            if (cols != null)
            {
                // Assert that none of the columns have variables.
                foreach (Term t in cols)
                {
                    if (t.HasVariables)
                        throw new Exception("GroundFact cannot be constructed with variables!");

                    if (t is TermObject == false)
                        onlyTermObjects = false;
                }
            }

            _onlyTermObjects = onlyTermObjects;
        }

        /// <summary>
        /// Construct a fact of relation <tt>relName</tt> with columns
        /// <tt>cols</tt>. Takes strings as arguments, and uses symbol table
        /// <tt>symTab</tt> to convert them to integer form. All columns must be
        /// object constants.
        /// </summary>
        /// <param name="symTab">The symbol table for converting strings to token numbers.</param>
        /// <param name="relName">The name of the relation, to be converted using <paramref name="symTab"/>.</param>
        /// <param name="cols">The columns, to be converted using <paramref name="symTab"/>, all representing object constants.</param>
        public GroundFact(SymbolTable symTab, String relName, params string[] cols)
            : base(symTab[relName], CreatTerms(symTab, cols))
        {
            _onlyTermObjects = true;
        }

        static Term[] CreatTerms(SymbolTable symTab, params string[] cols)
        {
            if (cols == null || cols.Length == 0)
                return EmptyTerms;

            var terms = new Term[cols.Length];

            for (int i = 0; i < cols.Length; i++)
                terms[i] = TermObject.MakeTermObject(symTab[cols[i]]);
            return terms;
        }

        /// <summary>
        /// Clone this ground fact, but with a new relation name. Keeps all columns intact.
        /// </summary>
        /// <param name="newRelName">newRelName The new name for the ground fact relation.</param>
        /// <returns>The cloned fact with the different name.</returns>
        public GroundFact Clone(int newRelName)
        {
            // False means don't clone the terms.
            return new GroundFact(false, newRelName, Terms);
        }

        public override Expression ApplySubstitution(Substitution sigma)
        {
            // Ground facts do not have variables, so there is nothing to do.
            return this;
        }

        public override bool HasOnlyTermObjects()
        {
            return _onlyTermObjects;
        }

        public override bool CanMapVariables(Expression other)
        {
            var gf = other as GroundFact;

            if (gf == null)
                return false;

            if (RelationName != gf.RelationName || Arity != gf.Arity)
                return false;

            // For ground facts, no variables, so things must be straight equal.
            for (int i = 0; i < Arity; i++)
                if (GetTerm(i).Equals(gf.GetTerm(i)) == false)
                    return false;

            return true;
        }

        /// <summary>
        /// Construct a fact from a GdlList. Note that the list <i>must</i> be
        /// a list of atoms, in other words, there cannot be any nested lists. The fact
        /// is constructed by taking the first element of the list as the fact's relation name,
        /// and every subsequent element as a column.
        /// </summary>
        /// <param name="list">list The list to build the fact from.</param>
        /// <returns>A ground fact representing the data from the list.</returns>
        /// <exception cref="InvalidCastException">when the passed list is not a list of atoms.</exception>
        /// <see cref="nJocLogic.gdl.GdlList"/>
        public static GroundFact FromList(GdlList list)
        {
            int relName = ((GdlAtom)list[0]).GetToken();

            var terms = new Term[list.Arity];

            for (int i = 0; i < list.Arity; i++)
                terms[i] = Term.BuildFromGdl(list[i + 1]);

            return new GroundFact(relName, terms);
        }

        public static new GroundFact FromExpression(GdlExpression exp)
        {
            var list = exp as GdlList;
            if (list != null)
                return FromList(list);
            var atom = exp as GdlAtom;
            if (atom != null)
                return new GroundFact(false, atom.GetToken(), null);

            // unknown expression type
            throw new Exception("GroundFact.fromExpression: don't know how to handle expressions of type " + exp.GetType().Name);
        }

        /// <summary>
        /// Compare this fact to <paramref name="fact"/>. Facts are compared first
        /// according to their relation name, second according to the number of
        /// columns and finally according to the tokens in the columns.
        /// </summary>
        /// <param name="fact">The fact to compare against.</param>
        /// <returns>
        /// -1 if 'this' &lt; <paramref name="fact"/>
        /// 0 if 'this' == <paramref name="fact"/>
        /// 1 if 'this' &gt; <paramref name="fact"/>
        /// </returns>
        public int CompareTo(GroundFact fact)
        {
            if (ReferenceEquals(this, fact))
                return 0;

            int comp = RelationName - fact.RelationName;
            if (comp != 0)
                return (comp > 0) ? 1 : -1;

            // relation names are equal at this point
            comp = Terms.Length - fact.Terms.Length;
            if (comp != 0)
                return (comp > 0) ? 1 : -1;

            // column lengths are equal at this point
            for (int i = 0; i < Terms.Length; i++)
            {
                comp = Terms[i].CompareTo(fact.Terms[i]);
                if (comp != 0)
                    return comp;
            }

            return 0;
        }

        public override Expression Uniquefy(Dictionary<TermVariable, TermVariable> newVarMap)
        {
            // Nothing to do, by definition.
            return this;
        }

        /// <summary>
        /// If <c>this</c> == <paramref name="f"/>, then return a successful unification, using
        /// an empty substitution; the facts are the same. General case: use normal unify algorithm.
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public override Substitution Unify(Fact f)
        {
            return Equals(f) ? new Substitution() : base.Unify(f);
        }

        public override IEnumerable<TermVariable> Variables { get { return null; } }
    }
}

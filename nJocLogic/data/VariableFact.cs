using System.Collections.Generic;
using nJocLogic.gdl;
using System;

namespace nJocLogic.data
{
    public class VariableFact : Fact
    {
        private readonly ICollection<TermVariable> _variables;

        public VariableFact(bool cloneCols, int relName, params Term[] columns)
            : base(relName, CreateTerms(cloneCols, columns))
        {

            _variables = new HashSet<TermVariable>();
            BuildVariableSet();
        }

        private void BuildVariableSet()
        {
            AddVarsFromTerms(Terms);
        }

        private void AddVarsFromTerms(IEnumerable<Term> terms)
        {
            foreach (Term t in terms)
            {
                var item = t as TermVariable;
                if (item != null)
                    _variables.Add(item);
                else
                {
                    var function = t as TermFunction;
                    if (function != null)
                        AddVarsFromTerms(function.Arguments);
                }
            }
        }

        public override IEnumerable<TermVariable> Variables { get { return _variables; } }

        public override Expression ApplySubstitution(Substitution sigma)
        {
            var columns = new Term[Terms.Length];

            bool vars = false;
            for (int i = 0; i < Terms.Length; i++)
            {
                columns[i] = Terms[i].ApplySubstitution(sigma);
                if (columns[i].HasVariables)
                    vars = true;
            }

            if (vars)
                return new VariableFact(false, RelationName, columns);
            var newFact = new GroundFact(false, RelationName, columns);
            //if (factQuery != null && factQuery.Contains(newFact))
            //{
            //    usedFacts.Add(new FactMapping { ground = newFact, variable = this });
            //}
            return newFact;
        }

        public override bool HasOnlyTermObjects()
        {
            // By definition, variable facts have things other than objects: variables!
            return false;
        }

        public override bool CanMapVariables(Expression other)
        {
            if (other is VariableFact == false)
                return false;

            var vf = (VariableFact)other;

            if (RelationName != vf.RelationName || Arity != vf.Arity)
                return false;

            var varMappings = new Dictionary<TermVariable, TermVariable>();

            for (int i = 0; i < Arity; i++)
                if (GetTerm(i).CanMapVariables(vf.GetTerm(i), varMappings) == false)
                    return false;

            return true;
        }

        public static new Fact FromExpression(GdlExpression exp)
        {
            if (exp is GdlAtom)
                return GroundFact.FromExpression(exp);

            var list = exp as GdlList;
            if (list != null)
                return FromList(list);

            // unknown expression type
            throw new Exception("GroundFact.fromExpression: don't know how to handle expressions of type " + exp.GetType().Name);
        }

        /// <summary>
        /// Construct a variable fact from a GdlList. Note that the list <i>must</i>
        /// be a list of atoms, in other words, there cannot be any nested lists. The
        /// fact is constructed by taking the first element of the list as the fact's
        /// relation name, and every subsequent element as a column. If an atom is
        /// found to be a variable, then that column is marked as a variable.
        /// </summary>
        /// <param name="list">The list to build the fact from.</param>
        /// <returns>A variable fact representing the data from the list.</returns>
        public static Fact FromList(GdlList list)
        {
            int relName = ((GdlAtom)list[0]).GetToken();

            var terms = new Term[list.Arity];

            // Turn each element of the list into a term.
            // Make sure to turn same variables into the same term.

            bool vars = false;

            var varMap = new Dictionary<GdlVariable, TermVariable>();

            for (int i = 0; i < list.Arity; i++)
            {
                GdlExpression exp = list[i + 1];

                if ((exp is GdlVariable) == false)
                {
                    terms[i] = Term.BuildFromGdl(exp, varMap);

                    // Check to see if this term has variables in it.
                    // (But don't bother if we already know that we have variables.)
                    if (!vars && terms[i].HasVariables)
                        vars = true;
                }
                else
                {
                    var var = (GdlVariable)exp;
                    terms[i] = new TermVariable(var.GetToken());
                    vars = true;
                }

            }

            // Only return a variable fact if this actually has variables 
            if (vars)
                return new VariableFact(true, relName, terms);
            return new GroundFact(relName, terms);
        }

        public override Expression Uniquefy(Dictionary<TermVariable, TermVariable> newVarMap)
        {
            var newTerms = new Term[Terms.Length];

            for (int i = 0; i < Terms.Length; i++)
                newTerms[i] = Terms[i].Uniquefy(newVarMap);

            return new VariableFact(false, RelationName, newTerms);
        }

        internal VariableFact Clone(int newRelName)
        {
            return new VariableFact(false, newRelName, Terms);
        }

        internal static VariableFact CloneWithEmptyTerms(Fact fact)
        {
            var terms = new Term[fact.Arity];
            for (int i = 0; i < fact.Arity; i++)
            {
                var termFunction = terms[i] as TermFunction;
                if (termFunction != null)
                    terms[i] = termFunction.CloneWithEmptyTerms();
                else
                    terms[i] = TermVariable.MakeTermVariable();
            }
            return new VariableFact(false, fact.RelationName, terms);
        }
    }

}

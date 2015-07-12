using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nJocLogic.data;

namespace nJocLogic.util.gdl.model.assignments
{
    public class AssignmentFunctionQueryTuple : List<TermObject>
    {
        public AssignmentFunctionQueryTuple(int querySize) : base(querySize) { }

        public AssignmentFunctionQueryTuple(List<TermObject> tuplePart) : base(tuplePart) { }

        public override int GetHashCode()
        {
            return this.Aggregate(1, (current, to) => 31 * current + (to == null ? 0 : to.GetHashCode()));
        }

        public override bool Equals(object obj)
        {
            var list = obj as AssignmentFunctionQueryTuple;
            if (list == null)
                return false;

            return list.SequenceEqual(this);
        }

        public override string ToString()
        {
            return string.Join(",", this);
        }
    }

    public class AssignmentFunction
    {
        //How is the AssignmentFunction going to operate?
        //Well, some of the variables are going to be specified as having one or more of these functions
        //apply to them. (If multiple apply, they all have to agree.)
        //We pass in the current value of the tuple and it gives us the value desired (or null).
        //This means it just has to know which indices in the tuple (i.e. which variables) correspond to
        //which slots in its native tuple.

        //Used when multiple assignment functions are relevant to the same variable. In this case we call these other
        //functions with the same arguments and return null if any of the answers differ.
        private readonly ImmutableList<AssignmentFunction> _internalFunctions;
        private readonly int _querySize;
        private readonly ImmutableList<bool> _isInputConstant;
        private readonly ImmutableDictionary<int, TermObject> _queryConstants;
        private readonly ImmutableList<int> _queryInputIndices;
        private readonly ImmutableDictionary<AssignmentFunctionQueryTuple, TermObject> _function;
        //Some sort of trie might work better here...

        private AssignmentFunction(ImmutableList<AssignmentFunction> internalFunctions,
                                   int querySize,
                                   ImmutableList<bool> isInputConstant,
                                   ImmutableDictionary<int, TermObject> queryConstants,
                                   ImmutableList<int> queryInputIndices,
                                   ImmutableDictionary<AssignmentFunctionQueryTuple, TermObject> function)
        {
            _internalFunctions = internalFunctions;
            _querySize = querySize;
            _isInputConstant = isInputConstant;
            _queryConstants = queryConstants;
            _queryInputIndices = queryInputIndices;
            _function = function;
        }

        public static AssignmentFunction Create(Fact conjunct, FunctionInfo functionInfo,
                                                TermVariable rightmostVar, List<TermVariable> varOrder,
                                                TermObjectSubstitution preassignment)
        {
            //We have to set up the things mentioned above...
            var internalFunctions = new List<AssignmentFunction>();

            //We can traverse the conjunct for the list of variables/constants...
            var terms = new List<Term>();
            GatherVars(conjunct.GetTerms(), terms);
            //Note that we assume here that the var of interest only
            //appears once in the relation...
            int varIndex = terms.IndexOf(rightmostVar);
            if (varIndex == -1)
            {
                Console.WriteLine("conjunct is: " + conjunct);
                Console.WriteLine("terms are: " + terms);
                Console.WriteLine("righmostVar is: " + rightmostVar);
            }
            terms.Remove(rightmostVar);
            IDictionary<AssignmentFunctionQueryTuple, TermObject> function = functionInfo.GetValueMap(varIndex);

            //Set up inputs and such, using terms
            int querySize = terms.Count;
            var isInputConstant = new List<bool>(terms.Count);
            IDictionary<int, TermObject> queryConstants = new Dictionary<int, TermObject>();
            var queryInputIndices = new List<int>(terms.Count);
            for (int i = 0; i < terms.Count; i++)
            {
                Term term = terms[i];
                var termObject = term as TermObject;
                if (termObject != null)
                {
                    isInputConstant.Add(true);
                    queryConstants[i] = termObject;
                    queryInputIndices.Add(-1);
                }
                else
                {
                    var termVariable = term as TermVariable;
                    if (termVariable != null)
                    {
                        //Is it in the head assignment?
                        Term mapping = preassignment.GetMapping(termVariable);
                        if (mapping != null)
                        {
                            isInputConstant.Add(true);
                            queryConstants[i] = (TermObject)mapping;
                            queryInputIndices.Add(-1);
                        }
                        else
                        {
                            isInputConstant.Add(false);
                            //						queryConstants.Add(null);
                            //What value do we put here?
                            //We want to grab some value out of the
                            //input tuple, which uses functional ordering
                            //Index of the relevant variable, by the
                            //assignment's ordering
                            queryInputIndices.Add(varOrder.IndexOf(termVariable));
                        }
                    }
                }
            }
            return new AssignmentFunction(
                internalFunctions.ToImmutableList(),
                querySize,
                isInputConstant.ToImmutableList(),
                queryConstants.ToImmutableDictionary(),
                queryInputIndices.ToImmutableList(),
                function.ToImmutableDictionary());
        }

        public bool Functional()
        {
            return _function != null;
        }

        private static void GatherVars(IEnumerable<Term> body, List<Term> terms)
        {
            foreach (Term term in body)
            {
                if (term is TermObject || term is TermVariable)
                    terms.Add(term);
                else
                {
                    var termFunction = term as TermFunction;
                    if (termFunction != null)
                        GatherVars(termFunction.Arguments.ToList(), terms);
                }
            }
        }

        public TermObject GetValue(List<TermObject> remainingTuple)
        {
            //We have a map from a tuple of GdlConstants to the TermObject we need, provided by the FunctionInfo.
            //We need to make the tuple for this map.
            AssignmentFunctionQueryTuple queryTuple = new AssignmentFunctionQueryTuple(_querySize);
            //Now we have to fill in the query
            for (int i = 0; i < _querySize; i++)
                queryTuple.Add(_isInputConstant[i] ? _queryConstants[i] : remainingTuple[_queryInputIndices[i]]);

            //The query is filled; we ask the map
            TermObject answer;
            _function.TryGetValue(queryTuple, out answer);

            if (_internalFunctions.Any(i => !i.GetValue(remainingTuple).Equals(answer)))
                return null;

            return answer;
        }
    }
}
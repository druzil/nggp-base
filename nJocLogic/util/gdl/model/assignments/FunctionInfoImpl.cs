using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nJocLogic.data;

namespace nJocLogic.util.gdl.model.assignments
{
    /// <summary>
    /// Represents information about a sentence form that is constant.
    /// </summary>
    public class FunctionInfoImpl : FunctionInfo
    {
        private readonly ISentenceForm _form;

        //True iff the slot has at most one value given the other slots' values
        private readonly List<bool> _dependentSlots = new List<bool>();
        private readonly List<IDictionary<AssignmentFunctionQueryTuple, TermObject>> _valueMaps = new List<IDictionary<AssignmentFunctionQueryTuple, TermObject>>();

        public FunctionInfoImpl(ISentenceForm form, ISet<Fact> trueSentences)
        {
            _form = form;

            int numSlots = form.TupleSize;

            for (int i = 0; i < numSlots; i++)
            {
                //We want to establish whether or not this is a constant...
                IDictionary<AssignmentFunctionQueryTuple, TermObject> functionMap = new Dictionary<AssignmentFunctionQueryTuple, TermObject>();
                bool functional = true;
                foreach (Fact sentence in trueSentences)
                {
                    //ConcurrencyUtils.checkForInterruption();
                    List<TermObject> tuple = sentence.TermObjects.ToList();
                    var tuplePart = new AssignmentFunctionQueryTuple(tuple.Count - 1);
                    tuplePart.AddRange(tuple.GetRange(0, i));
                    tuplePart.AddRange(tuple.GetRange(i + 1, tuple.Count - i - 1));
                    if (functionMap.ContainsKey(tuplePart))
                    {
                        //We have two tuples with different values in just this slot
                        functional = false;
                        break;
                    }
                    //Otherwise, we record it
                    functionMap[new AssignmentFunctionQueryTuple(tuplePart)] = tuple[i];
                }

                if (functional)
                {
                    //Record the function
                    _dependentSlots.Add(true);
                    _valueMaps.Add(functionMap);
                }
                else
                {
                    //Forget it
                    _dependentSlots.Add(false);
                    _valueMaps.Add(null);
                }
            }
        }

        public IDictionary<AssignmentFunctionQueryTuple, TermObject> GetValueMap(int index)
        {
            return _valueMaps[index];
        }

        public List<bool> GetDependentSlots()
        {
            return _dependentSlots;
        }

        /**
     * Given a sentence of the constant form's sentence form, finds all
     * the variables in the sentence that can be produced functionally.
     *
     * Note the corner casein If a variable appears twice in a sentence,
     * it CANNOT be produced in this way.
     */
        public ISet<TermVariable> GetProducibleVars(Fact sentence)
        {
            if (!_form.Matches(sentence))
                throw new Exception("Sentence " + sentence + " does not match constant form");
            List<Term> tuple = sentence.NestedTerms.ToList();

            ISet<TermVariable> candidateVars = new HashSet<TermVariable>();
            //Variables that appear multiple times go into multipleVars
            ISet<TermVariable> multipleVars = new HashSet<TermVariable>();
            //...which, of course, means we have to spot non-candidate vars
            ISet<TermVariable> nonCandidateVars = new HashSet<TermVariable>();

            for (int i = 0; i < tuple.Count; i++)
            {
                Term term = tuple[i];
                var termVariable = term as TermVariable;
                if (termVariable != null && !multipleVars.Contains(term))
                {
                    var var = termVariable;
                    if (candidateVars.Contains(var) || nonCandidateVars.Contains(var))
                    {
                        multipleVars.Add(var);
                        candidateVars.Remove(var);
                    }
                    else if (_dependentSlots[i])
                        candidateVars.Add(var);
                    else
                        nonCandidateVars.Add(var);
                }
            }

            return candidateVars;

        }
        public static FunctionInfo Create(ISentenceForm form, IConstantChecker constantChecker)
        {
            return new FunctionInfoImpl(form, constantChecker.GetTrueSentences(form));
        }

        public static FunctionInfo Create(ISentenceForm form, ISet<Fact> set)
        {
            return new FunctionInfoImpl(form, set);
        }

        public ISentenceForm GetSentenceForm()
        {
            return _form;
        }

        public override String ToString()
        {
            return "FunctionInfoImpl [form=" + _form + ", dependentSlots=" + _dependentSlots + ", valueMaps=" + _valueMaps + "]";
        }
    }
}
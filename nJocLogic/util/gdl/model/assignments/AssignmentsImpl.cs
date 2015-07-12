using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nJocLogic.data;
using nJocLogic.gameContainer;

namespace nJocLogic.util.gdl.model.assignments
{

    public class AssignmentsImpl : IEnumerable<TermObjectSubstitution>
    {
        private bool _empty;
        private bool _allDone;
        //Contains all the assignments of variables we could make
        private readonly TermObjectSubstitution _headAssignment;

        private readonly List<TermVariable> _varsToAssign;
        private readonly List<ImmutableList<TermObject>> _valuesToIterate;
        private readonly List<AssignmentFunction> _valuesToCompute;
        private readonly List<int> _indicesToChangeWhenNull; //See note below
        private readonly List<Fact> _distincts;
        private List<TermVariable> _varsToChangePerDistinct; //indexing same as distincts

        /// <summary>
        /// What does indicesToChangeWhenNull do? Well, sometimes after incrementing
        /// part of the iterator, we find that a function being used to define a slot
        /// in the tuple has no value corresponding to its inputs (the inputs are
        /// outside the function's domain). In that case, we set the value to null,
        /// then leave it to the makeNextAssignmentValid() method to deal with it.
        /// We want to increment something in the input, but we need to know what
        /// in the input we should increment (i.e. which is the rightmost slot in
        /// the function's input). This is recorded in indicesToChangeWhenNull. If
        /// a slot is not defined by a function, then presumably it will not be null,
        /// so its value here is unimportant. Setting its value to -1 would help
        /// catch errors.
        /// </summary>
        private readonly List<ImmutableList<ImmutableList<TermObject>>> _tuplesBySource; //indexed by conjunct
        private readonly List<int> _sourceDefiningSlot; //indexed by var slot
        private readonly List<ImmutableList<int>> _varsChosenBySource; //indexed by conjunct, then slot
        private readonly List<ImmutableList<bool>> _putDontCheckBySource; //indexed by conjunct, then slot

        /// <summary>
        /// Creates an Assignments object that generates AssignmentIterators.
        /// These can be used to efficiently iterate over all possible assignments
        /// foreach variables in a given rule.
        /// </summary>
        /// <param name="headAssignment">Assignment An assignment of variables whose values should be fixed. May be empty.</param>
        /// <param name="rule">The rule whose assignments we want to iterate over.</param>
        /// <param name="varDomains">A map containing the possible values foreach each variable in the rule. (All such values are GdlConstants.)</param>
        /// <param name="functionInfoMap"></param><param name="completedSentenceFormValues"></param>
        public AssignmentsImpl(TermObjectSubstitution headAssignment,
                               Implication rule, IDictionary<TermVariable, ISet<TermObject>> varDomains,
                               Dictionary<ISentenceForm, FunctionInfo> functionInfoMap,
                               IDictionary<ISentenceForm, ICollection<Fact>> completedSentenceFormValues)
        {
            _empty = false;
            _headAssignment = headAssignment;

            //We first have to find the remaining variables in the body
            _varsToAssign = rule.VariablesOrEmpty.ToList();
            //Remove all the duplicates; we do, however, want to keep the ordering
            var newVarsToAssign = new List<TermVariable>();
            var replacees = headAssignment.GetReplacees();
            foreach (TermVariable v in _varsToAssign)
                if (!newVarsToAssign.Contains(v) && !replacees.Contains(v))
                    newVarsToAssign.Add(v);
            _varsToAssign = newVarsToAssign;
            //varsToAssign is set at this point

            //We see if iterating over entire tuples will give us a
            //better result, and we look foreach the best way of doing that.

            //Let's get the domains of the variables
            //Dictionary<TermVariable, ISet<TermObject>> varDomains = model.getVarDomains(rule);
            //Since we're looking at a particular rule, we can do this one step better
            //by looking at the domain of the head, which may be more restrictive
            //and taking the intersections of the two domains where applicable
            //Dictionary<TermVariable, ISet<TermObject>> headVarDomains = model.getVarDomainsInSentence(rule.getHead());

            //We can run the A* search foreach a good set of source conjuncts
            //at this point, then use the result to build the rest.
            var completedSentenceFormSizes = new Dictionary<ISentenceForm, int>();
            if (completedSentenceFormValues != null)
                foreach (ISentenceForm form in completedSentenceFormValues.Keys)
                    completedSentenceFormSizes[form] = completedSentenceFormValues[form].Count;

            IterationOrderCandidate bestOrdering = GetBestIterationOrderCandidate(rule, varDomains, functionInfoMap, completedSentenceFormSizes, headAssignment, false);

            //Want to replace next few things with order
            //Need a few extra things to handle the use of iteration over existing tuples
            _varsToAssign = bestOrdering.GetVariableOrdering();

            //For each of these vars, we have to find one or the other.
            //Let's start by finding all the domains, a task already done.
            _valuesToIterate = new List<ImmutableList<TermObject>>(_varsToAssign.Count);

            foreach (TermVariable var in _varsToAssign)
            {
                if (varDomains.ContainsKey(var))
                {
                    _valuesToIterate.Add(varDomains[var].Any()
                                            ? varDomains[var].ToImmutableList()
                                            : ImmutableList.Create(TermObject.MakeTermObject(GameContainer.SymbolTable["0"])));
                }
                else
                    _valuesToIterate.Add(ImmutableList.Create(TermObject.MakeTermObject(GameContainer.SymbolTable["0"])));
            }
            //Okay, the iteration-over-domain is done.
            //Now let's look at sourced iteration.
            _sourceDefiningSlot = new List<int>(_varsToAssign.Count);
            for (int i = 0; i < _varsToAssign.Count; i++)
                _sourceDefiningSlot.Add(-1);

            //We also need to convert values into tuples
            //We should do so while constraining to any constants in the conjunct
            //Let's convert the conjuncts
            List<Fact> sourceConjuncts = bestOrdering.GetSourceConjuncts();
            _tuplesBySource = new List<ImmutableList<ImmutableList<TermObject>>>(sourceConjuncts.Count);//new List<List<List<TermObject>>>(sourceConjuncts.Count);
            _varsChosenBySource = new List<ImmutableList<int>>(sourceConjuncts.Count);//new List<List<int>>(sourceConjuncts.Count);
            _putDontCheckBySource = new List<ImmutableList<bool>>(sourceConjuncts.Count);//new List<List<bool>>(sourceConjuncts.Count);
            for (int j = 0; j < sourceConjuncts.Count; j++)
            {
                Fact sourceConjunct = sourceConjuncts[j];
                var form = new SimpleSentenceForm(sourceConjunct);
                //flatten into a tuple
                List<Term> conjunctTuple = sourceConjunct.NestedTerms.ToList();
                //Go through the vars/constants in the tuple
                var constraintSlots = new List<int>();
                var constraintValues = new List<TermObject>();
                var varsChosen = new List<int>();
                var putDontCheck = new List<bool>();
                for (int i = 0; i < conjunctTuple.Count; i++)
                {
                    Term term = conjunctTuple[i];
                    var termObject = term as TermObject;
                    if (termObject != null)
                    {
                        constraintSlots.Add(i);
                        constraintValues.Add(termObject);
                        //TODO: What if tuple size ends up being 0?
                        //Need to keep that in mind
                    }
                    else
                    {
                        var termVariable = term as TermVariable;
                        if (termVariable != null)
                        {
                            int varIndex = _varsToAssign.IndexOf(termVariable);
                            varsChosen.Add(varIndex);
                            if (_sourceDefiningSlot[varIndex] == -1)
                            {
                                //We define it
                                _sourceDefiningSlot[varIndex] = j;
                                putDontCheck.Add(true);
                            }
                            else //It's an overlap; we just check foreach consistency
                                putDontCheck.Add(false);
                        }
                        else
                            throw new Exception("Function returned in tuple");
                    }
                }
                _varsChosenBySource.Add(varsChosen.ToImmutableList());
                _putDontCheckBySource.Add(putDontCheck.ToImmutableList());

                //Now we put the tuples together
                //We use constraintSlots and constraintValues to check that the
                //tuples have compatible values
                ICollection<Fact> sentences = completedSentenceFormValues[form];
                var tuples = new List<ImmutableList<TermObject>>();
                foreach (Fact sentence in sentences)
                {
                    //Check that it doesn't conflict with our headAssignment
                    bool doBreak = false;
                    if (headAssignment.NumMappings() > 0)
                    {
                        Substitution tupleAssignment = sourceConjunct.Unify(sentence);
                        Func<TermVariable, bool> predicate = var => tupleAssignment.GetMapping(var) != null
                                                                    && !tupleAssignment.GetMapping(var).Equals(headAssignment.GetMapping(var));
                        if (headAssignment.GetReplacees().Any(predicate))
                            continue;
                    }

                    List<TermObject> longTuple = sentence.TermObjects.ToList();
                    var shortTuple = new List<TermObject>(varsChosen.Count);
                    for (int c = 0; c < constraintSlots.Count; c++)
                    {
                        int slot = constraintSlots[c];
                        TermObject value = constraintValues[c];
                        if (!longTuple[slot].Equals(value))
                        {
                            doBreak = true;
                            break;
                        }
                    }
                    if (doBreak)
                        continue;

                    int c1 = 0;
                    for (int s = 0; s < longTuple.Count; s++)
                    {
                        //constraintSlots is sorted in ascending order
                        if (c1 < constraintSlots.Count && constraintSlots[c1] == s)
                            c1++;
                        else
                            shortTuple.Add(longTuple[s]);
                    }
                    //The tuple fits the source conjunct
                    tuples.Add(shortTuple.ToImmutableList());
                }
                //sortTuples(tuples); //Needed? Useful? Not sure. Probably not?
                _tuplesBySource.Add(tuples.ToImmutableList());
            }


            //We now want to see which we can give assignment functions to
            _valuesToCompute = new List<AssignmentFunction>(_varsToAssign.Count);
            foreach (TermVariable var in _varsToAssign)
                _valuesToCompute.Add(null);

            _indicesToChangeWhenNull = new List<int>(_varsToAssign.Count);
            for (int i = 0; i < _varsToAssign.Count; i++)   //Change itself, why not? Actually, instead let's try -1, to catch bugs better
                _indicesToChangeWhenNull.Add(-1);

            //Now we have our functions already selected by the ordering
            //bestOrdering.functionalConjunctIndices;

            //Make AssignmentFunctions out of the ordering
            List<Fact> functionalConjuncts = bestOrdering.GetFunctionalConjuncts();
            //		System.out.println("functionalConjunctsin " + functionalConjuncts);
            foreach (Fact functionalConjunct in functionalConjuncts)
            {
                if (functionalConjunct != null)
                {
                    //These are the only ones that could be constant functions
                    var conjForm = new SimpleSentenceForm(functionalConjunct);
                    FunctionInfo functionInfo = null;

                    if (functionInfoMap != null)
                        functionInfo = functionInfoMap[conjForm];

                    if (functionInfo != null)
                    {
                        //Now we need to figure out which variables are involved
                        //and which are suitable as functional outputs.

                        //1) Which vars are in this conjunct?
                        List<TermVariable> varsInSentence = functionalConjunct.VariablesOrEmpty.ToList();
                        //2) Of these vars, which is "rightmost"?
                        TermVariable rightmostVar = GetRightmostVar(varsInSentence);
                        //3) Is it only used once in the relation?
                        if (varsInSentence.Count(v => v.Equals(rightmostVar)) != 1)
                            continue; //Can't use it
                        //4) Which slot is it used in in the relation?
                        //5) Build an AssignmentFunction if appropriate.
                        //   This should be able to translate from values of
                        //   the other variables to the value of the wanted
                        //   variable.
                        AssignmentFunction function = AssignmentFunction.Create(functionalConjunct, functionInfo, rightmostVar, _varsToAssign, headAssignment);
                        //We don't guarantee that this works until we check
                        if (!function.Functional())
                            continue;
                        int index = _varsToAssign.IndexOf(rightmostVar);
                        _valuesToCompute[index] = function;
                        ISet<TermVariable> remainingVarsInSentence = new HashSet<TermVariable>(varsInSentence);
                        remainingVarsInSentence.Remove(rightmostVar);
                        TermVariable nextRightmostVar = GetRightmostVar(remainingVarsInSentence);
                        _indicesToChangeWhenNull[index] = _varsToAssign.IndexOf(nextRightmostVar);
                    }
                }
            }

            //We now have the remainingVars also assigned their domains
            //We also cover the distincts here
            //Assume these are just variables and constants
            _distincts = new List<Fact>();
            foreach (Fact literal in rule.Antecedents.Conjuncts.OfType<Fact>())
                if (literal.RelationName == GameContainer.Parser.TokDistinct)
                    _distincts.Add(literal);

            ComputeVarsToChangePerDistinct();

            //Need to Add "distinct" restrictions to head assignment, too...
            CheckDistinctsAgainstHead();

            //We are ready foreach iteration
            //		System.out.println("headAssignmentin " + headAssignment);
            //		System.out.println("varsToAssignin " + varsToAssign);
            //		System.out.println("valuesToComputein " + valuesToCompute);
            //		System.out.println("sourceDefiningSlotin " + sourceDefiningSlot);
        }

        private TermVariable GetRightmostVar(ICollection<TermVariable> vars)
        {
            TermVariable rightmostVar = null;
            foreach (TermVariable var in _varsToAssign.Where(vars.Contains))
                rightmostVar = var;
            return rightmostVar;
        }

        public AssignmentsImpl()
        {
            //The assignment is impossible; return nothing
            _empty = true;
        }

        public AssignmentsImpl(Implication rule, IDictionary<TermVariable, ISet<TermObject>> varDomains,
                               Dictionary<ISentenceForm, FunctionInfo> functionInfoMap,
                               IDictionary<ISentenceForm, ICollection<Fact>> completedSentenceFormValues)
            : this(new TermObjectSubstitution(), rule, varDomains, functionInfoMap, completedSentenceFormValues)
        {
        }

        private void CheckDistinctsAgainstHead()
        {
            foreach (Fact distinct in _distincts)
            {
                Term term1 = distinct.GetTerm(0).ApplySubstitution(_headAssignment);
                Term term2 = distinct.GetTerm(1).ApplySubstitution(_headAssignment);
                if (term1.Equals(term2))
                {
                    //This fails
                    _empty = true;
                    _allDone = true;
                }
            }
        }

        private AssignmentIterationPlan GetPlan()
        {
            return AssignmentIterationPlan.Create(_varsToAssign,
                                                  _tuplesBySource,
                                                  _headAssignment,
                                                  _indicesToChangeWhenNull,
                                                  _distincts,
                                                  _varsToChangePerDistinct,
                                                  _valuesToCompute,
                                                  _sourceDefiningSlot,
                                                  _valuesToIterate,
                                                  _varsChosenBySource,
                                                  _putDontCheckBySource,
                                                  _empty,
                                                  _allDone);
        }

        private void ComputeVarsToChangePerDistinct()
        {
            //remember that iterators must be set up first
            _varsToChangePerDistinct = new List<TermVariable>(_varsToAssign.Count);
            foreach (Fact distinct in _distincts)
            {
                //For two vars, we want to record the later of the two
                //For one var, we want to record the one
                //For no vars, we just put null
                var varsInDistinct = new List<TermVariable>(2);
                if (distinct.GetTerm(0) is TermVariable)
                    varsInDistinct.Add((TermVariable)distinct.GetTerm(0));
                if (distinct.GetTerm(1) is TermVariable)
                    varsInDistinct.Add((TermVariable)distinct.GetTerm(1));

                TermVariable varToChange = null;
                if (varsInDistinct.Count == 1)
                    varToChange = varsInDistinct[0];
                else if (varsInDistinct.Count == 2)
                    varToChange = GetRightmostVar(varsInDistinct);
                _varsToChangePerDistinct.Add(varToChange);
            }
        }

        public static AssignmentsImpl GetAssignmentsProducingSentence(
            Implication rule, Fact sentence, /*SentenceModel model,*/ Dictionary<TermVariable, ISet<TermObject>> varDomains,
            Dictionary<ISentenceForm, FunctionInfo> functionInfoMap,
            Dictionary<ISentenceForm, ICollection<Fact>> completedSentenceFormValues)
        {
            //First, we see which variables must be set according to the rule head
            //(and see if there's any contradiction)
            var headAssignment = new TermObjectSubstitution();
            if (!SetVariablesInHead(rule.Consequent, sentence, headAssignment))
                return new AssignmentsImpl(); //Collections.emptySet();

            //Then we come up with all the assignments of the rest of the variables
            //We need to look foreach functions we can make use of

            return new AssignmentsImpl(headAssignment, rule, varDomains, functionInfoMap, completedSentenceFormValues);
        }

        //returns true if all variables were set successfully
        private static bool SetVariablesInHead(Fact head, Fact sentence, TermObjectSubstitution assignment)
        {
            return head.Arity == 0 || SetVariablesInHead(head.GetTerms(), sentence.GetTerms(), assignment);
        }

        private static bool SetVariablesInHead(List<Term> head, List<Term> sentence, TermObjectSubstitution assignment)
        {
            for (int i = 0; i < head.Count; i++)
            {
                Term headTerm = head[i];
                Term refTerm = sentence[i];
                if (headTerm is TermObject)
                {
                    if (!refTerm.Equals(headTerm))
                        //The rule can't produce this sentence
                        return false;
                }
                else
                {
                    var termVariable = headTerm as TermVariable;
                    if (termVariable != null)
                    {
                        var var = termVariable;
                        var curValue = assignment.GetMapping(var);
                        if (curValue != null && !curValue.Equals(refTerm)) //inconsistent assignment (e.g. head is (rel ?x ?x), sentence is (rel 1 2))
                            return false;

                        assignment.AddMapping(var, refTerm);
                    }
                    else
                    {
                        var termFunction = headTerm as TermFunction;
                        if (termFunction != null)
                        {
                            //Recurse on the body
                            var headFunction = termFunction;
                            var refFunction = (TermFunction)refTerm;
                            if (!SetVariablesInHead(headFunction.Arguments.ToList(), refFunction.Arguments.ToList(), assignment))
                                return false;
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Finds the iteration order (including variables, functions, and
        /// source conjuncts) that is expected to result in the fastest iteration.
        /// 
        /// The value that is compared foreach each ordering is the product of:
        /// - For each source conjunct, the number of tuples offered by the conjunct;
        /// - For each variable not defined by a function, the size of its domain.        
        /// </summary>
        /// <param name="rule"></param>
        /// <param name="varDomains"></param>
        /// <param name="functionInfoMap"></param>
        /// <param name="completedSentenceFormSizes">
        /// For each sentence form, this may optionally
        /// contain the number of possible sentences of this form. This is useful if the
        /// number of sentences is much lower than the product of its variables' domain
        /// sizes; however, if this Contains sentence forms where the set of sentences
        /// is unknown, then it may return an ordering that is unusable.
        /// </param>
        /// <param name="preassignment"></param>
        /// <param name="analyticFunctionOrdering"></param>
        /// <returns></returns>
        protected static IterationOrderCandidate GetBestIterationOrderCandidate(Implication rule,
                                                                                IDictionary<TermVariable, ISet<TermObject>> varDomains,
                                                                                Dictionary<ISentenceForm, FunctionInfo> functionInfoMap,
                                                                                Dictionary<ISentenceForm, int> completedSentenceFormSizes,
                                                                                TermObjectSubstitution preassignment,
                                                                                bool analyticFunctionOrdering)
        {
            //Here are the things we need to pass into the first IOC constructor
            var sourceConjunctCandidates = new List<Fact>();
            //What is a source conjunct candidate?
            //- It is a positive conjunct in the rule (i.e. a Fact in the body).
            //- It has already been fully defined; i.e. it is not recursively defined in terms of the current form.
            //Furthermore, we know the number of potentially true tuples in it.
            List<TermVariable> varsToAssign = rule.VariablesOrEmpty.ToList();
            var newVarsToAssign = new List<TermVariable>();
            foreach (TermVariable var in varsToAssign)
                if (!newVarsToAssign.Contains(var))
                    newVarsToAssign.Add(var);
            varsToAssign = newVarsToAssign;
            if (preassignment != null)
                varsToAssign.RemoveAll(v => preassignment.GetReplacees().Contains(v));

            //Calculate var domain sizes
            Dictionary<TermVariable, int> varDomainSizes = GetVarDomainSizes(varDomains);

            var sourceConjunctSizes = new List<int>();

            if (completedSentenceFormSizes != null)
                foreach (Expression conjunct in rule.Antecedents.Conjuncts)
                {
                    var fact = conjunct as Fact;
                    if (fact != null)
                    {
                        var form = new SimpleSentenceForm(fact);
                        if (completedSentenceFormSizes.ContainsKey(form))
                        {
                            int size = completedSentenceFormSizes[form];
                            //Newin Don't Add if it will be useless as a source
                            //For now, we take a strict definition of that
                            //Compare its size with the product of the domains
                            //of the variables it defines
                            //In the future, we could require a certain ratio
                            //to decide that this is worthwhile
                            var relation = fact;
                            ISet<TermVariable> vars = new HashSet<TermVariable>(relation.VariablesOrEmpty);
                            int maxSize = vars.Select(var => varDomainSizes[var]).Aggregate(1, (current, domainSize) => current * domainSize);
                            if (size < maxSize)
                            {
                                sourceConjunctCandidates.Add(relation);
                                sourceConjunctSizes.Add(size);
                            }
                        }
                    }
                }

            var functionalSentences = new List<Fact>();
            var functionalSentencesInfo = new List<FunctionInfo>();
            if (functionInfoMap != null)
                foreach (Expression conjunct in rule.Antecedents.Conjuncts)
                {
                    var fact = conjunct as Fact;
                    if (fact != null)
                    {
                        var form = new SimpleSentenceForm(fact);
                        if (functionInfoMap.ContainsKey(form))
                        {
                            functionalSentences.Add(fact);
                            functionalSentencesInfo.Add(functionInfoMap[form]);
                        }
                    }
                }

            //TODO: If we have a head assignment, treat everything as already replaced
            //Maybe just translate the rule? Or should we keep the pool clean?

            var emptyCandidate = new IterationOrderCandidate(varsToAssign, sourceConjunctCandidates,
                                                             sourceConjunctSizes, functionalSentences, functionalSentencesInfo, varDomainSizes);
            var searchQueue = new C5.IntervalHeap<IterationOrderCandidate> { emptyCandidate };

            while (searchQueue.Any())
            {
                IterationOrderCandidate curNode = searchQueue.DeleteMin();
                //			System.out.println("Node being checked outin " + curNode);
                if (curNode.IsComplete())   //This is the complete ordering with the lowest heuristic value                    
                    return curNode;

                searchQueue.AddAll(curNode.GetChildren(analyticFunctionOrdering));
            }
            throw new Exception("Found no complete iteration orderings");
        }

        private static Dictionary<TermVariable, int> GetVarDomainSizes(IDictionary<TermVariable, ISet<TermObject>> varDomains)
        {
            var varDomainSizes = new Dictionary<TermVariable, int>();
            foreach (TermVariable var in varDomains.Keys)
                varDomainSizes[var] = varDomains[var].Count;
            return varDomainSizes;
        }

        public static long GetNumAssignmentsEstimate(Implication rule, IDictionary<TermVariable, ISet<TermObject>> varDomains, IConstantChecker checker)
        {
            //First we need the best iteration order
            //Arguments we'll need to pass inin
            //- A SentenceModel
            //- constant forms
            //- completed sentence form sizes
            //- Variable domain sizes?

            var functionInfoMap = new Dictionary<ISentenceForm, FunctionInfo>();
            foreach (ISentenceForm form in checker.ConstantSentenceForms)
                functionInfoMap[form] = FunctionInfoImpl.Create(form, checker);

            //Populate variable domain sizes using the constant checker
            //var domainSizes = new Dictionary<ISentenceForm, int>();
            //foreach (ISentenceForm form in checker.ConstantSentenceForms)
            //    domainSizes[form] = checker.GetTrueSentences(form).Count;
            //TODO: Propagate these domain sizes as estimates foreach other rules?
            //Look foreach literals in the body of the rule and their ancestors?
            //Could we possibly do this elsewhere?

            IterationOrderCandidate ordering = GetBestIterationOrderCandidate(rule, varDomains, functionInfoMap, null, null, true);
            return ordering.GetHeuristicValue();
        }

        public IEnumerator<TermObjectSubstitution> GetEnumerator()
        {
            return new AssignmentIteratorImpl(GetPlan());
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new AssignmentIteratorImpl(GetPlan());
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using nJocLogic.data;

namespace nJocLogic.util.gdl.model.assignments
{
    /// <summary>
    /// This class has a natural ordering that is inconsistent with Equals.
    /// </summary>
    public class IterationOrderCandidate : IComparable<IterationOrderCandidate>
    {
        //Information specific to this ordering
        private readonly List<int> _sourceConjunctIndices; //Which conjuncts are we using as sources, and in what order?
        private readonly List<TermVariable> _varOrdering; //In what order do we assign variables?
        private readonly List<int> _functionalConjunctIndices; //Same size as varOrdering
        //Index of conjunct if functional, -1 otherwise
        private readonly List<int> _varSources; //Same size as varOrdering
        //For each variablein Which source conjunct
        //originally contributes it? -1 if none
        //Becomes sourceResponsibleForVar

        //Information shared by the orderings
        //Presumably, this will also be used to construct the iterator to be used...
        private readonly List<TermVariable> _varsToAssign;
        private readonly List<Fact> _sourceConjunctCandidates;
        private readonly List<int> _sourceConjunctSizes; //same indexing as candidates
        private readonly List<Fact> _functionalSentences;
        private readonly List<FunctionInfo> _functionalSentencesInfo; //Indexing same as functionalSentences
        private readonly Dictionary<TermVariable, int> _varDomainSizes;

        /**
     * This constructor is foreach creating the start node of the
     * search. No part of the ordering is specified.
     *
     * @param sourceConjunctCandidates
     * @param sourceConjunctSizes
     * @param functionalSentences
     * @param functionalSentencesInfo
     * @param allVars
     * @param varDomainSizes
     */
        public IterationOrderCandidate(
            List<TermVariable> varsToAssign,
            List<Fact> sourceConjunctCandidates,
            List<int> sourceConjunctSizes,
            List<Fact> functionalSentences,
            List<FunctionInfo> functionalSentencesInfo,
            Dictionary<TermVariable, int> varDomainSizes)
        {
            _sourceConjunctIndices = new List<int>();
            _varOrdering = new List<TermVariable>();
            _functionalConjunctIndices = new List<int>();
            _varSources = new List<int>();

            _varsToAssign = varsToAssign;
            _sourceConjunctCandidates = sourceConjunctCandidates;
            _sourceConjunctSizes = sourceConjunctSizes;
            _functionalSentences = functionalSentences;
            _functionalSentencesInfo = functionalSentencesInfo;
            _varDomainSizes = varDomainSizes;
        }

        public List<Fact> GetFunctionalConjuncts()
        {
            //Returns, foreach each var, the conjunct defining it (if any)
            var functionalConjuncts = new List<Fact>(_functionalConjunctIndices.Count);
            functionalConjuncts.AddRange(_functionalConjunctIndices.Select(index => index == -1 ? null : _functionalSentences[index]));
            return functionalConjuncts;
        }

        public List<Fact> GetSourceConjuncts()
        {
            //These are the selected source conjuncts, not just the candidates.
            var sourceConjuncts = new List<Fact>(_sourceConjunctIndices.Count);
            sourceConjuncts.AddRange(_sourceConjunctIndices.Select(index => _sourceConjunctCandidates[index]));
            return sourceConjuncts;
        }

        public List<TermVariable> GetVariableOrdering()
        {
            return _varOrdering;
        }

        /**
     * This constructor is foreach "completing" the ordering by
     * adding all remaining variables, in some arbitrary order.
     * No source conjuncts or functions are added.
     */
        public IterationOrderCandidate(IterationOrderCandidate parent)
        {
            //Shared rules
            _varsToAssign = parent._varsToAssign;
            _sourceConjunctCandidates = parent._sourceConjunctCandidates;
            _sourceConjunctSizes = parent._sourceConjunctSizes;
            _functionalSentences = parent._functionalSentences;
            _functionalSentencesInfo = parent._functionalSentencesInfo;
            _varDomainSizes = parent._varDomainSizes;

            //Individual rulesin
            //We can share this because we won't be adding to it
            _sourceConjunctIndices = parent._sourceConjunctIndices;
            //These others we'll be adding to
            _varOrdering = new List<TermVariable>(parent._varOrdering);
            _functionalConjunctIndices = new List<int>(parent._functionalConjunctIndices);
            _varSources = new List<int>(parent._varSources);
            //Fill out the ordering with all remaining variablesin Easy enough
            foreach (TermVariable var in _varsToAssign)
            {
                if (!_varOrdering.Contains(var))
                {
                    _varOrdering.Add(var);
                    _functionalConjunctIndices.Add(-1);
                    _varSources.Add(-1);
                }
            }
        }

        /**
     * This constructor is foreach adding a source conjunct to an
     * ordering.
     * @param i The index of the source conjunct being added.
     */
        public IterationOrderCandidate(IterationOrderCandidate parent, int i)
        {
            //Shared rulesin
            _varsToAssign = parent._varsToAssign;
            _sourceConjunctCandidates = parent._sourceConjunctCandidates;
            _sourceConjunctSizes = parent._sourceConjunctSizes;
            _functionalSentences = parent._functionalSentences;
            _functionalSentencesInfo = parent._functionalSentencesInfo;
            _varDomainSizes = parent._varDomainSizes;

            //Individual rules:
            _sourceConjunctIndices = new List<int>(parent._sourceConjunctIndices);
            _varOrdering = new List<TermVariable>(parent._varOrdering);
            _functionalConjunctIndices = new List<int>(parent._functionalConjunctIndices);
            _varSources = new List<int>(parent._varSources);
            //Add the new source conjunct
            _sourceConjunctIndices.Add(i);
            Fact sourceConjunctCandidate = _sourceConjunctCandidates[i];
            IEnumerable<TermVariable> varsFromConjunct = sourceConjunctCandidate.VariablesOrEmpty;
            //Ignore both previously added vars and duplicates
            //Oh, but we need to be careful here, at some point.
            //i.e., what if there are multiple of the same variable
            //in a single statement?
            //That should probably be handled later.
            foreach (TermVariable var in varsFromConjunct)
            {
                if (!_varOrdering.Contains(var))
                {
                    _varOrdering.Add(var);
                    _varSources.Add(i);
                    _functionalConjunctIndices.Add(-1);
                }
            }
        }

        /**
     * This constructor is foreach adding a function to the ordering.
     */
        public IterationOrderCandidate(
            IterationOrderCandidate parent,
            Fact functionalSentence,
            int functionalSentenceIndex, TermVariable functionOutput)
        {
            //Shared rulesin
            _varsToAssign = parent._varsToAssign;
            _sourceConjunctCandidates = parent._sourceConjunctCandidates;
            _sourceConjunctSizes = parent._sourceConjunctSizes;
            _functionalSentences = parent._functionalSentences;
            _functionalSentencesInfo = parent._functionalSentencesInfo;
            _varDomainSizes = parent._varDomainSizes;

            //Individual rulesin
            _sourceConjunctIndices = new List<int>(parent._sourceConjunctIndices);
            _varOrdering = new List<TermVariable>(parent._varOrdering);
            _functionalConjunctIndices = new List<int>(parent._functionalConjunctIndices);
            _varSources = new List<int>(parent._varSources);
            //And we Add the function
            IEnumerable<TermVariable> varsInFunction = functionalSentence.VariablesOrEmpty;
            //First, Add the remaining arguments
            foreach (TermVariable var in varsInFunction)
            {
                if (!_varOrdering.Contains(var) && !var.Equals(functionOutput) && _varsToAssign.Contains(var))
                {
                    _varOrdering.Add(var);
                    _functionalConjunctIndices.Add(-1);
                    _varSources.Add(-1);
                }
            }
            //Then the output
            _varOrdering.Add(functionOutput);
            _functionalConjunctIndices.Add(functionalSentenceIndex);
            _varSources.Add(-1);
        }

        public long GetHeuristicValue()
        {
            long heuristic = _sourceConjunctIndices.Aggregate<int, long>(1, (current, sourceIndex) => current * _sourceConjunctSizes[sourceIndex]);
            for (int v = 0; v < _varOrdering.Count; v++)
                if (_varSources[v] == -1 && _functionalConjunctIndices[v] == -1) //It's not set by a source conjunct or a function
                    heuristic *= _varDomainSizes[_varOrdering[v]];

            //We want complete orderings to show up faster
            //so we Add a little incentive to pick them
            //Add 1 to the value of non-complete orderings
            if (_varOrdering.Count < _varsToAssign.Count)
                heuristic++;

            //			System.out.println("Heuristic value is " + heuristic + " with functionalConjunctIndices " + functionalConjunctIndices);
            return heuristic;
        }

        public bool IsComplete()
        {
            return _varsToAssign.TrueForAll(_varOrdering.Contains);
        }

        public List<IterationOrderCandidate> GetChildren(bool analyticFunctionOrdering)
        {
            var allChildren = new List<IterationOrderCandidate>();
            allChildren.AddRange(GetSourceConjunctChildren());
            allChildren.AddRange(GetFunctionAddedChildren(analyticFunctionOrdering));
            //			System.out.println("Number of children being addedin " + allChildren.Count);
            return allChildren;
        }

        private IEnumerable<IterationOrderCandidate> GetSourceConjunctChildren()
        {
            var children = new List<IterationOrderCandidate>();

            //If we are already using functions, short-circuit to cut off
            //repetition of the search space
            if (_functionalConjunctIndices.Any(index => index != -1))
                return new List<IterationOrderCandidate>();

            //This means we want a reference to the original list of conjuncts.
            int lastSourceConjunctIndex = -1;
            if (_sourceConjunctIndices.Any())
                lastSourceConjunctIndex = _sourceConjunctIndices[_sourceConjunctIndices.Count - 1];

            for (int i = lastSourceConjunctIndex + 1; i < _sourceConjunctCandidates.Count; i++)
                children.Add(new IterationOrderCandidate(this, i));
            return children;
        }

        private IEnumerable<IterationOrderCandidate> GetFunctionAddedChildren(bool analyticFunctionOrdering)
        {
            //We can't just Add those functions that
            //are "ready" to be added. We should be adding all those variables
            //"leading up to" the functions and then applying the functions.
            //We can even take this one step further by only adding one child
            //per remaining constant function; we choose as our function output the
            //variable that is a candidate foreach functionhood that has the
            //largest domain, or one that is tied foreach largest.
            //New criterionin Must also NOT be in preassignment.

            var children = new List<IterationOrderCandidate>();

            //It would be really nice here to just analytically choose
            //the set of functions we're going to use.
            //Here's one approach foreach doing thatin
            //For each variable, get a list of the functions that could
            //potentially produce it.
            //For all the variables with no functions, Add them.
            //Then repeatedly find the function with the fewest
            //number of additional variables (hopefully 0!) needed to
            //specify it and Add it as a function.
            //The goal here is not to be optimal, but to be efficient!
            //Certain games (e.g. Pentago) break the old complete search method!

            //TODOin Eventual possible optimization herein
            //If something is dependent on a connected component that it is
            //not part of, wait until the connected component is resolved
            //(or something like that...)
            if (analyticFunctionOrdering && _functionalSentencesInfo.Count > 8)
            {
                //For each variable, a list of functions
                //(refer to functions by their indices)
                //and the set of outstanding vars they depend on...
                IDictionary<TermVariable, ISet<int>> functionsProducingVars = new Dictionary<TermVariable, ISet<int>>();
                //We start by adding to the varOrdering the vars not produced by functions
                //First, we have to find them
                for (int i = 0; i < _functionalSentencesInfo.Count; i++)
                {
                    Fact functionalSentence = _functionalSentences[i];
                    FunctionInfo functionInfo = _functionalSentencesInfo[i];
                    ISet<TermVariable> producibleVars = functionInfo.GetProducibleVars(functionalSentence);
                    foreach (TermVariable producibleVar in producibleVars)
                    {
                        if (!functionsProducingVars.ContainsKey(producibleVar))
                            functionsProducingVars[producibleVar] = new HashSet<int>();
                        functionsProducingVars[producibleVar].Add(i);
                    }
                }
                //Non-producible vars get iterated over before we start
                //deciding which functions to Add
                foreach (TermVariable var in _varsToAssign)
                {
                    if (!_varOrdering.Contains(var))
                    {
                        if (!functionsProducingVars.ContainsKey(var))
                        {
                            //Add var to the ordering
                            _varOrdering.Add(var);
                            _functionalConjunctIndices.Add(-1);
                            _varSources.Add(-1);
                        }
                    }
                }


                //Dictionary is from potential set of dependencies to function indices
                var functionsHavingDependencies = new Dictionary<ISet<TermVariable>, ISet<int>>();
                //Create this map...
                for (int i = 0; i < _functionalSentencesInfo.Count; i++)
                {
                    Fact functionalSentence = _functionalSentences[i];
                    FunctionInfo functionInfo = _functionalSentencesInfo[i];
                    ISet<TermVariable> producibleVars = functionInfo.GetProducibleVars(functionalSentence);
                    ISet<TermVariable> allVars = new HashSet<TermVariable>(functionalSentence.VariablesOrEmpty);
                    //Variables already in varOrdering don't go in dependents list
                    producibleVars.ExceptWith(_varOrdering);
                    allVars.ExceptWith(_varOrdering);
                    foreach (TermVariable producibleVar in producibleVars)
                    {
                        ISet<TermVariable> dependencies = new HashSet<TermVariable>(allVars);
                        dependencies.Remove(producibleVar);
                        if (!functionsHavingDependencies.ContainsKey(dependencies))
                            functionsHavingDependencies[dependencies] = new HashSet<int>();
                        functionsHavingDependencies[dependencies].Add(i);
                    }
                }
                //Now, we can keep creating functions to generate the remaining variables
                while (_varOrdering.Count < _varsToAssign.Count)
                {
                    if (!functionsHavingDependencies.Any())
                        throw new Exception("We should not run out of functions we could use");
                    //Find the smallest set of dependencies
                    ISet<TermVariable> dependencySetToUse = null;
                    if (functionsHavingDependencies.ContainsKey(new HashSet<TermVariable>()))
                        dependencySetToUse = new HashSet<TermVariable>();
                    else
                    {
                        int smallestSize = int.MaxValue;
                        foreach (ISet<TermVariable> dependencySet in functionsHavingDependencies.Keys)
                            if (dependencySet.Count < smallestSize)
                            {
                                smallestSize = dependencySet.Count;
                                dependencySetToUse = dependencySet;
                            }
                    }
                    //See if any of the functions are applicable
                    Debug.Assert(dependencySetToUse != null, "dependencySetToUse != null");
                    ISet<int> functions = functionsHavingDependencies[dependencySetToUse];
                    int functionToUse = -1;
                    TermVariable varProduced = null;
                    foreach (int function in functions)
                    {
                        Fact functionalSentence = _functionalSentences[function];
                        FunctionInfo functionInfo = _functionalSentencesInfo[function];
                        ISet<TermVariable> producibleVars = functionInfo.GetProducibleVars(functionalSentence);
                        producibleVars.ExceptWith(dependencySetToUse);
                        producibleVars.ExceptWith(_varOrdering);
                        if (producibleVars.Any())
                        {
                            functionToUse = function;
                            varProduced = producibleVars.First();
                            break;
                        }
                    }

                    if (functionToUse == -1)                            //None of these functions were actually useful now? Dump the dependency set
                        functionsHavingDependencies.Remove(dependencySetToUse);
                    else
                    {
                        //Apply the function
                        //1) Add the remaining dependencies as iterated variables
                        foreach (TermVariable var in dependencySetToUse)
                        {
                            _varOrdering.Add(var);
                            _functionalConjunctIndices.Add(-1);
                            _varSources.Add(-1);
                        }

                        Debug.Assert(varProduced != null, "varProduced != null");
                        //2) Add the function's produced variable (varProduced)
                        _varOrdering.Add(varProduced);
                        _functionalConjunctIndices.Add(functionToUse);
                        _varSources.Add(-1);
                        //3) Remove all vars added this way from all dependency sets
                        ISet<TermVariable> addedVars = new HashSet<TermVariable>(dependencySetToUse);
                        addedVars.Add(varProduced);
                        //Tricky, because we have to merge sets
                        //Easier to use a new map
                        var newFunctionsHavingDependencies = new Dictionary<ISet<TermVariable>, ISet<int>>();
                        foreach (var entry in functionsHavingDependencies)
                        {
                            ISet<TermVariable> newKey = new HashSet<TermVariable>(entry.Key);
                            newKey.ExceptWith(addedVars);
                            if (!newFunctionsHavingDependencies.ContainsKey(newKey))
                                newFunctionsHavingDependencies[newKey] = new HashSet<int>();
                            newFunctionsHavingDependencies[newKey].UnionWith(entry.Value);
                        }
                        functionsHavingDependencies = newFunctionsHavingDependencies;
                        //4) Remove this function from the lists?
                        foreach (ISet<int> functionSet in functionsHavingDependencies.Values)
                            functionSet.Remove(functionToUse);
                    }

                }

                //Now we need to actually return the ordering in a list
                //Here's the quick way to do that...
                //(since we've added all the new stuff to ourself already)
                return ImmutableList.Create(new IterationOrderCandidate(this));

            }

            //Let's try a new technique foreach restricting the space of possibilities...
            //We already have an ordering on the functions
            //Let's try to constrain things to that order
            //Namely, if i<j and constant form j is already used as a function,
            //we cannot use constant form i UNLESS constant form j supplies
            //as its variable something used by constant form i.
            //We might also try requiring that c.f. i NOT provide a variable
            //used by c.f. j, though there may be multiple possibilities as
            //to what it could provide.
            int lastFunctionUsedIndex = -1;
            if (_functionalConjunctIndices.Any())
                lastFunctionUsedIndex = _functionalConjunctIndices.Max();

            ISet<TermVariable> varsProducedByFunctions = new HashSet<TermVariable>();
            for (int i = 0; i < _functionalConjunctIndices.Count; i++)
                if (_functionalConjunctIndices[i] != -1)
                    varsProducedByFunctions.Add(_varOrdering[i]);

            for (int i = 0; i < _functionalSentencesInfo.Count; i++)
            {
                Fact functionalSentence = _functionalSentences[i];
                FunctionInfo functionInfo = _functionalSentencesInfo[i];

                if (i < lastFunctionUsedIndex)
                {
                    //We need to figure out whether i could use any of the
                    //vars we're producing with functions
                    //TODOin Try this with a finer grain
                    //i.e., see if i needs a var from a function that is after
                    //it, not one that might be before it
                    IEnumerable<TermVariable> varsInSentence = functionalSentence.VariablesOrEmpty;
                    if (!varsInSentence.Intersect(varsProducedByFunctions).Any())
                        continue;
                }

                //What is the best variable to grab from this form, if there are any?
                TermVariable bestVariable = GetBestVariable(functionalSentence, functionInfo);
                if (bestVariable == null)
                    continue;
                var newCandidate = new IterationOrderCandidate(this, functionalSentence, i, bestVariable);
                children.Add(newCandidate);
            }

            //If there are no more functions to Add, Add the completed version
            if (!children.Any())
                children.Add(new IterationOrderCandidate(this));
            return children;
        }

        private TermVariable GetBestVariable(Fact functionalSentence, FunctionInfo functionInfo)
        {
            //If all the variables that can be set by the functional sentence are in
            //the varOrdering, we return null. Otherwise, we return one of
            //those with the largest domain.

            //The FunctionInfo is sentence-independent, so we need the context
            //of the sentence (which has variables in it).
            List<Term> tuple = functionalSentence.NestedTerms.ToList();
            List<Boolean> dependentSlots = functionInfo.GetDependentSlots();
            if (tuple.Count != dependentSlots.Count)
                throw new Exception("Mismatched sentence " + functionalSentence + " and constant form " + functionInfo);

            ISet<TermVariable> candidateVars = new HashSet<TermVariable>();
            for (int i = 0; i < tuple.Count; i++)
            {
                Term term = tuple[i];
                if (term is TermVariable && dependentSlots[i] && !_varOrdering.Contains(term) && _varsToAssign.Contains(term))
                    candidateVars.Add((TermVariable)term);
            }

            //TODO: Should we just generate the candidate vars with a call to getProducibleVars? ---- added from commit
            ISet<TermVariable> producibleVars = functionInfo.GetProducibleVars(functionalSentence);
            candidateVars.IntersectWith(producibleVars);

            //Now we look at the domains, trying to find the largest
            TermVariable bestVar = null;
            int bestDomainSize = 0;
            foreach (TermVariable var in candidateVars)
            {
                int domainSize = _varDomainSizes[var];
                if (domainSize > bestDomainSize)
                {
                    bestVar = var;
                    bestDomainSize = domainSize;
                }
            }
            return bestVar; //null if none are usable
        }

        //This class has a natural ordering that is inconsistent with Equals.

        public int CompareTo(IterationOrderCandidate o)
        {
            long diff = GetHeuristicValue() - o.GetHeuristicValue();
            if (diff < 0)
                return -1;
            return diff == 0 ? 0 : 1;
        }

        public override String ToString()
        {
            return _varOrdering + " with sources " + GetSourceConjuncts() + "; functional?: " + _functionalConjunctIndices + "; domain sizes are " + _varDomainSizes;
        }
    }
}
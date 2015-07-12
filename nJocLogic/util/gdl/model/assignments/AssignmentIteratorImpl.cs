
// Not thread-safe

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using nJocLogic.data;

namespace nJocLogic.util.gdl.model.assignments
{
    public class AssignmentIteratorImpl : AssignmentIterator
    {
        private readonly List<int> _sourceTupleIndices;
        //This time we just have integers to deal with
        private readonly List<int> _valueIndices;
        private List<TermObject> _nextAssignment = new List<TermObject>();
        private readonly TermObjectSubstitution _assignmentMap = new TermObjectSubstitution();

        private readonly bool _headOnly;
        private bool _done;
        private readonly AssignmentIterationPlan _plan;

        public AssignmentIteratorImpl(AssignmentIterationPlan plan)
        {
            _plan = plan;
            //TODO: Handle this case with a separate class
            if (plan.VarsToAssign == null)
            {
                _headOnly = true;
                return;
            }

            //Set up source tuple...
            _sourceTupleIndices = new List<int>(plan.TuplesBySource.Count);
            for (int i = 0; i < plan.TuplesBySource.Count; i++)
                _sourceTupleIndices.Add(0);

            //Set up...
            _valueIndices = new List<int>(plan.VarsToAssign.Count);
            for (int i = 0; i < plan.VarsToAssign.Count; i++)
            {
                _valueIndices.Add(0);
                _nextAssignment.Add(null);
            }

            _assignmentMap.Add(plan.HeadAssignment);

            //Update "nextAssignment" according to the values of the
            //value indices
            UpdateNextAssignment();
            //Keep updating it until something really works
            MakeNextAssignmentValid();
        }


        private void MakeNextAssignmentValid()
        {
            if (_nextAssignment == null)
                return;

            //Something new that can pop up with functional constants...
            for (int i = 0; i < _nextAssignment.Count; i++)
                if (_nextAssignment[i] == null)
                {
                    //Some function doesn't agree with the answer here. So what do we increment?
                    IncrementIndex(_plan.IndicesToChangeWhenNull[i]);
                    if (_nextAssignment == null)
                        return;
                    i = -1;
                }

            //Find all the unsatisfied distincts
            //Find the pair with the earliest var. that needs to be changed
            var varsToChange = new List<TermVariable>();
            for (int d = 0; d < _plan.Distincts.Count; d++)
            {
                Fact distinct = _plan.Distincts[d];
                //The assignments must use the assignments implied by nextAssignment
                TermObject term1 = ReplaceVariables(distinct.GetTerm(0));
                TermObject term2 = ReplaceVariables(distinct.GetTerm(1));
                if (term1.Equals(term2)) //need to change one of these
                    varsToChange.Add(_plan.VarsToChangePerDistinct[d]);
            }
            if (varsToChange.Any()) //We want just the one, as it is a full restriction on its own behalf
                ChangeOneInNext(new List<TermVariable> { GetLeftmostVar(varsToChange) });
        }

        private TermVariable GetLeftmostVar(ICollection<TermVariable> vars)
        {
            return _plan.VarsToAssign.FirstOrDefault(vars.Contains);
        }

        private TermObject ReplaceVariables(Term term)
        {
            if (term is TermFunction)
                throw new Exception("Function in the distinct... not handled");

            if (term is TermObject)
                return (TermObject)term;

            //Use the assignments implied by nextAssignment
            if (_plan.HeadAssignment.GetMapping((TermVariable)term) != null)
                return (TermObject)_plan.HeadAssignment.GetMapping((TermVariable)term); //Translated in head assignment

            int index = _plan.VarsToAssign.IndexOf((TermVariable)term);
            return _nextAssignment[index];
        }

        private void IncrementIndex(int index)
        {
            if (index < 0)
            {
                //Trash the iterator
                _nextAssignment = null;
                return;
            }
            if (_plan.ValuesToCompute != null && _plan.ValuesToCompute.ContainsKey(index))
            {
                //The constant at this index is functionally computed
                IncrementIndex(index - 1);
                return;
            }
            if (_plan.SourceDefiningSlot[index] != -1)
            {
                //This is set by a source; increment the source
                IncrementSource(_plan.SourceDefiningSlot[index]);
                return;
            }
            //We try increasing the var at index by 1.
            //Everything to the right of it gets reset.
            //If it can't be increased, increase the number
            //to the left instead. If nothing can be
            //increased, trash the iterator.
            int curValue = _valueIndices[index];
            if (curValue == _plan.ValuesToIterate[index].Count - 1)
            {
                //We have no room to increase the value
                IncrementIndex(index - 1);
                return;
            }
            //Increment the current value
            _valueIndices[index] = curValue + 1;
            //Reset everything to the right of the current value
            for (int i = index + 1; i < _valueIndices.Count; i++)
                _valueIndices[i] = 0;

            //Update the assignment
            UpdateNextAssignment();
        }

        private void IncrementSource(int source)
        {
            if (source < 0)
            {
                //Trash the iterator
                _nextAssignment = null;
                return;
            }

            //If we can't increase this source, increase the one to the left instead
            int curValue = _sourceTupleIndices[source];
            if (curValue == _plan.TuplesBySource[source].Count - 1)
            {
                IncrementSource(source - 1);
                return;
            }
            //Increment the current source
            _sourceTupleIndices[source] = curValue + 1;
            //Reset all the sources to the right of it
            for (int i = source + 1; i < _sourceTupleIndices.Count; i++)
                _sourceTupleIndices[i] = 0;

            //Reset all the values set by iteration over domains
            for (int i = 0; i < _valueIndices.Count; i++)
                _valueIndices[i] = 0;

            //Update the assignment
            UpdateNextAssignment();
        }


        private void UpdateNextAssignment()
        {
            //Let's set according to the sources before we get to the remainder
            for (int s = 0; s < _sourceTupleIndices.Count; s++)
            {
                ImmutableList<ImmutableList<TermObject>> tuples = _plan.TuplesBySource[s];
                int curIndex = _sourceTupleIndices[s];
                if (tuples.Count == 0)
                {
                    // This could happen if e.g. there are no tuples that agree with
                    // the headAssignment.
                    _nextAssignment = null;
                    return;
                }
                IList<TermObject> tuple = tuples[curIndex];
                IList<int> varsChosen = _plan.VarsChosenBySource[s];
                IList<Boolean> putDontCheckTuple = _plan.PutDontCheckBySource[s];
                for (int i = 0; i < tuple.Count; i++)
                {
                    TermObject value = tuple[i];
                    bool putDontCheck = putDontCheckTuple[i];
                    int varSlotChosen = varsChosen[i];
                    if (putDontCheck)
                        _nextAssignment[varSlotChosen] = value;
                    else
                    {
                        //It's only at this point that we get to check...
                        if (!_nextAssignment[varSlotChosen].Equals(value))
                        {
                            //We need to correct the value
                            //This is wrong! The current tuple may be the constraining tuple.
                            //But we might need it for performance reasons when there isn't that case...
                            //TODO: Restore this when we can tell it's appropriate
                            //incrementSourceToGetValueInSlot(s, nextAssignment[varSlotChosen), i);
                            IncrementSource(s);
                            //updateNextAssignment(); (should be included at end of calling function)
                            return;
                        }
                    }
                }
            }

            for (int i = 0; i < _valueIndices.Count; i++)
            {
                if ((_plan.ValuesToCompute == null || !_plan.ValuesToCompute.ContainsKey(i)) && _plan.SourceDefiningSlot[i] == -1)
                {
                    _nextAssignment[i] = _plan.ValuesToIterate[i][_valueIndices[i]];
                }
                else if (_plan.SourceDefiningSlot[i] == -1)
                {
                    //Fill in based on a function
                    //Note that the values on the left must already be filled in
                    TermObject valueFromFunction = _plan.ValuesToCompute[i].GetValue(_nextAssignment);
                    //					System.out.println("Setting based on a function: slot " + i + " to value " + valueFromFunction);
                    _nextAssignment[i] = valueFromFunction;
                }
            }
        }

        public void ChangeOneInNext(ICollection<TermVariable> vars)
        {
            //Basically, we want to increment the rightmost one...
            //Corner cases:
            if (_nextAssignment == null)

                return;
            if (!vars.Any())
            {
                if (_headOnly)
                {
                    _done = true;
                    return;
                }
                //Something currently constant is false
                //The assignment is done
                _done = true;
                return;
            }
            if (_plan.VarsToAssign == null)
                Console.WriteLine("headOnly: " + _headOnly);

            TermVariable rightmostVar = GetRightmostVar(vars);
            IncrementIndex(_plan.VarsToAssign.IndexOf(rightmostVar));
            MakeNextAssignmentValid();

        }

        public void ChangeOneInNext(ICollection<TermVariable> varsToChange, TermObjectSubstitution assignment)
        {
            if (_nextAssignment == null)
                return;

            //First, we stop and see if any of these have already been
            //changed (in nextAssignment)
            foreach (TermVariable varToChange in varsToChange)
            {
                //int index = _plan.VarsToAssign.IndexOf(varToChange);
                int index = _plan.IndexOfVariableToAssign(varToChange);
                if (index != -1)
                {
                    var assignedValue = (TermObject)assignment.GetMapping(varToChange);
                    if (assignedValue == null)
                        throw new Exception("assignedValue is null; varToChange is " + varToChange + " and assignment is " + assignment);

                    if (_nextAssignment == null)
                        throw new Exception("nextAssignment is null");

                    if (!assignedValue.Equals(_nextAssignment[index]))    //We've already changed one of these                    
                        return;
                }
            }

            //Okay, we actually need to change one of these
            ChangeOneInNext(varsToChange);
        }

        public bool HasNext()
        {
            if (_plan.Empty)
                return false;
            if (_headOnly)
                return (!_plan.AllDone && !_done);

            return (_nextAssignment != null);
        }

        public TermObjectSubstitution Next()
        {
            if (_headOnly)
            {
                if (_plan.AllDone || _done)
                    throw new Exception("Asking for next when all done");
                _done = true;
                return _plan.HeadAssignment;
            }

            if (_nextAssignment == null)
                return null;

            UpdateMap(); //Sets assignmentMap

            //Adds one to the nextAssignment
            IncrementIndex(_valueIndices.Count - 1);
            MakeNextAssignmentValid();

            return _assignmentMap;
        }

        private void UpdateMap()
        {
            //Sets the map to match the nextAssignment
            for (int i = 0; i < _plan.VarsToAssign.Count; i++)
                _assignmentMap.AddMapping(_plan.VarsToAssign[i], _nextAssignment[i]);
        }

        private TermVariable GetRightmostVar(ICollection<TermVariable> vars)
        {
            TermVariable rightmostVar = null;
            foreach (TermVariable var in _plan.VarsToAssign.Where(vars.Contains))
                rightmostVar = var;
            return rightmostVar;
        }

        public void Remove()
        {
            //Not implemented
        }

        public TermObjectSubstitution Current
        {
            get { return _assignmentMap; }
        }

        public void Dispose()
        {

        }

        object System.Collections.IEnumerator.Current
        {
            get { return Current; }
        }

        public bool MoveNext()
        {
            //return Next() != null && HasNext();
            //return Next() != null && _assignmentMap.NumMappings() > 0;
            return Next() != null;
        }

        public void Reset()
        {

        }
    }
}
using System.Collections.Generic;
using System.Collections.Immutable;
using nJocLogic.data;

namespace nJocLogic.util.gdl.model.assignments
{
    public class AssignmentIterationPlan
    {
        //TODO: Come up with better representations
        public AssignmentIterationPlan(
            ImmutableList<TermVariable> varsToAssign,
            ImmutableList<ImmutableList<ImmutableList<TermObject>>> tuplesBySource,
            TermObjectSubstitution headAssignment,
            ImmutableList<int> indicesToChangeWhenNull,
            ImmutableList<Fact> distincts,
            ImmutableDictionary<int, TermVariable> varsToChangePerDistinct,
            ImmutableDictionary<int, AssignmentFunction> valuesToCompute,
            ImmutableList<int> sourceDefiningSlot,
            ImmutableList<ImmutableList<TermObject>> valuesToIterate,
            ImmutableList<ImmutableList<int>> varsChosenBySource,
            ImmutableList<ImmutableList<bool>> putDontCheckBySource,
            bool empty,
            bool allDone)
        {
            VarsToAssign = varsToAssign;
            TuplesBySource = tuplesBySource;
            HeadAssignment = headAssignment;
            IndicesToChangeWhenNull = indicesToChangeWhenNull;
            Distincts = distincts;
            VarsToChangePerDistinct = varsToChangePerDistinct;
            ValuesToCompute = valuesToCompute;
            SourceDefiningSlot = sourceDefiningSlot;
            ValuesToIterate = valuesToIterate;
            VarsChosenBySource = varsChosenBySource;
            PutDontCheckBySource = putDontCheckBySource;
            Empty = empty;
            AllDone = allDone;

            _indexOfVariables = new Dictionary<TermVariable, int>();
            if (varsToAssign!=null)
            {                
                for (int i = 0; i < VarsToAssign.Count; i++)
                    _indexOfVariables[varsToAssign[i]] = i;
            }
        }

        readonly Dictionary<TermVariable, int> _indexOfVariables; 

        public int IndexOfVariableToAssign(TermVariable varToChange)
        {
            int indexOfVariable;
            if (_indexOfVariables.TryGetValue(varToChange, out indexOfVariable))
                return indexOfVariable;
            return -1;
        }

        public ImmutableList<TermVariable> VarsToAssign { get; private set; }
        public ImmutableList<ImmutableList<ImmutableList<TermObject>>> TuplesBySource { get; private set; }
        public TermObjectSubstitution HeadAssignment { get; private set; }
        public ImmutableList<int> IndicesToChangeWhenNull { get; private set; }
        public ImmutableList<Fact> Distincts { get; private set; }
        public ImmutableDictionary<int, TermVariable> VarsToChangePerDistinct { get; private set; }
        public ImmutableDictionary<int, AssignmentFunction> ValuesToCompute { get; private set; }
        public ImmutableList<int> SourceDefiningSlot { get; private set; }
        public ImmutableList<ImmutableList<TermObject>> ValuesToIterate { get; private set; }
        public ImmutableList<ImmutableList<int>> VarsChosenBySource { get; private set; }
        public ImmutableList<ImmutableList<bool>> PutDontCheckBySource { get; private set; }
        public bool Empty { get; private set; }
        public bool AllDone { get; private set; }

        private static readonly AssignmentIterationPlan EmptyIterationPlan =
            new AssignmentIterationPlan(null,null,null,null,null,null,null,null,null,null,null,true,false);

        public static AssignmentIterationPlan Create(List<TermVariable> varsToAssign,
                                                     List<ImmutableList<ImmutableList<TermObject>>> tuplesBySource,
                                                     TermObjectSubstitution headAssignment,
                                                     List<int> indicesToChangeWhenNull,
                                                     List<Fact> distincts,
                                                     List<TermVariable> varsToChangePerDistinct,
                                                     List<AssignmentFunction> valuesToCompute,
                                                     List<int> sourceDefiningSlot,
                                                     List<ImmutableList<TermObject>> valuesToIterate,
                                                     List<ImmutableList<int>> varsChosenBySource,
                                                     List<ImmutableList<bool>> putDontCheckBySource,
                                                     bool empty,
                                                     bool allDone)
        {
            if (empty)
                return EmptyIterationPlan;
            return new AssignmentIterationPlan(varsToAssign.ToImmutableList(),
                                               tuplesBySource.ToImmutableList(),
                                               headAssignment, //Immutable
                                               indicesToChangeWhenNull.ToImmutableList(),
                                               distincts.ToImmutableList(),
                                               FromNullableList(varsToChangePerDistinct),
                                               FromNullableList(valuesToCompute),
                                               sourceDefiningSlot.ToImmutableList(),
                                               valuesToIterate.ToImmutableList(),
                                               varsChosenBySource.ToImmutableList(),
                                               putDontCheckBySource.ToImmutableList(),
                                               false,
                                               allDone);
        }

        private static ImmutableDictionary<int, T> FromNullableList<T>(List<T> nullableList)
        {
            var builder = ImmutableDictionary.CreateBuilder<int, T>();
            for (int i = 0; i < nullableList.Count; i++)
                if (nullableList[i] != null)
                    builder[i] = nullableList[i];
            return builder.ToImmutable();
        }
    }
}

using nJocLogic.data;
using nJocLogic.gdl;

namespace nJocLogic.knowledge
{
    public class RelationNameProcessor : FactProcessor
    {
        private readonly int _relName;

        /**
         * Create a processor that will rename relations to the value of 'relName'.
         * 
         * @param relName The relation name to set relations to.
         * @param symTable The symbol table to look up the string name in.
         */
        public RelationNameProcessor(string relName, SymbolTable symTable)
        {
            _relName = symTable[relName];
        }

        /**
         * Create a processor that will rename relations to the value of 'relName'.
         * 
         * @param relName The relation name to set relations to.
         */
        public RelationNameProcessor(int relName)
        {
            _relName = relName;
        }

        /**
         * Change the name of the relation 'fact'.
         * 
         * @param fact The relation to rename.
         */
        public override GroundFact ProcessFact(GroundFact fact)
        {
            return fact.Clone(_relName);
        }

        public VariableFact ProcessFact(VariableFact fact)
        {
            return fact.Clone(_relName);
        }

        //public Fact processFact(Fact fact)
        //{
        //    if (fact is VariableFact)
        //        return processFact(fact as VariableFact);

        //    return processFact(fact as GroundFact);
        //}

        public Fact ProcessBaseFact(Fact fact)
        {
            if (fact is VariableFact)
                return ProcessFact(fact as VariableFact);

            return ProcessFact(fact as GroundFact);
        }
    }
}

using nJocLogic.data;

namespace nJocLogic.knowledge
{
    public abstract class FactProcessor
    {
        public virtual GroundFact ProcessFact(GroundFact fact)
        {
            return fact;
        }
    }
}

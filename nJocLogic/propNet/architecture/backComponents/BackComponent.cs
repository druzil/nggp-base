namespace nJocLogic.propNet.architecture.backComponents
{
    public abstract class BackComponent : Component
    {
        public bool CachedValue { get; set; }
        public int Revision { get; set; }

        public override string ToString()
        {
            return string.Format("{0} {1}", GetType().Name, Value);
        }
    }
}

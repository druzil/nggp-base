namespace nJocLogic.propNet.architecture.forwardComponents
{
    using data;

    class ForwardComponentFactory : IComponentFactory
    {
        public IProposition CreateProposition(Fact name)
        {
            return new ForwardProposition(name);
        }

        public IAnd CreateAnd()
        {
            return new And();
        }

        public IOr CreateOr()
        {
            return new Or();
        }

        public INot CreateNot()
        {
            return new Not();
        }

        public IConstant CreateConstant(bool value)
        {
            return new Constant(value);
        }

        public ITransition CreateTransition()
        {
            return new Transition();
        }
    }
}

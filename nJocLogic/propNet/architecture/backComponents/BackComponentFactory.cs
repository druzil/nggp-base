namespace nJocLogic.propNet.architecture.backComponents
{
    using data;

    public class BackComponentFactory : IComponentFactory
    {
        public IProposition CreateProposition(Fact name)
        {
            return new BackProposition(name);
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

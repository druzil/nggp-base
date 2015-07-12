namespace nJocLogic.propNet.architecture
{
    using data;

    public interface IComponentFactory
    {
        IProposition CreateProposition(Fact name);
        IAnd CreateAnd();
        IOr CreateOr();
        INot CreateNot();
        IConstant CreateConstant(bool value);
        ITransition CreateTransition();
    }
}

namespace nJocLogic.propNet.architecture
{
    using data;

    /// <summary>
    /// The Proposition class is designed to represent named latches.
    /// </summary>
    public interface IProposition : IComponent
    {
        // The name of the Proposition. 'Set' should only be rarely used; the name of a proposition is usually constant over its entire lifetime.
        Fact Name { get; set; }
        Fact Underlying { set; }

        Fact GetRealFact();

        bool IsBase { get; }
        bool IsInput { get; }
        bool IsInit { get; }
    }
}
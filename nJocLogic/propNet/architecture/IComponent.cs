namespace nJocLogic.propNet.architecture
{
    using System;
    using System.Collections.Generic;

    public interface IComponent
    {
        HashSet<IComponent> Inputs { get; }
        HashSet<IComponent> Outputs { get; }

        /// <summary>
        /// Adds a new input.
        /// </summary>
        /// <param name="input">A new input.</param>
        void AddInput(IComponent input);

        void RemoveInput(IComponent input);
        void RemoveOutput(IComponent output);
        void RemoveAllInputs();
        void RemoveAllOutputs();

        /// <summary>
        /// Adds a new output.
        /// </summary>
        /// <param name="output">A new output.</param>
        void AddOutput(IComponent output);

        /// <summary>
        /// A convenience method, to get a single input.
        /// To be used only when the component is known to have exactly one input.
        /// </summary>
        /// <returns>The single input to the component.</returns>
        IComponent GetSingleInput();

        /// <summary>
        /// A convenience method, to get a single output.
        /// To be used only when the component is known to have exactly one output.
        /// </summary>
        /// <returns>The single output to the component.</returns>
        IComponent GetSingleOutput();

        /// <summary>
        /// Returns the value of the IComponent.
        /// </summary>
        /// <returns>The value of the IComponent.</returns>
        bool Value { get; set; }

        String ToDot();
    }

    public interface IAnd : IComponent { }
    public interface IOr : IComponent { }
    public interface INot : IComponent { }
    public interface IConstant : IComponent { }
    public interface ITransition : IComponent { }
}
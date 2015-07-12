namespace nJocLogic.propNet.architecture
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;

    public abstract class Component : IComponent
    {
        public HashSet<IComponent> Inputs { get; private set; }
        public HashSet<IComponent> Outputs { get; private set; }    

        /// <summary>
        /// Creates a new IComponent with no inputs or outputs.
        /// </summary>
        protected Component()
        {
            Inputs = new HashSet<IComponent>();
            Outputs = new HashSet<IComponent>();
        }

        /// <summary>
        /// Adds a new input.
        /// </summary>
        /// <param name="input">A new input.</param>
        public virtual void AddInput(IComponent input)
        {
            _firstInput = null;
            Inputs.Add(input);
        }

        public virtual void RemoveInput(IComponent input)
        {
            _firstInput = null;
            Inputs.Remove(input);
        }

        public void RemoveOutput(IComponent output)
        {
            Outputs.Remove(output);
        }

        public virtual void RemoveAllInputs()
        {
            _firstInput = null;
            Inputs.Clear();
        }

        public void RemoveAllOutputs()
        {
            Outputs.Clear();
        }

        /// <summary>
        /// Adds a new output.
        /// </summary>
        /// <param name="output">A new output.</param>
        public void AddOutput(IComponent output)
        {
            Outputs.Add(output);
        }

        private IComponent _firstInput;
        /// <summary>
        /// A convenience method, to get a single input.
        /// To be used only when the component is known to have exactly one input.
        /// </summary>
        /// <returns>The single input to the component.</returns>
        public IComponent GetSingleInput()
        {
            if (_firstInput != null)
                return _firstInput;

            Debug.Assert(Inputs.Count == 1);
            _firstInput = Inputs.First();
            return _firstInput;
        }

        /// <summary>
        /// A convenience method, to get a single output.
        /// To be used only when the component is known to have exactly one output.
        /// </summary>
        /// <returns>The single output to the component.</returns>
        public IComponent GetSingleOutput()
        {
            //assert outputs.size() == 1;
            Debug.Assert(Outputs.Count == 1);
            return Outputs.First();
        }


        /// <summary>
        /// Returns the value of the IComponent.
        /// </summary>
        /// <returns>The value of the IComponent.</returns>
        public abstract bool Value { get; set; }

        public abstract String ToDot();

        /// <summary>
        /// Returns a configurable representation of the IComponent in .dot format.
        /// </summary>
        /// <param name="shape">The value to use as the <tt>shape</tt> attribute.</param>
        /// <param name="fillcolor">The value to use as the <tt>fillcolor</tt> attribute.</param>
        /// <param name="label">The value to use as the <tt>label</tt> attribute.</param>
        /// <returns>A representation of the IComponent in .dot format.</returns>
        protected String ToDot(String shape, String fillcolor, String label)
        {
            var sb = new StringBuilder();

            sb.Append("\"@" + GetHashCode().ToString("X") + "\"[shape=" + shape + ", style= filled, fillcolor=" + fillcolor + ", label=\"" +
                      label + "\"]; ");
            foreach (IComponent component in Outputs)
                sb.Append("\"@" + GetHashCode().ToString("X") + "\"->" + "\"@" + component.GetHashCode().ToString("X") + "\"; ");

            return sb.ToString();
        }
    }
}

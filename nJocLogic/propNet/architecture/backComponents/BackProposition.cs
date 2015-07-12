namespace nJocLogic.propNet.architecture.backComponents
{
    using System;
    using System.Linq;
    using data;
    using gameContainer;

    /// <summary>
    /// The Proposition class is designed to represent named latches.
    /// </summary>
    public sealed class BackProposition : BackComponent, IProposition
    {
        public Fact Name { get; set; }  // The name of the Proposition. 'Set' should only be rarely used; the name of a proposition is usually constant over its entire lifetime.
        public Fact Underlying { private get; set; }

        public override bool Value { get; set; }

        /// <summary>
        /// Creates a new Proposition with name <tt>name</tt>.
        /// </summary>
        /// <param name="name">The name of the Proposition.</param>
        public BackProposition(Fact name)
        {
            Name = name;
            Value = false;
        }

        public Fact GetRealFact()
        {
            return Underlying ?? Name;
        }

        public override String ToDot()
        {
            return ToDot("circle", Value ? "red" : "white", Name.ToString());
        }

        private bool _isbasedefined;
        private bool _isbase;
        public bool IsBase
        {
            get
            {
                if (_isbasedefined)
                    return _isbase;

                _isbase = Inputs.Count == 1 && Inputs.First() is Transition;
                _isbasedefined = true;
                return _isbase;
            }
        }

        public bool IsInput
        {
            get { return Name.RelationName == GameContainer.Parser.TokDoes; }
        }

        public bool IsInit
        {
            get { return Name.RelationName == GameContainer.Parser.TokInit; }
        }

        public override string ToString()
        {
            return string.Format("{0} {1}", Name, Value);
        }

        public override void AddInput(IComponent input)
        {
            _isbasedefined = false;
            base.AddInput(input);
        }

        public override void RemoveInput(IComponent input)
        {
            _isbasedefined = false;
            base.RemoveInput(input);
        }

        public override void RemoveAllInputs()
        {
            _isbasedefined = false;
            base.RemoveAllInputs();
        }
    }
}
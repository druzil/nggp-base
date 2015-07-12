namespace nJocLogic.propNet.architecture.forwardComponents
{
    using System;
    using System.Linq;
    using data;
    using gameContainer;

    /// <summary>
    /// The Proposition class is designed to represent named latches.
    /// </summary>
    public sealed class ForwardProposition : ForwardComponent, IProposition
    {
        public Fact Name { get; set; }      /** The name of the Proposition. 'Set' should only be rarely used; the name of a proposition is usually constant over its entire lifetime.*/
        public Fact Underlying { private get; set; }

        /// <summary>
        /// Creates a new Proposition with name <tt>name</tt>.
        /// </summary>
        /// <param name="name">The name of the Proposition.</param>
        public ForwardProposition(Fact name)
        {
            Name = name;
            Value = false;
        }

        #region Overrides of ForwardComponent

        /// <summary>
        /// Derives the components value from its inputs
        /// </summary>
        /// <returns></returns>
        public override bool DeriveValue()
        {
            return GetSingleInput().Value;
        }

        #endregion

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

        public override string ToString()
        {
            string deriveValue = "None";
            if (Inputs.Count == 1)
                deriveValue = DeriveValue().ToString();

            return string.Format("{0} current:{1} Derived:{2}", Name, Value, deriveValue);
        }
    }
}
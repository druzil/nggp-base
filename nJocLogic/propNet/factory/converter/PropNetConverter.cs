using System.Collections.Generic;

namespace nJocLogic.propNet.factory.converter
{
    using System.Diagnostics;
    using System.Linq;
    using architecture;
    //using architecture.backComponents;
    using data;
    using gameContainer;
    using knowledge;

    /// <summary>
    /// The PropNetConverter class defines PropNet conversion for the PropNetFactory
    /// class. This takes in a flattened game description, and converts it into an
    /// equivalent PropNet.
    /// </summary>
    public sealed class PropNetConverter
    {
        private readonly IComponentFactory _componentFactory;
        private Dictionary<Fact, IProposition> _propositions;	// An archive of Propositions, indexed by name
        private HashSet<IComponent> _components;					// An archive of Components
        private readonly RelationNameProcessor _doesProcessor;
        private readonly RelationNameProcessor _trueProcessor;
        private readonly GroundFact _anonFact;

        public PropNetConverter(IComponentFactory componentFactory)
        {
            _componentFactory = componentFactory;
            _doesProcessor = new RelationNameProcessor("does", GameContainer.SymbolTable);
            _trueProcessor = new RelationNameProcessor("true", GameContainer.SymbolTable);
            _anonFact = new GroundFact(GameContainer.SymbolTable, "anon");
        }

        /// <summary>
        /// Converts a game description to a PropNet using the following process
        /// (note that this method and all of the methods that it invokes assume that
        /// <tt>description</tt> has already been flattened by a PropNetFlattener):
        /// <ol>
        /// <li>Transforms each of the rules in <tt>description</tt> into
        /// equivalent PropNet Components.</li>
        /// <li>Adds or gates to Propositions with more than one input.</li>
        /// <li>Adds inputs that are implicitly specified by <tt>description</tt>.</li>
        /// </ol>
        /// </summary>
        /// <param name="roles"></param>
        /// <param name="description">A game description.</param>
        /// <returns>An equivalent PropNet.</returns>
        public PropNet Convert(List<TermObject> roles, List<Implication> description)
        {
            _propositions = new Dictionary<Fact, IProposition>();
            _components = new HashSet<IComponent>();

            foreach (Implication rule in description)
            {
                var antecedents = rule.Antecedents;
                if (antecedents != null && antecedents.Conjuncts.Any())
                    ConvertRule(rule);
                else
                    ConvertStatic(rule.Consequent);
            }

            FixDisjunctions();
            AddMissingInputs();

            return new PropNet(roles, _components);
        }

        /// <summary>
        /// Creates an equivalent InputProposition for every LegalProposition where none already exists.
        /// </summary>
        private void AddMissingInputs()
        {
            List<IProposition> addList = (from proposition in _propositions.Values
                                          where proposition.Name.RelationName == GameContainer.Parser.TokLegal
                                          select proposition).ToList();

            foreach (IProposition addItem in addList)
                _components.Add(GetProposition(_doesProcessor.ProcessBaseFact(addItem.Name)));
        }

        /// <summary>
        /// Converts a literal to equivalent PropNet Components and returns a
        /// reference to the last of those components.
        /// </summary>
        /// <param name="literal">The literal to convert to equivalent PropNet Components.</param>
        /// <returns>The last of those components.</returns>
        private IProposition ConvertConjunct(Expression literal)
        {
            var not = literal as Negation;
            if (not != null)
            {
                IProposition input = ConvertConjunct(not.Negated);
                var no = _componentFactory.CreateNot();
                var output = _componentFactory.CreateProposition(_anonFact);

                Link(input, no);
                Link(no, output);

                _components.Add(input);
                _components.Add(no);
                _components.Add(output);

                return output;
            }

            var sentence = (Fact)literal;

            if (sentence.RelationName == GameContainer.Parser.TokDistinct)
            {
                IProposition proposition = _componentFactory.CreateProposition(_anonFact);
                IConstant constant = _componentFactory.CreateConstant(!sentence.GetTerm(0).Equals(sentence.GetTerm(1)));

                Link(constant, proposition);

                _components.Add(proposition);
                _components.Add(constant);

                return proposition;
            }
            else
            {
                IProposition proposition = GetProposition(sentence);
                _components.Add(proposition);
                return proposition;
            }
        }

        /// <summary>
        /// Converts a sentence to equivalent PropNet Components and returns the
        /// first of those components.
        /// </summary>
        /// <param name="sentence">The sentence to convert to equivalent PropNet Components.</param>
        /// <returns>The first of those Components.</returns>
        private IProposition ConvertHead(Fact sentence)
        {
            if (sentence.RelationName == GameContainer.Parser.TokNext)
            {
                IProposition head = GetProposition(_trueProcessor.ProcessBaseFact(sentence));
                ITransition transition = _componentFactory.CreateTransition();
                IProposition preTransition = _componentFactory.CreateProposition(_anonFact);

                Link(preTransition, transition);
                Link(transition, head);

                _components.Add(head);
                _components.Add(transition);
                _components.Add(preTransition);

                return preTransition;
            }
            IProposition proposition = GetProposition(sentence);
            _components.Add(proposition);

            return proposition;
        }

        /// <summary>
        /// Converts a rule into equivalent PropNet Components by invoking the
        /// <tt>convertHead()</tt> method on the head, and the
        /// <tt>convertConjunct</tt> method on every literal in the body and
        /// joining the results by an and gate.
        /// </summary>
        /// <param name="rule">The rule to convert.</param>
        private void ConvertRule(Implication rule)
        {
            IProposition head = ConvertHead(rule.Consequent);
            IAnd and = _componentFactory.CreateAnd();

            Link(and, head);

            _components.Add(head);
            _components.Add(and);

            foreach (Expression literal in rule.Antecedents.Conjuncts)
                Link(ConvertConjunct(literal), and);
        }

        /// <summary>
        /// Converts a sentence to equivalent PropNet Components.
        /// </summary>
        /// <param name="sentence">The sentence to convert to equivalent PropNet Components.</param>
        private void ConvertStatic(Fact sentence)
        {
            if (sentence.RelationName == GameContainer.Parser.TokInit)
            {
                IProposition init = GetProposition(new GroundFact(GameContainer.Parser.TokInit));
                var transition = _componentFactory.CreateTransition();
                IProposition proposition = GetProposition(_trueProcessor.ProcessBaseFact(sentence));

                Link(init, transition);
                Link(transition, proposition);

                _components.Add(init);
                _components.Add(transition);
                _components.Add(proposition);
            }

            var constant = _componentFactory.CreateConstant(true);
            IProposition prop = GetProposition(sentence);

            Link(constant, prop);

            _components.Add(constant);
            _components.Add(prop);
        }

        /// <summary>
        /// Creates an or gate to combine the inputs to a IProposition wherever one
        /// has more than one input.
        /// </summary>
        private void FixDisjunctions()
        {
            IEnumerable<IProposition> fixList = _propositions.Values.Where(proposition => proposition.Inputs.Count > 1);

            foreach (IProposition fixItem in fixList)
            {
                IOr or = _componentFactory.CreateOr();
                int i = 0;
                foreach (IComponent input in fixItem.Inputs)
                {
                    i++;

                    Fact relation = fixItem.Name;
                    string value = GameContainer.SymbolTable[relation.RelationName];

                    int token = GameContainer.SymbolTable[value + "-" + i];
                    var groundFact = relation.Arity == 0 ? new GroundFact(token) : new GroundFact(token, relation.GetTerms().ToArray());
                    IProposition disjunct = _componentFactory.CreateProposition(groundFact);

                    disjunct.Underlying = fixItem.Name;

                    Debug.Assert(input.Outputs.Count == 1);
                    input.Outputs.Clear();

                    Link(input, disjunct);
                    Link(disjunct, or);

                    _components.Add(disjunct);
                }

                fixItem.Inputs.Clear();
                Link(or, fixItem);

                _components.Add(or);
            }
        }

        /// <summary>
        /// Returns a IProposition with name <tt>term</tt>, creating one if none
        /// already exists.
        /// </summary>
        /// <param name="sentence">The name of the IProposition.</param>
        /// <returns>A IProposition with name <tt>term</tt>.</returns>
        private IProposition GetProposition(Fact sentence)
        {
            if (!_propositions.ContainsKey(sentence))
                _propositions[sentence] = _componentFactory.CreateProposition(sentence);
            return _propositions[sentence];
        }

        /// <summary>
        /// Adds inputs and outputs to <tt>source</tt> and <tt>target</tt> such
        /// that <tt>source</tt> becomes an input to <tt>target</tt>.
        /// </summary>
        /// <param name="source">A component.</param>
        /// <param name="target">A second component.</param>
        private static void Link(IComponent source, IComponent target)
        {
            source.AddOutput(target);
            target.AddInput(source);
        }
    }
}
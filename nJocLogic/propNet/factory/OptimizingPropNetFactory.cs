using System.Configuration;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Wintellect.PowerCollections;

namespace nJocLogic.propNet.factory
{
    using architecture;
    using data;
    using gameContainer;
    using gdl;
    using knowledge;
    using util.gdl;
    using util.gdl.GdlCleaner;
    using util.gdl.model;
    using util.gdl.model.assignments;

    /// <summary>
    /// A propnet factory meant to optimize the propnet before it's even built, mostly through transforming the GDL. (The 
    /// transformations identify certain classes of rules that have poor performance and replace them with equivalent rules that 
    /// have better performance, with performance measured by the size of the propnet.)
    ///
    /// Known issues:
    /// - Does not work on games with many advanced forms of recursion. These include:
    ///   - Anything that breaks the SentenceModel
    ///   - Multiple sentence forms which reference one another in rules
    ///   - Not 100% confirmed to work on games where recursive rules have multiple recursive conjuncts
    /// - Currently runs some of the transformations multiple times. A Description object containing information about the 
    ///   description and its properties would alleviate this.
    /// - It does not have a way of automatically solving the "unaffected piece rule" problem.
    /// - Depending on the settings and the situation, the behavior of the CondensationIsolator can be either too aggressive or 
    ///   not aggressive enough. Both result in excessively large games. A more sophisticated version of the CondensationIsolator 
    ///   could solve these problems. A stopgap alternative is to try both settings and use the smaller propnet (or the first to 
    ///   be created, if multithreading).
    ///</summary>
    public class OptimizingPropNetFactory
    {
        private static readonly Logger Logger = LogManager.GetLogger("logic.propnet.factory");
        private static readonly RelationNameProcessor TrueProcessor;
        private static readonly RelationNameProcessor DoesProcessor;
        private static readonly GroundFact TempFact;
        private static IComponentFactory _componentFactory;

        static OptimizingPropNetFactory()
        {
            DoesProcessor = new RelationNameProcessor("does", GameContainer.SymbolTable);
            TrueProcessor = new RelationNameProcessor("true", GameContainer.SymbolTable);
            TempFact = new GroundFact(GameContainer.SymbolTable, "temp");
        }

        /// <summary>
        /// Creates a PropNet for the game with the given description.
        /// </summary>
        public static PropNet Create(IList<Expression> description, IComponentFactory componentFactory)
        {
            Console.WriteLine("Building propnet...");

            _componentFactory = componentFactory;
            DateTime startTime = DateTime.UtcNow;

            description = GdlCleaner.Run(description);
            description = DeORer.Run(description);
            description = VariableConstrainer.ReplaceFunctionValuedVariables(description);
            description = Relationizer.Run(description);
            description = CondensationIsolator.Run(description);

            if (Logger.IsDebugEnabled)
                foreach (var gdl in description)
                    Logger.Debug(gdl);

            //We want to start with a rule graph and follow the rule graph. Start by finding general information about the game
            ISentenceDomainModel model = SentenceDomainModelFactory.CreateWithCartesianDomains(description);
            //Restrict domains to values that could actually come up in rules.
            //See chinesecheckers4's "count" relation for an example of why this could be useful.
            model = SentenceDomainModelOptimizer.RestrictDomainsToUsefulValues(model);

            Logger.Debug("Setting constants...");

            //TODO: ConstantChecker constantChecker = ConstantCheckerFactory.createWithForwardChaining(model);
            IConstantChecker constantChecker = ConstantCheckerFactory.CreateWithProver(model);
            Logger.Debug("Done setting constants");

            HashSet<String> sentenceFormNames = SentenceForms.GetNames(model.SentenceForms);
            bool usingBase = sentenceFormNames.Contains("base");
            bool usingInput = sentenceFormNames.Contains("input");

            //For now, we're going to build this to work on those with a particular restriction on the dependency graph:
            //Recursive loops may only contain one sentence form. This describes most games, but not all legal games.
            IDictionary<ISentenceForm, ICollection<ISentenceForm>> dependencyGraph = model.DependencyGraph;
            Logger.Debug("Computing topological ordering... ");

            //ConcurrencyUtils.checkForInterruption();
            IEnumerable<ISentenceForm> topologicalOrdering = GetTopologicalOrdering(model.SentenceForms, dependencyGraph, usingBase, usingInput);
            Logger.Debug("done");

            List<TermObject> roles = GameContainer.GameInformation.GetRoles();
            var components = new Dictionary<Fact, IComponent>();
            var negations = new Dictionary<Fact, IComponent>();
            IConstant trueComponent = _componentFactory.CreateConstant(true);
            IConstant falseComponent = _componentFactory.CreateConstant(false);
            var functionInfoMap = new Dictionary<ISentenceForm, FunctionInfo>();
            var completedSentenceFormValues = new Dictionary<ISentenceForm, ICollection<Fact>>();

            var sentenceFormAdder = new SentenceFormAdder(_componentFactory, DoesProcessor, TrueProcessor, TempFact);

            foreach (ISentenceForm form in topologicalOrdering)
            {
                //ConcurrencyUtils.checkForInterruption();
                Logger.Debug("Adding sentence form " + form);

                if (constantChecker.IsConstantForm(form))
                {
                    Logger.Debug(" (constant)");
                    //Only add it if it's important
                    if (form.Name.Equals(GameContainer.Parser.TokLegal) || form.Name.Equals(GameContainer.Parser.TokGoal)
                                                                            || form.Name.Equals(GameContainer.Parser.TokInit))
                    {
                        //Add it
                        foreach (Fact trueSentence in constantChecker.GetTrueSentences(form))
                        {
                            var trueProp = _componentFactory.CreateProposition(trueSentence);
                            trueProp.AddInput(trueComponent);
                            trueComponent.AddOutput(trueProp);
                            components[trueSentence] = trueComponent;
                        }
                    }

                    Logger.Debug("Checking whether {0} is a functional constant...", form);
                    AddConstantsToFunctionInfo(form, constantChecker, functionInfoMap);
                    AddFormToCompletedValues(form, completedSentenceFormValues, constantChecker);

                    continue;
                }
                Logger.Debug(string.Empty);
                //TODO: Adjust "recursive forms" appropriately
                //Add a temporary sentence form thingy? ...
                var temporaryComponents = new Dictionary<Fact, IComponent>();
                var temporaryNegations = new Dictionary<Fact, IComponent>();

                sentenceFormAdder.AddSentenceForm(form, model, components, negations, trueComponent, falseComponent, usingBase, usingInput,
                    ImmutableHashSet.Create(form), temporaryComponents, temporaryNegations, functionInfoMap, constantChecker,
                    completedSentenceFormValues);

                //TODO: Pass these over groups of multiple sentence forms
                if (temporaryComponents.Any())
                    Logger.Debug("Processing temporary components...");
                ProcessTemporaryComponents(temporaryComponents, temporaryNegations, components, negations, trueComponent, falseComponent);
                AddFormToCompletedValues(form, completedSentenceFormValues, components);

                //TODO: Add this, but with the correct total number of components (not just Propositions)
                Console.WriteLine("  {0} components added", completedSentenceFormValues[form].Count);
            }
            //Connect "next" to "true"
            Logger.Debug("Adding transitions...");
            AddTransitions(components);
            //Set up "init" proposition
            Logger.Debug("Setting up 'init' proposition...");
            SetUpInit(components, trueComponent, falseComponent);
            //Now we can safely...
            RemoveUselessBasePropositions(components, negations, trueComponent, falseComponent);
            Logger.Debug("Creating component set...");
            var componentSet = new HashSet<IComponent>(components.Values);
            CompleteComponentSet(componentSet);
            //ConcurrencyUtils.checkForInterruption();
            Logger.Debug("Initializing propnet object...");
            //Make it look the same as the PropNetFactory results, until we decide how we want it to look
            NormalizePropositions(componentSet);
            var propnet = new PropNet(roles, componentSet);

            Logger.Debug("Done setting up propnet; took {0}ms, has {1} components and {2} links", (DateTime.UtcNow - startTime).TotalMilliseconds, componentSet.Count, propnet.GetNumLinks());
            Logger.Debug("Propnet has {0} ands; {1} ors; {2} nots", propnet.GetNumAnds(), propnet.GetNumOrs(), propnet.GetNumNots());

            if (ConfigurationManager.AppSettings["OutputPropNet"] == "true")
                propnet.RenderToFile("propnet.dot");

            return propnet;
        }


        private static void RemoveUselessBasePropositions(Dictionary<Fact, IComponent> components, Dictionary<Fact, IComponent> negations,
            IConstant trueComponent, IConstant falseComponent)
        {
            bool changedSomething = false;
            foreach (KeyValuePair<Fact, IComponent> entry in components)
            {
                if (entry.Key.RelationName == GameContainer.Parser.TokTrue)
                {
                    IComponent comp = entry.Value;
                    if (!comp.Inputs.Any())
                    {
                        comp.AddInput(falseComponent);
                        falseComponent.AddOutput(comp);
                        changedSomething = true;
                    }
                }
            }
            if (!changedSomething)
                return;

            OptimizeAwayTrueAndFalse(components, negations, trueComponent, falseComponent);
        }

        /// <summary>
        /// Changes the propositions contained in the propnet so that they correspond
        /// to the outputs of the PropNetFactory. This is for consistency and for
        /// backwards compatibility with respect to state machines designed for the
        /// old propnet factory. Feel free to remove this for your player.
        /// </summary>
        private static void NormalizePropositions(IEnumerable<IComponent> componentSet)
        {
            foreach (IComponent component in componentSet)
            {
                var prop = component as IProposition;
                if (prop != null && (prop.Name != null && prop.Name.RelationName == GameContainer.Parser.TokNext))
                    prop.Name = new GroundFact(GameContainer.SymbolTable, "anon");
            }
        }

        private static void AddFormToCompletedValues(ISentenceForm form, Dictionary<ISentenceForm, ICollection<Fact>> completedSentenceFormValues,
            IConstantChecker constantChecker)
        {
            completedSentenceFormValues[form] = new List<Fact>(constantChecker.GetTrueSentences(form));
        }


        private static void AddFormToCompletedValues(ISentenceForm form, IDictionary<ISentenceForm, ICollection<Fact>> completedSentenceFormValues,
            Dictionary<Fact, IComponent> components)
        {
            //Kind of inefficient. Could do better by collecting these as we go,
            //then adding them back into the CSFV map once the sentence forms are complete.
            //completedSentenceFormValues.put(form, new ArrayList<Fact>());
            completedSentenceFormValues[form] = components.Keys.Where(form.Matches).ToList();
        }


        private static void AddConstantsToFunctionInfo(ISentenceForm form, IConstantChecker constantChecker,
                                                       IDictionary<ISentenceForm, FunctionInfo> functionInfoMap)
        {
            functionInfoMap[form] = FunctionInfoImpl.Create(form, constantChecker);
        }


        private static void ProcessTemporaryComponents(Dictionary<Fact, IComponent> temporaryComponents,
            IReadOnlyDictionary<Fact, IComponent> temporaryNegations, Dictionary<Fact, IComponent> components,
            Dictionary<Fact, IComponent> negations, IComponent trueComponent, IComponent falseComponent)
        {
            //For each component in temporary components, we want to "put it back"
            //into the main components section.
            //We also want to do optimization here...
            //We don't want to end up with anything following from true/false.

            //Everything following from a temporary component (its outputs)
            //should instead become an output of the actual component.
            //If there is no actual component generated, then the statement
            //is necessarily FALSE and should be replaced by the false
            //component.
            foreach (Fact sentence in temporaryComponents.Keys)
            {
                IComponent tempComp = temporaryComponents[sentence];
                IComponent component;
                components.TryGetValue(sentence, out component);
                IComponent realComp = component ?? falseComponent;
                foreach (IComponent output in tempComp.Outputs)
                {
                    //Disconnect
                    output.RemoveInput(tempComp);
                    //tempComp.removeOutput(output); //do at end
                    //Connect
                    output.AddInput(realComp);
                    realComp.AddOutput(output);
                }
                tempComp.RemoveAllOutputs();

                if (temporaryNegations.ContainsKey(sentence))
                {
                    //Should be pointing to a "not" that now gets input from realComp
                    //Should be fine to put into negations
                    negations[sentence] = temporaryNegations[sentence];
                    //If this follows true/false, will get resolved by the next set of optimizations
                }

                OptimizeAwayTrueAndFalse(components, negations, trueComponent, falseComponent);
            }
        }

        /// <summary>
        /// Components and negations may be null, if e.g. this is a post-optimization.
        /// TrueComponent and falseComponent are required.
        /// 
        /// Doesn't actually work that way... shoot. Need something that will remove the
        /// component from the propnet entirely.
        /// </summary>
        private static void OptimizeAwayTrueAndFalse(Dictionary<Fact, IComponent> components, Dictionary<Fact, IComponent> negations,
                                                     IComponent trueComponent, IComponent falseComponent)
        {
            while (HasNonEssentialChildren(trueComponent) || HasNonEssentialChildren(falseComponent))
            {
                OptimizeAwayTrue(components, negations, null, trueComponent, falseComponent);
                OptimizeAwayFalse(components, negations, null, trueComponent, falseComponent);
            }
        }

        public static void OptimizeAwayTrueAndFalse(PropNet pn, IComponent trueComponent, IComponent falseComponent)
        {
            while (HasNonEssentialChildren(trueComponent) || HasNonEssentialChildren(falseComponent))
            {
                OptimizeAwayTrue(null, null, pn, trueComponent, falseComponent);
                OptimizeAwayFalse(null, null, pn, trueComponent, falseComponent);
            }
        }

        //TODO: Create a version with just a set of components that we can share with post-optimizations
        private static void OptimizeAwayFalse(IDictionary<Fact, IComponent> components, Dictionary<Fact, IComponent> negations,
            PropNet pn, IComponent trueComponent, IComponent falseComponent)
        {
            Debug.Assert((components != null && negations != null) || pn != null);
            Debug.Assert((components == null && negations == null) || pn == null);
            foreach (IComponent output in falseComponent.Outputs.ToList())
            {
                if (IsEssentialProposition(output) || output is ITransition)
                {
                    //Since this is the false constant, there are a few "essential" types we don't actually want to keep around.
                    if (!IsLegalOrGoalProposition(output))
                        continue;
                }
                var prop = output as IProposition;
                if (prop != null)
                {
                    //Move its outputs to be outputs of false
                    foreach (IComponent child in prop.Outputs)
                    {
                        //Disconnect
                        child.RemoveInput(prop);
                        //output.removeOutput(child); //do at end
                        //Reconnect; will get children before returning, if nonessential
                        falseComponent.AddOutput(child);
                        child.AddInput(falseComponent);
                    }
                    prop.RemoveAllOutputs();

                    if (!IsEssentialProposition(prop))
                    {
                        //Remove the proposition entirely
                        falseComponent.RemoveOutput(prop);
                        output.RemoveInput(falseComponent);
                        //Update its location to the trueComponent in our map
                        if (components != null)
                        {
                            components[prop.Name] = falseComponent;
                            negations[prop.Name] = trueComponent;
                        }
                        else
                            pn.RemoveComponent(prop);
                    }
                }
                else
                {
                    var and = output as IAnd;
                    if (and != null)
                    {
                        //Attach children of and to falseComponent
                        foreach (IComponent child in and.Outputs)
                        {
                            child.AddInput(falseComponent);
                            falseComponent.AddOutput(child);
                            child.RemoveInput(and);
                        }
                        //Disconnect and completely
                        and.RemoveAllOutputs();
                        foreach (IComponent parent in and.Inputs)
                            parent.RemoveOutput(and);
                        and.RemoveAllInputs();
                        if (pn != null)
                            pn.RemoveComponent(and);
                    }
                    else
                    {
                        var or = output as IOr;
                        if (or != null)
                        {
                            //Remove as input from or
                            or.RemoveInput(falseComponent);
                            falseComponent.RemoveOutput(or);
                            //If or has only one input, remove it
                            if (or.Inputs.Count == 1)
                            {
                                IComponent input = or.GetSingleInput();
                                or.RemoveInput(input);
                                input.RemoveOutput(or);
                                foreach (IComponent output1 in or.Outputs)
                                {
                                    //Disconnect from and
                                    output1.RemoveInput(or);
                                    //or.removeOutput(out); //do at end
                                    //Connect directly to the new input
                                    output1.AddInput(input);
                                    input.AddOutput(output1);
                                }
                                or.RemoveAllOutputs();
                                if (pn != null)
                                {
                                    pn.RemoveComponent(or);
                                }
                            }
                            else if (!or.Inputs.Any())
                            {
                                if (pn != null)
                                {
                                    pn.RemoveComponent(or);
                                }
                            }
                        }
                        else
                        {
                            var not = output as INot;
                            if (not != null)
                            {
                                //Disconnect from falseComponent
                                not.RemoveInput(falseComponent);
                                falseComponent.RemoveOutput(not);
                                //Connect all children of the not to trueComponent
                                foreach (IComponent child in not.Outputs)
                                {
                                    //Disconnect
                                    child.RemoveInput(not);
                                    //not.removeOutput(child); //Do at end
                                    //Connect to trueComponent
                                    child.AddInput(trueComponent);
                                    trueComponent.AddOutput(child);
                                }
                                not.RemoveAllOutputs();
                                if (pn != null)
                                    pn.RemoveComponent(not);
                            }
                            else if (output is ITransition)
                            {
                                //???
                                throw new Exception("Fix optimizeAwayFalse's case for Transitions");
                            }
                        }
                    }
                }
            }
        }


        private static bool IsLegalOrGoalProposition(IComponent comp)
        {
            var prop = comp as IProposition;
            if (prop == null)
                return false;

            return prop.Name.RelationName == GameContainer.Parser.TokLegal || prop.Name.RelationName == GameContainer.Parser.TokGoal;
        }

        private static void OptimizeAwayTrue(IDictionary<Fact, IComponent> components, IDictionary<Fact, IComponent> negations,
            PropNet pn, IComponent trueComponent, IComponent falseComponent)
        {
            Debug.Assert((components != null && negations != null) || pn != null);
            foreach (IComponent output in trueComponent.Outputs.ToList())
            {
                if (IsEssentialProposition(output) || output is ITransition)
                    continue;
                var prop = output as IProposition;
                if (prop != null)
                {
                    //Move its outputs to be outputs of true
                    foreach (IComponent child in prop.Outputs)
                    {
                        //Disconnect
                        child.RemoveInput(prop);
                        //output.removeOutput(child); //do at end
                        //Reconnect; will get children before returning, if nonessential
                        trueComponent.AddOutput(child);
                        child.AddInput(trueComponent);
                    }
                    prop.RemoveAllOutputs();

                    if (!IsEssentialProposition(prop))
                    {
                        //Remove the proposition entirely
                        trueComponent.RemoveOutput(prop);
                        output.RemoveInput(trueComponent);
                        //Update its location to the trueComponent in our map
                        if (components != null)
                        {
                            components[prop.Name] = trueComponent;
                            Debug.Assert(negations != null, "negations != null");
                            negations[prop.Name] = falseComponent;
                        }
                        else
                            pn.RemoveComponent(prop);
                    }
                }
                else
                {
                    var or = output as IOr;
                    if (or != null)
                    {
                        //Attach children of or to trueComponent
                        foreach (IComponent child in or.Outputs)
                        {
                            child.AddInput(trueComponent);
                            trueComponent.AddOutput(child);
                            child.RemoveInput(or);
                        }
                        //Disconnect or completely
                        or.RemoveAllOutputs();
                        foreach (IComponent parent in or.Inputs)
                            parent.RemoveOutput(or);
                        or.RemoveAllInputs();
                        if (pn != null)
                            pn.RemoveComponent(or);
                    }
                    else
                    {
                        var and = output as IAnd;
                        if (and != null)
                        {
                            //Remove as input from and
                            and.RemoveInput(trueComponent);
                            trueComponent.RemoveOutput(and);
                            //If and has only one input, remove it
                            if (and.Inputs.Count == 1)
                            {
                                IComponent input = and.GetSingleInput();
                                and.RemoveInput(input);
                                input.RemoveOutput(and);
                                foreach (IComponent output1 in and.Outputs)
                                {
                                    //Disconnect from and
                                    output1.RemoveInput(and);
                                    //and.removeOutput(out); //do at end
                                    //Connect directly to the new input
                                    output1.AddInput(input);
                                    input.AddOutput(output1);
                                }
                                and.RemoveAllOutputs();
                                if (pn != null)
                                    pn.RemoveComponent(and);
                            }
                            else if (and.Inputs.Any())
                                if (pn != null)
                                    pn.RemoveComponent(and);
                        }
                        else
                        {
                            var not = output as INot;
                            if (not != null)
                            {
                                //Disconnect from trueComponent
                                not.RemoveInput(trueComponent);
                                trueComponent.RemoveOutput(not);
                                //Connect all children of the not to falseComponent
                                foreach (IComponent child in not.Outputs)
                                {
                                    //Disconnect
                                    child.RemoveInput(not);
                                    //not.removeOutput(child); //Do at end
                                    //Connect to falseComponent
                                    child.AddInput(falseComponent);
                                    falseComponent.AddOutput(child);
                                }
                                not.RemoveAllOutputs();
                                if (pn != null)
                                    pn.RemoveComponent(not);
                            }
                        }
                    }
                }
            }
        }


        private static bool HasNonEssentialChildren(IComponent trueComponent)
        {
            IEnumerable<IComponent> nonTransitions = trueComponent.Outputs.Where(child => !(child is ITransition));
            foreach (IComponent child in nonTransitions)
            {
                if (!IsEssentialProposition(child))
                    return true;
                //We don't want any grandchildren, either
                if (child.Outputs.Any())
                    return true;
            }
            return false;
        }

        private static bool IsEssentialProposition(IComponent component)
        {
            if (!(component is IProposition))
                return false;

            //We're looking for things that would be outputs of "true" or "false",
            //but we would still want to keep as propositions to be read by the
            //state machine
            var prop = (IProposition)component;
            var name = prop.Name.RelationName;

            //return name.Equals(LEGAL) /*|| name.Equals(NEXT)*/ || name.Equals(GOAL)|| name.Equals(INIT) || name.Equals(TERMINAL);
            Parser parser = GameContainer.Parser;
            return name == parser.TokLegal || name == parser.TokGoal || name == parser.TokInit || name == parser.TokTerminal;
        }


        private static void CompleteComponentSet(ISet<IComponent> componentSet)
        {
            var newComponents = new HashSet<IComponent>();
            var componentsToTry = new HashSet<IComponent>(componentSet);
            while (componentsToTry.Any())
            {
                foreach (IComponent c in componentsToTry)
                {
                    IEnumerable<IComponent> outputsNotInSet = c.Outputs.Where(output => !componentSet.Contains(output));
                    foreach (IComponent output in outputsNotInSet)
                        newComponents.Add(output);

                    IEnumerable<IComponent> inputsNotInSet = c.Inputs.Where(input => !componentSet.Contains(input));
                    foreach (IComponent input in inputsNotInSet)
                        newComponents.Add(input);
                }
                foreach (var comp in newComponents)
                    componentSet.Add(comp);
                componentsToTry = newComponents;
                newComponents = new HashSet<IComponent>();
            }
        }


        private static void AddTransitions(IReadOnlyDictionary<Fact, IComponent> components)
        {
            foreach (KeyValuePair<Fact, IComponent> entry in components)
            {
                Fact sentence = entry.Key;

                if (sentence.RelationName == GameContainer.Parser.TokNext)
                {
                    //connect to true
                    Fact trueSentence = TrueProcessor.ProcessBaseFact(sentence);
                    IComponent nextComponent = entry.Value;
                    IComponent trueComponent;

                    //There might be no true component (for example, because the bases
                    //told us so). If that's the case, don't have a transition.
                    if (!components.TryGetValue(trueSentence, out trueComponent)) // Skipping transition to supposedly impossible 'trueSentence'
                        continue;

                    var transition = _componentFactory.CreateTransition();
                    transition.AddInput(nextComponent);
                    nextComponent.AddOutput(transition);
                    transition.AddOutput(trueComponent);
                    trueComponent.AddInput(transition);
                }
            }
        }

        //TODO: Replace with version using constantChecker only
        //TODO: This can give problematic results if interpreted in
        //the standard way (see test_case_3d)
        private static void SetUpInit(IDictionary<Fact, IComponent> components, IConstant trueComponent, IConstant falseComponent)
        {
            var initProposition = _componentFactory.CreateProposition(new GroundFact(GameContainer.SymbolTable["INIT"]));
            IEnumerable<KeyValuePair<Fact, IComponent>> trueComponents = components.Where(entry => entry.Value == trueComponent);
            IEnumerable<KeyValuePair<Fact, IComponent>> trueInits = trueComponents.Where(entry => entry.Key.RelationName == GameContainer.Parser.TokInit);
            IEnumerable<Fact> trueInitFacts = trueInits.Select(entry => TrueProcessor.ProcessBaseFact(entry.Key));
            IEnumerable<IComponent> initTrueSentenceComponents = trueInitFacts.Select(trueSentence => components[trueSentence]);

            foreach (IComponent trueSentenceComponent in initTrueSentenceComponents)
            {
                if (!trueSentenceComponent.Inputs.Any())
                {
                    //Case where there is no transition input
                    //Add the transition input, connect to init, continue loop
                    var transition = _componentFactory.CreateTransition();
                    //init goes into transition
                    transition.AddInput(initProposition);
                    initProposition.AddOutput(transition);
                    //transition goes into component
                    trueSentenceComponent.AddInput(transition);
                    transition.AddOutput(trueSentenceComponent);
                }
                else
                {
                    //The transition already exists
                    IComponent transition = trueSentenceComponent.GetSingleInput();

                    //We want to add init as a thing that precedes the transition
                    //Disconnect existing input
                    IComponent input = transition.GetSingleInput();
                    //input and init go into or, or goes into transition
                    input.RemoveOutput(transition);
                    transition.RemoveInput(input);
                    var orInputs = new List<IComponent>(2) { input, initProposition };
                    Orify(orInputs, transition, falseComponent);
                }
            }
        }

        /// <summary>
        /// Adds an or gate connecting the inputs to produce the output.
        /// Handles special optimization cases like a true/false input.
        /// </summary>
        private static void Orify(ICollection<IComponent> inputs, IComponent output, IConstant falseProp)
        {
            //TODO: Look for already-existing ors with the same inputs?
            //Or can this be handled with a GDL transformation?

            //Special case: An input is the true constant
            IEnumerable<IComponent> trueConstants = inputs.Where(input => input is IConstant && input.Value);
            foreach (IComponent input in trueConstants)
            {
                //True constant: connect that to the component, done
                input.AddOutput(output);
                output.AddInput(input);
                return;
            }

            //Special case: An input is "or"
            //I'm honestly not sure how to handle special cases here...
            //What if that "or" gate has multiple outputs? Could that happen?

            //For reals... just skip over any false constants
            var or = _componentFactory.CreateOr();
            IEnumerable<IComponent> nonConstants = inputs.Where(input => !(input is IConstant));
            foreach (IComponent input in nonConstants)
            {
                input.AddOutput(or);
                or.AddInput(input);
            }

            //What if they're all false? (Or inputs is empty?) Then no inputs at this point...
            if (!or.Inputs.Any())
            {
                //Hook up to "false"
                falseProp.AddOutput(output);
                output.AddInput(falseProp);
                return;
            }
            //If there's just one, on the other hand, don't use the or gate
            if (or.Inputs.Count == 1)
            {
                IComponent input = or.GetSingleInput();
                input.RemoveOutput(or);
                or.RemoveInput(input);
                input.AddOutput(output);
                output.AddInput(input);
                return;
            }
            or.AddOutput(output);
            output.AddInput(or);
        }

        //TODO: This code is currently used by multiple classes, so perhaps it should be
        //factored out into the SentenceModel.
        private static IEnumerable<ISentenceForm> GetTopologicalOrdering(
            ICollection<ISentenceForm> forms,
            IDictionary<ISentenceForm, ICollection<ISentenceForm>> dependencyGraph, bool usingBase, bool usingInput)
        {
            //We want each form as a key of the dependency graph to
            //follow all the forms in the dependency graph, except maybe itself
            var queue = new Queue<ISentenceForm>(forms);
            var ordering = new List<ISentenceForm>(forms.Count);
            var alreadyOrdered = new HashSet<ISentenceForm>();
            while (queue.Any())
            {
                ISentenceForm curForm = queue.Dequeue();
                bool readyToAdd = !(dependencyGraph.ContainsKey(curForm) &&
                                    dependencyGraph[curForm].Any(d => !d.Equals(curForm) && !alreadyOrdered.Contains(d)));
                //Don't add if there are dependencies

                //Don't add if it's true/next/legal/does and we're waiting for base/input
                if (usingBase && (curForm.Name.Equals(GameContainer.Parser.TokTrue)
                                   || curForm.Name.Equals(GameContainer.Parser.TokNext)
                                   || curForm.Name.Equals(GameContainer.Parser.TokInit)))
                {
                    //Have we added the corresponding base sf yet?
                    ISentenceForm baseForm = curForm.WithName(GameContainer.SymbolTable["base"]);
                    if (!alreadyOrdered.Contains(baseForm))
                        readyToAdd = false;
                }

                if (usingInput && (curForm.Name.Equals(GameContainer.Parser.TokDoes)
                                    || curForm.Name.Equals(GameContainer.Parser.TokLegal)))
                {
                    ISentenceForm inputForm = curForm.WithName(GameContainer.SymbolTable["input"]);
                    if (!alreadyOrdered.Contains(inputForm))
                        readyToAdd = false;
                }
                //Add it
                if (readyToAdd)
                {
                    ordering.Add(curForm);
                    alreadyOrdered.Add(curForm);
                }
                else
                    queue.Enqueue(curForm);
                //TODO: Add check for an infinite loop here, or stratify loops

                //ConcurrencyUtils.checkForInterruption();
            }
            return ordering;
        }

    }
}

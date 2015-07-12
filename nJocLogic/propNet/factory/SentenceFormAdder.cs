using System;
using System.Collections.Generic;
using System.Linq;
using nJocLogic.data;
using nJocLogic.gameContainer;
using nJocLogic.knowledge;
using nJocLogic.propNet.architecture;
using nJocLogic.util.gdl;
using nJocLogic.util.gdl.model;
using nJocLogic.util.gdl.model.assignments;

namespace nJocLogic.propNet.factory
{
    internal class SentenceFormAdder
    {
        private readonly IComponentFactory _componentFactory;
        private readonly RelationNameProcessor _doesProcessor;
        private readonly RelationNameProcessor _trueProcessor;
        private readonly GroundFact _tempFact;

        public SentenceFormAdder(IComponentFactory componentFactory, RelationNameProcessor doesProcessor,
            RelationNameProcessor trueProcessor, GroundFact tempFact)
        {
            _componentFactory = componentFactory;
            _doesProcessor = doesProcessor;
            _trueProcessor = trueProcessor;
            _tempFact = tempFact;
        }

        public void AddSentenceForm(ISentenceForm form, ISentenceDomainModel model,
            IDictionary<Fact, IComponent> components,
            IDictionary<Fact, IComponent> negations, IConstant trueComponent, IConstant falseComponent,
            bool usingBase, bool usingInput, ISet<ISentenceForm> recursionForms,
            IDictionary<Fact, IComponent> temporaryComponents, IDictionary<Fact, IComponent> temporaryNegations,
            Dictionary<ISentenceForm, FunctionInfo> functionInfoMap, IConstantChecker constantChecker,
            Dictionary<ISentenceForm, ICollection<Fact>> completedSentenceFormValues)
        {
            //This is the meat of it (along with the entire Assignments class).
            //We need to enumerate the possible propositions in the sentence form...
            //We also need to hook up the sentence form to the inputs that can make it true.
            //We also try to optimize as we go, which means possibly removing the
            //proposition if it isn't actually possible, or replacing it with
            //true/false if it's a constant.

            ISet<Fact> alwaysTrueSentences = model.GetSentencesListedAsTrue(form);
            ISet<Implication> rules = model.GetRules(form);

            foreach (Fact alwaysTrueSentence in alwaysTrueSentences)
            {
                //We add the sentence as a constant
                if (alwaysTrueSentence.RelationName == GameContainer.Parser.TokLegal
                    || alwaysTrueSentence.RelationName == GameContainer.Parser.TokNext
                    || alwaysTrueSentence.RelationName == GameContainer.Parser.TokGoal)
                {
                    var prop = _componentFactory.CreateProposition(alwaysTrueSentence);
                    //Attach to true
                    trueComponent.AddOutput(prop);
                    prop.AddInput(trueComponent);
                    //Still want the same components;
                    //we just don't want this to be anonymized
                }
                //Assign as true
                components[alwaysTrueSentence] = trueComponent;
                negations[alwaysTrueSentence] = falseComponent;
            }

            //For does/true, make nodes based on input/base, if available
            if (usingInput && form.Name.Equals(GameContainer.Parser.TokDoes))
            {
                //Add only those propositions for which there is a corresponding INPUT
                ISentenceForm inputForm = form.WithName(GameContainer.SymbolTable["input"]);
                foreach (Fact inputSentence in constantChecker.GetTrueSentences(inputForm))
                {
                    Fact doesSentence = _doesProcessor.ProcessBaseFact(inputSentence);
                    var prop = _componentFactory.CreateProposition(doesSentence);
                    components[doesSentence] = prop;
                }
                return;
            }
            if (usingBase && form.Name.Equals(GameContainer.Parser.TokTrue))
            {
                ISentenceForm baseForm = form.WithName(GameContainer.SymbolTable["base"]);
                foreach (Fact baseSentence in constantChecker.GetTrueSentences(baseForm))
                {
                    Fact trueSentence = _trueProcessor.ProcessBaseFact(baseSentence);
                    IProposition prop = _componentFactory.CreateProposition(trueSentence);
                    components[trueSentence] = prop;
                }
                return;
            }

            //var recursiveFormCache = new RecursionFormsCache(recursionForms);

            var inputsToOr = new Dictionary<Fact, HashSet<IComponent>>();
            foreach (Implication rule in rules)
            {
                AssignmentsImpl assignments = AssignmentsFactory.GetAssignmentsForRule(rule, model, functionInfoMap,
                    completedSentenceFormValues);

                //Calculate vars in live (non-constant, non-distinct) conjuncts
                ISet<TermVariable> varsInLiveConjuncts = GetVarsInLiveConjuncts(rule,
                    constantChecker.ConstantSentenceForms);
                foreach (var head in rule.Consequent.VariablesOrEmpty)
                    varsInLiveConjuncts.Add(head);
                var varsInRule = new HashSet<TermVariable>(rule.VariablesOrEmpty);
                bool preventDuplicatesFromConstants = varsInRule.Count > varsInLiveConjuncts.Count;

                bool[] constantFormCheck = new bool[rule.Antecedents.Constituents.Length];
                for (int i = 0; i < rule.Antecedents.Constituents.Length; i++)
                {
                    Expression literal = rule.Antecedents.Constituents[i];
                    var fact = literal as Fact;
                    var negated = literal as Negation;
                    if (fact != null || negated != null)
                    {
                        if (negated != null)
                            fact = (Fact) negated.Negated;

                        ISentenceForm conjunctForm = model.GetSentenceForm(fact);
                        if (constantChecker.IsConstantForm(conjunctForm))
                            constantFormCheck[i] = true;
                    }
                }


                for (var asnItr = (AssignmentIteratorImpl) assignments.GetEnumerator(); asnItr.MoveNext();)
                {
                    TermObjectSubstitution assignment = asnItr.Current;
                    if (assignment == null)
                        continue; //Not sure if this will ever happen

                    //ConcurrencyUtils.checkForInterruption();

                    var sentence = (Fact) rule.Consequent.ApplySubstitution(assignment);

                    //Now we go through the conjuncts as before, but we wait to hook them up.
                    var componentsToConnect = new List<IComponent>(rule.Consequent.Arity);
                    for (int i = 0; i < rule.Antecedents.Constituents.Length; i++)
                    {
                        Expression literal = rule.Antecedents.Constituents[i];
                        var fact = literal as Fact;
                        if (fact != null)
                        {
                            if (fact.RelationName != GameContainer.Parser.TokDistinct)
                            {
                                //Get the sentence post-substitutions
                                var transformed = (Fact) literal.ApplySubstitution(assignment);

                                //Check for constant-ness
                                //ISentenceForm conjunctForm = model.GetSentenceForm(transformed);
                                //if (constantChecker.IsConstantForm(conjunctForm))
                                if (constantFormCheck[i])
                                {
                                    if (!constantChecker.IsTrueConstant(transformed))
                                    {
                                        List<TermVariable> varsToChange = GetVarsInConjunct(literal);
                                        asnItr.ChangeOneInNext(varsToChange, assignment);
                                        componentsToConnect.Add(null);
                                    }
                                    continue;
                                }

                                //If conj is null and this is a sentence form we're still handling, hook up to a temporary sentence form
                                IComponent conj;
                                if (!components.TryGetValue(transformed, out conj))
                                    temporaryComponents.TryGetValue(transformed, out conj);

                                if (conj == null && InSentenceFormGroup(transformed, recursionForms))
                                {
                                    //Set up a temporary component
                                    var tempProp = _componentFactory.CreateProposition(transformed);
                                    temporaryComponents[transformed] = tempProp;
                                    conj = tempProp;
                                }
                                //Let's say this is false; we want to backtrack and change the right variable
                                if (conj == null || IsThisConstant(conj, falseComponent))
                                {
                                    List<TermVariable> varsInConjunct = GetVarsInConjunct(literal);
                                    asnItr.ChangeOneInNext(varsInConjunct, assignment);
                                    //These last steps just speed up the process
                                    //telling the factory to ignore this rule
                                    componentsToConnect.Add(null);
                                    continue; //look at all the other restrictions we'll face
                                }

                                componentsToConnect.Add(conj);
                            }
                        }
                        else
                        {
                            var negation = literal as Negation;
                            if (negation != null)
                            {
                                //Add a "not" if necessary
                                //Look up the negation
                                var inner = (Fact) negation.Negated;
                                var transformed = (Fact) inner.ApplySubstitution(assignment);

                                //Add constant-checking here...
                                //ISentenceForm conjunctForm = model.GetSentenceForm(transformed);
                                //if (constantChecker.IsConstantForm(conjunctForm))
                                if (constantFormCheck[i])
                                {
                                    if (constantChecker.IsTrueConstant(transformed))
                                    {
                                        List<TermVariable> varsToChange = GetVarsInConjunct(negation);
                                        asnItr.ChangeOneInNext(varsToChange, assignment);
                                        componentsToConnect.Add(null);
                                    }
                                    continue;
                                }

                                IComponent conj;
                                negations.TryGetValue(transformed, out conj);
                                if (IsThisConstant(conj, falseComponent))
                                {
                                    //We need to change one of the variables inside
                                    List<TermVariable> varsInConjunct = GetVarsInConjunct(inner);
                                    asnItr.ChangeOneInNext(varsInConjunct, assignment);
                                    //ignore this rule
                                    componentsToConnect.Add(null);
                                    continue;
                                }
                                if (conj == null)
                                    temporaryNegations.TryGetValue(transformed, out conj);

                                //Check for the recursive case:
                                if (conj == null && InSentenceFormGroup(transformed, recursionForms))
                                {
                                    IComponent positive;
                                    if (!components.TryGetValue(transformed, out positive))
                                        temporaryComponents.TryGetValue(transformed, out positive);

                                    if (positive == null)
                                    {
                                        //Make the temporary proposition
                                        var tempProp = _componentFactory.CreateProposition(transformed);
                                        temporaryComponents[transformed] = tempProp;
                                        positive = tempProp;
                                    }
                                    //Positive is now set and in temporaryComponents
                                    //Evidently, wasn't in temporaryNegations
                                    //So we add the "not" gate and set it in temporaryNegations
                                    var not = _componentFactory.CreateNot();
                                    //Add positive as input
                                    not.AddInput(positive);
                                    positive.AddOutput(not);
                                    temporaryNegations[transformed] = not;
                                    conj = not;
                                }
                                if (conj == null)
                                {
                                    IComponent positive;
                                    components.TryGetValue(transformed, out positive);
                                    //No, because then that will be attached to "negations", which could be bad

                                    if (positive == null)
                                    {
                                        //So the positive can't possibly be true (unless we have recurstion)
                                        //and so this would be positive always
                                        //We want to just skip this conjunct, so we continue to the next

                                        continue; //to the next conjunct
                                    }

                                    //Check if we're sharing a component with another sentence with a negation
                                    //(i.e. look for "nots" in our outputs and use those instead)
                                    INot existingNotOutput = GetNotOutput(positive);
                                    if (existingNotOutput != null)
                                    {
                                        componentsToConnect.Add(existingNotOutput);
                                        negations[transformed] = existingNotOutput;
                                        continue; //to the next conjunct
                                    }

                                    var not = _componentFactory.CreateNot();
                                    not.AddInput(positive);
                                    positive.AddOutput(not);
                                    negations[transformed] = not;
                                    conj = not;
                                }
                                componentsToConnect.Add(conj);
                            }
                            else
                            {
                                throw new Exception("Unwanted Expression type");
                            }
                        }
                    }
                    if (!componentsToConnect.Contains(null))
                    {
                        //Connect all the components
                        IProposition andComponent = _componentFactory.CreateProposition(_tempFact);

                        Andify(componentsToConnect, andComponent, trueComponent);
                        if (!IsThisConstant(andComponent, falseComponent))
                        {
                            if (!inputsToOr.ContainsKey(sentence))
                                inputsToOr[sentence] = new HashSet<IComponent>();
                            inputsToOr[sentence].Add(andComponent);
                            //We'll want to make sure at least one of the non-constant
                            //components is changing
                            if (preventDuplicatesFromConstants)
                                asnItr.ChangeOneInNext(varsInLiveConjuncts, assignment);
                        }
                    }
                }
            }

            //At the end, we hook up the conjuncts
            foreach (var entry in inputsToOr)
            {
                //ConcurrencyUtils.checkForInterruption();

                Fact sentence = entry.Key;
                HashSet<IComponent> inputs = entry.Value;
                var realInputs = new HashSet<IComponent>();
                foreach (IComponent input in inputs)
                    if (input is IConstant || !input.Inputs.Any())
                        realInputs.Add(input);
                    else
                    {
                        realInputs.Add(input.GetSingleInput());
                        input.GetSingleInput().RemoveOutput(input);
                        input.RemoveAllInputs();
                    }

                var prop = _componentFactory.CreateProposition(sentence);
                Orify(realInputs, prop, falseComponent);
                components[sentence] = prop;
            }

            //True/does sentences will have none of these rules, but
            //still need to exist/"float"
            //We'll do this if we haven't used base/input as a basis
            if (form.Name.Equals(GameContainer.Parser.TokTrue) || form.Name.Equals(GameContainer.Parser.TokDoes))
                foreach (Fact sentence in model.GetDomain(form)) //ConcurrencyUtils.checkForInterruption();
                    components[sentence] = _componentFactory.CreateProposition(sentence);
        }

        private static HashSet<TermVariable> GetVarsInLiveConjuncts(Implication rule,
            ISet<ISentenceForm> constantSentenceForms)
        {
            var result = new HashSet<TermVariable>();
            foreach (var literal in rule.Antecedents.Constituents)
            {
                var fact = literal as Fact;
                if (fact != null)
                {
                    if (!InSentenceFormGroup(fact, constantSentenceForms))
                        result.UnionWith(fact.VariablesOrEmpty);
                }
                else
                {
                    var not = literal as Negation;
                    if (not != null && !InSentenceFormGroup((Fact) not.Negated, constantSentenceForms))
                        result.UnionWith(not.VariablesOrEmpty);
                }
            }
            return result;
        }

        public static bool InSentenceFormGroup(Fact sentence, ISet<ISentenceForm> forms)
        {
            return forms.Any(form => form.Matches(sentence));
        }

        private void Andify(List<IComponent> inputs, IComponent output, IConstant trueProp)
        {
            //Special case: If the inputs include false, connect false to thisComponent
            IEnumerable<IComponent> falseConstants = inputs.Where(c => c is IConstant && !c.Value);
            foreach (IComponent c in falseConstants)
            {
                //Connect false (c) to the output
                output.AddInput(c);
                c.AddOutput(output);
                return;
            }

            //For reals... just skip over any true constants
            var and = _componentFactory.CreateAnd();
            IEnumerable<IComponent> nonConstants = inputs.Where(input => !(input is IConstant));
            foreach (IComponent input in nonConstants)
            {
                input.AddOutput(and);
                and.AddInput(input);
            }

            //What if they're all true? (Or inputs is empty?) Then no inputs at this point...
            if (!and.Inputs.Any())
            {
                //Hook up to "true"
                trueProp.AddOutput(output);
                output.AddInput(trueProp);
                return;
            }
            //If there's just one, on the other hand, don't use the and gate
            if (and.Inputs.Count == 1)
            {
                IComponent input = and.GetSingleInput();
                input.RemoveOutput(and);
                and.RemoveInput(input);
                input.AddOutput(output);
                output.AddInput(input);
                return;
            }
            and.AddOutput(output);
            output.AddInput(and);
        }

        /// <summary>
        /// Adds an or gate connecting the inputs to produce the output.
        /// Handles special optimization cases like a true/false input.
        /// </summary>
        private void Orify(ICollection<IComponent> inputs, IComponent output, IConstant falseProp)
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

        private static bool IsThisConstant(IComponent conj, IConstant constantComponent)
        {
            if (conj == constantComponent)
                return true;
            return (conj is IProposition && conj.Inputs.Count == 1 && conj.GetSingleInput() == constantComponent);
        }

        private static INot GetNotOutput(IComponent positive)
        {
            return positive.Outputs.OfType<INot>().FirstOrDefault();
        }

        private static List<TermVariable> GetVarsInConjunct(Expression literal)
        {
            return literal.VariablesOrEmpty.ToList();
        }
    }
}

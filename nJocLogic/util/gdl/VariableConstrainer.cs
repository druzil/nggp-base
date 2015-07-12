using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Wintellect.PowerCollections;

namespace nJocLogic.util.gdl
{
    using data;
    using gameContainer;
    using model;

    public class VariableConstrainer
    {
        /// <summary>
        /// Modifies a GDL description by replacing all rules in which variables could be bound to functions, so that 
        /// the new rules will only bind constants to variables. 
        /// 
        /// Not guaranteed to work if the GDL is written strangely, such as when they include rules in which certain 
        /// conjuncts are never or always true. Not guaranteed to work when rules are unsafe, i.e., they contain variables 
        /// only appearing in the head, a negated literal, and/or a distinct literal. (In fact, this can be a good way to 
        /// test for GDL errors, which often result in exceptions.)
        /// 
        /// Not guaranteed to finish in a reasonable amount of time in pathological cases, where the
        /// number of possible functional structures is prohibitively large.
        /// </summary>
        /// <param name="description">A GDL game description.</param>
        /// <returns>A modified version of the same game.</returns>
        public static IList<Expression> ReplaceFunctionValuedVariables(IList<Expression> description)
        {
            ISentenceFormModel model = SentenceFormModelFactory.Create(description);

            // Find "ambiguities" between sentence rules: "If we have sentence form X with variables in slots [...], it 
            // could be aliased to sentence form Y instead"
            MultiDictionary<ISentenceForm, Ambiguity> ambiguitiesByOriginalForm = GetAmbiguitiesByOriginalForm(model);
            if (!ambiguitiesByOriginalForm.Any())
                return description;

            IList<Expression> expandedRules = ApplyAmbiguitiesToRules(description, ambiguitiesByOriginalForm, model);
            return CleanUpIrrelevantRules(expandedRules);
        }

        /// <summary>
        /// An ambiguity represents a particular relationship between two sentence forms. It says that if sentence form 
        /// "original" appears in a rule and has Variables in particular slots, it could be equivalent to the sentence 
        /// form "replacement" if functions are assigned to its variables.
        /// 
        /// The goal of this transformation is to make it possible for users of the game description to treat it as if 
        /// functions could not be assigned to variables. This requires adding or modifying rules to account for the extra cases.
        /// </summary>
        private class Ambiguity
        {
            public ISentenceForm Original { get; private set; }
            public ISentenceForm Replacement { get; private set; }
            private readonly ImmutableDictionary<int, TermFunction> _replacementsByOriginalTupleIndex;

            public Ambiguity(ISentenceForm original, Dictionary<int, TermFunction> replacementsByOriginalTupleIndex,
                ISentenceForm replacement)
            {
                Debug.Assert(original != null);
                Debug.Assert(replacementsByOriginalTupleIndex != null);
                Debug.Assert(replacementsByOriginalTupleIndex.Any());
                Debug.Assert(replacement != null);
                foreach (int varIndex in replacementsByOriginalTupleIndex.Keys)
                    Debug.Assert(varIndex < original.TupleSize);
                Original = original;
                _replacementsByOriginalTupleIndex = replacementsByOriginalTupleIndex.ToImmutableDictionary();
                Replacement = replacement;
            }

            public override String ToString()
            {
                string indicies = string.Join(",", _replacementsByOriginalTupleIndex.Select(kv => string.Format("[{0}={1}]", kv.Key, kv.Value)));
                return string.Format("Ambiguity [original={0}, replacementsByOriginalTupleIndex={1}, replacement={2}]",
                    Original, indicies, Replacement);
            }

            /// <summary>
            /// Returns true iff the given sentence could correspond to a sentence of the replacement form, for some variable assignment. 
            /// </summary>
            /// <param name="sentence"></param>
            /// <returns></returns>
            public bool Applies(Fact sentence)
            {
                if (!Original.Matches(sentence))
                    return false;

                List<Term> tuple = sentence.NestedTerms.ToList();
                return _replacementsByOriginalTupleIndex.Keys.All(varIndex => tuple[varIndex] is TermVariable);
            }

            public Substitution GetReplacementAssignment(Fact sentence, UnusedVariableGenerator varGen)
            {
                Debug.Assert(Applies(sentence));

                var assignment = new Substitution();
                List<Term> tuple = sentence.NestedTerms.ToList();
                foreach (int varIndex in _replacementsByOriginalTupleIndex.Keys)
                {
                    TermFunction function = _replacementsByOriginalTupleIndex[varIndex];

                    TermFunction replacementFunction = varGen.ReplaceVariablesAndConstants(function);
                    assignment.AddMapping((TermVariable)tuple[varIndex], replacementFunction);
                }
                return assignment;
            }
        }

        private static MultiDictionary<ISentenceForm, Ambiguity> GetAmbiguitiesByOriginalForm(ISentenceFormModel model)
        {
            var result = new MultiDictionary<ISentenceForm, Ambiguity>(true);
            MultiDictionary<int, ISentenceForm> formsByName = GetFormsByName(model);

            foreach (int name in formsByName.Keys)
            {
                ICollection<ISentenceForm> forms = formsByName[name];
                foreach (ISentenceForm form in forms)
                    result[form] = GetAmbiguities(form, forms);
            }

            ISet<ISentenceForm> allForms = formsByName.Values.ToImmutableHashSet();
            foreach (Ambiguity ambiguity in result.Values)
            {
                Debug.Assert(allForms.Contains(ambiguity.Original));
                Debug.Assert(allForms.Contains(ambiguity.Replacement));
            }

            return result;
        }

        private static MultiDictionary<int, ISentenceForm> GetFormsByName(ISentenceFormModel model)
        {
            var result = new MultiDictionary<int, ISentenceForm>(true);
            foreach (ISentenceForm value in GetAllSentenceForms(model))
                result.Add(value.Name, value);
            return result;
        }

        private static IEnumerable<ISentenceForm> GetAllSentenceForms(ISentenceFormModel model)
        {
            // The model may only have sentence forms for sentences that can actually be true. It may be missing sentence forms that are used in 
            // the rules only, with no actual corresponding sentences. We want to make sure these are included.
            var forms = new HashSet<ISentenceForm>(model.SentenceForms);
            GdlVisitors.VisitAll(model.Description, new GdlVisitor { VisitSentence = sentence => forms.Add(new SimpleSentenceForm(sentence)) });
            return forms;
        }

        private static List<Ambiguity> GetAmbiguities(ISentenceForm original, IEnumerable<ISentenceForm> forms)
        {
            return forms.Select(form => FindAmbiguity(original, form)).Where(ambiguity => ambiguity != null).ToList();
        }

        private static Ambiguity FindAmbiguity(ISentenceForm original, ISentenceForm replacement)
        {
            Debug.Assert(original.Name == replacement.Name);
            if (original.Equals(replacement))
                return null;

            var replacementsByOriginalTupleIndex = new Dictionary<int, TermFunction>();
            //Make the arguments ?v0, ?v1, ?v2, ... so we can find the tuple indices easily
            Fact originalSentence = original.GetSentenceFromTuple(GetNumberedTuple(original.TupleSize));
            Fact replacementSentence = replacement.GetSentenceFromTuple(GetNumberedTuple(replacement.TupleSize));

            bool success = FindAmbiguity(originalSentence.GetTerms(), replacementSentence.GetTerms(), replacementsByOriginalTupleIndex);
            return success ? new Ambiguity(original, replacementsByOriginalTupleIndex, replacement) : null;
        }

        private static bool FindAmbiguity(IList<Term> originalBody, IList<Term> replacementBody,
            IDictionary<int, TermFunction> replacementsByOriginalTupleIndex)
        {
            if (originalBody.Count != replacementBody.Count)
                return false;

            for (int i = 0; i < originalBody.Count; i++)
            {
                Term originalTerm = originalBody[i];
                Term replacementTerm = replacementBody[i];
                if (replacementTerm is TermVariable)
                {
                    if (!(originalTerm is TermVariable))
                        return false;
                }
                else if (replacementTerm is TermFunction)
                {
                    if (originalTerm is TermVariable)
                    {
                        string stringName = GameContainer.SymbolTable[(originalTerm as TermVariable).Name];
                        int varIndex = int.Parse(stringName.Replace("?v", ""));
                        replacementsByOriginalTupleIndex[varIndex] = (TermFunction)replacementTerm;
                    }
                    else if (originalTerm is TermFunction)
                    {
                        var originalFunction = (TermFunction)originalTerm;
                        var replacementFunction = (TermFunction)replacementTerm;
                        if (originalFunction.FunctionName != replacementFunction.FunctionName)
                            return false;

                        bool successSoFar = FindAmbiguity(originalFunction.Arguments.ToList(),
                                                          replacementFunction.Arguments.ToList(),
                                                          replacementsByOriginalTupleIndex);
                        if (!successSoFar)
                            return false;
                    }
                    else
                        throw new Exception();
                }
                else
                    throw new Exception();
            }
            return true;
        }

        private static IList<Term> GetNumberedTuple(int tupleSize)
        {
            var result = new List<Term>();
            for (int i = 0; i < tupleSize; i++)
                result.Add(new TermVariable(GameContainer.SymbolTable["?v" + i]));
            return result;
        }

        private static IList<Expression> ApplyAmbiguitiesToRules(IEnumerable<Expression> description,
            MultiDictionary<ISentenceForm, Ambiguity> ambiguitiesByOriginalForm, ISentenceFormModel model)
        {
            ImmutableList<Expression>.Builder result = ImmutableList.CreateBuilder<Expression>();
            foreach (Expression gdl in description)
            {
                var implication = gdl as Implication;
                if (implication != null)
                    ApplyAmbiguities(implication, ambiguitiesByOriginalForm, model).ForEach(result.Add);
                else
                    result.Add(gdl);
            }

            return result.ToImmutable();
        }

        private static List<Implication> ApplyAmbiguities(Implication originalRule, MultiDictionary<ISentenceForm, Ambiguity> ambiguitiesByOriginalForm,
            ISentenceFormModel model)
        {
            var rules = new List<Implication> { originalRule };
            //Each literal can potentially multiply the number of rules we have, so we apply each literal separately to the entire list of rules so far.
            IEnumerable<Expression> originalSentences = ImmutableHashSet.Create(originalRule.Consequent).Concat(originalRule.Antecedents.Conjuncts);
            foreach (Expression literal in originalSentences)
            {
                var newRules = new List<Implication>();
                foreach (Implication rule in rules)
                {
                    Debug.Assert(originalRule.Consequent.Arity == rule.Consequent.Arity);
                    newRules.AddRange(ApplyAmbiguitiesForLiteral(literal, rule, ambiguitiesByOriginalForm, model));
                }
                rules = newRules;
            }
            return rules;
        }

        private static IEnumerable<Implication> ApplyAmbiguitiesForLiteral(Expression literal, Implication rule,
            IDictionary<ISentenceForm, ICollection<Ambiguity>> ambiguitiesByOriginalForm, ISentenceFormModel model)
        {
            var results = new List<Implication> { rule };
            var varGen = new UnusedVariableGenerator(rule);

            var fact = literal as Fact;
            if (fact != null)
            {
                ISentenceForm form = model.GetSentenceForm(fact);
                ICollection<Ambiguity> ambiguities;
                if (ambiguitiesByOriginalForm.TryGetValue(form, out ambiguities))
                {
                    IEnumerable<Ambiguity> applicableAmiguities = ambiguities.Where(ambiguity => ambiguity.Applies(fact));
                    IEnumerable<Substitution> substitutions = applicableAmiguities.Select(ambiguity => ambiguity.GetReplacementAssignment(fact, varGen));
                    IEnumerable<Implication> implications = substitutions.Select(substitution => (Implication)rule.ApplySubstitution(substitution));
                    results.AddRange(implications);
                }
            }
            else if (literal is Negation)
            {
                // Do nothing. Variables must appear in a positive literal in the
                // rule, and will be handled there.
            }
            else if (literal is Disjunction)
            {
                throw new Exception("ORs should have been removed");
                //} else if (literal is GdlDistinct) {
                // Do nothing
            }

            return results;
        }


        private sealed class UnusedVariableGenerator
        {
            private int _count = 1;
            private readonly ISet<TermVariable> _originalVarsFromRule;

            public UnusedVariableGenerator(Expression rule)
            {
                _originalVarsFromRule = rule.VariablesOrEmpty.ToImmutableHashSet();
            }

            private TermVariable GetUnusedVariable()
            {
                TermVariable curVar;
                do
                    curVar = new TermVariable(GameContainer.SymbolTable["?a" + _count++]);
                while (_originalVarsFromRule.Contains(curVar));

                return curVar;
            }

            public TermFunction ReplaceVariablesAndConstants(Term function)
            {
                var assignment = new Substitution();

                var termsToReplace = new HashSet<Term>();
                GdlVisitors.VisitTerm(function, new GdlVisitor
                {
                    VisitConstant = constant => termsToReplace.Add(constant),
                    VisitVariable = variable => termsToReplace.Add(variable)
                });

                foreach (TermVariable var in GetVariables(function))
                    assignment.AddMapping(var, GetUnusedVariable());

                return (TermFunction)function.ApplySubstitution(assignment);
            }

            private static IEnumerable<TermVariable> GetVariables(Term function)
            {
                var variables = new HashSet<TermVariable>();
                var queue = new Queue<Term>();
                queue.Enqueue(function);
                while (queue.Any())
                {
                    var item = queue.Dequeue();
                    var variable = item as TermVariable;
                    if (variable != null)
                        variables.Add(variable);
                    else
                    {
                        var termFunction = item as TermFunction;
                        if (termFunction != null)
                            foreach (Term arg in termFunction.Arguments)
                                queue.Enqueue(arg);
                    }
                }
                return variables;
            }
        }

        /// <summary>
        /// Removes rules with sentences with empty domains. These simply won't have
        /// sentence forms in the generated sentence model, so this is fairly easy.
        /// </summary>
        /// <param name="expandedRules"></param>
        /// <returns></returns>
        private static IList<Expression> CleanUpIrrelevantRules(IList<Expression> expandedRules)
        {
            ImmutableSentenceFormModel model = SentenceFormModelFactory.Create(expandedRules);

            IEnumerable<Expression> result = expandedRules.Where(input =>
                {
                    if (!(input is Implication)) // If it's not a rule, leave it in                
                        return true;

                    var rule = (Implication)input;
                    // Used just as a bool we can change from the inner class
                    var shouldRemove = new AtomicBoolean(false);
                    GdlVisitors.VisitAll(rule, new GdlVisitor
                        {
                            VisitSentence = sentence =>
                                {
                                    ISentenceForm form = model.GetSentenceForm(sentence);
                                    if (!model.SentenceForms.Contains(form))
                                        shouldRemove.SetValue(true);
                                }
                        }
                        );
                    return !shouldRemove.Value;
                });

            return result.ToImmutableList();
        }
    }
}
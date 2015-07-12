using System.Collections.Immutable;
using System.Linq;
using Wintellect.PowerCollections;
using System.Collections.Generic;

namespace nJocLogic.util.gdl.model
{
    using gameContainer;
    using data;

    public class SentenceFormModelFactory
    {
        ///<summary>
        /// Creates a SentenceFormModel for the given game description.
        /// <para/>
        /// It is recommended to use the <see cref="GdlCleaner"/> on the game description before constructing this model, to 
        /// prevent some common problems with slightly invalid game descriptions.
        /// <para/>
        /// It is also recommended to use the <see cref="VariableConstrainer"/> on the description  before using this. If the 
        /// description allows for function-valued variables, some aspects of the model, including the dependency graph, may 
        /// be incorrect.
        ///</summary>
        public static ImmutableSentenceFormModel Create(IList<Expression> description)
        {
            ImmutableList<Expression> gameRules = description.ToImmutableList();
            ImmutableHashSet<ISentenceForm> sentenceForms = GetSentenceForms(gameRules);
            MultiDictionary<ISentenceForm, Implication> rulesByForm = GetRulesByForm(gameRules, sentenceForms);
            MultiDictionary<ISentenceForm, Fact> trueSentencesByForm = GetTrueSentencesByForm(gameRules, sentenceForms);
            MultiDictionary<ISentenceForm, ISentenceForm> dependencyGraph = GetDependencyGraph(sentenceForms, rulesByForm);
            ImmutableHashSet<ISentenceForm> constantSentenceForms = GetConstantSentenceForms(sentenceForms, dependencyGraph);
            ImmutableHashSet<ISentenceForm> independentSentenceForms = GetIndependentSentenceForms(sentenceForms, dependencyGraph);

            return new ImmutableSentenceFormModel(gameRules,
                                                    sentenceForms,
                                                    constantSentenceForms,
                                                    independentSentenceForms,
                                                    dependencyGraph,
                                                    rulesByForm,
                                                    trueSentencesByForm);
        }

        /// <summary>
        /// Independent sentences are those that do not depend on 'does' facts - eg control
        /// </summary>
        /// <param name="sentenceForms"></param>
        /// <param name="dependencyGraph"></param>
        /// <returns></returns>
        private static ImmutableHashSet<ISentenceForm> GetIndependentSentenceForms(ImmutableHashSet<ISentenceForm> sentenceForms,
            MultiDictionary<ISentenceForm, ISentenceForm> dependencyGraph)
        {
            MultiDictionary<ISentenceForm, ISentenceForm> augmentedGraph = AugmentGraphWithLanguageRules(dependencyGraph, sentenceForms);
            ImmutableHashSet<ISentenceForm> moveDependentSentenceForms =
                DependencyGraphs.GetMatchingAndDownstream(sentenceForms, augmentedGraph,
                    SentenceForms.DoesPred);
            return sentenceForms.SymmetricExcept(moveDependentSentenceForms).ToImmutableHashSet();
        }

        /// <summary>
        /// Contant sentences are those that do not have a dependency on 'true' or 'does' facts
        /// </summary>
        /// <param name="sentenceForms"></param>
        /// <param name="dependencyGraph"></param>
        /// <returns></returns>
        private static ImmutableHashSet<ISentenceForm> GetConstantSentenceForms(ImmutableHashSet<ISentenceForm> sentenceForms,
            MultiDictionary<ISentenceForm, ISentenceForm> dependencyGraph)
        {
            MultiDictionary<ISentenceForm, ISentenceForm> augmentedGraph = AugmentGraphWithLanguageRules(dependencyGraph, sentenceForms);
            ImmutableHashSet<ISentenceForm> changingSentenceForms =
                DependencyGraphs.GetMatchingAndDownstream(sentenceForms, augmentedGraph,
                                                            d => SentenceForms.TruePred(d) || SentenceForms.DoesPred(d));
            return sentenceForms.SymmetricExcept(changingSentenceForms).ToImmutableHashSet();
        }

        /// <summary>
        /// Modifies the graph by adding dependencies corresponding to language rules that apply in a looser 
        /// sense: TRUE forms depend on NEXT forms and DOES forms depend on LEGAL forms.
        /// </summary>
        private static MultiDictionary<ISentenceForm, ISentenceForm> AugmentGraphWithLanguageRules(
            MultiDictionary<ISentenceForm, ISentenceForm> dependencyGraph, ICollection<ISentenceForm> sentenceForms)
        {
            var newGraph = new MultiDictionary<ISentenceForm, ISentenceForm>(true);
            foreach (KeyValuePair<ISentenceForm, ICollection<ISentenceForm>> kv in dependencyGraph)
                newGraph[kv.Key] = kv.Value;

            foreach (ISentenceForm form in sentenceForms)
            {
                if (form.Name == GameContainer.Parser.TokTrue)
                {
                    ISentenceForm nextForm = form.WithName(GameContainer.Parser.TokNext);
                    if (sentenceForms.Contains(nextForm))
                        newGraph.Add(form, nextForm);
                }
                else if (form.Name == GameContainer.Parser.TokDoes)
                {
                    ISentenceForm legalForm = form.WithName(GameContainer.Parser.TokLegal);
                    if (sentenceForms.Contains(legalForm))
                        newGraph.Add(form, legalForm);
                }
            }
            return newGraph;
        }

        private static MultiDictionary<ISentenceForm, ISentenceForm> GetDependencyGraph(ImmutableHashSet<ISentenceForm> sentenceForms,
            MultiDictionary<ISentenceForm, Implication> rulesByForm)
        {
            var dependencyGraph = new MultiDictionary<ISentenceForm, ISentenceForm>(false);
            foreach (KeyValuePair<ISentenceForm, ICollection<Implication>> entry in rulesByForm)
                foreach (Implication rule in entry.Value)
                    foreach (Expression bodyLiteral in rule.Antecedents.Conjuncts)
                        foreach (ISentenceForm sentence in GetSentenceFormsInBody(bodyLiteral, sentenceForms))
                            dependencyGraph.Add(entry.Key, sentence);
            return dependencyGraph;
        }

        private static IEnumerable<ISentenceForm> GetSentenceFormsInBody(Expression bodyLiteral, IEnumerable<ISentenceForm> sentenceForms)
        {
            var forms = new HashSet<ISentenceForm>();

            GdlVisitors.VisitAll(bodyLiteral, new GdlVisitor
                {
                    VisitSentence = fact =>
                        {
                            foreach (ISentenceForm form in sentenceForms.Where(form => form.Matches(fact)))
                                forms.Add(form);
                        }
                });

            return forms;
        }

        private static MultiDictionary<ISentenceForm, Fact> GetTrueSentencesByForm(IEnumerable<Expression> gameRules,
            ImmutableHashSet<ISentenceForm> sentenceForms)
        {

            var builder = new MultiDictionary<ISentenceForm, Fact>(false);

            foreach (Fact sentence in gameRules.OfType<Fact>())
                foreach (ISentenceForm form in sentenceForms)
                    if (form.Matches(sentence))
                    {
                        builder.Add(form, sentence);
                        break;
                    }

            return builder;
        }

        private static MultiDictionary<ISentenceForm, Implication> GetRulesByForm(IEnumerable<Expression> gameRules,
            ImmutableHashSet<ISentenceForm> sentenceForms)
        {
            var builder = new MultiDictionary<ISentenceForm, Implication>(false);

            foreach (var rule in gameRules.OfType<Implication>())
                foreach (ISentenceForm form in sentenceForms)
                    if (form.Matches(rule.Consequent))
                    {
                        builder.Add(form, rule);
                        break;
                    }

            return builder;
        }

        private static ImmutableHashSet<ISentenceForm> GetSentenceForms(IList<Expression> gameRules)
        {
            return new SentenceFormsFinder(gameRules).FindSentenceForms();
        }
    }
}

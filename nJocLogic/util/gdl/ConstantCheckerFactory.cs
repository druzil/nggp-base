using System;
using System.Collections.Generic;
using System.Linq;
using nJocLogic.data;
using nJocLogic.gameContainer;
using nJocLogic.knowledge;
using nJocLogic.reasoner;
using nJocLogic.reasoner.AimaProver;
using nJocLogic.util.gdl.model;
using nJocLogic.util.gdl.transforms;
using Wintellect.PowerCollections;

namespace nJocLogic.util.gdl
{
    public class ConstantCheckerFactory
    {
        /**
     * Precomputes the true sentences in every constant sentence form in the given
     * sentence model and returns the results in the form of a ConstantChecker.
     *
     * The implementation uses a forward-chaining reasoner.
     *
     * For accurate results, the rules used should have had the {@link NewVariableConstrainer}
     * transformation applied to them.
     *
     * On average, this approach is more efficient than {@link #createWithProver(SentenceFormModel)}.
     */
        //public static ImmutableConstantChecker createWithForwardChaining(SentenceDomainModel model)  {
        //    GdlChainingReasoner reasoner = GdlChainingReasoner.create(model);
        //    GdlSentenceSet sentencesByForm = reasoner.getConstantSentences();
        //    addSentencesTrueByRulesDifferentially(sentencesByForm, model, reasoner);
        //    return ImmutableConstantChecker.create(model,
        //            Multimaps.filterKeys(sentencesByForm.getSentences(), Predicates.in(model.getConstantSentenceForms())));
        //}

        //private static void addSentencesTrueByRulesDifferentially(
        //        GdlSentenceSet sentencesByForm,
        //        SentenceDomainModel domainModel, GdlChainingReasoner reasoner)  {
        //    SentenceFormModel model = domainModel;
        //    Set<SentenceForm> constantForms = model.getConstantSentenceForms();
        //    //Find the part of the dependency graph dealing only with the constant forms.
        //    MultiDictionary<SentenceForm, SentenceForm> dependencySubgraph =
        //            Multimaps.filterKeys(model.getDependencyGraph(), Predicates.in(constantForms));
        //    dependencySubgraph = Multimaps.filterValues(model.getDependencyGraph(), Predicates.in(constantForms));
        //    dependencySubgraph = ImmutableMultimap.copyOf(dependencySubgraph);
        //    List<Set<SentenceForm>> ordering = DependencyGraphs.toposortSafe(constantForms, dependencySubgraph);

        //    foreach (Set<SentenceForm> stratum in ordering) {
        //        // One non-differential pass, collecting the changes
        //        GdlSentenceSet newlyTrueSentences = GdlSentenceSet.create();
        //        foreach (SentenceForm form in stratum) {
        //            foreach (Implication rule in model.getRules(form)) {
        //                GdlSentenceSet ruleResults =
        //                        reasoner.getRuleResults(rule, domainModel, sentencesByForm);
        //                if (!reasoner.isSubsetOf(sentencesByForm, ruleResults)) {
        //                    sentencesByForm = reasoner.getUnion(sentencesByForm, ruleResults);
        //                    newlyTrueSentences = reasoner.getUnion(newlyTrueSentences, ruleResults);
        //                }
        //            }
        //        }

        //        // Now a lot of differential passes to deal with recursion efficiently
        //        bool somethingChanged = true;
        //        while (somethingChanged) {
        //            somethingChanged = false;
        //            GdlSentenceSet newStuffInThisPass = GdlSentenceSet.create();
        //            foreach (SentenceForm form in stratum) {
        //                foreach (Implication rule in model.getRules(form)) {
        //                    GdlSentenceSet ruleResults =
        //                            reasoner.getRuleResultsForNewSentences(rule, domainModel, sentencesByForm,
        //                                    newlyTrueSentences);
        //                    if (!reasoner.isSubsetOf(sentencesByForm, ruleResults)) {
        //                        somethingChanged = true;
        //                        newStuffInThisPass = reasoner.getUnion(newStuffInThisPass, ruleResults);
        //                    }
        //                }
        //            }
        //            sentencesByForm = reasoner.getUnion(sentencesByForm, newStuffInThisPass);
        //            newlyTrueSentences = newStuffInThisPass;
        //        }
        //    }
        //}

        /**
     * Precomputes the true sentences in every constant sentence form in the given
     * sentence model and returns the results in the form of a ConstantChecker.
     *
     * The implementation uses a backwards-chaining theorem prover.
     *
     * In most (but not all) cases, {@link #createWithForwardChaining(SentenceDomainModel)}
     * is more efficient.
     */
        public static ImmutableConstantChecker CreateWithProver(ISentenceFormModel model)
        {
            var sentencesByForm = new MultiDictionary<ISentenceForm, Fact>(false);
            AddSentencesTrueByRules(sentencesByForm, model);
            return ImmutableConstantChecker.Create(model, sentencesByForm);
        }

        //INFO: using the old BasicReasoner which appears to be much slower
        //private static void AddSentencesTrueByRules(
        //    MultiDictionary<ISentenceForm, Fact> sentencesByForm,
        //    ISentenceFormModel model)
        //{
        //    BasicReasoner prover = GameContainer.Reasoner;
        //    var context = new ProofContext(new BasicKB(), GameContainer.Parser);
        //    foreach (ISentenceForm form in model.ConstantSentenceForms)
        //    {
        //        Fact query = form.GetSentenceFromTuple(GetVariablesTuple(form.TupleSize));
        //        IEnumerable<GroundFact> answers = prover.GetAllAnswers(query, context);
        //        foreach (GroundFact result in answers)
        //        {
        //            //ConcurrencyUtils.checkForInterruption();
        //            //Variables may end up being replaced with functions, which is not what we want here, so we have to double-check that the form is correct.
        //            if (form.Matches(result))
        //                sentencesByForm.Add(form, result);
        //        }
        //    }
        //}

        private static void AddSentencesTrueByRules(MultiDictionary<ISentenceForm, Fact> sentencesByForm, ISentenceFormModel model)
        {
            AimaProver prover = new AimaProver(model.Description);
            foreach (ISentenceForm form in model.ConstantSentenceForms)
            {
                Fact query = form.GetSentenceFromTuple(GetVariablesTuple(form.TupleSize));
                HashSet<Fact> context = new HashSet<Fact>();
                foreach (Fact result in prover.AskAll(query, context))
                    if (form.Matches(result))
                        sentencesByForm.Add(form, result);
            }
        }

        private static IList<Term> GetVariablesTuple(int tupleSize)
        {
            return Enumerable.Range(0, tupleSize).Select(i => (Term)new TermVariable(GameContainer.SymbolTable["?" + i])).ToList();
        }
    }
}

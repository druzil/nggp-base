using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using nJocLogic.data;
using nJocLogic.gameContainer;
using Wintellect.PowerCollections;

namespace nJocLogic.util.gdl.model
{
    public class SentenceDomainModelOptimizer
    {
        ///<summary>
        /// Given a SentenceDomainModel, returns an ImmutableSentenceDomainModel with Cartesian domains that tries to 
        /// minimize the domains of sentence forms without impacting the game rules. In particular, when sentences are 
        /// restricted to these domains, the answers to queries about terminal, legal, goal, next, and init sentences
        /// will not change.
        ///
        /// Note that if a sentence form is not used in a meaningful way by the game, it may end up with an empty domain.
        ///
        /// The description for the game must have had the <see cref="VariableConstrainer"/> applied to it.
        ///</summary>
        public static ImmutableSentenceDomainModel RestrictDomainsToUsefulValues(ISentenceDomainModel oldModel)
        {
            // Start with everything from the current domain model.
            var neededAndPossibleConstantsByForm = new Dictionary<ISentenceForm, MultiDictionary<int, TermObject>>();
            foreach (ISentenceForm form in oldModel.SentenceForms)
            {
                neededAndPossibleConstantsByForm[form] = new MultiDictionary<int, TermObject>(false);
                AddDomain(neededAndPossibleConstantsByForm[form], oldModel.GetDomain(form), form);
            }

            MinimizeDomains(oldModel, neededAndPossibleConstantsByForm);

            return ToSentenceDomainModel(neededAndPossibleConstantsByForm, oldModel);
        }

        /// <summary>
        /// To minimize the contents of the domains, we repeatedly go through two processes to reduce the domain:
        ///   
        /// 1) We remove unneeded constants from the domain. These are constants which (in their position) do not 
        ///    contribute to any sentences with a GDL keyword as its name; that is, it never matters whether a 
        ///    sentence with that constant in that position is true or false.
        /// 2) We remove impossible constants from the domain. These are constants which cannot end up in their 
        ///    position via any rule or sentence in the game description, given the current domain.
        ///   
        /// Constants removed because of one type of pass or the other may cause other constants in other sentence 
        /// forms to become unneeded or impossible, so we make multiple passes until everything is stable.
        /// </summary>
        /// <param name="oldModel"></param>
        /// <param name="neededAndPossibleConstantsByForm"></param>
        public static void MinimizeDomains(ISentenceDomainModel oldModel, Dictionary<ISentenceForm, MultiDictionary<int, TermObject>> neededAndPossibleConstantsByForm)
        {
            bool somethingChanged = true;
            while (somethingChanged)
            {
                somethingChanged = RemoveUnneededConstants(neededAndPossibleConstantsByForm, oldModel);
                somethingChanged |= RemoveImpossibleConstants(neededAndPossibleConstantsByForm, oldModel);
            }
        }

        private static void AddDomain(IDictionary<int, ICollection<TermObject>> setMultimap, ISentenceFormDomain domain, ISentenceForm form)
        {
            for (int i = 0; i < form.TupleSize; i++)
                setMultimap[i] = domain.GetDomainForSlot(i);
        }

        private static bool RemoveImpossibleConstants(Dictionary<ISentenceForm, MultiDictionary<int, TermObject>> curDomains, ISentenceFormModel model)
        {
            var newPossibleConstantsByForm = new Dictionary<ISentenceForm, MultiDictionary<int, TermObject>>();
            foreach (ISentenceForm form in curDomains.Keys)
                newPossibleConstantsByForm[form] = new MultiDictionary<int, TermObject>(false);

            PopulateInitialPossibleConstants(newPossibleConstantsByForm, curDomains, model);

            bool somethingChanged = true;
            while (somethingChanged)
                somethingChanged = PropagatePossibleConstants(newPossibleConstantsByForm, curDomains, model);

            return RetainNewDomains(curDomains, newPossibleConstantsByForm);
        }

        private static void PopulateInitialPossibleConstants(IDictionary<ISentenceForm, MultiDictionary<int, TermObject>> newPossibleConstantsByForm,
            IDictionary<ISentenceForm, MultiDictionary<int, TermObject>> curDomains, ISentenceFormModel model)
        {
            //Add anything in the head of a rule...
            foreach (Implication rule in GetRules(model.Description))
                AddConstantsFromSentenceIfInOldDomain(newPossibleConstantsByForm, curDomains, model, rule.Consequent);

            //... and any true sentences
            foreach (ISentenceForm form in model.SentenceForms)
                foreach (Fact sentence in model.GetSentencesListedAsTrue(form))
                    AddConstantsFromSentenceIfInOldDomain(newPossibleConstantsByForm, curDomains, model, sentence);
        }

        private static bool PropagatePossibleConstants(Dictionary<ISentenceForm, MultiDictionary<int, TermObject>> newPossibleConstantsByForm,
            IDictionary<ISentenceForm, MultiDictionary<int, TermObject>> curDomain, ISentenceFormModel model)
        {
            //Injection: Go from the intersections of variable values in rules to the
            //values in their heads
            bool somethingChanged = false;

            foreach (Implication rule in GetRules(model.Description))
            {
                Fact head = rule.Consequent;

                var domainsOfHeadVars = new Dictionary<TermVariable, ISet<TermObject>>();
                foreach (TermVariable varInHead in rule.Consequent.VariablesOrEmpty.ToImmutableHashSet())
                {
                    ISet<TermObject> domain = GetVarDomainInRuleBody(varInHead, rule, newPossibleConstantsByForm, curDomain, model);
                    domainsOfHeadVars[varInHead] = domain;
                    somethingChanged |= AddPossibleValuesToSentence(domain, head, varInHead, newPossibleConstantsByForm, model);
                }
            }

            var parser = GameContainer.Parser;
            //Language-based injections
            somethingChanged |= ApplyLanguageBasedInjections(parser.TokInit, parser.TokTrue, newPossibleConstantsByForm);
            somethingChanged |= ApplyLanguageBasedInjections(parser.TokNext, parser.TokTrue, newPossibleConstantsByForm);
            somethingChanged |= ApplyLanguageBasedInjections(parser.TokLegal, parser.TokDoes, newPossibleConstantsByForm);

            return somethingChanged;
        }

        private static bool ApplyLanguageBasedInjections(int curName, int resultingName,
            Dictionary<ISentenceForm, MultiDictionary<int, TermObject>> newPossibleConstantsByForm)
        {
            bool somethingChanged = false;
            foreach (ISentenceForm form in newPossibleConstantsByForm.Keys)
            {
                //ConcurrencyUtils.checkForInterruption();
                if (form.Name == curName)
                {
                    ISentenceForm resultingForm = form.WithName(resultingName);

                    MultiDictionary<int, TermObject> curFormDomain = newPossibleConstantsByForm[form];
                    MultiDictionary<int, TermObject> resultingFormDomain = newPossibleConstantsByForm[resultingForm];

                    var before = resultingFormDomain.Count;
                    foreach (KeyValuePair<int, ICollection<TermObject>> domain in curFormDomain)
                        resultingFormDomain.Add(domain);
                    somethingChanged |= before != resultingFormDomain.Count;
                }
            }
            return somethingChanged;
        }

        private static ISet<TermObject> GetVarDomainInRuleBody(TermVariable varInHead, Implication rule,
            IDictionary<ISentenceForm, MultiDictionary<int, TermObject>> newPossibleConstantsByForm,
            IDictionary<ISentenceForm, MultiDictionary<int, TermObject>> curDomain, ISentenceFormModel model)
        {
            try
            {
                var domains = new List<ISet<TermObject>>();
                foreach (Fact conjunct in GetPositiveConjuncts(rule.Antecedents.Conjuncts.ToList()))
                    if (conjunct.VariablesOrEmpty.Contains(varInHead))
                        domains.Add(GetVarDomainInSentence(varInHead, conjunct, newPossibleConstantsByForm, curDomain, model));
                return GetIntersection(domains);
            }
            catch (Exception e)
            {
                throw new Exception("Error in rule " + rule + " for variable " + varInHead, e);
            }
        }

        private static ISet<TermObject> GetVarDomainInSentence(TermVariable var1, Fact conjunct,
                                IDictionary<ISentenceForm, MultiDictionary<int, TermObject>> newPossibleConstantsByForm,
                                IDictionary<ISentenceForm, MultiDictionary<int, TermObject>> curDomain, ISentenceFormModel model)
        {
            ISentenceForm form = model.GetSentenceForm(conjunct);
            List<Term> tuple = conjunct.NestedTerms.ToList();

            var domains = new List<ISet<TermObject>>();
            for (int i = 0; i < tuple.Count; i++)
                if (Equals(tuple[i], var1))
                {
                    domains.Add(new HashSet<TermObject>(newPossibleConstantsByForm[form][i]));
                    domains.Add(new HashSet<TermObject>(curDomain[form][i]));
                }
            return GetIntersection(domains);
        }

        private static ISet<TermObject> GetIntersection(IReadOnlyList<ISet<TermObject>> domains)
        {
            if (!domains.Any())
                throw new Exception("Unsafe rule has no positive conjuncts");

            ISet<TermObject> intersection = new HashSet<TermObject>(domains[0]);
            for (int i = 1; i < domains.Count; i++)
                intersection.IntersectWith(domains[i]);
            return intersection;
        }

        private static bool RemoveUnneededConstants(Dictionary<ISentenceForm, MultiDictionary<int, TermObject>> curDomains, ISentenceFormModel model)
        {
            var newNeededConstantsByForm = new Dictionary<ISentenceForm, MultiDictionary<int, TermObject>>();
            foreach (ISentenceForm form in curDomains.Keys)
                newNeededConstantsByForm[form] = new MultiDictionary<int, TermObject>(false);
            PopulateInitialNeededConstants(newNeededConstantsByForm, curDomains, model);

            bool somethingChanged = true;
            while (somethingChanged)
                somethingChanged = PropagateNeededConstants(newNeededConstantsByForm, curDomains, model);

            return RetainNewDomains(curDomains, newNeededConstantsByForm);
        }

        private static bool RetainNewDomains(Dictionary<ISentenceForm, MultiDictionary<int, TermObject>> curDomains,
                                            IReadOnlyDictionary<ISentenceForm, MultiDictionary<int, TermObject>> newDomains)
        {
            bool somethingChanged = false;
            foreach (ISentenceForm form in curDomains.Keys.ToList())
            {
                MultiDictionary<int, TermObject> newDomain = newDomains[form];

                //int before = curDomains[form].Count;
                var newCurDomain = new MultiDictionary<int, TermObject>(false);
                foreach (var mainEntry in curDomains[form].ToImmutableDictionary())
                    foreach (TermObject entry in mainEntry.Value)
                        if (newDomain.Contains(mainEntry.Key, entry))
                            newCurDomain.Add(mainEntry.Key, entry);

                somethingChanged |= curDomains[form].Count != newCurDomain.Count;
                curDomains[form] = newCurDomain;
            }
            return somethingChanged;
        }

        private static bool PropagateNeededConstants(Dictionary<ISentenceForm, MultiDictionary<int, TermObject>> neededConstantsByForm,
                                        Dictionary<ISentenceForm, MultiDictionary<int, TermObject>> curDomains, ISentenceFormModel model)
        {
            bool somethingChanged = ApplyRuleHeadPropagation(neededConstantsByForm, curDomains, model);
            return somethingChanged | ApplyRuleBodyOnlyPropagation(neededConstantsByForm, curDomains, model);
        }


        private static bool ApplyRuleBodyOnlyPropagation(IDictionary<ISentenceForm, MultiDictionary<int, TermObject>> neededConstantsByForm,
                                            Dictionary<ISentenceForm, MultiDictionary<int, TermObject>> curDomains, ISentenceFormModel model)
        {
            bool somethingChanged = false;
            //If a variable does not appear in the head of a variable, then all the values that are in the intersections of all the
            //domains from the positive conjuncts containing the variable become needed.

            foreach (Implication rule in GetRules(model.Description))
            {
                Fact head = rule.Consequent;
                ISet<TermVariable> varsInHead = head.VariablesOrEmpty.ToImmutableHashSet();

                IDictionary<TermVariable, ISet<TermObject>> varDomains = GetVarDomains(rule, curDomains, model);
                foreach (TermVariable var in rule.VariablesOrEmpty.ToImmutableHashSet())
                {
                    if (!varsInHead.Contains(var))
                    {
                        ISet<TermObject> neededConstants = varDomains[var];
                        if (neededConstants == null)
                            throw new Exception(string.Format("var is {0};\nvarDomains key set is {1};\nvarsInHead is {2};\nrule is " + rule, var, varDomains.Keys, varsInHead));

                        foreach (Expression conjunct in rule.Antecedents.Conjuncts)
                            somethingChanged |= AddPossibleValuesToConjunct(neededConstants, conjunct, var, neededConstantsByForm, model);
                    }
                }
            }
            return somethingChanged;
        }

        class OptimizerSentenceFormDomain : ISentenceFormDomain
        {
            private readonly ISentenceForm form;
            private readonly Dictionary<ISentenceForm, MultiDictionary<int, TermObject>> curDomains;

            public OptimizerSentenceFormDomain(ISentenceForm form, Dictionary<ISentenceForm, MultiDictionary<int, TermObject>> curDomains)
            {
                this.form = form;
                this.curDomains = curDomains;
            }

            public IEnumerator<Fact> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public ISentenceForm Form { get { return form; } }

            public ISet<TermObject> GetDomainForSlot(int slotIndex)
            {
                return !curDomains.ContainsKey(form)
                    ? JavaHashSet<TermObject>.Empty
                    : new JavaHashSet<TermObject>(curDomains[form][slotIndex]);
            }
        }

        private class OptimizerSentenceDomainModel : AbstractSentenceDomainModel
        {
            private readonly Dictionary<ISentenceForm, MultiDictionary<int, TermObject>> curDomains;

            public OptimizerSentenceDomainModel(ISentenceFormModel formModel, Dictionary<ISentenceForm, MultiDictionary<int, TermObject>> curDomains)
                : base(formModel)
            {
                this.curDomains = curDomains;
            }

            public override ISentenceFormDomain GetDomain(ISentenceForm form)
            {
                return new OptimizerSentenceFormDomain(form, curDomains);
            }
        }

        private static IDictionary<TermVariable, ISet<TermObject>> GetVarDomains(Implication rule,
            Dictionary<ISentenceForm, MultiDictionary<int, TermObject>> curDomains, ISentenceFormModel model)
        {
            var domainModel = new OptimizerSentenceDomainModel(model, curDomains);
            return SentenceDomainModels.GetVarDomains(rule, domainModel, SentenceDomainModels.VarDomainOpts.IncludeHead);
        }

        private static bool ApplyRuleHeadPropagation(Dictionary<ISentenceForm, MultiDictionary<int, TermObject>> neededConstantsByForm,
                                    Dictionary<ISentenceForm, MultiDictionary<int, TermObject>> curDomains, ISentenceFormModel model)
        {
            bool somethingChanged = false;
            //If a term that is a variable in the head of a rule needs a particular value, AND that variable is possible (i.e. in the
            //current domain) in every appearance of the variable in positive conjuncts in the rule's body, then the value is
            //needed in every appearance of the variable in the rule (positive or negative).
            foreach (Implication rule in GetRules(model.Description))
            {
                Fact head = rule.Consequent;
                ISentenceForm headForm = model.GetSentenceForm(head);
                List<Term> headTuple = head.NestedTerms.ToList();

                IDictionary<TermVariable, ISet<TermObject>> varDomains = GetVarDomains(rule, curDomains, model);

                for (int i = 0; i < headTuple.Count; i++)
                {
                    //ConcurrencyUtils.checkForInterruption();
                    var curVar = headTuple[i] as TermVariable;
                    if (curVar != null)
                    {
                        var neededConstants = new HashSet<TermObject>(neededConstantsByForm[headForm][i]);

                        //Whittle these down based on what's possible throughout the rule
                        ISet<TermObject> neededAndPossibleConstants = new HashSet<TermObject>(neededConstants);
                        neededAndPossibleConstants.IntersectWith(varDomains[curVar]);
                        //Relay those values back to the conjuncts in the rule body
                        foreach (Expression conjunct in rule.Antecedents.Conjuncts)
                            somethingChanged |= AddPossibleValuesToConjunct(neededAndPossibleConstants, conjunct, curVar, neededConstantsByForm, model);
                    }
                }
            }
            return somethingChanged;
        }

        private static bool AddPossibleValuesToConjunct(ISet<TermObject> neededAndPossibleConstants, Expression conjunct, TermVariable curVar,
                                IDictionary<ISentenceForm, MultiDictionary<int, TermObject>> neededConstantsByForm, ISentenceFormModel model)
        {
            var fact = conjunct as Fact;
            if (fact != null)
                return fact.RelationName != GameContainer.Parser.TokDistinct
                       && AddPossibleValuesToSentence(neededAndPossibleConstants, fact, curVar, neededConstantsByForm, model);

            var negation = conjunct as Negation;
            if (negation != null)
                return AddPossibleValuesToSentence(neededAndPossibleConstants, (Fact)negation.Negated, curVar, neededConstantsByForm, model);

            if (conjunct is Disjunction)
                throw new Exception("The SentenceDomainModelOptimizer is not designed for game descriptions with OR. Use the DeORer.");
            throw new Exception("Unexpected literal type " + conjunct.GetType() + " for literal " + conjunct);
        }

        private static bool AddPossibleValuesToSentence(ISet<TermObject> neededAndPossibleConstants, Fact sentence, TermVariable curVar,
            IDictionary<ISentenceForm, MultiDictionary<int, TermObject>> neededConstantsByForm, ISentenceFormModel model)
        {
            bool somethingChanged = false;

            ISentenceForm form = model.GetSentenceForm(sentence);
            List<Term> tuple = sentence.NestedTerms.ToList();
            Debug.Assert(form.TupleSize == tuple.Count);

            for (int i = 0; i < tuple.Count; i++)
            {
                if (Equals(tuple[i], curVar))
                {
                    Debug.Assert(neededConstantsByForm[form] != null);
                    Debug.Assert(neededAndPossibleConstants != null);
                    var before = neededConstantsByForm[form][i].Count;
                    neededConstantsByForm[form].AddMany(i, neededAndPossibleConstants);
                    somethingChanged |= before != neededConstantsByForm[form][i].Count;
                }
            }
            return somethingChanged;
        }

        private static IEnumerable<Fact> GetPositiveConjuncts(IEnumerable<Expression> body)
        {
            return body.OfType<Fact>().Where(fact => fact.RelationName != GameContainer.Parser.TokDistinct);
        }

        /// <summary>
        /// Unlike getPositiveConjuncts, this also returns sentences inside NOT literals.
        /// </summary> 
        private static IEnumerable<Fact> GetAllSentencesInBody(List<Expression> body)
        {
            var sentences = new List<Fact>();
            GdlVisitors.VisitAll(body, new GdlVisitor { VisitSentence = sentence => sentences.Add(sentence) });
            return sentences;
        }

        private static IEnumerable<Implication> GetRules(IEnumerable<Expression> description)
        {
            return description.Where(input => input is Implication).Cast<Implication>();
        }

        private static readonly ImmutableHashSet<int> AlwaysNeededSentenceNames = ImmutableHashSet.Create(
                GameContainer.Parser.TokNext, GameContainer.Parser.TokGoal, GameContainer.Parser.TokLegal,
                GameContainer.Parser.TokInit, GameContainer.Parser.TokRole, GameContainer.SymbolTable["base"],
                GameContainer.SymbolTable["input"], GameContainer.Parser.TokTrue, GameContainer.Parser.TokDoes);

        private static void PopulateInitialNeededConstants(IDictionary<ISentenceForm, MultiDictionary<int, TermObject>> newNeededConstantsByForm,
            IDictionary<ISentenceForm, MultiDictionary<int, TermObject>> curDomains, ISentenceFormModel model)
        {
            // If the term model is part of a keyword-named sentence, then it is needed. This includes base and init.
            foreach (ISentenceForm form in model.SentenceForms) //ConcurrencyUtils.checkForInterruption();
                if (AlwaysNeededSentenceNames.Contains(form.Name))
                    foreach (var kv in curDomains[form])
                        newNeededConstantsByForm[form].AddMany(kv.Key, kv.Value);

            // If the term has a constant value in some sentence in the BODY of a rule, then it is needed.
            foreach (Implication rule in GetRules(model.Description))
                foreach (Fact sentence in GetAllSentencesInBody(rule.Antecedents.Conjuncts.ToList()))
                    AddConstantsFromSentenceIfInOldDomain(newNeededConstantsByForm, curDomains, model, sentence);
        }

        private static void AddConstantsFromSentenceIfInOldDomain(IDictionary<ISentenceForm, MultiDictionary<int, TermObject>> newConstantsByForm,
            IDictionary<ISentenceForm, MultiDictionary<int, TermObject>> oldDomain, ISentenceFormModel model, Fact sentence)
        {
            ISentenceForm form = model.GetSentenceForm(sentence);
            List<Term> tuple = sentence.NestedTerms.ToList();
            if (tuple.Count != form.TupleSize)
                throw new Exception();

            for (int i = 0; i < form.TupleSize; i++)
            {
                var term = tuple[i] as TermObject;
                if (term != null && oldDomain[form][i].Contains(term))
                    newConstantsByForm[form].Add(i, term);
            }
        }

        private static ImmutableSentenceDomainModel ToSentenceDomainModel(
            IDictionary<ISentenceForm, MultiDictionary<int, TermObject>> neededAndPossibleConstantsByForm, ISentenceFormModel formModel)
        {
            var domains = new Dictionary<ISentenceForm, ISentenceFormDomain>();
            foreach (ISentenceForm form in formModel.SentenceForms)
                domains[form] = new CartesianSentenceFormDomain(form, neededAndPossibleConstantsByForm[form]);

            return new ImmutableSentenceDomainModel(formModel, domains);
        }
    }
}

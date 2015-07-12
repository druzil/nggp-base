using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace nJocLogic.util.gdl
{
    using data;
    using gameContainer;
    using propNet.factory;
    using model;
    using model.assignments;

    /// <summary>
    /// The CondensationIsolator is a GDL transformation designed to split up rules in a way 
    /// that results in smaller propnets. For example, we may have a rule as follows:
    /// <para/>
    /// (&lt;= (foo ?x ?y)
    ///     (bar ?x ?y)
    ///     (baz ?y ?z))
    /// <para/>
    /// In the propnet, this will result in one AND node for each combination of ?x, ?y, and ?z. The 
    /// CondensationIsolator would split it up as follows:
    /// <para/>
    /// (&lt;= (foo ?x ?y)
    ///     (bar ?x ?y)
    ///     (baz_tmp0 ?y))
    /// (&lt;= (baz_tmp0 ?y)
    ///     (baz ?y ?z))
    /// <para/>
    /// In the propnet, there will now be one AND node for each combination of ?x and ?y and one new link 
    /// for each combination of ?y and ?z, but there will not be a cross-product of the domains of all three.
    /// <para/>
    /// "Condensation" refers to the type of rule generated, in which we simply ignore certain variables.
    /// </summary>
    public class CondensationIsolator
    {
        public static List<Expression> Run(IList<Expression> description)
        {
            // This class is not put together in any "optimal" way, so it's left in an unpolished state for 
            // now. A better version would use estimates of the impact of breaking apart rules. (It also needs 
            // to stop itself from making multiple new relations with the same meaning.)

            //This version will be rather advanced. In particular, it will try to incorporate
            //1) More thorough scanning for condensations;
            //2) Condensations that are only safe to perform because of mutexes.

            //TODO: Don't perform condensations on stuff like (Add _ _ _)...
            //In general, don't perform condensations where the headroom is huge?
            //Better yet... DON'T perform condensations on recursive functions!
            //As for headroom... maybe make sure that # of vars eliminated > # "kept"
            //Or make sure none are kept? Use directional connected components?

            //How do we define a condensation, and what needs to be true in it?
            //Definition: A condensation set is a set of conjuncts of a sentence.
            //Restrictions:
            //1) There must be some variable not in the head of the sentence that appears exclusively in the 
            //   condensation set. (This means we can easily find sets one of which must be a condensation set.)
            //2) For any variable appearing in a distinct or not conjunct in the set, there must be a positive 
            //   conjunct in the set also containing that variable. This does apply to variables found in the head.
            //3) There must be at least one non-distinct literal outside the condensation set.

            //How mutexes work:
            //Say we have a rule
            //  (<= (r1 ?b)
            //      (r2 ?a ?b ?c)
            //      (r3 ?b ?c)
            //		(r4 ?a)
            //		(r5 ?c))
            //If we wanted to factor out ?a, we'd normally have to do
            /*  (<= (r6 ?b ?c)
             * 		(r2 ?a ?b ?c)
             * 		(r4 ?a))
             *  (<= (r1 ?b)
             * 		(r6 ?b ?c)
             * 		(r3 ?b ?c)
             * 		(r5 ?c))
             * But if we know r2 is a mutex, instead we can do (notice r2 splitting):
             *  (<= (r6 ?b)
             * 		(r2 ?a ?b ?c)
             * 		(r4 ?a))
             *  (<= (r1 ?b)
             *  	(r2 ?a ?b ?c)
             *  	(r6 ?b)
             *  	(r3 ?b ?c)
             *  	(r5 ?c))
             * Which in turn becomes:
             *  (<= (r6 ?b)
             * 		(r2 ?a ?b ?c)
             * 		(r4 ?a))
             *  (<= (r7 ?b)
             *  	(r2 ?a ?b ?c)
             *  	(r3 ?b ?c)
             *  	(r5 ?c))
             *  (<= (r1 ?b)
             *  	(r6 ?b)
             *		(r7 ?b))
             * Both r6 and r7 can be further condensed to ignore ?c and ?a, respectively. What just happened?
             * 1) The condensation set for ?a included the mutex r2.
             * 2) r2 (by itself) would have required ?c to be included as an argument passed back to the 
             *      original rule, which is undesirable. Instead, as it's a mutex, we leave a copy in the 
             *      original rule and don't include the ?c.
             *
             * So, what kind of algorithm can we find to solve this task?
             */
            var newDescription = new List<Expression>();
            var rulesToAdd = new Queue<Implication>();

            foreach (Expression gdl in description)
            {
                var implication = gdl as Implication;
                if (implication == null)
                    newDescription.Add(gdl);
                else
                    rulesToAdd.Enqueue(implication);
            }

            //Don't use the model indiscriminately; it reflects the old description, not necessarily the new one
            ISentenceDomainModel model = SentenceDomainModelFactory.CreateWithCartesianDomains(description);
            model = SentenceDomainModelOptimizer.RestrictDomainsToUsefulValues(model);
            var sentenceNameSource = new UnusedSentenceNameSource(model);
            //TODO: ConstantChecker constantChecker = ConstantCheckerFactory.createWithForwardChaining(model);	
            IConstantChecker constantChecker = ConstantCheckerFactory.CreateWithProver(model);

            ISet<ISentenceForm> constantForms = model.ConstantSentenceForms;

            ISentenceDomain condensorDomain = model;
            var curDescription = new List<Expression>(description);
            while (rulesToAdd.Any())
            {
                Implication curRule = rulesToAdd.Dequeue();
                if (IsRecursive(curRule))
                {
                    //Don't mess with it!
                    newDescription.Add(curRule);
                    continue;
                }
                Fact curRuleHead = curRule.Consequent;

                if (SentenceFormAdder.InSentenceFormGroup(curRuleHead, constantForms))
                {
                    newDescription.Add(curRule);
                    continue;
                }
                ISet<Expression> condensationSet = GetCondensationSet(curRule, condensorDomain, constantChecker, sentenceNameSource);

                if (condensationSet != null)
                {
                    List<Implication> newRules = ApplyCondensation(condensationSet, curRule, sentenceNameSource);
                    newRules.ForEach(rulesToAdd.Enqueue);

                    //Since we're making only small changes, we can readjust
                    //the model as we go, instead of recomputing it
                    var oldRules = new List<Expression> { curRule };
                    var replacementDescription = new List<Expression>(curDescription);
                    replacementDescription.RemoveAll(oldRules.Contains);
                    replacementDescription.AddRange(newRules);
                    curDescription = replacementDescription;
                    condensorDomain = AugmentModelWithNewForm(condensorDomain, newRules);
                }
                else
                    newDescription.Add(curRule);
            }
            return newDescription;
        }

        /// <summary>
        /// Save the description in a new file
        /// Useful for debugging chains of condensations to see which cause decreased performance
        /// </summary>
        public static void SaveKif(IEnumerable<Expression> description)
        {
            String filename = "ci0.kif";
            int filenum = 0;
            while (File.Exists(filename))
                filename = "ci" + ++filenum + ".kif";

            StreamWriter out1 = null;
            try
            {
                out1 = new StreamWriter(filename);
                foreach (Expression gdl in description)
                    out1.WriteLine(gdl + "\n");
            }
            catch (IOException e)
            {
                Console.WriteLine(e.StackTrace);
            }
            finally
            {
                try
                {
                    if (out1 != null)
                        out1.Close();
                }
                catch (IOException) { }
            }
        }

        private static bool IsRecursive(Implication rule)
        {
            return rule.Antecedents.Conjuncts.OfType<Fact>().Any(literal => literal.RelationName.Equals(rule.Consequent.RelationName));
        }

        private class UnusedSentenceNameSource
        {
            private readonly HashSet<String> _allNamesSoFar;

            internal UnusedSentenceNameSource(ISentenceFormModel model)
            {
                ISet<String> initialNames = SentenceForms.GetNames(model.SentenceForms);
                _allNamesSoFar = new HashSet<string>(initialNames);
            }

            public int GetNameWithPrefix(int prefix)
            {
                string prefixString = GameContainer.SymbolTable[prefix];
                for (int i = 0; ; i++)
                {
                    String candidateName = prefixString + "_tmp" + i;
                    if (!_allNamesSoFar.Contains(candidateName))
                    {
                        _allNamesSoFar.Add(candidateName);
                        return GameContainer.SymbolTable[candidateName];
                    }
                }
            }
        }

        private static List<Implication> ApplyCondensation(ICollection<Expression> condensationSet, Implication rule,
            UnusedSentenceNameSource sentenceNameSource)
        {

            var varsInCondensationSet = new HashSet<TermVariable>();
            foreach (Expression literal in condensationSet)
                foreach (var variable in literal.VariablesOrEmpty)
                    varsInCondensationSet.Add(variable);
            var varsToKeep = new HashSet<TermVariable>();
            //Which vars do we "keep" (put in our new condensed literal)?
            //Vars that are both:
            //1) In the condensation set, in a non-mutex literal
            //2) Either in the head or somewhere else outside the condensation set
            foreach (Expression literal in condensationSet)
                foreach (var variable in literal.VariablesOrEmpty)
                    varsToKeep.Add(variable);

            var varsToKeep2 = new HashSet<TermVariable>();
            foreach (var variable in rule.Consequent.VariablesOrEmpty)
                varsToKeep2.Add(variable);
            foreach (Expression literal in rule.Antecedents.Conjuncts)
                if (!condensationSet.Contains(literal))
                    foreach (var variable in literal.VariablesOrEmpty)
                        varsToKeep2.Add(variable);

            varsToKeep.IntersectWith(varsToKeep2);

            //Now we're ready to split it apart
            //Let's make the new rule
            var orderedVars = new List<Term>(varsToKeep);
            int condenserName = sentenceNameSource.GetNameWithPrefix(rule.Consequent.RelationName);
            //Make the rule head
            var condenserHead = new VariableFact(false, condenserName, orderedVars.ToArray());
            var condenserBody = new List<Expression>(condensationSet);
            var condenserRule = new Implication(condenserHead, condenserBody.ToArray());

            //TODO: Look for existing rules matching the new one
            var remainingLiterals = rule.Antecedents.Conjuncts.Where(literal => !condensationSet.Contains(literal)).ToList();

            remainingLiterals.Add(condenserHead);
            var modifiedRule = new Implication(rule.Consequent, remainingLiterals.ToArray());

            var newRules = new List<Implication>(2) { condenserRule, modifiedRule };
            return newRules;
        }

        private static ISet<Expression> GetCondensationSet(Implication rule, ISentenceDomain model, IConstantChecker checker,
                                                           UnusedSentenceNameSource sentenceNameSource)
        {
            //We use each variable as a starting point
            List<TermVariable> varsInRule = rule.VariablesOrEmpty.ToList();
            List<TermVariable> varsInHead = rule.Consequent.VariablesOrEmpty.ToList();
            var varsNotInHead = new List<TermVariable>(varsInRule);
            varsNotInHead.RemoveAll(varsInHead.Contains);

            foreach (TermVariable var in varsNotInHead)
            {
                var minSet = new HashSet<Expression>();
                foreach (Expression literal in rule.Antecedents.Conjuncts)
                    if (literal.VariablesOrEmpty.Contains(var))
                        minSet.Add(literal);

                //#1 is already done
                //Now we try #2
                var varsNeeded = new HashSet<TermVariable>();
                var varsSupplied = new HashSet<TermVariable>();
                foreach (Expression literal in minSet)
                {
                    if (literal is Negation)
                    {
                        foreach (var variable in literal.VariablesOrEmpty)
                            varsNeeded.Add(variable);
                    }
                    else if (literal is Fact)
                    {
                        if (((Fact)literal).RelationName == GameContainer.Parser.TokDistinct)
                            foreach (var variable in literal.VariablesOrEmpty)
                                varsNeeded.Add(variable);
                        else
                            foreach (var variable in literal.VariablesOrEmpty)
                                varsSupplied.Add(variable);
                    }
                }
                varsNeeded.RemoveWhere(varsSupplied.Contains);
                if (varsNeeded.Any())
                    continue;

                var candidateSuppliersList = new List<ISet<Expression>>();
                foreach (TermVariable varNeeded in varsNeeded)
                {
                    var suppliers = new HashSet<Expression>();
                    foreach (Expression literal in rule.Antecedents.Conjuncts)
                        if (literal is Fact)
                            if (literal.VariablesOrEmpty.Contains(varNeeded))
                                suppliers.Add(literal);
                    candidateSuppliersList.Add(suppliers);
                }

                //TODO: Now... I'm not sure if we want to minimize the number of literals added, or the number of variables added
                //Right now, I don't have time to worry about optimization. Currently, we pick one at random
                //TODO: Optimize this
                var literalsToAdd = new HashSet<Expression>();
                foreach (ISet<Expression> suppliers in candidateSuppliersList)
                    if (!suppliers.Intersect(literalsToAdd).Any())
                        literalsToAdd.Add(suppliers.First());

                minSet.UnionWith(literalsToAdd);

                if (GoodCondensationSetByHeuristic(minSet, rule, model, checker, sentenceNameSource))
                    return minSet;

            }
            return null;
        }

        private static bool GoodCondensationSetByHeuristic(ICollection<Expression> minSet, Implication rule, ISentenceDomain model,
            IConstantChecker checker, UnusedSentenceNameSource sentenceNameSource)
        {
            //We actually want the sentence model here so we can see the domains
            //also, if it's a constant, ...
            //Anyway... we want to compare the heuristic for the number of assignments
            //and/or links that will be generated with or without the condensation set
            //Heuristic for a rule is A*(L+1), where A is the number of assignments and
            //L is the number of literals, unless L = 1, in which case the heuristic is
            //just A. This roughly captures the number of links that would be generated
            //if this rule were to be generated.
            //Obviously, there are differing degrees of accuracy with which we can
            //represent A.
            //One way is taking the product of all the variables in all the domains.
            //However, we can do better by actually asking the Assignments class for
            //its own heuristic of how it would implement the rule as-is.
            //The only tricky aspect here is that we need an up-to-date SentenceModel,
            //and in some cases this could be expensive to compute. Might as well try
            //it, though...

            //Heuristic for the rule as-is:

            long assignments = AssignmentsImpl.GetNumAssignmentsEstimate(rule,
                SentenceDomainModels.GetVarDomains(rule, model, SentenceDomainModels.VarDomainOpts.IncludeHead),
                checker);
            int literals = rule.Consequent.Arity;
            if (literals > 1)
                literals++;

            //We have to "and" the literals together
            //Note that even though constants will be factored out, we're concerned here
            //with getting through them in a reasonable amount of time, so we do want to
            //count them. TODO: Not sure if they should be counted in L, though...
            long curRuleHeuristic = assignments * literals;
            //And if we split them up...
            List<Implication> newRules = ApplyCondensation(minSet, rule, sentenceNameSource);
            Implication r1 = newRules[0], r2 = newRules[1];

            //Augment the model
            ISentenceDomain newModel = AugmentModelWithNewForm(model, newRules);

            long a1 = AssignmentsImpl.GetNumAssignmentsEstimate(r1,
                SentenceDomainModels.GetVarDomains(r1, newModel, SentenceDomainModels.VarDomainOpts.IncludeHead), checker);
            long a2 = AssignmentsImpl.GetNumAssignmentsEstimate(r2,
                SentenceDomainModels.GetVarDomains(r2, newModel, SentenceDomainModels.VarDomainOpts.IncludeHead), checker);
            int l1 = r1.Consequent.Arity; if (l1 > 1) l1++;
            int l2 = r2.Consequent.Arity; if (l2 > 1) l2++;

            //Whether we split or not depends on what the two heuristics say
            long newRulesHeuristic = a1 * l1 + a2 * l2;
            return newRulesHeuristic < curRuleHeuristic;
        }

        private class CondensorSentenceDomainModel : ISentenceDomain
        {
            private readonly ISentenceForm _newForm;
            private readonly ISentenceFormDomain _newFormDomain;
            private readonly ISentenceDomain _oldModel;

            public CondensorSentenceDomainModel(ISentenceForm newForm, ISentenceFormDomain newFormDomain, ISentenceDomain oldModel)
            {
                _newForm = newForm;
                _newFormDomain = newFormDomain;
                _oldModel = oldModel;
            }

            public ISentenceFormDomain GetDomain(ISentenceForm form)
            {
                return form.Equals(_newForm) ? _newFormDomain : _oldModel.GetDomain(form);
            }
        }

        private static ISentenceDomain AugmentModelWithNewForm(ISentenceDomain oldModel, List<Implication> newRules)
        {
            var newForm = new SimpleSentenceForm(newRules[0].Consequent);
            ISentenceFormDomain newFormDomain = GetNewFormDomain(newRules[0], oldModel, newForm);
            return new CondensorSentenceDomainModel(newForm, newFormDomain, oldModel);
        }

        private static ISentenceFormDomain GetNewFormDomain(Implication condensingRule, ISentenceDomain oldModel, ISentenceForm newForm)
        {
            var varDomains = SentenceDomainModels.GetVarDomains(condensingRule, oldModel, SentenceDomainModels.VarDomainOpts.BodyOnly);

            var domainsForSlots = new List<ISet<TermObject>>();
            foreach (Term term in condensingRule.Consequent.NestedTerms)
            {
                if (!(term is TermVariable))
                    throw new Exception("Expected all slots in the head of a condensing rule to be variables, but the rule was: " + condensingRule);
                domainsForSlots.Add(varDomains[(TermVariable)term]);
            }
            return new CartesianSentenceFormDomain(newForm, domainsForSlots);
        }
    }
}

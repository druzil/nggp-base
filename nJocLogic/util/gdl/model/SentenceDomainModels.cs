using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Wintellect.PowerCollections;
using nJocLogic.data;
using nJocLogic.gameContainer;

namespace nJocLogic.util.gdl.model
{
    public class SentenceDomainModels
    {
        public enum VarDomainOpts
        {
            IncludeHead,
            BodyOnly
        }

        public static IDictionary<TermVariable, ISet<TermObject>> GetVarDomains(
            Implication rule,
            ISentenceDomain domainModel,
            VarDomainOpts includeHead)
        {
            // For each positive definition of sentences in the rule, intersect their
            // domains everywhere the variables show up
            var varDomainsByVar = new MultiDictionary<TermVariable, ISet<TermObject>>(true);
            foreach (Expression literal in GetSentences(rule, includeHead))
            {
                var sentence = literal as Fact;
                if (sentence != null && sentence.RelationName != GameContainer.Parser.TokDistinct)
                {
                    var form = new SimpleSentenceForm(sentence);
                    ISentenceFormDomain formWithDomain = domainModel.GetDomain(form);

                    List<Term> tuple = sentence.NestedTerms.ToList();
                    for (int i = 0; i < tuple.Count; i++)
                    {
                        var variable = tuple[i] as TermVariable;
                        if (variable != null)
                            varDomainsByVar.Add(variable, formWithDomain.GetDomainForSlot(i));
                    }
                }
            }

            return CombineDomains(varDomainsByVar);
        }

        /// <summary>
        /// Return all the sentences in a rule (the antecedents) and include the head fact if specified
        /// </summary>
        /// <param name="rule">The rule to return the sentences from</param>
        /// <param name="includeHead">Include the consequent as well as the antecedents</param>
        /// <returns>An enumerable of all sentences in the rule</returns>
        public static IEnumerable<Expression> GetSentences(Implication rule, VarDomainOpts includeHead)
        {
            return includeHead == VarDomainOpts.IncludeHead
                       ? ImmutableList.Create(rule.Consequent).Concat(rule.Antecedents.Conjuncts)
                       : rule.Antecedents.Conjuncts;
        }

        private static IDictionary<TermVariable, ISet<TermObject>> CombineDomains(
            IEnumerable<KeyValuePair<TermVariable, ICollection<ISet<TermObject>>>> varDomainsByVar)
        {
            var result = new Dictionary<TermVariable, ISet<TermObject>>();
            foreach (var kv in varDomainsByVar)
                result[kv.Key] = IntersectSets(kv.Value);

            return result.ToImmutableDictionary();
        }

        private static ISet<T> IntersectSets<T>(ICollection<ISet<T>> input)
        {
            if (!input.Any())
                throw new Exception("Can't take an intersection of no sets");
            ISet<T> result = null;
            foreach (ISet<T> set in input)
            {
                if (result == null)
                    result = new HashSet<T>(set);
                else
                    result.IntersectWith(set);
            }
            Debug.Assert(result != null);
            return result;
        }
    }
}

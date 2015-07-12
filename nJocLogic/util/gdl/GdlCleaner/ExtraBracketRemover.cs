using System;
using System.Collections.Generic;
using System.Linq;
using nJocLogic.data;
using nJocLogic.gameContainer;

namespace nJocLogic.util.gdl.GdlCleaner
{
    using System.Reflection;

    /// <summary>
    /// Removes terms that are empty (Arity==0)
    /// </summary>
    static internal class ExtraBracketRemover
    {
        internal static List<Expression> Run(IEnumerable<Expression> description)
        {
            var newDescription = new List<Expression>();
            foreach (Expression gdl in description)
            {
                var fact = gdl as Fact;
                var rule = gdl as Implication;
                if (fact != null)
                    newDescription.Add(CleanParentheses(fact));
                else
                    newDescription.Add(rule != null ? CleanParentheses(rule) : gdl);
            }
            return newDescription;
        }

        private static Implication CleanParentheses(Implication rule)
        {
            Fact cleanedHead = CleanParentheses(rule.Consequent);
            Expression[] cleanedBody = rule.Antecedents.Conjuncts.Select(CleanParentheses).ToArray();
            return new Implication(cleanedHead, cleanedBody);
        }

        private static Expression CleanParentheses(Expression literal)
        {

            var negation = literal as Negation;
            if (negation != null)
            {
                Expression body = negation.Negated;
                return new Negation(CleanParentheses(body));
            }
            var disjunction = literal as Disjunction;
            if (disjunction != null)
            {
                Expression[] ors = disjunction.Constituents;
                return new Disjunction(ors.Select(CleanParentheses).ToArray());
            }
            var fact = literal as Fact;
            if (fact != null)
            {
                if (fact.RelationName == GameContainer.Parser.TokDistinct)
                {
                    Term term1 = CleanParentheses(fact.GetTerm(0));
                    Term term2 = CleanParentheses(fact.GetTerm(1));
                    return new VariableFact(true, fact.RelationName, term1, term2);
                }
                return CleanParentheses(fact);
            }
            throw new Exception("Unexpected literal type in GdlCleaner");
        }

        private static Fact CleanParentheses(Fact sentence)
        {
            if (sentence.Arity == 0)
                return sentence;
            Term[] cleanedBody = sentence.GetTerms().Select(CleanParentheses).ToArray();
            if (!cleanedBody.Any())
                return new GroundFact(sentence.RelationName);

            return new VariableFact(true, sentence.RelationName, cleanedBody);
        }

        private static Term CleanParentheses(Term term)
        {
            if (term is TermObject || term is TermVariable)
                return term;

            var function = term as TermFunction;
            if (function != null)
            {
                //The whole point of the function
                if (function.Arity == 0)
                    throw new NotImplementedException();
                //return function.Name;
                var cleanedBody = new List<Term>();
                for (int i = 0; i < function.Arity; i++)
                    cleanedBody.Add(CleanParentheses(function.GetTerm(i)));
                return new TermFunction(function.FunctionName, cleanedBody.ToArray());
            }
            throw new Exception(string.Format("Unexpected term type in {0}", MethodBase.GetCurrentMethod().DeclaringType));
        }
    }
}
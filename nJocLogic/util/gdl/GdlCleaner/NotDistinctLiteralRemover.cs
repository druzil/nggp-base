using System;
using System.Collections.Generic;
using nJocLogic.data;
using nJocLogic.gameContainer;

namespace nJocLogic.util.gdl.GdlCleaner
{
    static internal class NotDistinctLiteralRemover {
        /// <summary>
        /// Get rid of (not (distinct _ _)) literals in rules
        /// </summary>
        /// <param name="description">all expressions</param>
        /// <returns>all expressions except not distinct literals</returns>
        internal static List<Expression> Run(IEnumerable<Expression> description)
        {
            var newDescription = new List<Expression>();
            foreach (Expression gdl in description)
            {
                var rule = gdl as Implication;
                if (rule != null)
                {
                    Implication cleaned = RemoveNotDistinctLiteral(rule);
                    if (cleaned != null)
                        newDescription.Add(cleaned);
                }
                else
                    newDescription.Add(gdl);
            }
            return newDescription;
        }

        private static Implication RemoveNotDistinctLiteral(Implication rule)
        {
            while (rule != null && GetNotDistinctLiteral(rule) != null)
                rule = RemoveNotDistinctLiteral(rule, GetNotDistinctLiteral(rule));

            return rule;
        }

        private static Negation GetNotDistinctLiteral(Implication rule)
        {
            foreach (Expression literal in rule.Antecedents.Conjuncts)
            {
                var not = literal as Negation;
                if (not != null)
                {
                    var negated = not.Negated as Fact;
                    if (negated != null && (negated.RelationName == GameContainer.Parser.TokDistinct))
                    {
                        //For now, we can only deal with this if not both are functions.
                        //That means we have to skip that case at this point.
                        if (!(negated.GetTerm(0) is TermFunction) || !(negated.GetTerm(1) is TermFunction))
                            return not;
                    }
                }
            }
            return null;
        }

        private static Implication RemoveNotDistinctLiteral(Implication rule, Negation notDistinctLiteral)
        {
            //Figure out the substitution we want...
            //If we have two constantsin Either Remove one or
            //maybe get rid of the ___?
            //One is a variablein Replace the variable with the other thing
            //throughout the rule
            var distinct = (Fact)notDistinctLiteral.Negated;
            Term arg1 = distinct.GetTerm(0);
            Term arg2 = distinct.GetTerm(1);
            if (arg1 == arg2)
            {
                //Just Remove that literal
                var newBody = new List<Expression>();
                newBody.AddRange(rule.Antecedents.Conjuncts);
                newBody.Remove(notDistinctLiteral);
                return new Implication(rule.Consequent, newBody.ToArray());
            }
            var p1 = arg1 as TermVariable;
            if (p1 != null)
            {
                //What we return will still have the not-distinct literal,
                //but it will get replaced in the next pass.
                //(Even if we have two variables, they will be equal next time through.)
                var sub = new Substitution();
                sub.AddMapping(p1, arg2);
                return (Implication)rule.ApplySubstitution(sub);
            }
            var variable = arg2 as TermVariable;
            if (variable != null)
            {
                var sub = new Substitution();
                sub.AddMapping(variable, arg1);
                return (Implication)rule.ApplySubstitution(sub);                
            }
            if (arg1 is TermObject || arg2 is TermObject)
            {
                //We have two non-equal constants, or a constant and a function.
                //The rule should have no effect.
                return null;
            }
            //We have two functions. Complicated! (Have to replace them with unified version.)
            //We pass on this case for now.
            //TODO: Implement correctly.
            throw new Exception("We can't currently handle (not (distinct <function> <function>)).");
        }
    }
}
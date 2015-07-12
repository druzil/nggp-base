/**
 * As a GDL transformer, this class takes in a GDL description of a game,
 * transforms it in some way, and outputs a new GDL descriptions of a game
 * which is functionally equivalent to the original game.
 *
 * The AimaProver does not correctly apply "distinct" literals in rules if
 * they have not yet been bound. (See test_distinct_beginning_rule.kif for
 * an example where this comes up.) The same is true for "not" literals.
 * This transformation moves "distinct" and "not" literals later in the
 * rule, so they always appear after sentence literals have defined those
 * variables.
 *
 * This should be applied to the input to the ProverStateMachine until this
 * bug is fixed some other way.
 */

using System.Collections.Generic;
using System.Linq;
using nJocLogic.data;
using nJocLogic.gameContainer;
using nJocLogic.util.gdl;

namespace nJocLogic.reasoner.AimaProver
{
    public class DistinctAndNotMover
    {
        public static List<Expression> Run(IList<Expression> oldRules)
        {
            oldRules = DeORer.Run(oldRules);

            List<Expression> newRules = new List<Expression>(oldRules.Count);
            newRules.AddRange(from gdl in oldRules
                              let rule = gdl as Implication
                              select rule != null ? ReorderRule(rule) : gdl);
            return newRules;
        }

        private static Implication ReorderRule(Implication oldRule)
        {
            var newBody = new List<Expression>(oldRule.Antecedents.Constituents);
            RearrangeDistinctsAndNots(newBody);
            return new Implication(oldRule.Consequent, newBody.ToArray());
        }

        private static void RearrangeDistinctsAndNots(List<Expression> ruleBody)
        {
            int oldIndex = FindDistinctOrNotToMoveIndex(ruleBody);
            while (oldIndex != -1)
            {
                Expression literalToMove = ruleBody[oldIndex];
                ruleBody.RemoveAt(oldIndex);
                ReinsertLiteralInRightPlace(ruleBody, literalToMove);

                oldIndex = FindDistinctOrNotToMoveIndex(ruleBody);
            }
        }

        //Returns null if no distincts have to be moved.
        private static int FindDistinctOrNotToMoveIndex(List<Expression> ruleBody)
        {
            HashSet<TermVariable> setVars = new HashSet<TermVariable>();
            for (int i = 0; i < ruleBody.Count; i++) 
            {
                Expression literal = ruleBody[i];
                var fact = literal as Fact;

                if (fact != null && fact.RelationName != GameContainer.Parser.TokDistinct)
                    setVars.UnionWith(literal.VariablesOrEmpty);
                else if (fact != null || literal is Negation)
                    if (!AllVarsInLiteralAlreadySet(literal, setVars))
                        return i;
            }
            return -1;
        }

        private static void ReinsertLiteralInRightPlace(List<Expression> ruleBody, Expression literalToReinsert)
        {
            HashSet<TermVariable> setVars = new HashSet<TermVariable>();
            for (int i = 0; i < ruleBody.Count; i++)
            {
                Expression literal = ruleBody[i];
                if (literal is Fact)
                {
                    setVars.UnionWith(literal.VariablesOrEmpty);

                    if (AllVarsInLiteralAlreadySet(literalToReinsert, setVars))
                    {
                        ruleBody.Insert(i + 1, literalToReinsert);
                        return;
                    }
                }
            }
        }

        private static bool AllVarsInLiteralAlreadySet(Expression literal, HashSet<TermVariable> setVars)
        {
            return literal.VariablesOrEmpty.All(setVars.Contains);
        }
    }
}

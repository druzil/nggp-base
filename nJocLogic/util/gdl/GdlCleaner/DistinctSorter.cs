using System.Collections.Generic;
using System.Linq;
using nJocLogic.data;
using nJocLogic.gameContainer;

namespace nJocLogic.util.gdl.GdlCleaner
{
    /// <summary>
    /// This is for the solver to work properly - it might not affect the forward chaining method
    /// Sorts conjuncts so that a distincts appear only once all their inputs will have concrete values.
    /// We can't just put distincts at the end as recursive functions may never end without the distincts to constrain them.
    /// </summary>
    class DistinctSorter
    {
        internal static List<Expression> Run(IEnumerable<Expression> description)
        {
            var newDescription = new List<Expression>();
            foreach (Expression gdl in description)
            {
                var rule = gdl as Implication;
                if (rule != null)
                {
                    Implication cleaned = SortDistincts(rule);
                    if (cleaned != null)
                        newDescription.Add(cleaned);
                }
                else
                    newDescription.Add(gdl);
            }
            return newDescription;
        }

        private static Implication SortDistincts(Implication rule)
        {
            return new Implication(rule.Consequent, SortDistincts(rule.Antecedents.Conjuncts));
        }

        public static Expression[] SortDistincts(Expression[] expressions)
        {
            var varsSoFar = new HashSet<TermVariable>();
            var heldDistincts = new List<Fact>();
            var newAntes = new List<Expression>();

            foreach (Expression ante in expressions)
            {
                var vars = ante.VariablesOrEmpty;

                var fact = ante as Fact;
                if (fact != null && fact.RelationName == GameContainer.Parser.TokDistinct)
                {
                    if (vars.Any(v => !varsSoFar.Contains(v)))
                        heldDistincts.Add(fact);
                    else
                        newAntes.Add(fact);
                }
                else
                {
                    newAntes.Add(ante);

                    int beforeCount = varsSoFar.Count;
                    varsSoFar.UnionWith(vars);
                    if (beforeCount != varsSoFar.Count)
                    {
                        for (int i = heldDistincts.Count - 1; i >= 0; i--)
                        {
                            var current = heldDistincts[i];
                            if (current.VariablesOrEmpty.All(v => varsSoFar.Contains(v)))
                            {
                                heldDistincts.RemoveAt(i);
                                newAntes.Add(current);
                            }
                        }
                    }
                }
            }
            return newAntes.ToArray();
        }
    }
}

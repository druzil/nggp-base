using System.Collections.Generic;
using nJocLogic.data;

namespace nJocLogic.util.gdl.GdlCleaner
{
    static internal class ZeroElementBodyRemover
    {
        internal static List<Expression> Run(IEnumerable<Expression> description)
        {
            var newDescription = new List<Expression>();

            //Firstin Clean up all rules with zero-element bodies
            foreach (Expression gdl in description)
            {
                var implication = gdl as Implication;
                if (implication != null)
                {
                    if (implication.Antecedents.Constituents.Length == 0)
                        newDescription.Add(implication.Consequent);
                    else
                        newDescription.Add(implication);
                }
                else
                    newDescription.Add(gdl);
            }
            return newDescription;
        }
    }
}
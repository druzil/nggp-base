using System.Collections.Generic;
using System.Linq;
using nJocLogic.data;
using nJocLogic.gameContainer;

namespace nJocLogic.util.gdl.GdlCleaner
{
    static internal class OldBaseSentenceRemover {
        /// <summary>
        /// Get rid of the old style of "base" sentences (with arity more than 1, not in rules)
        /// See e.g. current version of Qyshinsu on the Dresden server
        /// </summary>
        /// <param name="description"></param>
        /// <returns></returns>
        internal static List<Expression> Run(List<Expression> description)
        {
            var newDescription = new List<Expression>();
            bool removeBaseSentences = description.OfType<Fact>()
                                                  .Any(
                                                      relation =>
                                                          GameContainer.SymbolTable[relation.RelationName] == "base" &&
                                                          relation.Arity != 1);

            //Note that in this case, we have to Remove ALL of them or we might
            //misinterpret this as being the new kind of "base" relation
            foreach (Expression gdl in description)
            {
                var fact = gdl as Fact;
                if (fact == null)
                    newDescription.Add(gdl);
                else if (!removeBaseSentences || GameContainer.SymbolTable[fact.RelationName] != "base")
                    newDescription.Add(fact);
            }
            return newDescription;
        }
    }
}
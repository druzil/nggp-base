using System;
using System.Collections.Generic;
using System.Linq;
using nJocLogic.data;

namespace nJocLogic.util.gdl
{
    /// <summary>
    /// As a GDL transformer, this class takes in a GDL description of a game,
    /// transforms it in some way, and outputs a new GDL descriptions of a game
    /// which is functionally equivalent to the original game.
    /// 
    /// DeORer removes OR rules from the GDL. Technically, these rules shouldn't
    /// be in the GDL in the first place, but it's very straightforward to remove
    /// them, so we do that so that we can handle GDL descriptions that use OR.
    /// 
    /// The resultant description contains just Facts and Implications
    /// </summary>
    public class DeORer
    {
        /// <summary>
        /// Implicaitons are deOr'd all other expressions are just returned as it
        /// </summary>
        /// <param name="description"></param>
        /// <returns></returns>
        public static IList<Expression> Run(IList<Expression> description)
        {
            var newDesc = new List<Expression>();
            foreach (Expression gdl in description)
            {
                var implication = gdl as Implication;
                if (implication != null)
                {
                    var rule = implication;
                    List<List<Expression>> newBodies = DeOr(rule.Antecedents.Conjuncts.ToList());
                    newDesc.AddRange(newBodies.Select(body => new Implication(rule.Consequent, body.ToArray())));
                }
                else
                    newDesc.Add(gdl);
            }
            return newDesc;
        }

        public static List<List<Expression>> DeOr(List<Expression> rhs)
        {
            var wrapped = new List<List<Expression>> { rhs };
            return DeOr2(wrapped);
        }

        private static List<List<Expression>> DeOr2(List<List<Expression>> rhsList)
        {
            while (true)
            {
                var rval = new List<List<Expression>>();
                bool expandedSomething = false;
                foreach (List<Expression> rhs in rhsList)
                {
                    int i = 0;
                    if (expandedSomething)
                        rval.Add(rhs); //If I've already expanded this function call
                    else
                    {
                        foreach (Expression lit in rhs)
                        {
                            List<Expression> expandedList = ExpandFirstOr(lit);

                            if (expandedList.Count > 1)
                            {
                                foreach (Expression replacement in expandedList)
                                {
                                    var newRhs = new List<Expression>(rhs);
                                    newRhs[i] = replacement;
                                    rval.Add(newRhs);
                                }
                                expandedSomething = true;
                                break;
                            }

                            i++;
                        }
                        if (!expandedSomething) //If I didn't find anything to expand
                            rval.Add(rhs);
                    }
                }

                if (expandedSomething)
                {
                    rhsList = rval;
                    continue;
                }
                return rhsList;
            }
        }

        /// <summary>
        /// If the expression is a Negation or Disjunction then expand it one level - otherwise just return it
        /// </summary>
        private static List<Expression> ExpandFirstOr(Expression gdl)
        {       
            var not = gdl as Negation;
            if (not != null)
            {
                Expression negated = not.Negated;
                if (negated is Disjunction)
                    throw new Exception("This should have been cleaned up by the gdl cleaner");

                List<Expression> expandedChild = ExpandFirstOr(negated);
                return expandedChild.Select(g => new Negation(g)).Cast<Expression>().ToList();
            }
            var or = gdl as Disjunction;
            if (or != null)
                return or.GetDisjuncts().ToList();

            var fact = gdl as Fact;
            if (fact != null)     //Can safely be ignored, won't contain 'or'
                return new List<Expression> { gdl };

            if (gdl is Implication)
                throw new Exception("An implication has been nested - this shouldn't occur");

            throw new Exception("An expression type hasn't been handled - possibly a conjunction?");

        }
    }
}

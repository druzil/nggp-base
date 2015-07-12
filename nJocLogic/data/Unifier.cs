using System.Collections.Generic;
using NLog;
using System;
using nJocLogic.gameContainer;
using nJocLogic.util;
using System.Linq;

namespace nJocLogic.data
{
    public static class Unifier
    {
        private static int _unificationLevel;

        private static readonly Logger Logger = LogManager.GetLogger("logic.prover.unify");

        private static void EnterUnificationLevel(Fact f1, Fact f2)
        {
            if (!Logger.IsDebugEnabled)
                return;

            Logger.Debug(string.Format("{0}Unifier: attempting to unify {1} and {2}", Util.MakeIndent(_unificationLevel), f1.ToString(GameContainer.SymbolTable), f2.ToString(GameContainer.SymbolTable)));
            _unificationLevel++;
        }

        private static void ExitUnificationLevel(Substitution subs)
        {
            if (!Logger.IsDebugEnabled) return;
            _unificationLevel--;

            if (_unificationLevel < 0)
                throw new Exception("Unification level < 0!!");

            Logger.Debug(subs == null
                             ? string.Format("{0}Unifier: failed to unify.", Util.MakeIndent(_unificationLevel))
                             : string.Format("{0}Unifier: mgu = {1}", Util.MakeIndent(_unificationLevel), subs));
        }

        /// <summary>
        /// Unification will try to find a mapping that will go from the second expression to the first.
        /// </summary>
        /// <param name="exp1">Expression to map to</param>
        /// <param name="exp2">Expression to map from</param>
        /// <returns>List of mappigs that satify. If none exist returns null</returns>
        public static Substitution Mgu(Expression exp1, Expression exp2)
        {
            if (exp1.GetType() != exp2.GetType())
                return null;

            var fact1 = exp1 as Fact;
            if (fact1 != null)
                return Mgu(fact1, (Fact) exp2);

            var dis1 = exp1 as Disjunction;
            if (dis1 != null)
                return Mgu(dis1, (Disjunction) exp2);

            var neg1 = exp1 as Negation;
            if (neg1 != null)
                return Mgu(neg1, (Negation)exp2);

            throw new Exception("Unhandled type in unifier Mgu");
        }

        private static bool Mgu(Expression exp1, Expression exp2, Substitution subsSoFar)
        {
            if (exp1.GetType() != exp2.GetType())
                return false;

            var fact1 = exp1 as Fact;
            if (fact1 != null)
                return Mgu(fact1, (Fact)exp2, subsSoFar);

            var dis1 = exp1 as Disjunction;
            if (dis1 != null)
                return Mgu(dis1, (Disjunction)exp2, subsSoFar);

            var neg1 = exp1 as Negation;
            if (neg1 != null)
                return Mgu(neg1, (Negation)exp2, subsSoFar);

            throw new Exception("Unhandled type in unifier Mgu");
        }

        private static Substitution Mgu(Disjunction dis1, Disjunction dis2)
        {
            List<Expression> contents1 = dis1.GetDisjuncts().ToList();
            List<Expression> contents2 = dis2.GetDisjuncts().ToList();
            if (contents1.Count != contents2.Count)
                return null;

            var subs = new Substitution();

            return contents1.Where((t, i) => Mgu(t, contents2[i], subs) == false).Any() ? null : subs;
        }

        private static Substitution Mgu(Negation neg1, Negation neg2)
        {
            return Mgu(neg1.Negated, neg2.Negated);
        }

        public static Substitution Mgu(Fact f1, Fact f2)
        {
            // Make sure this is even worth our time to check
            if (f1.RelationName != f2.RelationName)
                return null;
            if (f1.Arity != f2.Arity)
                return null;

            EnterUnificationLevel(f1, f2);

            var subs = new Substitution();

            if (Mgu(f1, f2, subs))
            {
                ExitUnificationLevel(subs);
                return subs;
            }
            ExitUnificationLevel(null);
            return null;
        }

        private static bool Mgu(Fact f1, Fact f2, Substitution subsSoFar)
        {
            // Make sure this is even worth our time to check
            if (f1.RelationName != f2.RelationName)
                return false;
            if (f1.Arity != f2.Arity)
                return false;

            // Find the mgu for each column of the facts
            for (int i = 0; i < f1.Arity; i++)
            {
                // If there is no mgu, just die
                if (Mgu(f1.GetTerm(i), f2.GetTerm(i), subsSoFar) == false)
                    return false;
            }

            return true;
        }

        private static bool Mgu(Term t1, Term t2, Substitution subsSoFar)
        {
            return t1.Mgu(t2, subsSoFar);
        }
    }

}

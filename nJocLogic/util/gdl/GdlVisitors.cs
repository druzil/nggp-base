

using System;
using System.Collections.Generic;
using System.Linq;
using nJocLogic.data;

namespace nJocLogic.util.gdl
{
    using gameContainer;

    /// <summary>
    /// Visits the given Expression object and any contained Expression objects within
    /// with the given GdlVisitor. For example, when called on a Implication,
    /// the visitor's visitConstant function is called once for every
    /// constant anywhere in the head or body of the rule.
    /// </summary>
    public class GdlVisitors
    {
        public static void VisitAll(Expression gdl, GdlVisitor visitor)
        {
            visitor.VisitGdl(gdl);
            var rule = gdl as Implication;
            if (rule != null)
                VisitRule(rule, visitor);
            else
                VisitLiteral(gdl, visitor);
        }

        public static void VisitAll(ICollection<Expression> collection, GdlVisitor visitor)
        {
            foreach (Expression gdl in collection)
                VisitAll(gdl, visitor);
        }
        private static void VisitRule(Implication rule, GdlVisitor visitor)
        {
            visitor.VisitRule(rule);
            VisitAll(rule.Consequent, visitor);
            VisitAll(rule.Antecedents.Constituents, visitor);
        }
        private static void VisitLiteral(Expression literal, GdlVisitor visitor)
        {
            visitor.VisitLiteral(literal);
            var fact = literal as Fact;
            if (fact != null)
            {
                if (fact.RelationName == GameContainer.Parser.TokDistinct)
                    VisitDistinct(fact, visitor);
                else
                    VisitSentence(fact, visitor);
            }
            else if (literal is Negation)
                VisitNot((Negation) literal, visitor);
            else if (literal is Disjunction)
                VisitOr((Disjunction) literal, visitor);
            else
                throw new Exception("Unexpected GdlLiteral type " + literal.GetType());
        }

        private static void VisitDistinct(Fact distinct, GdlVisitor visitor)
        {
            visitor.VisitDistinct(distinct);
            VisitTerm(distinct.GetTerm(0), visitor);
            VisitTerm(distinct.GetTerm(1), visitor);
        }

        private static void VisitOr(Disjunction or, GdlVisitor visitor)
        {
            visitor.VisitOr(or);

            var expressions = or.GetDisjuncts().ToList();
            for (int i = 0; i < expressions.Count(); i++)
                VisitAll(expressions[i], visitor);
        }

        private static void VisitNot(Negation not, GdlVisitor visitor)
        {
            visitor.VisitNot(not);
            VisitAll(not.Negated, visitor);
        }

        private static void VisitSentence(Fact sentence, GdlVisitor visitor)
        {
            visitor.VisitSentence(sentence);
            VisitRelation(sentence, visitor);
        }

        private static void VisitRelation(Fact relation, GdlVisitor visitor)
        {
            visitor.VisitRelation(relation);
            VisitTerms(relation.GetTerms(), visitor);
        }

        public static void VisitTerm(Term term, GdlVisitor visitor)
        {
            visitor.VisitTerm(term);
            var constant = term as TermObject;
            if (constant != null)
                visitor.VisitConstant(constant);
            else if (term is TermVariable)
                visitor.VisitVariable((TermVariable) term);
            else if (term is TermFunction)
                VisitFunction((TermFunction) term, visitor);
            else
                throw new Exception("Unexpected Term type " + term.GetType());
        }
        private static void VisitFunction(TermFunction function, GdlVisitor visitor)
        {
            visitor.VisitFunction(function);
            VisitTerms(function.Arguments, visitor);
        }

        internal static void VisitTerms(IEnumerable<Term> terms, GdlVisitor visitor)
        {
            foreach(var term in terms)
                VisitTerm(term, visitor);
        }

    }
}

using System.Collections.Generic;
using System.Linq;
using nJocLogic.data;
using nJocLogic.gameContainer;

namespace nJocLogic.reasoner.AimaProver
{
    public class Substituter
    {
        public static Expression Substitute(Expression literal, Substitution theta)
        {
            return SubstituteLiteral(literal, theta);
        }

        public static Fact Substitute(Fact sentence, Substitution theta)
        {
            return SubstituteSentence(sentence, theta);
        }

        public static Implication Substitute(Implication rule, Substitution theta)
        {
            return SubstituteRule(rule, theta);
        }

        private static Fact SubstituteDistinct(Fact distinct, Substitution theta)
        {
            if (distinct is GroundFact)
                return distinct;

            Term arg1 = SubstituteTerm(distinct.GetTerm(0), theta);
            Term arg2 = SubstituteTerm(distinct.GetTerm(1), theta);

            return arg1.HasVariables || arg2.HasVariables
                ? new VariableFact(false, distinct.RelationName, arg1, arg2)
                : (Fact) new GroundFact(distinct.RelationName, arg1, arg2);
        }

        private static TermFunction SubstituteFunction(TermFunction function, Substitution theta)
        {
            if (!function.HasVariables)
                return function;

            var body = new List<Term>();
            for (int i = 0; i < function.Arity; i++)
                body.Add(SubstituteTerm(function.GetTerm(i), theta));

            return new TermFunction(function.FunctionName, body.ToArray());
        }

        private static Expression SubstituteLiteral(Expression literal, Substitution theta)
        {
            var negation = literal as Negation;
            if (negation != null)
                return SubstituteNot(negation, theta);

            var disjunction = literal as Disjunction;
            if (disjunction != null)
                return SubstituteOr(disjunction, theta);

            var fact = (Fact) literal;

            return fact.RelationName == GameContainer.Parser.TokDistinct 
                ? SubstituteDistinct(fact, theta) 
                : SubstituteSentence(fact, theta);
        }

        private static Negation SubstituteNot(Negation not, Substitution theta)
        {
            return not.VariablesOrEmpty.Any() 
                ? new Negation(SubstituteLiteral(not.Negated, theta)) 
                : not;
        }

        private static Disjunction SubstituteOr(Disjunction or, Substitution theta)
        {
            return or.VariablesOrEmpty.Any() 
                ? new Disjunction(or.Constituents.Select(t => SubstituteLiteral(t, theta)).ToArray()) 
                : or;
        }        

        private static Fact SubstituteSentence(Fact sentence, Substitution theta)
	{
            if (sentence is GroundFact)
                return sentence;          

            var body = new List<Term>();
            for (int i = 0; i < sentence.Arity; i++)
                body.Add(SubstituteTerm(sentence.GetTerm(i), theta));

            var variableFact = new VariableFact(false, sentence.RelationName, body.ToArray());
            return variableFact.VariablesOrEmpty.Any()
                ? variableFact
                : (Fact) new GroundFact(sentence.RelationName, body.ToArray());
	}

        private static Term SubstituteTerm(Term term, Substitution theta)
        {
            if (term is TermObject)
                return term;

            var termVariable = term as TermVariable;
            return termVariable != null 
                ? SubstituteVariable(termVariable, theta) 
                : SubstituteFunction((TermFunction) term, theta);
        }

        private static Term SubstituteVariable(TermVariable variable, Substitution theta)
        {
            if (!theta.Contains(variable))
                return variable;

            Term result = theta[variable];
            Term betterResult;

            while (!(betterResult = SubstituteTerm(result, theta)).Equals(result))
                result = betterResult;

            theta[variable] = result;
            return result;
        }

        private static Implication SubstituteRule(Implication rule, Substitution theta)
        {
            Fact head = Substitute(rule.Consequent, theta);

            return new Implication(head, rule.Constituents.Select(literal => SubstituteLiteral(literal, theta)).ToArray());
        }
    }

}
using System.Collections.Generic;
using System.Linq;
using nJocLogic.data;
using nJocLogic.gameContainer;

namespace nJocLogic.reasoner.AimaProver
{
    public class VariableRenamer
    {
        private int _nextName;

        public VariableRenamer()
        {
            _nextName = 0;
        }

        public Implication Rename(Implication rule)
        {
            return RenameRule(rule, new Dictionary<TermVariable, TermVariable>());
        }

        public Fact Rename(Fact sentence)
        {
            return RenameSentence(sentence, new Dictionary<TermVariable, TermVariable>());
        }

        private Fact RenameDistinct(Fact distinct, Dictionary<TermVariable, TermVariable> renamings)
        {
            if (distinct is GroundFact)
                return distinct;

            Term arg1 = RenameTerm(distinct.GetTerm(0), renamings);
            Term arg2 = RenameTerm(distinct.GetTerm(1), renamings);

            return new VariableFact(false, distinct.RelationName, arg1, arg2);
        }

        private TermFunction RenameFunction(TermFunction function, Dictionary<TermVariable, TermVariable> renamings)
        {
            if (!function.HasVariables)
                return function;

            var body = new List<Term>();
            for (int i = 0; i < function.Arity; i++)
                body.Add(RenameTerm(function.GetTerm(i), renamings));

            return new TermFunction(function.FunctionName, body.ToArray());
        }

        private Expression RenameLiteral(Expression literal, Dictionary<TermVariable, TermVariable> renamings)
        {
            var negation = literal as Negation;
            if (negation != null)
                return RenameNot(negation, renamings);

            var disjunction = literal as Disjunction;
            if (disjunction != null)
                return RenameOr(disjunction, renamings);

            var fact = (Fact)literal;

            return fact.RelationName == GameContainer.Parser.TokDistinct 
                ? RenameDistinct(fact, renamings) 
                : RenameSentence(fact, renamings);
        }

        private Negation RenameNot(Negation not, Dictionary<TermVariable, TermVariable> renamings)
        {
            return not.VariablesOrEmpty.Any()
                ? new Negation(RenameLiteral(not.Negated, renamings))
                : not;
        }

        private Disjunction RenameOr(Disjunction or, Dictionary<TermVariable, TermVariable> renamings)
        {
            return or.VariablesOrEmpty.Any()
                ? new Disjunction(or.Constituents.Select(t => RenameLiteral(t, renamings)).ToArray())
                : or;
        }

        private Implication RenameRule(Implication rule, Dictionary<TermVariable, TermVariable> renamings)
        {
            if (!rule.VariablesOrEmpty.Any())
                return rule;

            Fact head = RenameSentence(rule.Consequent, renamings);

            List<Expression> body = new List<Expression>();
            for (int i = 0; i < rule.NumAntecedents(); i++)
                body.Add(RenameLiteral(rule.Constituents[i], renamings));

            return new Implication(head, body.ToArray());
        }

        private Fact RenameSentence(Fact sentence, Dictionary<TermVariable, TermVariable> renamings)
	{
            if (sentence is GroundFact)
                return sentence;

            var body = new List<Term>();
            for (int i = 0; i < sentence.Arity; i++)
                body.Add(RenameTerm(sentence.GetTerm(i), renamings));

            return new VariableFact(false, sentence.RelationName, body.ToArray());
	}

        private Term RenameTerm(Term term, Dictionary<TermVariable, TermVariable> renamings)
        {
            if (term is TermObject)
                return term;

            var termVariable = term as TermVariable;
            if (termVariable != null)
                return RenameVariable(termVariable, renamings);

            return RenameFunction((TermFunction)term, renamings);
        }

        private TermVariable RenameVariable(TermVariable variable, IDictionary<TermVariable, TermVariable> renamings)
        {
            if (!renamings.ContainsKey(variable))
                //renamings[variable] = TermVariable.MakeTermVariable();
                renamings[variable] = new TermVariable(GameContainer.SymbolTable["?R" + _nextName++]);

            return renamings[variable];
        }

    }
}
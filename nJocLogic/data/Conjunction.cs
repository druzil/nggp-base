using System;
using System.Linq;
using System.Collections.Generic;
using nJocLogic.gdl;
using System.IO;

namespace nJocLogic.data
{
    public class Conjunction : Expression
    {
        private readonly int _hashcode;

        public Conjunction() : this(false, null) { }

        public Conjunction(Expression[] sentences) : this(true, sentences) { }

        public Conjunction(bool clone, params Expression[] sentences)
        {
            if (sentences == null)
                Conjuncts = EmptySentences;
            else
            {
                Conjuncts = clone ? (Expression[]) sentences.Clone() : sentences;
                Conjuncts = UnwrapToBaseExpressions(Conjuncts);
            }

            _hashcode = 0;
            foreach (var conjunct in Conjuncts)
                _hashcode = _hashcode ^ conjunct.GetHashCode();
        }

        /// <summary>
        /// if a conjunction contains a single conjunction as an expression return its conjects rather then the single conjunction
        /// </summary>
        /// <param name="conjuncts"></param>
        /// <returns></returns>
        static Expression[] UnwrapToBaseExpressions(Expression[] conjuncts)
        {
            if (conjuncts.Length != 1)
                return conjuncts;

            var conjunction = conjuncts[0] as Conjunction;
            return conjunction != null ? UnwrapToBaseExpressions(conjunction.Conjuncts) : conjuncts;
        }

        public int NumConjuncts()
        {
            return Conjuncts.Length;
        }

        private Expression GetConjunct(int whichOne)
        {
            return Conjuncts[whichOne];
        }

        public readonly Expression[] Conjuncts;

        public override bool HasTermFunction(int functionName)
        {
            return Conjuncts.Any(s => s.HasTermFunction(functionName));
        }

        public override bool HasTermVariable(int varName)
        {
            return Conjuncts.Any(s => s.HasTermVariable(varName));
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;

            var conjunct = obj as Conjunction;

            return conjunct != null && Conjuncts.SequenceEqual(conjunct.Conjuncts);
        }

        public override int GetHashCode()
        {
            return _hashcode;
        }

        public override bool CanMapVariables(Expression other)
        {
            var sl = other as Conjunction;

            if (sl == null)
                return false;

            if (NumConjuncts() != sl.NumConjuncts())
                return false;

            for (int i = 0; i < NumConjuncts(); i++)
                if (GetConjunct(i).CanMapVariables(sl.GetConjunct(i)) == false)
                    return false;

            return true;
        }

        public override Expression ApplySubstitution(Substitution sigma)
        {
            var newSentences = new Expression[Conjuncts.Length];

            for (int i = 0; i < Conjuncts.Length; i++)
                newSentences[i] = Conjuncts[i].ApplySubstitution(sigma);

            // TODO: remove duplicates if any were created during application of substitution        
            return new Conjunction(false, newSentences);
        }

        public override void PrintToStream(StreamWriter target, SymbolTable symtab)
        {
            if (Conjuncts.Length == 0)
                return;

            target.Write("Rule body: ");
            int i;
            for (i = 0; i < Conjuncts.Length - 1; i++)
            {
                Conjuncts[i].PrintToStream(target, symtab);
                target.Write(" & ");
            }
            Conjuncts[i].PrintToStream(target, symtab);
        }

        public override Expression Uniquefy(Dictionary<TermVariable, TermVariable> varMap)
        {
            var newSentences = new Expression[Conjuncts.Length];

            for (int i = 0; i < Conjuncts.Length; i++)
                newSentences[i] = Conjuncts[i].Uniquefy(varMap);

            return new Conjunction(false, newSentences);
        }

        public override Expression[] Constituents { get { return Conjuncts; } }

        public override bool IsEquivalent(Expression target)
        {
            if (!(target is Conjunction))
                return false;

            foreach (Expression s in Constituents)
            {
                bool matched = target.Constituents.Any(t => t.IsEquivalent(s));
                if (!matched)
                    return false;
            }
            return true;
        }

        public override string Output()
        {
            if (Conjuncts.Length == 0)
                return string.Empty;

            var result = string.Empty;

            for (int i = 0; i < Conjuncts.Length; i++)
            {
                if (i != 0)
                    result += Environment.NewLine;
                result += Conjuncts[i].Output();
            }
            return result;
        }
    }
}

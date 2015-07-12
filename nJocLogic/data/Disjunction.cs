using System;
using System.Linq;
using System.IO;
using nJocLogic.gdl;
using System.Collections.Generic;

namespace nJocLogic.data
{
    public class Disjunction : Expression
    {
        readonly private Expression[] _sentences;
        private readonly int _hashcode;
 
        public Disjunction(Expression[] sentences) : this(true, sentences) { }

        public Disjunction(bool clone, Expression[] sentences)
        {
            if (sentences == null)
                _sentences = EmptySentences;
            else
            {
                if (clone)
                    _sentences = (Expression[])sentences.Clone();
                else
                    _sentences = sentences;
            }

            _hashcode = 0;
            foreach (var conjunct in _sentences)
                _hashcode = _hashcode ^ conjunct.GetHashCode();
        }

        public override int GetHashCode()
        {
            return _hashcode;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;

            var disjunct = obj as Disjunction;

            return disjunct != null && _sentences.SequenceEqual(disjunct._sentences);
        }

        public override Expression ApplySubstitution(Substitution sigma)
        {
            var newSentences = new Expression[_sentences.Length];

            for (int i = 0; i < _sentences.Length; i++)
                newSentences[i] = _sentences[i].ApplySubstitution(sigma);

            return new Disjunction(newSentences);
        }

        private Expression GetDisjunct(int whichOne)
        {
            return _sentences[whichOne];
        }

        private int NumDisjuncts()
        {
            return _sentences.Length;
        }

        public IEnumerable<Expression> GetDisjuncts()
        {
            return _sentences;
        }

        public override bool HasTermFunction(int functionName)
        {
            return _sentences.Any(s => s.HasTermFunction(functionName));
        }

        public override bool HasTermVariable(int varName)
        {
            return _sentences.Any(s => s.HasTermVariable(varName));
        }

        public override bool CanMapVariables(Expression other)
        {
            var os = other as Disjunction;

            if (os == null)
                return false;

            if (NumDisjuncts() != os.NumDisjuncts())
                return false;

            for (int i = 0; i < NumDisjuncts(); i++)
                if (GetDisjunct(i).CanMapVariables(os.GetDisjunct(i)) == false)
                    return false;

            return true;
        }

        public override void PrintToStream(StreamWriter target, SymbolTable symtab)
        {
            target.Write("(or ");

            int i;
            for (i = 0; i < _sentences.Length - 1; i++)
            {
                _sentences[i].PrintToStream(target, symtab);
                target.Write(' ');
            }
            _sentences[i].PrintToStream(target, symtab);

            target.Write(')');
        }

        public override Expression Uniquefy(Dictionary<TermVariable, TermVariable> varMap)
        {
            var newSentences = new Expression[_sentences.Length];

            for (int i = 0; i < newSentences.Length; i++)
                newSentences[i] = _sentences[i].Uniquefy(varMap);

            return new Disjunction(false, newSentences);

        }

        public override Expression[] Constituents { get { return _sentences; } }

        public override bool IsEquivalent(Expression target)
        {
            if (!(target is Disjunction))
                return false;

            foreach (var s in Constituents)
            {
                bool matched = target.Constituents.Any(t => t.IsEquivalent(s));
                if (!matched)
                    return false;
            }
            return true;
        }

        public override string Output()
        {
            string result = "(or ";

            for (int i = 0; i < _sentences.Length; i++)
            {
                if (i != 0)
                    result += Environment.NewLine;
                result += _sentences[i].Output();
            }
            result += ')';
            return result;
        }
    }
}


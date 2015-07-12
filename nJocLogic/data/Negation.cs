using nJocLogic.gdl;
using System.Collections.Generic;
using System.IO;

namespace nJocLogic.data
{
    public class Negation : Expression
    {
        public Negation(Expression negated)
        {
            Negated = negated;
        }

        public override int GetHashCode()
        {
            return Negated.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;

            var no = obj as Negation;
            return no != null && Negated.Equals(no.Negated);
        }

        public override Expression ApplySubstitution(Substitution sigma)
        {
            return new Negation(Negated.ApplySubstitution(sigma));
        }

        public Expression Negated { get; private set; }

        public override bool HasTermFunction(int functionName)
        {
            return Negated.HasTermFunction(functionName);
        }

        public override bool HasTermVariable(int varName)
        {
            return Negated.HasTermVariable(varName);
        }

        public override bool CanMapVariables(Expression other)
        {
            if (other is Negation == false)
                return false;

            return Negated.CanMapVariables(((Negation)other).Negated);
        }

        public override void PrintToStream(StreamWriter target, SymbolTable symtab)
        {
            target.Write("(not ");
            Negated.PrintToStream(target, symtab);
            target.Write(")");
        }

        public override Expression Uniquefy(Dictionary<TermVariable, TermVariable> varMap)
        {
            return new Negation(Negated.Uniquefy(varMap));
        }

        public override Expression[] Constituents { get { return new[] { Negated }; } }

        public override bool IsEquivalent(Expression target)
        {
            return Equals(target);
        }

        public override string Output()
        {
            return string.Format("(not {0})", Negated.Output());
        }
    }
}

using System.Collections.Generic;
using nJocLogic.gdl;
using System;

namespace nJocLogic.data
{
    public class TermObject : Term
    {
        readonly public int Token;

        private TermObject(int token)
        {
            Token = token;
        }

        private static readonly Dictionary<int, TermObject> ObjMemoMap = new Dictionary<int, TermObject>();

        public static TermObject MakeTermObject(int token)
        {
            TermObject term;
            if (!ObjMemoMap.TryGetValue(token, out term))
            {
                term = new TermObject(token);
                ObjMemoMap[token] = term;                
            }

            return term;
        }

        public override Term Clone()
        {
            return this; // nothing to do for term objects!
        }

        public int GetToken()
        {
            return Token;
        }

        public override int GetHashCode()
        {
            return Token;
        }

        public override bool Equals(object obj)
        {
            var termObject = obj as TermObject;

            if (termObject == null)
                return false;

            return Token == termObject.Token;
        }

        public override int TotalColumns
        {
            get { return 1; }
            protected set { }
        }

        protected override int CompareTo(TermFunction t)
        {
            // Obj < Func < Var
            return -1;
        }

        protected override int CompareTo(TermVariable t)
        {
            // Obj < Func < Var
            return -1;
        }

        protected override int CompareTo(TermObject t)
        {
            return Math.Sign(Token - t.Token);
        }

        public override string ToString(SymbolTable symtab)
        {
            return symtab[Token];
        }

        public override Term ApplySubstitution(Substitution sigma)
        {
            // Nothing to do here -- no variables in object constants.
            return this;
        }

        public override bool HasVariables
        {
            get
            {
                // term-objects never have variables (true by definition -- they are object constants)
                return false;
            }
        }

        public override bool HasTermFunction(int functionName)
        {
            // false by definition
            return false;
        }

        public override bool HasVariable(int varName)
        {
            // false by definition
            return false;
        }

        public override Term Uniquefy(Dictionary<TermVariable, TermVariable> newVarMap)
        {
            // Nothing to do, by definition
            return this;
        }

        public override bool CanMapVariables(Term other, Dictionary<TermVariable, TermVariable> varMap)
        {
            return Equals(other);
        }

        public override bool Mgu(Term t, Substitution subsSoFar)
        {
            if (t is TermObject)
                return Equals(t);

            var variable = t as TermVariable;
            if (variable != null)   // Reverse the problem; the TermVariable class handles this 
                return variable.Mgu(this, subsSoFar);

            if (t is TermFunction)   // Can't unify a function and a constant.
                return false;

            throw new ApplicationException("TermObject.mgu: Don't know how to handle term of type " + t.GetType().Name);
        }
    }
}

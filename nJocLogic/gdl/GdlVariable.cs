using System.Collections.Generic;

namespace nJocLogic.gdl
{
    public class GdlVariable : GdlExpression
    {
        static private Dictionary<int, GdlVariable> varMap = new Dictionary<int, GdlVariable>();

        readonly private int token_;

        private GdlVariable(SymbolTable symTab, int token)
            : base(symTab)
        {
            token_ = token;
        }

        static public GdlVariable GetGdlVariable(SymbolTable symTab, int token)
        {
            if (varMap.ContainsKey(token))
                return varMap[token];

            GdlVariable var = new GdlVariable(symTab, token);
            varMap[token] = var;
            return var;
        }

        public override string ToString()
        {
            return "?" + symbolTable_[token_];
        }

        public int GetToken()
        {
            return token_;
        }

        public override bool Equals(object obj)
        {
            return this == obj;
        }

        public override int GetHashCode()
        {
            return GetToken();
        }
    }

}

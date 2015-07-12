
namespace nJocLogic.gdl
{
    public class GdlAtom : GdlExpression
    {
        readonly int _token;

        public GdlAtom(SymbolTable symTab, int token)
            : base(symTab)
        {
            _token = token;
        }

        public int GetToken()
        {
            return _token;
        }

        public override string ToString()
        {
            return symbolTable_[_token];
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;

            if (obj.GetType() == typeof(GdlAtom))
            {
                var rhs = (GdlAtom)obj;
                return rhs._token == _token;
            }
            var objString = obj as string;
            if (objString != null)
                return Equals(objString);

            if (obj is int)
                return _token == ((int)obj);

            return false;
        }

        public bool Equals(string str)
        {
            return str != null && str.ToLower().Equals(symbolTable_[_token]);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

    }
}

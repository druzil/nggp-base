using System.Collections.Generic;
using System;
namespace nJocLogic.gdl
{
    public class SymbolTable
    {
        private readonly Dictionary<string, int> _idToToken;
        private readonly Dictionary<int, string> _tokenToId;

        private int _nextTokenNum;

        public SymbolTable()
        {
            _idToToken = new Dictionary<string, int>();
            _tokenToId = new Dictionary<int, string>();
            _nextTokenNum = 256;
        }

        public void Clear()
        {
            _idToToken.Clear();
            _tokenToId.Clear();
        }

        public bool ContainsKey(object key)
        {
            var s = key as string;
            if (s != null)
                return _idToToken.ContainsKey(s);
            if (key is int)
                return _tokenToId.ContainsKey((int)key);

            throw new InvalidCastException("Symbol table cannot contain keys of type " + key.GetType().Name);
        }

        public bool ContainsValue(object value)
        {
            var s = value as string;
            if (s != null)
                return _tokenToId.ContainsValue(s);
            if (value is int)
                return _idToToken.ContainsValue((int)value);

            throw new InvalidCastException("Symbol table cannot contain values of type " + value.GetType().Name);
        }

        public string this[int token]
        {
            get
            {
                return _tokenToId.ContainsKey(token) ? _tokenToId[token] : null;
            }
        }

        public int this[string identifier]
        {
            get
            {
                if (_idToToken.ContainsKey(identifier))
                    return _idToToken[identifier];

                _idToToken[identifier] = _nextTokenNum;
                _tokenToId[_nextTokenNum] = identifier;

                int token = _nextTokenNum;
                _nextTokenNum++;

                return token;
            }
        }

        public bool IsEmpty()
        {
            return _idToToken.Count==0;
        }

        public int Size
        {
            get { return _idToToken.Count; }
        }

        public int GetHighestToken()
        {
            return _nextTokenNum;
        }

    }

}

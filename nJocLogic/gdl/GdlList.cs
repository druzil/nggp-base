using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;

namespace nJocLogic.gdl
{
    public sealed class GdlList : GdlExpression, IEnumerable<GdlExpression>
    {
        readonly private GdlExpression[] _elements;

        readonly private bool _atomList;

        public GdlList(SymbolTable symtab, GdlExpression[] elements)
            : base(symtab)
        {
            _elements = (GdlExpression[])elements.Clone();     //TODO does this require a deep clone

            _atomList = _elements.All(exp => exp is GdlAtom);
        }

        public static GdlList BuildFromWords(SymbolTable symbolTable, params string[] args)
        {
            var atoms = new GdlExpression[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                string word = args[i];

                if (word.StartsWith("?"))
                {
                    word = word.Substring(1);
                    int token = symbolTable[word];
                    atoms[i] = GdlVariable.GetGdlVariable(symbolTable, token);
                }
                else
                {
                    int token = symbolTable[word];
                    atoms[i] = new GdlAtom(symbolTable, token);
                }
            }
            return new GdlList(symbolTable, atoms);
        }

        public bool IsAtomList()
        {
            return _atomList;
        }

        public int Size
        {
            get { return _elements.Length; }
        }

        public int Arity
        {
            get { return Size - 1; }
        }

        public GdlExpression this[int elem]
        {
            get { return _elements[elem]; }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("(");

            foreach (GdlExpression exp in _elements)
            {
                sb.Append(exp);
                sb.Append(" ");
            }

            if (_elements.Any())
                sb.Remove(sb.Length - 1, 1);

            sb.Append(")");
            return sb.ToString();
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;

            var gdlObj = obj as GdlList;
            return gdlObj != null && _elements.SequenceEqual(gdlObj._elements);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }


        public IEnumerator<GdlExpression> GetEnumerator()
        {
            foreach (var e in _elements)
                yield return e;

            //return (IEnumerator<GdlExpression>)elements_.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _elements.GetEnumerator();
        }
    }
}
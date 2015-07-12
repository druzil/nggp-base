using System;
using nJocLogic.gameContainer;
using nJocLogic.gdl;
using System.Collections.Generic;

namespace nJocLogic.data
{
    public abstract class Term : IComparable<Term>
    {
        public override string ToString()
        {
            return ToString(GameContainer.SymbolTable);
        }

        public abstract string ToString(SymbolTable symtab);

        protected abstract int CompareTo(TermObject t);
        protected abstract int CompareTo(TermFunction t);
        protected abstract int CompareTo(TermVariable t);

        public int CompareTo(Term t)
        {
            var o = t as TermObject;
            if (o != null)
                return CompareTo(o);
            var function = t as TermFunction;
            return function != null ? CompareTo(function) : CompareTo((TermVariable)t);
        }

        public abstract Term Clone();

        public abstract Term ApplySubstitution(Substitution sigma);

        /// <summary>
        /// Get the total number of columns needed to represent this term.
        /// Objects and variables only need one column: the name of the term. Term functions
        /// need one column for their name, and then the sum of the needed columns for each             
        /// of their arguments.             
        /// </summary>
        /// <value>The total number of columns needed to represent this term.</value>
        public abstract int TotalColumns { get; protected set; }

        public abstract bool HasVariables { get; }

        public abstract bool HasTermFunction(int functionName);
        public abstract bool HasVariable(int varName);

        public abstract bool CanMapVariables(Term other, Dictionary<TermVariable, TermVariable> varMap);

        public abstract Term Uniquefy(Dictionary<TermVariable, TermVariable> newVarMap);

        public abstract bool Mgu(Term t, Substitution subsSoFar);

        public static Term BuildFromGdl(GdlExpression expression)
        {
            return BuildFromGdl(expression, new Dictionary<GdlVariable, TermVariable>());
        }

        public static Term BuildFromGdl(GdlExpression expression, Dictionary<GdlVariable, TermVariable> varMap)
        {
            var atom = expression as GdlAtom;
            if (atom != null)
                return TermObject.MakeTermObject(atom.GetToken());
            var gdlList = expression as GdlList;
            if (gdlList != null)
            {
                GdlList list = gdlList;

                // Grab the function name
                int name = ((GdlAtom)list[0]).GetToken();

                // Convert each term
                var terms = new Term[list.Arity];

                for (int i = 0; i < list.Arity; i++)
                {
                    GdlExpression elem = list[i + 1];
                    var elemVariable = elem as GdlVariable;

                    if (elemVariable == null)
                        terms[i] = BuildFromGdl(elem, varMap);
                    else
                        terms[i] = new TermVariable(elemVariable.GetToken());
                }

                return new TermFunction(name, terms);
            }
            throw new Exception("Term.buildFromGdl: cannot handle GDL of type " + expression.GetType().Name);
        }
    }
}


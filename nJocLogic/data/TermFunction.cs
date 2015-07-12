using nJocLogic.gdl;
using System.Text;
using System.Collections.Generic;
using System;
using System.Linq;

namespace nJocLogic.data
{
    public class TermFunction : Term
    {
        readonly public int FunctionName;
        readonly protected internal Term[] Arguments; // these arguments are either variables or objects

        readonly private static Term[] EmptyArgs = new Term[0];
        private readonly bool _hasVariables;

        public TermFunction(int funcName, Term[] args) : this(true, funcName, args) { }

        public TermFunction(bool cloneCols, int funcName, Term[] args)
        {
            FunctionName = funcName;

            if (args == null)
            {
                Arguments = EmptyArgs;
                _hasVariables = false;
            }
            else
            {
                // Check if any of the term arguments are variables
                _hasVariables = args.Any(t => t.HasVariables);

                if (cloneCols)
                    Arguments = (Term[])args.Clone();
                else
                    Arguments = args;
            }

            // Compute the total columns needed
            TotalColumns = 1 + Arguments.Sum(t => t.TotalColumns); // 1 for the name
        }

        public int Arity
        {
            get { return Arguments.Length; }
        }

        public Term GetTerm(int whichOne)
        {
            return Arguments[whichOne];
        }

        public override sealed int TotalColumns { get; protected set; }

        public override Term Clone()
        {
            var args = new Term[Arguments.Length];

            for (int i = 0; i < Arguments.Length; i++)
                args[i] = Arguments[i].Clone();

            return new TermFunction(false, FunctionName, args);
        }

        public override Term ApplySubstitution(Substitution sigma)
        {
            var args = new Term[Arguments.Length];

            for (int i = 0; i < Arguments.Length; i++)
                args[i] = Arguments[i].ApplySubstitution(sigma);

            return new TermFunction(false, FunctionName, args);
        }

        protected override int CompareTo(TermFunction t)
        {
            int comp = FunctionName - t.FunctionName;

            if (comp != 0)
                return Math.Sign(comp);

            // At this point, function names are equal

            comp = Arguments.Length - t.Arguments.Length;

            if (comp != 0)
                return Math.Sign(comp);

            // At this point, both term-functions have same number of arguments

            for (int i = 0; i < Arguments.Length; i++)
            {
                comp = Arguments[i].CompareTo(t.Arguments[i]);

                if (comp != 0)
                    return comp;
            }

            return 0;
        }

        public override bool Equals(object obj)
        {
            var func = obj as TermFunction;

            if (func == null)
                return false;

            return FunctionName == func.FunctionName && Arguments.Length == func.Arguments.Length && Arguments.SequenceEqual(func.Arguments);
        }

        public override int GetHashCode()
        {
            return Arguments.Aggregate(FunctionName, (current, arg) => (current << 1) ^ arg.GetHashCode());
        }

        protected override int CompareTo(TermObject t)
        {
            // Obj < Func < Var
            return 1;
        }

        protected override int CompareTo(TermVariable t)
        {
            // Obj < Func < Var
            return -1;
        }

        public override bool CanMapVariables(Term other, Dictionary<TermVariable, TermVariable> varMap)
        {
            if (other is TermFunction == false)
                return false;

            var tf = (TermFunction)other;

            if (FunctionName != tf.FunctionName || Arity != tf.Arity)
                return false;

            for (int i = 0; i < Arity; i++)
            {
                if (GetTerm(i).CanMapVariables(tf.GetTerm(i), varMap) == false)
                    return false;
            }

            return true;
        }

        public override string ToString(SymbolTable symtab)
        {
            var sb = new StringBuilder();

            sb.Append('(');
            sb.Append(symtab[FunctionName]);

            if (Arguments.Length > 0)
                sb.Append(' ');

            // Print all but the last argument
            int i;
            for (i = 0; i < Arguments.Length - 1; i++)
            {
                sb.Append(Arguments[i].ToString(symtab));
                sb.Append(' ');
            }

            // Print the last argument
            sb.Append(Arguments[i].ToString(symtab));

            sb.Append(')');

            return sb.ToString();
        }

        public override bool HasVariables { get { return _hasVariables; } }

        public override bool HasTermFunction(int functionName)
        {
            return FunctionName == functionName || Arguments.Any(t => t.HasTermFunction(functionName));
        }

        public override bool HasVariable(int varName)
        {
            return Arguments.Any(t => t.HasVariable(varName));
        }

        public override Term Uniquefy(Dictionary<TermVariable, TermVariable> newVarMap)
        {
            var newArgs = new Term[Arguments.Length];

            for (int i = 0; i < Arguments.Length; i++)
                newArgs[i] = Arguments[i].Uniquefy(newVarMap);

            return new TermFunction(false, FunctionName, newArgs);
        }

        public override bool Mgu(Term t, Substitution subsSoFar)
        {
            if (t is TermObject)// Cannot map functions to constants.                
                return false;

            var variable = t as TermVariable;
            if (variable != null)// Reverse the problem; the TermVariable class handles this                 
                return variable.Mgu(this, subsSoFar);

            var function = t as TermFunction;
            if (function != null)
            {
                TermFunction f = function;

                // Make sure that our function names are equal
                if (FunctionName != f.FunctionName)
                    return false;

                // Make sure arities are the same
                if (Arity != f.Arity)
                    return false;

                // Finally, make sure we can get the mgu of all arguments
                for (int i = 0; i < Arity; i++)
                    if (Arguments[i].Mgu(f.Arguments[i], subsSoFar) == false)
                        return false;

                // All good!
                return true;
            }
            throw new Exception("TermFunction.mgu: Don't know how to handle term of type " + t.GetType().Name);
        }

        /**
         * Checks whether the function contains references to the input variable.
         * Used for unification of a variable and a function ('occurs' check).
         * 
         * @param variable The variable whose presence to check for.
         * @return True if <tt>variable</tt> appears anywhere within the function's arguments.
         */
        public bool HasVariable(TermVariable variable)
        {
            for (int i = 0; i < Arity; i++)
            {
                var termVariable = Arguments[i] as TermVariable;
                if (termVariable != null)
                {
                    if (termVariable.Equals(variable))
                        return true;
                }
                else
                {
                    var function = Arguments[i] as TermFunction;
                    if (function != null && function.HasVariable(variable))
                        return true;
                }
            }
            return false;
        }

        public TermFunction CloneWithEmptyTerms()
        {
            var terms = new Term[Arity];
            for (int i = 0; i < Arity; i++)
            {
                var termFunction = terms[i] as TermFunction;
                if (termFunction != null)
                    terms[i] = termFunction.CloneWithEmptyTerms();
                else
                    terms[i] = TermVariable.MakeTermVariable();
            }
            return new TermFunction(FunctionName, terms);
        }
    }
}

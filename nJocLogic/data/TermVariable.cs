using System;
using nJocLogic.gdl;
using System.Collections.Generic;

namespace nJocLogic.data
{
    public class TermVariable : Term
    {
        private static int _nextUnique = 290;

        private readonly int _varName;

        public TermVariable(int varName)
        {
            _varName = varName;
        }

        public static void SetUniqueStart(int start)
        {
            _nextUnique = start;
        }

        public static TermVariable MakeTermVariable()
        {
            return new TermVariable(_nextUnique++);
        }

        public override Term ApplySubstitution(Substitution sigma)
        {
            // Does sigma apply to our variable?
            Term replacement = sigma.GetMapping(this);

            if (replacement == null)
                return this; // nothing to change!

            // Otherwise, return the variable's replacement
            return replacement;
        }

        public override int TotalColumns { get { return 1; } protected set { } }

        public override Term Clone()
        {
            return new TermVariable(_varName);
        }

        public override string ToString(SymbolTable symtab)
        {
            return "?var" + _varName;
        }

        protected override int CompareTo(TermObject t)
        {
            // Obj < Func < Var
            return 1;
        }

        protected override int CompareTo(TermFunction t)
        {
            // Obj < Func < Var
            return 1;
        }

        protected override int CompareTo(TermVariable t)
        {
            return Math.Sign(_varName - t._varName);
        }

        public override bool HasVariables { get { return true; } }

        public override bool HasTermFunction(int functionName)
        {
            return false;
        }

        public override bool HasVariable(int varName)
        {
            return _varName == varName;
        }

        public int Name
        {
            get { return _varName; }
        }

        public override bool Equals(object obj)
        {
            var tvObj = obj as TermVariable;

            if (tvObj == null)
                return false;

            return _varName == tvObj._varName;
        }

        public override int GetHashCode()
        {
            return _varName;
        }

        public override bool CanMapVariables(Term other, Dictionary<TermVariable, TermVariable> varMap)
        {
            var tvOther = other as TermVariable;

            if (tvOther == null)
                return false;

            TermVariable mapped;
            // Both are variables, so either the first has mapped to no variable, or first must map to second            
            if (varMap.TryGetValue(this, out mapped))
                return mapped.Equals(other);

            varMap[this] = tvOther;
            return true;
        }

        public override Term Uniquefy(Dictionary<TermVariable, TermVariable> newVarMap)
        {
            TermVariable newVar;
            if (!newVarMap.TryGetValue(this, out newVar))
            {
                newVar = MakeTermVariable();
                newVarMap[this] = newVar;
            }

            return newVar;
        }

        public override bool Mgu(Term t, Substitution subsSoFar)
        {
            if (t is TermObject)
            {
                Term replacement = subsSoFar.GetMapping(this);

                if (replacement != null)
                {
                    TermVariable termVariable = replacement as TermVariable;
                    if (termVariable != null)
                    {
                        subsSoFar.AddMapping(this, t);
                        subsSoFar.AddMapping(termVariable, t);
                        return true;
                    }
                    return replacement.Equals(t);
                }

                // There was no replacement:
                // Add a mapping for the variable to this term-object
                subsSoFar.AddMapping(this, t);
                return true;

            }
            var variable = t as TermVariable;
            if (variable != null)
            {
                TermVariable it = variable;

                Term myReplacement = subsSoFar.GetMapping(this);
                Term itsReplacement = subsSoFar.GetMapping(it);

                if (itsReplacement == null)
                {
                    // just map 'it' to me (or my replacement)
                    if (myReplacement == null)
                    {
                        if (!Equals(it))
                            subsSoFar.AddMapping(it, this);
                    }
                    else
                    {
                        if (!(myReplacement is TermVariable) || !myReplacement.Equals(it))
                            subsSoFar.AddMapping(it, myReplacement);
                    }

                    return true;
                }

                // At this point, 'it' has a replacement.
                if (myReplacement == null)
                {
                    // I don't have a replacement, so map me to it, or to its replacement
                    if (!(itsReplacement is TermVariable) || !itsReplacement.Equals(this))
                        subsSoFar.AddMapping(this, itsReplacement);

                    return true;
                }

                // At this point, both term variables have replacements.
                // So make sure that they are the same!
                return myReplacement.Equals(itsReplacement);
            }
            var func = t as TermFunction;
            if (func != null)
            {
                Term myReplacement = subsSoFar.GetMapping(this);

                // Case 1: I have a replacement
                if (myReplacement != null)
                    // See if my replacement can be unified with the function
                    return myReplacement.Mgu(func, subsSoFar);

                    // Case 2: I have no replacement
                TermFunction itsReplacement = subsSoFar.GetMapping(func);

                if (itsReplacement.HasVariable(this))
                    return false;

                // just set my replacement to the function
                subsSoFar.AddMapping(this, itsReplacement);
                return true;
            }
            throw new Exception("TermVariable.mgu: Don't know how to handle term of type " + t.GetType().Name);
        }
    }
}

using System.Collections.Generic;
using System.IO;
using nJocLogic.gameContainer;
using nJocLogic.gdl;

namespace nJocLogic.data
{
    using System.Linq;

    public abstract class Expression
    {
        protected static readonly Expression[] EmptySentences = new Expression[0];
        protected static readonly Term[] EmptyTerms = new Term[0];

        public abstract string Output();

        public override string ToString()
        {
            return ToString(GameContainer.SymbolTable);
        }

        public string ToString(SymbolTable symtab)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream))
                {
                    PrintToStream(writer, symtab);
                    writer.Flush();
                    stream.Seek(0, SeekOrigin.Begin);
                    return (new StreamReader(stream)).ReadToEnd();
                }
            }
        }

        public abstract Expression ApplySubstitution(Substitution sigma);

        public virtual Expression Uniquefy()
        {
            return Uniquefy(new Dictionary<TermVariable, TermVariable>());
        }

        public abstract Expression Uniquefy(Dictionary<TermVariable, TermVariable> varMap);

        public abstract void PrintToStream(StreamWriter target, SymbolTable symtab);

        /// <summary>
        /// Return true if this expression contains a functional term with name <paramref name="functionName"/>. 
        /// </summary>
        /// <param name="functionName">The name of the term function to check for.</param>
        /// <returns>True if this expression contains the term function.</returns>
        public abstract bool HasTermFunction(int functionName);

        /// <summary>
        /// Return true if this expression contains a given variable. 
        /// </summary>
        /// <param name="varName">The variable to search for</param>
        /// <returns>True if this expression contains the variable.</returns>
        public abstract bool HasTermVariable(int varName);

        /// <summary>
        /// Can this expression be mapped to the other expression? An expression can be mapped to another expression 
        /// if and only if there is a unifier from the one to the other that only makes variable assignments.
        /// 
        /// <para/>
        /// Note that this relationship is <b>not</b> symmetric: one expression can be mapped to another expression, 
        /// despite that second relation not mapping to the first.
        /// </summary>
        /// <param name="other">The expression with which to test mapping.</param>
        /// <returns>True if <b>this</b> can be mapped to <paramref name="other"/>.</returns>
        public abstract bool CanMapVariables(Expression other);

        public abstract Expression[] Constituents { get; }

        public virtual IEnumerable<TermVariable> Variables
        {
            get
            {
                var vars = new HashSet<TermVariable>();
                foreach (Expression exp in Constituents)
                    foreach (TermVariable var in exp.VariablesOrEmpty)
                        vars.Add(var);

                return vars;
            }
        }

        public IEnumerable<TermVariable> VariablesOrEmpty { get { return Variables ?? new List<TermVariable>(); } }

        /// <summary>
        /// Finds if all the expressions variables exist in the passed in list
        /// </summary>
        /// <param name="variableList">A list of target variables</param>
        /// <returns>True if all variables in the expression are contained in the passed in list</returns>
        public bool AreAllVariablesContainIn(IEnumerable<TermVariable> variableList)
        {
            return !VariablesNotContainedIn(variableList).Any();
        }

        public IEnumerable<TermVariable> VariablesNotContainedIn(IEnumerable<TermVariable> variableList)
        {
            return VariablesOrEmpty.Except(variableList);
        }

        public virtual IEnumerable<TermObject> TermObjects
        {
            get
            {
                var objects = new HashSet<TermObject>();
                foreach (var exp in Constituents)
                    foreach (var obj in exp.TermObjectsOrEmpty)
                        objects.Add(obj);

                return objects;
            }
        }

        public IEnumerable<TermObject> TermObjectsOrEmpty { get { return TermObjects ?? new List<TermObject>(); } }

        /// <summary>
        /// Same as Equal but doesn't matter about the order for Conjunctions and Disjunctions
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public abstract bool IsEquivalent(Expression target);
    }

}

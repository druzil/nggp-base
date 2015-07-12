using System;
using NLog;
using System.Collections.Generic;
using System.IO;
using nJocLogic.gdl;
using System.Linq;

namespace nJocLogic.data
{
    public class Implication : Expression
    {
        private readonly Fact _consequent;
        private readonly Conjunction _antecedents;
        private readonly int _hashcode;

        private static readonly Logger Logger = LogManager.GetLogger("logic.prover");

        readonly static private Conjunction EmptyAntecedents = new Conjunction();

        public Implication(Fact head, params Expression[] conjuncts)
            : this(true, head, conjuncts)
        {
        }

        public Implication(bool clone, Fact head, params Expression[] conjuncts)
        {
            _consequent = head;
            _antecedents = conjuncts == null ? EmptyAntecedents : new Conjunction(clone, conjuncts);

            unchecked
            {
                _hashcode = ((_consequent != null ? _consequent.GetHashCode() : 0) * 397) ^ (_antecedents != null ? _antecedents.GetHashCode() : 0);
            }
        }

        public Implication(Fact head, Conjunction conjuncts)
        {
            _consequent = head;
            _antecedents = conjuncts ?? EmptyAntecedents;

            unchecked
            {
                _hashcode = ((_consequent != null ? _consequent.GetHashCode() : 0) * 397) ^ (_antecedents != null ? _antecedents.GetHashCode() : 0);
            }
        }

        /// <summary>
        /// Can this rule apply to a fact? True if the rule's consequent has the same
        /// fact name and the same arity. Note that a rule can apply to fact, without 
        /// being unifiable with it.
        /// </summary>
        /// <param name="f">The fact to check application for.</param>
        /// <returns>True if this rule applies to the fact.</returns>
        public bool CanApplyTo(Fact f)
        {
            return _consequent.RelationName == f.RelationName && _consequent.Arity == f.Arity;
        }

        public Fact Consequent { get { return _consequent; } }

        public Conjunction Antecedents { get { return _antecedents; } }

        public int NumAntecedents()
        {
            return _antecedents.NumConjuncts();
        }

        public override Expression Uniquefy()
        {
            var varMap = new Dictionary<TermVariable, TermVariable>();

            var newHead = (Fact)_consequent.Uniquefy(varMap);
            var newConjuncts = (Conjunction)_antecedents.Uniquefy(varMap);

            return new Implication(newHead, newConjuncts);
        }

        public override Expression ApplySubstitution(Substitution sigma)
        {
            var newHead = (Fact)_consequent.ApplySubstitution(sigma);
            var newConjuncts = (Conjunction)_antecedents.ApplySubstitution(sigma);

            //return new Implication(false, newHead, newConjuncts);     - INFO: bug? creates a conjunct in a conjunct
            return new Implication(newHead, newConjuncts);
        }

        public override bool CanMapVariables(Expression other)
        {
            if (other is Implication == false)
                return false;

            var impl = (Implication)other;

            if (impl._consequent.RelationName != _consequent.RelationName)
                return false;

            if (impl._antecedents.NumConjuncts() != _antecedents.NumConjuncts())
                return false;

            var varMappings = new Dictionary<TermVariable, TermVariable>();

            // First, check the heads' terms
            for (int i = 0; i < _consequent.Arity; i++)
            {
                Term t1 = _consequent.GetTerm(i);
                Term t2 = impl._consequent.GetTerm(i);

                if (t1.CanMapVariables(t2, varMappings) == false)
                    return false;
            }

            // TODO: implement the rest of Implication.canMapVariables
            // (we don't actually need to use this, I think)

            Logger.Error("WARNING: Implication.canMapVariables not implemented");

            return false;
        }

        public override bool HasTermFunction(int functionName)
        {
            return _consequent.HasTermFunction(functionName) || _antecedents.Conjuncts.Any(exp => exp.HasTermFunction(functionName));
        }

        public override bool HasTermVariable(int varName)
        {
            return _consequent.HasTermVariable(varName) || _antecedents.Conjuncts.Any(exp => exp.HasTermVariable(varName));
        }

        public override void PrintToStream(StreamWriter target, SymbolTable symtab)
        {
            target.Write("(<= ");
            _consequent.PrintToStream(target, symtab);
            target.Write(" ");
            _antecedents.PrintToStream(target, symtab);
            target.Write(")");
        }

        public override Expression Uniquefy(Dictionary<TermVariable, TermVariable> varMap)
        {
            var newHead = (Fact)_consequent.Uniquefy(varMap);
            var newConjuncts = (Conjunction)_antecedents.Uniquefy(varMap);

            return new Implication(false, newHead, newConjuncts);
        }

        /// <summary>
        /// Returns each of the expressions in the antecedents
        /// </summary>
        public override Expression[] Constituents { get { return _antecedents.Constituents; } }

        //public bool ContainsRelationName(string relationName, SymbolTable symTab)
        //{
        //    var queue = new Queue<Expression>(GetAntecedents().GetConjuncts());

        //    while (queue.Count > 0)
        //    {
        //        Expression exp = queue.Dequeue();
        //        var expFact = exp as Fact;

        //        if (expFact != null)
        //        {
        //            if (symTab[expFact.RelationName] == relationName)
        //                return true;
        //        }
        //        else
        //            exp.GetConstituents().ToList().ForEach(queue.Enqueue);

        //    }
        //    return false;
        //}

        public bool CanUnifyFact(Fact fact)
        {
            return UnifyFact(fact) != null;
        }

        public Substitution UnifyFact(Fact fact)
        {
            var queue = new Queue<Expression>(Antecedents.Conjuncts);

            while (queue.Count > 0)
            {
                var exp = queue.Dequeue();
                if (exp is Fact)
                {
                    var subs = (exp as Fact).Unify(fact);

                    if (subs != null)
                        return subs;
                }
                else
                    exp.Constituents.ToList().ForEach(queue.Enqueue);
            }
            return null;
        }

        public static bool CanUnifyFact(IEnumerable<Implication> implications, Fact fact)
        {
            return implications.Any(i => i.CanUnifyFact(fact));
        }

        public override IEnumerable<TermVariable> Variables { get { return _consequent.VariablesOrEmpty.Concat(_antecedents.VariablesOrEmpty); } }

        public override IEnumerable<TermObject> TermObjects { get { return _consequent.TermObjectsOrEmpty.Concat(_antecedents.TermObjectsOrEmpty); } }

        public override bool IsEquivalent(Expression target)
        {
            var impl = target as Implication;
            if (impl == null)
                return false;

            return Consequent.IsEquivalent(impl.Consequent) && Antecedents.IsEquivalent(impl.Antecedents);
        }

        public override string Output()
        {
            return string.Format("(<= {0}{1}{2})", _consequent.Output(), Environment.NewLine, _antecedents.Output());
        }

        internal bool IsImmediateRecursive()
        {
            int head = Consequent.RelationName;
            var body = Antecedents.Constituents;
            return body.OfType<Fact>().Any(fact => fact.RelationName == head);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Implication) obj);
        }

        protected bool Equals(Implication other)
        {
            return Equals(_consequent, other._consequent) && Equals(_antecedents, other._antecedents);
        }

        public override int GetHashCode()
        {
            return _hashcode;
        }
    }
}

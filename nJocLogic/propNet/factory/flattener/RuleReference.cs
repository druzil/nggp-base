using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using nJocLogic.data;
using System.Linq;

namespace nJocLogic.propNet.factory.flattener
{
    /// <summary>
    /// A deOred rule, its conditions, and parameters of the head of the rule
    /// </summary>
    public class RuleReference
    {        
        private readonly int _hashCode ;
        public ImmutableList<Term> ProductionTemplate { get; private set; }							            // The template from the rule head, contains only variables and constants
        public ImmutableList<PropNetFlattener.Condition> Conditions;
        public readonly Implication OriginalRule;

        public RuleReference(Implication originalRule, IEnumerable<PropNetFlattener.Condition> conditions, IList<Term> productionTemplate = null)
        {
            OriginalRule = originalRule;
            ProductionTemplate = productionTemplate==null ? ImmutableList<Term>.Empty :  productionTemplate.ToImmutableList();

            Conditions = conditions.ToImmutableList();

            int producttionTemplateHashCode = 1;

            if (productionTemplate != null)
                foreach (Term term in productionTemplate)
                    producttionTemplateHashCode = 31 * producttionTemplateHashCode + (term == null ? 0 : term.GetHashCode());

            int conditionsHashcode = Conditions.Aggregate(1, (current, cond) => 31 * current + (cond == null ? 0 : cond.GetHashCode()));
            _hashCode = producttionTemplateHashCode + conditionsHashcode;
        }

        public override String ToString()
        {
            return "\n\tProduction: " + (ProductionTemplate != null ? ProductionTemplate.ToString() : "null") + " conditions: " + (Conditions != null ? Conditions.ToString() : "null");
        }

        public override bool Equals(Object other)
        {
            var rhs = other as RuleReference;

            if (rhs == null)
                return false;

            return rhs.ProductionTemplate.SequenceEqual(ProductionTemplate) && rhs.Conditions.Equals(Conditions);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }
    }
}
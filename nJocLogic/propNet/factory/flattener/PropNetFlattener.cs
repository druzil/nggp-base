using System.Collections.Generic;
using System.Collections.Immutable;
using NLog;
using Logger = NLog.Logger;

namespace nJocLogic.propNet.factory.flattener
{
    using System;
    using System.Linq;
    using data;
    using gameContainer;
    using util.gdl;
    using util.gdl.model.assignments;

    /// <summary>
    /// PropNetFlattener is an implementation of a GDL flattener using fixed-point
    /// analysis of the rules. This flattener works on many small and medium-sized
    /// games, but can fail on very large games.
    /// 
    /// To use this class:
    ///      PropNetFlattener PF = new PropNetFlattener(description);
    ///      var flatDescription = PF.flatten();
    ///      return converter.convert(flatDescription);
    /// </summary>
    public class PropNetFlattener
    {
        private static readonly Logger Logger = LogManager.GetLogger("logic.flattener");

        private readonly List<Expression> _description;

        /// <summary>
        /// A mapping from a TermObject to each assignment that it exists in
        /// </summary>
        public class Index : Dictionary<TermObject, Assignments> { }

        /// <summary>
        /// Holds a term, its constants and variables and a generic version of itself (where constants and variables have been replaced by a generic variable)
        /// Also holds the domain of the condition
        /// </summary>
        public class Condition
        {
            private readonly PropNetFlattener _flattener;

            public Condition(Term template, PropNetFlattener flattener)
            {
                _flattener = flattener;
                Template = GetConstantAndVariableList(template);
                _key = FindGenericForm(template);
                UpdateDom();
            }

            internal void UpdateDom()
            {
                Dom = !_flattener._domains.ContainsKey(_key) ? null : _flattener._domains[_key];
            }

            public readonly List<Term> Template;    // A list of contants and variables that are used in the term (_key)
            public Domain Dom;
            readonly Term _key;

            public override String ToString()
            {
                return Template.ToString();
            }

            public override int GetHashCode()
            {
                return _key.GetHashCode();
            }
            public override bool Equals(object obj)
            {
                var cond = obj as Condition;
                return cond != null && _key.Equals(cond._key);
            }
        }

        /// <summary>
        /// Holds a term and all its rules and possible assignments that aren't constant
        /// </summary>
        public class Domain
        {
            public Domain(Term name)
            {
                _name = name;
            }

            public readonly Assignments Assignments = new Assignments();
            public readonly List<Index> Indices = new List<Index>();
            public readonly HashSet<RuleReference> RuleRefs = new HashSet<RuleReference>();

            private readonly Term _name;

            public override String ToString()
            {
                return "\nName: " + _name + "\nvalues: " + Assignments;//+"\nruleRefs: "+ruleRefs;
            }

            /// <summary>
            /// First index all the term objects in all the assignments
            /// Then remove any conditiions from Rules that contain constant terms
            /// </summary>
            public void BuildIndices()
            {
                foreach (Assignment assignment in Assignments)
                    AddAssignmentToIndex(assignment);

                RemoveConstantConditions(RuleRefs);
            }

            /// <summary>
            /// For each term object in the assignment sure add a mapping (in Indicies) from that term object to this assignment
            /// </summary>
            /// <param name="assignment"></param>
            public void AddAssignmentToIndex(Assignment assignment)
            {
                for (int i = 0; i < assignment.Count; i++)
                {
                    TermObject c = assignment[i];
                    if (Indices.Count <= i)
                        Indices.Add(new Index());
                    Index index = Indices[i];

                    if (!index.ContainsKey(c))
                        index[c] = new Assignments();
                    Assignments val = index[c];
                    val.Add(assignment);
                }
            }
        }

        private static readonly TermVariable FillerVar = new TermVariable(GameContainer.SymbolTable["?#*#"]);

        readonly Dictionary<Term, Domain> _domains = new Dictionary<Term, Domain>();

        private readonly List<RuleReference> _extraRefs = new List<RuleReference>();

        public PropNetFlattener(List<Expression> description)
        {
            _description = description;
        }

        public List<Implication> Flatten()
        {
            //Find universe and initial domains
            foreach (Expression gdl in _description)
                InitializeDomains(gdl);

            foreach (Domain d in _domains.Values)
                d.BuildIndices();

            //Compute the actual domains of everything
            UpdateDomains();

            return GetAllInstantiations();
        }

        private List<Implication> GetAllInstantiations()
        {
            List<Implication> rval = (from relation in _description.OfType<Fact>()
                                      where !GameContainer.SymbolTable[relation.RelationName].Equals("base")
                                      select new Implication(relation)).ToList();

            foreach (Domain d in _domains.Values)
                foreach (RuleReference r in d.RuleRefs)
                    foreach (TermObjectSubstitution varInstantiation in FindSatisfyingInstantiations(r))
                    {
                        if (varInstantiation.IsMappedTo(null))
                            throw new Exception("Shouldn't instantiate anything to null.");
                        rval.Add((Implication) r.OriginalRule.ApplySubstitution(varInstantiation));
                        if (rval.Last().ToString().Contains("null"))
                            throw new Exception("Shouldn't instantiate anything to null: " + rval.Last());
                    }

            RemoveConstantConditions(_extraRefs);
            foreach (RuleReference r in _extraRefs)
            {
                var varInstantiations = FindSatisfyingInstantiations(r);

                foreach (TermObjectSubstitution varInstantiation in varInstantiations)
                {
                    if (varInstantiation.IsMappedTo(null))
                        throw new Exception("Shouldn't instantiate anything to null.");
                    rval.Add((Implication)r.OriginalRule.ApplySubstitution(varInstantiation));

                    if (rval.Last().ToString().Contains("null"))
                        throw new Exception("Shouldn't instantiate anything to null.");
                }

                if (!varInstantiations.Any())
                    rval.Add((Implication)r.OriginalRule.ApplySubstitution(new TermObjectSubstitution()));
            }

            return rval;
        }

        private static void RemoveConstantConditions(ICollection<RuleReference> ruleRefs)
        {
            foreach (RuleReference ruleRef in ruleRefs.ToImmutableList())
            {
                var newConditions = new List<Condition>();
                foreach (Condition c in ruleRef.Conditions)
                {
                    if (c.Dom == null)
                        c.UpdateDom();

                    if (c.Dom != null)
                        newConditions.Add(c);
                }
                if (newConditions.Count != ruleRef.Conditions.Count)
                {
                    ruleRefs.Remove(ruleRef);
                    ruleRefs.Add(new RuleReference(ruleRef.OriginalRule, newConditions, ruleRef.ProductionTemplate));
                }
            }
        }

        /// <summary>
        /// Go through each expression, if it is a fact find what constants it is using and create assignments for them;
        /// for implications find the constants and variables used in the head (if any) and add each of expressions in the body as conditions
        /// This is the precursor step for resolving the domains
        /// </summary>
        /// <param name="gdl"></param>
        void InitializeDomains(Expression gdl)
        {
            var relation = gdl as Fact;
            var rule = gdl as Implication;
            if (relation != null)
            {
                String name = GameContainer.SymbolTable[relation.RelationName];
                if (!name.Equals("base"))
                {
                    Term term = relation.ToTerm();
                    Term generified = FindGenericForm(term);
                    Assignment instantiation = GetConstantList(term);
                    if (!_domains.ContainsKey(generified))
                        _domains[generified] = new Domain(generified);
                    _domains[generified].Assignments.Add(instantiation);
                }
            }
            else if (rule != null)
            {
                Fact head = rule.Consequent;
                List<Term> productionTemplate = null;
                ICollection<RuleReference> rules= _extraRefs;
                if (head.Arity > 0)
                {
                    Term term = head.ToTerm();

                    Term generified = FindGenericForm(term);
                    if (!_domains.ContainsKey(generified))
                        _domains[generified] = new Domain(generified);
                    Domain dom = _domains[generified];

                    productionTemplate = GetConstantAndVariableList(term);
                    rules = dom.RuleRefs;
                }

                IEnumerable<List<Expression>> newRHSs = DeORer.DeOr(rule.Antecedents.Conjuncts.ToList());
                foreach (List<Expression> rhs in newRHSs)
                {
                    IEnumerable<Condition> conditions = rhs.OfType<Fact>().Select(fact => new Condition(fact.ToTerm(), this));
                    var ruleRef = new RuleReference(new Implication(head, rhs.ToArray()), conditions, productionTemplate);
                    rules.Add(ruleRef);
                }
            }
        }

        /// <summary>
        /// Returns all constants that are using in the term and any term functions contained within it
        /// </summary>
        private static Assignment GetConstantList(Term term)
        {
            var termObject = term as TermObject;
            var rval = new Assignment();
            if (termObject != null)
                rval.Add(termObject);
            else if (term is TermVariable)
                throw new Exception("Called getConstantList on something containing a variable.");
            else
                rval.AddRange(((TermFunction)term).Arguments.SelectMany(GetConstantList));

            return rval;
        }

        /// <summary>
        /// Returns all constants and variables that are using in the term and any term functions contained within it
        /// </summary>
        private static List<Term> GetConstantAndVariableList(Term term)
        {
            var rval = new List<Term>();
            if (term is TermObject)
            {
                rval.Add(term);
                return rval;
            }
            if (term is TermVariable)
            {
                rval.Add(term);
                return rval;
            }

            var func = (TermFunction)term;
            rval.AddRange(func.Arguments.SelectMany(GetConstantAndVariableList));

            return rval;
        }

        /// <summary>
        /// Returns a generic form of the term where all constants and variables have been replaced by a generic variable
        /// </summary>
        private static Term FindGenericForm(Term term)
        {
            if (term is TermObject)
                return FillerVar;
            if (term is TermVariable)
                return FillerVar;

            var func = (TermFunction)term;
            Term[] newBody = func.Arguments.Select(FindGenericForm).ToArray();

            int name = func.FunctionName;
            if (name == GameContainer.Parser.TokLegal)
                name = GameContainer.Parser.TokDoes;
            else if (name == GameContainer.Parser.TokNext)
                name = GameContainer.Parser.TokTrue;
            else if (name == GameContainer.Parser.TokInit)
                name = GameContainer.Parser.TokTrue;
            return new TermFunction(name, newBody);
        }

        void UpdateDomains()
        {
            bool changedSomething = true;
            int itrNum = 0;
            var lastUpdatedDomains = new HashSet<Domain>(_domains.Values);
            while (changedSomething)
            {
                Logger.Info("StateMachine", "Beginning domain finding iteration: " + itrNum);

                var currUpdatedDomains = new HashSet<Domain>();
                changedSomething = false;
                int rulesConsidered = 0;
                foreach (Domain d in _domains.Values)
                {
                    foreach (RuleReference ruleRef in d.RuleRefs)
                    {
                        if (!ruleRef.Conditions.Any(c => lastUpdatedDomains.Contains(c.Dom)))
                            continue;

                        rulesConsidered++;

                        HashSet<TermObjectSubstitution> instantiations = FindSatisfyingInstantiations(ruleRef);
                        foreach (TermObjectSubstitution instantiation in instantiations)
                        {
                            var a = new Assignment();
                            foreach (Term t in ruleRef.ProductionTemplate)
                            {
                                var term = t as TermObject;
                                if (term != null)
                                    a.Add(term);
                                else
                                {
                                    var var = (TermVariable)t;
                                    a.Add((TermObject)instantiation.GetMapping(var));
                                }
                            }

                            if (!d.Assignments.Contains(a))
                            {
                                currUpdatedDomains.Add(d);
                                d.Assignments.Add(a);
                                changedSomething = true;
                                d.AddAssignmentToIndex(a);
                            }
                        }
                        if (!instantiations.Any())
                        { //There might just be no variables in the rule
                            var a = new Assignment();
                            //FindSatisfyingInstantiations(ruleRef); //just for debugging
                            bool isVar = false;
                            foreach (Term t in ruleRef.ProductionTemplate)
                            {
                                var term = t as TermObject;
                                if (term != null)
                                    a.Add(term);
                                else
                                {
                                    //There's a variable and we didn't find an instantiation
                                    isVar = true;
                                    break;
                                }
                            }

                            if (!isVar && !d.Assignments.Contains(a))
                            {
                                currUpdatedDomains.Add(d);
                                d.Assignments.Add(a);
                                changedSomething = true;
                                d.AddAssignmentToIndex(a);
                            }
                        }
                    }
                }
                itrNum++;
                lastUpdatedDomains = currUpdatedDomains;
                Logger.Info("\tDone with iteration.  Considered " + rulesConsidered + " rules.");
            }
        }

        private static HashSet<TermObjectSubstitution> FindSatisfyingInstantiations(RuleReference ruleRef)
        {
            return FindSatisfyingInstantiations(ruleRef.Conditions, 0, new TermObjectSubstitution());
        }

        private static HashSet<TermObjectSubstitution> FindSatisfyingInstantiations(IList<Condition> conditions, int idx, TermObjectSubstitution instantiation)
        {
            var rval = new HashSet<TermObjectSubstitution>();
            if (idx == conditions.Count)
            {
                rval.Add(instantiation);
                return rval;
            }

            Condition cond = conditions[idx];
            Domain dom = cond.Dom;
            Assignments assignments = null;
            for (int i = 0; i < cond.Template.Count; i++)
            {
                Term t = cond.Template[i];
                TermObject c = null;
                var v = t as TermVariable;
                var obj = t as TermObject;
                if (v != null)
                    c = (TermObject)instantiation.GetMapping(v);
                else if (obj != null)
                    c = obj;

                if (c != null)
                {
                    if (assignments == null)
                    {
                        assignments = new Assignments();
                        if (dom.Indices.Count > i)  //if this doesn't hold it is because there are no assignments and the indices haven't been set up yet
                        {
                            Index index = dom.Indices[i];
                            if (index.ContainsKey(c))  //Might be no assignment which satisfies this condition
                                index[c].ToList().ForEach(idxc => assignments.Add(idxc));
                        }
                    }
                    else
                    {
                        if (dom.Indices.Count > i)
                        {
                            Index index = dom.Indices[i];
                            if (index.ContainsKey(c)) //Might be no assignment which satisfies this condition
                                assignments.RemoveWhere(r => !index[c].Contains(r));
                        }
                        else  //This is when we've tried to find an assignment for a form that doesn't have any assignments yet.  Pretend it returned an empty set
                            assignments.Clear();
                    }
                }
            }
            if (assignments == null) //case where there are no constants to be consistent with
                assignments = dom.Assignments;

            foreach (Assignment a in assignments)
            {
                var newInstantiation = new TermObjectSubstitution().Copy(instantiation);
                for (int i = 0; i < a.Count; i++)
                {
                    var var = cond.Template[i] as TermVariable;
                    if (var != null)
                    {
                        Term mapping = instantiation.GetMapping(var);
                        if (mapping == null)
                            newInstantiation.AddMapping(var, a[i]);
                    }
                }
                FindSatisfyingInstantiations(conditions, idx + 1, newInstantiation).ToList().ForEach(f => rval.Add(f));
            }

            return rval;
        }
    }
}

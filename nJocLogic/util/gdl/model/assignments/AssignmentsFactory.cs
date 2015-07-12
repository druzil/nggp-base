using System;
using System.Collections.Generic;
using System.Linq;
using nJocLogic.data;

namespace nJocLogic.util.gdl.model.assignments
{
    public class AssignmentsFactory
    {

        public static AssignmentsImpl GetAssignmentsForRule(Implication rule,
                                                            ISentenceDomainModel model, Dictionary<ISentenceForm, FunctionInfo> functionInfoMap,
                                                            Dictionary<ISentenceForm, ICollection<Fact>> completedSentenceFormValues)
        {
            return new AssignmentsImpl(rule,
                SentenceDomainModels.GetVarDomains(rule, model, SentenceDomainModels.VarDomainOpts.IncludeHead),
                functionInfoMap,
                completedSentenceFormValues);
        }

        public static AssignmentsImpl GetAssignmentsForRule(Implication rule,
                                                            Dictionary<TermVariable, ISet<TermObject>> varDomains,
                                                            Dictionary<ISentenceForm, FunctionInfo> functionInfoMap,
                                                            Dictionary<ISentenceForm, ICollection<Fact>> completedSentenceFormValues)
        {
            return new AssignmentsImpl(rule,
                varDomains,
                functionInfoMap,
                completedSentenceFormValues);
        }

        public static AssignmentsImpl GetAssignmentsWithRecursiveInput(Implication rule,
                                                                       ISentenceDomainModel model, ISentenceForm form, Fact input,
                                                                       Dictionary<ISentenceForm, FunctionInfo> functionInfoMap,
                                                                       Dictionary<ISentenceForm, ICollection<Fact>> completedSentenceFormValues)
        {
            //Look for the literal(s) in the rule with the sentence form of the
            //recursive input. This can be tricky if there are multiple matching
            //literals.
            var matchingLiterals = rule.Antecedents.Conjuncts.OfType<Fact>().Where(form.Matches).ToList();

            var assignmentsList = new List<AssignmentsImpl>();
            foreach (Fact matchingLiteral in matchingLiterals)
            {
                var preassignment = new TermObjectSubstitution();
                preassignment.Add(matchingLiteral.Unify(input));
                if (preassignment.NumMappings() > 0)
                {
                    var assignments = new AssignmentsImpl(
                        preassignment,
                        rule,
                        //TODO: This one getVarDomains call is why a lot of
                        //SentenceModel/DomainModel stuff is required. Can
                        //this be better factored somehow?
                        SentenceDomainModels.GetVarDomains(rule, model, SentenceDomainModels.VarDomainOpts.IncludeHead),
                        functionInfoMap,
                        completedSentenceFormValues);
                    assignmentsList.Add(assignments);
                }
            }

            if (assignmentsList.Count == 0)
                return new AssignmentsImpl();
            if (assignmentsList.Count == 1)
                return assignmentsList[0];
            throw new Exception("Not yet implemented: assignments for recursive functions with multiple recursive conjuncts");
            //TODO: Plan to implement by subclassing TermObjectSubstitution into something
            //that contains and iterates over multiple TermObjectSubstitution
        }

        //TODO: Put the constructor that uses the SentenceModel here


    }
}

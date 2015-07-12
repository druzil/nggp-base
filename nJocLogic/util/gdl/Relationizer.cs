using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using nJocLogic.data;
using nJocLogic.gameContainer;
using nJocLogic.util.gdl.model;

namespace nJocLogic.util.gdl
{
    public class Relationizer
    {
        /// <summary>
        /// Searches the description for statements that are needlessly treated as base propositions when they could be
        /// expressed as simple relations, and replaces them with these simpler forms.
        /// 
        /// Some games have been written such that unchanging facts of the game are listed as base propositions. Often, this 
        /// is so the fact can be accessed by a visualization. Gamers usually don't need this distinction, and can reduce 
        /// the costs in time and memory of processing the game if these statements are instead transformed into sentences.
        /// </summary>
        /// <param name="description"></param>
        /// <returns></returns>
        public static List<Expression> Run(IList<Expression> description)
        {
            ImmutableSentenceFormModel model = SentenceFormModelFactory.Create(description);
            int next = GameContainer.Parser.TokNext;

            var nextFormsToReplace = new List<ISentenceForm>();
            IEnumerable<ISentenceForm> nextForms = model.SentenceForms.Where(m => m.Name.Equals(next));
            //Find the update rules for each "true" statement
            foreach (ISentenceForm nextForm in nextForms)
            {
                //See if there is exactly one update rule, and it is the persistence rule
                ISet<Implication> updateRules = model.GetRules(nextForm);

                if (updateRules.Count == 1)
                {
                    Implication updateRule = updateRules.First();
                    //Persistence rule: Exactly one literal, the "true" form of the sentence
                    if (updateRule.Antecedents.Constituents.Length == 1)
                    {
                        Expression literal = updateRule.Antecedents.Constituents[0];
                        var body = literal as Fact;
                        
                        if (body!=null)                        
                        {
                            //Check that it really is the true form
                            ISentenceForm trueForm = nextForm.WithName(GameContainer.Parser.TokTrue);
                            if (trueForm.Matches(body))
                            {
                                Fact head = updateRule.Consequent;
                                //Check that the tuples are the same, and that they consist of distinct variables
                                List<Term> headTuple = head.NestedTerms.ToList();
                                List<Term> bodyTuple = body.NestedTerms.ToList();
                                if (headTuple.Equals(bodyTuple) && headTuple.Distinct().Count() == headTuple.Count)
                                    nextFormsToReplace.Add(nextForm);
                            }
                        }
                    }
                }
            }

            var newDescription = new List<Expression>(description);
            //Now, replace the next forms
            foreach (ISentenceForm nextForm in nextFormsToReplace)
            {
                ISentenceForm initForm = nextForm.WithName(GameContainer.Parser.TokInit);
                ISentenceForm trueForm = nextForm.WithName(GameContainer.Parser.TokTrue);

                //Go through the rules and relations, making replacements as needed
                for (int i = 0; i < newDescription.Count; i++)
                {
                    Expression gdl = newDescription[i];
                    var relation = gdl as Fact;
                    if (relation != null)
                    {
                        //Replace initForm
                        if (initForm.Matches(relation))
                        {
                            var terms = relation.GetTerms();
                            var function = terms[0] as TermFunction;
                            Debug.Assert(function != null);
                            newDescription[i] = new VariableFact(true, function.FunctionName, function.Arguments);
                        }
                    }
                    else if (gdl is Implication)
                    {
                        var rule = (Implication)gdl;
                        //Remove persistence rule (i.e. rule with next form as head)
                        Fact head = rule.Consequent;
                        if (nextForm.Matches(head))
                            newDescription.RemoveAt(i--);
                        else
                        {
                            //Replace true in bodies of rules with relation-only form
                            List<Expression> body = rule.Antecedents.Constituents.ToList();
                            List<Expression> newBody = ReplaceRelationInBody(body, trueForm);
                            if (!body.Equals(newBody))
                                newDescription[i] = new Implication(head, newBody.ToArray());
                        }
                    }
                }
            }
            return newDescription;
        }

        private static List<Expression> ReplaceRelationInBody(IEnumerable<Expression> body, ISentenceForm trueForm)
        {
            return body.Select(literal => ReplaceRelationInLiteral(literal, trueForm)).ToList();
        }

        private static Expression ReplaceRelationInLiteral(Expression literal, ISentenceForm trueForm)
        {
            var sentence = literal as Fact;
            if (sentence != null)
                if (trueForm.Matches(sentence))
                {
                    var terms = sentence.GetTerms();
                    var function = terms[0] as TermFunction;
                    Debug.Assert(function != null);
                    return new VariableFact(true, function.FunctionName, function.Arguments);
                }
                else
                    return literal;

            var not = literal as Negation;
            if (not != null)
                return new Negation(ReplaceRelationInLiteral(not.Negated, trueForm));

            var or = literal as Disjunction;
            if (or != null)
            {
                var newOrBody = new List<Expression>();
                for (int i = 0; i < or.GetDisjuncts().Count(); i++)
                    newOrBody.Add(ReplaceRelationInLiteral(or.Constituents[i], trueForm));
                return new Disjunction(newOrBody.ToArray());
            }
            throw new Exception(string.Format("Unanticipated GDL literal type {0} encountered in Relationizer", literal.GetType()));
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using nJocLogic.data;
using nJocLogic.gameContainer;

namespace nJocLogic.reasoner.AimaProver
{
    public class AimaProver
    {
        private readonly KnowledgeBase _knowledgeBase;

        public AimaProver(IList<Expression> description)
        {
            description = DistinctAndNotMover.Run(description);
            _knowledgeBase = new KnowledgeBase(description);
        }

        private HashSet<Fact> Ask(Fact query, HashSet<Fact> context, bool askOne)
        {
            LinkedList<Expression> goals = new LinkedList<Expression>();
            goals.AddFirst(query);

            var answers = new HashSet<Substitution>();
            HashSet<Fact> alreadyAsking = new HashSet<Fact>();
            Ask(goals, new KnowledgeBase(context), new Substitution(), new ProverCache(), new VariableRenamer(), askOne, answers, alreadyAsking);

            var results = new HashSet<Fact>();
            foreach (Substitution theta in answers)
                results.Add(Substituter.Substitute(query, theta));

            return results;
        }

        private void Ask(LinkedList<Expression> goals, KnowledgeBase context, Substitution theta, ProverCache cache, VariableRenamer renamer, 
            bool askOne, HashSet<Substitution> results, HashSet<Fact> alreadyAsking)
        {
            if (goals.Count == 0)
                results.Add(theta);
            else
            {
                Expression literal = goals.First.Value;
                goals.RemoveFirst();
                Expression qPrime = Substituter.Substitute(literal, theta);

                var fact = qPrime as Fact;
                if (fact != null && fact.RelationName == GameContainer.Parser.TokDistinct)
                    AskDistinct(fact, goals, context, theta, cache, renamer, askOne, results, alreadyAsking);
                else
                {
                    var negation = qPrime as Negation;
                    if (negation != null)
                        AskNot(negation, goals, context, theta, cache, renamer, askOne, results, alreadyAsking);
                    else
                    {
                        var disjunction = qPrime as Disjunction;
                        if (disjunction != null)
                            AskOr(disjunction, goals, context, theta, cache, renamer, askOne, results, alreadyAsking);
                        else
                        {
                            var sentence = (Fact) qPrime;
                            AskSentence(sentence, goals, context, theta, cache, renamer, askOne, results, alreadyAsking);
                        }
                    }
                }

                goals.AddFirst(literal);
            }
        }

        public HashSet<Fact> AskAll(Fact query, HashSet<Fact> context)
        {
            return Ask(query, context, false);
        }

        private void AskDistinct(Fact distinct, LinkedList<Expression> goals, KnowledgeBase context, Substitution theta, 
            ProverCache cache, VariableRenamer renamer, bool askOne, HashSet<Substitution> results, HashSet<Fact> alreadyAsking)
        {
            if (!distinct.GetTerm(0).Equals(distinct.GetTerm(1)))
                Ask(goals, context, theta, cache, renamer, askOne, results, alreadyAsking);
        }

        private void AskNot(Negation not, LinkedList<Expression> goals, KnowledgeBase context, Substitution theta, ProverCache cache, 
            VariableRenamer renamer, bool askOne, HashSet<Substitution> results, HashSet<Fact> alreadyAsking)
        {
            var notGoals = new LinkedList<Expression>();
            notGoals.AddLast(not.Negated);

            var notResults = new HashSet<Substitution>();
            Ask(notGoals, context, theta, cache, renamer, true, notResults, alreadyAsking);

            if (notResults.Count == 0)
                Ask(goals, context, theta, cache, renamer, askOne, results, alreadyAsking);
        }

        public Fact AskOne(Fact query, HashSet<Fact> context)
        {
            return Ask(query, context, true).FirstOrDefault();
        }

        private void AskOr(Disjunction or, LinkedList<Expression> goals, KnowledgeBase context, Substitution theta, 
            ProverCache cache, VariableRenamer renamer, bool askOne, HashSet<Substitution> results, HashSet<Fact> alreadyAsking)
        {
            foreach (Expression expression in or.Constituents)
            {
                goals.AddFirst(expression);
                Ask(goals, context, theta, cache, renamer, askOne, results, alreadyAsking);
                goals.RemoveFirst();

                if (askOne && (results.Any()))
                    break;
            }
        }

        private void AskSentence(Fact sentence, LinkedList<Expression> goals, KnowledgeBase context, Substitution theta, 
            ProverCache cache, VariableRenamer renamer, bool askOne, HashSet<Substitution> results, HashSet<Fact> alreadyAsking)
        {
            if (!cache.Contains(sentence))
            {
                //Prevent infinite loops on certain recursive queries.
                if(alreadyAsking.Contains(sentence)) {
                    return;
                }
                alreadyAsking.Add(sentence);
                List<Implication> candidates = new List<Implication>();
                candidates.AddRange(_knowledgeBase.Fetch(sentence));
                candidates.AddRange(context.Fetch(sentence));

                var sentenceResults = new HashSet<Substitution>();
                foreach (Implication rule in candidates)
                {
                    Implication r = renamer.Rename(rule);
                    Substitution thetaPrime = Unifier.Unify(r.Consequent, sentence);

                    if (thetaPrime != null)
                    {
                        LinkedList<Expression> sentenceGoals = new LinkedList<Expression>();
                        for (int i = 0; i < r.NumAntecedents(); i++)
                            sentenceGoals.AddLast(r.Antecedents.Constituents[i]);

                        Ask(sentenceGoals, context, theta.Compose(thetaPrime), cache, renamer, false, sentenceResults, alreadyAsking);
                    }
                }

                cache[sentence] = sentenceResults;
                alreadyAsking.Remove(sentence);
            }

            foreach (Substitution thetaPrime in cache[sentence])
            {
                Ask(goals, context, theta.Compose(thetaPrime), cache, renamer, askOne, results, alreadyAsking);
                if (askOne && (results.Any()))
                    break;
            }
        }
	
        public bool Prove(Fact query, HashSet<Fact> context)
        {
            return AskOne(query, context) != null;
        }

    }
}
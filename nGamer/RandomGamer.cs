using System;
using System.Collections.Generic;
using System.Linq;
using nJocLogic.data;
using nJocLogic.game;
using nJocLogic.gdl;

namespace nGamer
{
    class RandomGamer : Gamer
    {
        public RandomGamer(string gameId, Parser p)
            : base(gameId, p)
        {
        }

        public override void StopIt()
        {
        }

        protected override Tuple<Term, string, string> MoveThink()
        {
            HashSet<Fact> context = new HashSet<Fact>();
            foreach (Fact ground in CurrentContext)
                context.Add(ground);

            TermObject role = Roles[MyRoleIndex];
            List<GroundFact> myMoves = GetAllAnswers(context, "legal", role.ToString(), "?x").Cast<GroundFact>().ToList();

            Term myMove = myMoves[Random.Next(myMoves.Count)].GetTerm(1);
            return new Tuple<Term, string, string>(myMove, string.Empty, string.Empty);

        }
    }
}

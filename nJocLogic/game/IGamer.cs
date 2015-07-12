using System.Collections.Generic;

namespace nJocLogic.game
{
    using System;
    using data;
    using gdl;
    using knowledge;

    public interface IGamer
    {
        void InitializeGame(TermObject assignedRole, int playClock, GameInformation gameInfo);

        List<PayOff> GetPayoffs(GdlList prevMoves);

        void StopIt();

        Tuple<GdlExpression, string, string> Play(GdlList prevMoves);
    }

    public class PayOff
    {
        public string PlayerName;
        public int Payoff;
        public bool PlayedRole;

        public PayOff(string name, int payoff, bool isRole)
        {
            PlayerName = name;
            Payoff = payoff;
            PlayedRole = isRole;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using nJocLogic.network;
using nJocLogic.util;

namespace nGamer.network
{
    public class InfoRequestHandler :  RequestHandler
    {
        public InfoRequestHandler(ISocketWrapper socket, HttpHeader header, string gameId) : base(socket, header, gameId)
        {
        }

        protected override void Execute()
        {
            SendAnswer("( ( name DruzilBot ) ( status available ) )");  //TODO: configure... also "busy"
            Finish();
        }
    }
}

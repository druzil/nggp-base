namespace nGamer.network
{
    using System;
    using nJocLogic.gdl;
    using nJocLogic.network;
    using nJocLogic.util;

    public class RequestHandlerFactory : nJocLogic.network.RequestHandlerFactory
    {
        protected override RequestHandler CreateFromList(ISocketWrapper socket, RequestHandler.HttpHeader header, GdlList list)
        {
            list = (GdlList)list[0];

            var command = list[0] as GdlAtom;

            if (command == null)
                throw new Exception("First element of message received in list is not an atom! Got: " + list[0]);

            RequestHandler result;

            if (command.Equals("info"))
                result = new InfoRequestHandler(socket, header, string.Empty);
            else
            {
                string matchId = list[1].ToString();                

                if (command.Equals("start"))
                    result = new StartRequestHandler(socket, header, list, matchId);
                else if (command.Equals("play"))
                    result = new PlayRequestHandler(socket, header, list, matchId);
                else if (command.Equals("stop") || command.Equals("abort"))
                    result = new StopRequestHandler(socket, header, list, matchId);
                else if (command.Equals("kill"))
                    result = new KillRequestHandler(socket, header, list, matchId);     // TODO: make this more secure!!!
                else
                    throw new Exception("Cannot handle request of type: " + command);
            }
            return result;
        }

    }

}

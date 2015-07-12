using System;
using System.IO;
using System.Linq;
using System.Text;
using nJocLogic.gameContainer;
using nJocLogic.gdl;
using nJocLogic.util;
using NLog;

namespace nJocLogic.network
{
    public abstract class RequestHandlerFactory
    {
        private static readonly Logger Logger = LogManager.GetLogger("logic.network");

        public RequestHandler CreateRequestHandler(ConnectionManager manager, ISocketWrapper socket)
        {
            RequestHandler.HttpHeader header;
            string gdl = string.Empty; // new string(contentInput);
            using (var input = new StreamReader(socket.GetStream()))
            {
                header = ReadHeader(input);

                var buffer = new char[header.ContentLength];
                int bytesRemaining = header.ContentLength;
                while (bytesRemaining > 0)
                {
                    int read = input.Read(buffer, 0, header.ContentLength);
                    gdl += new string(buffer).Substring(0, read);
                    bytesRemaining -= read;
                }
            }
            GdlList content = GameContainer.Parser.Parse(gdl);

            if (content == null || !content.Any())
            {
                Console.WriteLine("There was no content parsed");
                return null;
            }

            RequestHandler handler = CreateFromList(socket, header, content);
            handler.SetManager(manager);
            return handler;
        }

        private static RequestHandler.HttpHeader ReadHeader(TextReader input)
        {
            var result = new RequestHandler.HttpHeader();

            string line;

            Logger.Debug("Parsing message header.");

            while ((line = input.ReadLine()) != null)
            {
                Logger.Debug("Got line: " + line);

                if (String.Compare(line.Trim(), "", StringComparison.Ordinal) == 0)
                    break;
                if (line.StartsWith("Sender:"))
                    result.Receiver = line.Substring(8);
                else if (line.StartsWith("Receiver:"))
                    result.Receiver = line.Substring(10);
                else if (line.StartsWith("Content-length:")
                          || line.StartsWith("Content-Length:"))
                    result.ContentLength = int.Parse(line.Substring(16));
            }

            Logger.Debug("Done parsing message header. Content length: " + result.ContentLength);

            return result;
        }

        protected abstract RequestHandler CreateFromList(ISocketWrapper socket, RequestHandler.HttpHeader header, GdlList list);
    }

}

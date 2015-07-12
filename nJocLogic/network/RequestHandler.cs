using System;
using System.IO;
using nJocLogic.gameContainer;
using nJocLogic.util;
using NLog;

namespace nJocLogic.network
{
    public abstract class RequestHandler
    {
        protected const string ReplyHeader =
                "HTTP/1.0 200 OK\n" +
                "Content-type: text/acl\n" +
                "Access-Control-Allow-Origin: *\n" +
                "Content-length: ";
        protected const string Separator = "\n";

        private ISocketWrapper _socket;
        private TextWriter _writer;

        protected string GameId;

        protected ConnectionManager Manager;

        private static readonly Logger Logger = LogManager.GetLogger("logic.network");

        public class HttpHeader
        {
            public string Sender;
            public string Receiver;
            public int ContentLength;
        }

        private readonly HttpHeader _header;

        /**
         * Constructor.
         * 
         * @param socket The socket connection used for this handler.
         * @param header The HTTP header sent with this request.
         * @param gameId The game this connection is operating on.
         * 
         * @throws IOException If something goes wrong with the socket.
         */
        protected RequestHandler(ISocketWrapper socket, HttpHeader header, string gameId)
        {
            _header = header;

            _socket = socket;
            //writer_ = new StreamWriter(socket_.getOutputStream());
            //writer_ = new StreamWriter(socket_.GetStream());
            _writer = _socket.GetWriter();

            GameId = gameId;
        }

        public string GetGameId()
        {
            return GameId;
        }

        public HttpHeader GetHeader()
        {
            return _header;
        }

        public void SetManager(ConnectionManager manager)
        {
            Manager = manager;
        }

        /// <summary>
        /// Mark request as 'finished' i.e. handled. This closes the socket.
        /// </summary>
        public void Finish()
        {            
            _socket.Close();

            _socket = null;
            _writer = null;
        }
       

        /// <summary>
        /// Thread function: 'execute' is run in an independent thread
        /// </summary>
        public void Run()
        {
            try
            {
                Execute();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
        }
                  
        /// <summary>
        /// Main function; contains the normal execution of a request handler. The
        /// request handler is responsible for taking appropriate action and sending
        /// an answer back to the game master.
        /// </summary>
        protected abstract void Execute(); 

        /// <summary>
        /// Sends an answer to the game master.
        /// Prints the basic reply header first, then the content length and finally the content itself.
        /// The socket should probably be closed after the answer is sent.
        /// </summary>
        /// <param name="answer">The answer to send over the socket.</param>
        protected void SendAnswer(string answer)
        {
            _writer.Write(ReplyHeader);
            _writer.Write(answer.Length + Separator + Separator + answer + Separator);
            _writer.Flush();

            Logger.Info(string.Format("{0}: Replied with: {1}", GameId, answer));
        }
    }

}

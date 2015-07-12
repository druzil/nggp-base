using System;
using System.Net;
using System.Net.Sockets;
using nJocLogic.game;
using nJocLogic.util;
using NLog;

namespace nJocLogic.network
{
    public class ConnectionManager
    {
        private readonly Socket _server;
        private bool _running;

        private readonly RequestHandlerFactory _factory;

        private static readonly Logger Logger = LogManager.GetLogger("logic.network");

        /// <summary>
        /// Creation of the http server.
        /// </summary>
        /// <param name="port">Which port to open the player on.</param>
        /// <param name="factory">The RequestHandler factory used to create request handlers.</param>
        public ConnectionManager(int port, RequestHandlerFactory factory)
        {
            _server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
            _server.Bind(new IPEndPoint(IPAddress.Any, port));
            _server.Listen(100);
            _factory = factory;
            Logger.Info("Listening on port " + port + ".");
        }

        /// <summary>
        /// Server loop: looks for incoming connections and pass them on to a request handler
        /// which is further handed over to the game manager.  
        /// </summary>
        public void Run()
        {
            _running = true;
            try
            {
                Logger.Info("Awaiting incoming connections...");

                Socket incoming;
                while ((incoming = _server.Accept()) != null)
                {
                    string hostname = incoming.RemoteEndPoint.ToString();
                    Logger.Info("Incoming connection from " + hostname);

                    try
                    {
                        RequestHandler handler = _factory.CreateRequestHandler(this, new SocketWrapper(incoming));
                        if (handler != null)
                            GameManager.NewRequest(handler);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Error handling request from " + hostname);
                        Console.WriteLine(e.StackTrace);
                    }
                }
            }
            catch (Exception e)
            {
                if (_running)
                {
                    Logger.Error("General network failure, argh");
                    Console.WriteLine(e.StackTrace);
                }
            }
        }

        /**
         * Safely shuts down the server.
         */
        public void Shutdown()
        {
            if (!_running)
                return;
            _running = false;
            try
            {
                _server.Close();
            }
            catch (Exception)
            {
            }
        }
    }

}

using System;
using System.Reflection;
using System.Threading;
using WebSocketSharp;

namespace WSTest
{
    public class WSTest
    {
        public const string DEFAULT_URI = "wss://echo.websocket.org/";

        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] != "")
            {
                WebSocketSetNativeDirectory(args[0]);
            }

            string uri = args.Length > 1 ? args[1] : DEFAULT_URI;
            if (!new WSTest(uri).Run())
            {
                return 1;
            }
            return 0;
        }

        public static bool WebSocketSetNativeDirectory(string nativeLibDir)
        {
            // if this uses fake websocket-sharp, set native lib directory to ./lib/ for testing
            // some games/mods may have to do that to work around mod loader limitations
            // NOTE: this has to be done before creating the first websocket, so before TyyConnectAndLogin
            Type websocketType = Type.GetType("WebSocketSharp.WebSocket,websocket-sharp");
            if (websocketType == null)
            {
                Console.WriteLine("WARNING: could not set native lib dir for WebSocketSharp.WebSocket");
                return false;
            }

            MethodInfo setDirectory = websocketType.GetMethod("SetDirectory", BindingFlags.Public | BindingFlags.Static);
            if (setDirectory == null)
            {
                Console.WriteLine("WARNING: no SetDirectory in WebSocketSharp.WebSocket - wrong DLL?");
                return false;
            }

            setDirectory.Invoke(null, new object[]{nativeLibDir});
            Console.WriteLine("Set WebSocket native lib directory to " + nativeLibDir);
            return true;
        }

        WebSocket _ws;
        private object _lock;
        private bool _done = false;
        private bool _success = false;
        private bool _pingSent = false;
        private bool _echoSent = false;

        public WSTest(string uri)
        {
            _lock = new object();
            _ws = new WebSocket(uri);
            _ws.OnOpen += OnOpen;
            _ws.OnClose += OnClose;
            _ws.OnError += OnError;
            _ws.OnMessage += OnMessage;
        }

        void OnOpen(object sender, EventArgs e)
        {
            Console.WriteLine("Connection established");
        }

        void OnClose(object sender, CloseEventArgs e)
        {
            string text = e.Reason;
            if (text.Length > 0)
            {
                text += " ";
            }
            text += "(" + e.Code + ")";
            Console.WriteLine("Connection closed: " + text);
        }

        void OnError(object sender, ErrorEventArgs e)
        {
            Console.WriteLine("Error: " + e.Message);
            lock (_lock)
            {
                _success = false;
                _done = true;
            }
        }

        void OnMessage(object sender, MessageEventArgs e)
        {
            string text = e.Data;
            Console.WriteLine("Recv: " + text);
            lock (_lock)
            {
                if (_echoSent)
                {
                    _success = text == "Test";
                    _done = true;
                }
            }
        }

        public bool Run()
        {
            _ws.Connect();
            for (int i = 0; i < 800; i++)
            {
                Thread.Sleep(5);
                lock (_lock)
                {
                    if (_ws.ReadyState == WebSocketState.Open)
                    {
                        if (!_pingSent)
                        {
                            Console.WriteLine("Pinging");
                            if (!_ws.IsAlive)
                            {
                                _success = false;
                                break;
                            }
                            _pingSent = true;
                        }
                        else if (!_echoSent)
                        {
                            string text = "Test";
                            Console.WriteLine("Send: " + text);
                            _echoSent = true;
                            _ws.Send(text);
                        }
                    }
                    if (_done)
                    {
                        break;
                    }
                }
            }

            _ws.Close();

            lock (_lock)
            {
                if (!_success)
                {
                    Console.WriteLine("FAILED");
                }
                return _success;
            }
        }
    }
}

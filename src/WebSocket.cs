// main library file. this implements the websocket sharp compatible WebSocket

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using WebSocketSharp.Net;


// build time configuration to fine tune runtime:
// NO_WEBSOCKET_MULTI_THREADED_CLOSE: run onClose events in the EventDispatcher thread instead of spaning a new one


namespace WebSocketSharp
{
    public partial class WebSocket : IDisposable
    {
        private Uri uri;
        private WebSocketWorker worker = null;
        private WebSocketEventDispatcher dispatcher = null;
        private object dispatcherLock = new object();
        private List<byte[]> pings = new List<byte[]>();
        private DateTime lastPong;
        private volatile WebSocketState readyState = WebSocketState.New;
        string lastError;
        private int _id;
        static object _lastIdLock = new object();
        static int _lastId = 0;

        public event EventHandler OnOpen;

        public event EventHandler<CloseEventArgs> OnClose;

        public event EventHandler<ErrorEventArgs> OnError;

        public event EventHandler<MessageEventArgs> OnMessage;

        private void error(string message, Exception exception = null)
        {
            // FIXME: on .net >=4.0 we could use an async task to fire from main thread
            debug("Error: " + message);
            if (exception == null)
            {
                exception = new Exception(message);
            }
            ErrorEventArgs e = new ErrorEventArgs(message, exception);
            dispatcher.Enqueue(e);
        }

        public WebSocket(string uriString)
        {
            // TODO: automatic ws:// or wss://?
            lock(_lastIdLock)
            {
                _id = _lastId + 1;
                _lastId = _id;
            }
            debug("new (\"" + uriString + "\")");
            uri = new Uri(uriString);
            ws = wspp_new_from(uriString, directory);
            setHandlers();
        }

        public WebSocket(string uriString, string[] protocols)
        {
            // TODO: automatic ws:// or wss://?
            lock(_lastIdLock)
            {
                _id = _lastId + 1;
                _lastId = _id;
            }
            debug("new (\"" + uriString + "\", " +
                  (protocols == null ? "null" : ("[" + string.Join(", ", protocols) + "]")) + ")");
            uri = new Uri(uriString);
            ws = wspp_new_from(uriString, directory);
            setHandlers();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // no need to call finalizer
        }

        protected virtual void Dispose(bool disposing)
        {
            debug("disposing");
            if (ws != UIntPtr.Zero)
            {
                clearHandlers();
                var old = ws;
                ws = UIntPtr.Zero;

                // need to close before disposing worker
                debug("shutting down");
                close(old, 1001, "Going away");

                // try to stop the worker thread
                try
                {
                    if (worker != null)
                        worker.Dispose();
                }
                catch (Exception)
                {
                    // ignore
                }
                worker = null;

                // try to stop the dispatcher thread
                try
                {
                    if (dispatcher != null)
                        dispatcher.Dispose();
                }
                catch (InvalidOperationException)
                {
                    WebSocketEventDispatcher tmp = dispatcher;
                    new Thread(new ThreadStart(delegate
                    {
                        try
                        {
                            tmp.Dispose();
                        }
                        catch(Exception)
                        {
                            // ignore
                        }
                    })).Start();
                }
                catch (Exception)
                {
                    // ignore
                }
                dispatcher = null;

                debug("wspp_delete");
                wspp_delete(old);

                openHandler = null;
                closeHandler = null;
                messageHandler = null;
                errorHandler = null;
                pongHandler = null;
                dispatcherLock = null;
            }
        }

        ~WebSocket()
        {
            Dispose(false);
        }

        private void debug(string msg)
        {
            #if DEBUG
            Console.WriteLine("WebSocket " + _id + ": " + msg);
            #endif
        }

        private static void sdebug(string msg)
        {
            #if DEBUG
            Console.WriteLine("WebSocket: " + msg);
            #endif
        }

        private void warn(string msg)
        {
            Console.WriteLine("WARNING: WebSocket " + _id + ": " + msg);
        }

        private void pingBlocking(byte[] data, int timeout=15000)
        {
            if (worker.IsCurrentThread) {
                throw new InvalidOperationException("Can't wait for reply from worker thread");
            }

            lock(pings)
            {
                pings.Add(data);
            }

            Ping(data);

            // wait for ping handler to remove it from the list
            for (int i=0; i<timeout; i++) {
                Thread.Sleep(1);
                lock(pings)
                {
                    bool pongReceived = true;
                    foreach (byte[] b in pings) {
                        if (b == data) {
                            pongReceived = false;
                            break;
                        }
                    }
                    if (pongReceived) {
                        return;
                    }
                }
            }
            lock(pings)
            {
                pings.Remove(data);
            }
            debug("pong timeout");
            throw new TimeoutException();
        }

        public void Connect()
        {
            lastError = "";
            connect();
            while (worker != null && worker.IsAlive && readyState != WebSocketState.Open) {
                Thread.Sleep(1);
            }
            if (readyState != WebSocketState.Open) {
                throw new Exception("Connect failed" + ((lastError == "") ? "" : (": " + lastError)));
            }
        }

        public void ConnectAsync()
        {
            lastError = "";
            connect();
            // throw for immediate failure to get nicer stack traces where possible
            // later failure will run OnError, success will run OnOpen
            if (readyState != WebSocketState.Open && readyState != WebSocketState.Connecting) {
                throw new Exception("Connect failed" + ((lastError == "") ? "" : (": " + lastError)));
            }
        }

        private void connect()
        {
            if (readyState != WebSocketState.Closed && readyState != WebSocketState.New) {
                throw new InvalidOperationException("Invalid state: " + readyState.ToString());
            }

        #if !WEBSOCKET_MULTI_THREADED_CLOSE
            if (readyState != WebSocketState.New) {
                Thread.Sleep(1); // prefer running dispose threads before acquiring new resources
            }
        #endif

            debug("ReadyState = Connecting");
            readyState = WebSocketState.Connecting;

            if (ws == UIntPtr.Zero) {
                throw new ObjectDisposedException(GetType().FullName);
            }

            if (worker != null && !worker.IsAlive) {
                // previous session finished
                worker = null;
            }

            lock (dispatcherLock) {
                if (dispatcher == null) {
                    debug("creating dispatcher");
                    // we dispatch events from a separate thread to avoid deadlocks
                    dispatcher = new WebSocketEventDispatcher(); // (this)
                    dispatcher.OnOpen += dispatchOnOpen;
                    dispatcher.OnClose += dispatchOnClose;
                    dispatcher.OnError += dispatchOnError;
                    dispatcher.OnMessage += dispatchOnMessage;
                    dispatcher.Start();
                }
            }

            debug("wspp_connect");
            wspp_connect(ws);

            if (worker == null) {
                // start worker after queing connect
                debug("creating worker");
                worker = new WebSocketWorker(ws);
                worker.Start();
            } else {
                debug("worker already running");
            }
        }

        public void Close()
        {
            Close(1001);
        }

        public void Close(ushort code)
        {
            Close(code, "");
        }

        public void Close(ushort code, string reason)
        {
            debug("Close(" + code + ", \"" + reason + "\")");
            if (ws == UIntPtr.Zero) {
                throw new ObjectDisposedException(GetType().FullName);
            }

            debug("ReadyState = Closing");
            readyState = WebSocketState.Closing;
            close(ws, code, reason);

            if (worker.IsCurrentThread) {
                throw new InvalidOperationException("Can't wait for reply from worker thread");
            }
            while (worker != null && worker.IsAlive && readyState != WebSocketState.Closed) {
                Thread.Sleep(1);
            }
        }

        public void CloseAsync()
        {
            CloseAsync(1001);
        }

        public void CloseAsync(ushort code)
        {
            CloseAsync(code, "");
        }

        public void CloseAsync(ushort code, string reason)
        {
            debug("CloseAsync(" + code + ", \"" + reason + "\")");
            if (ws == UIntPtr.Zero) {
                throw new ObjectDisposedException(GetType().FullName);
            }

            debug("ReadyState = Closing");
            readyState = WebSocketState.Closing;
            close(ws, code, reason);
        }

        public WebSocketState ReadyState {
            get {
                return readyState;
            }
        }

        public bool IsAlive
        {
            get {
                if (readyState != WebSocketState.Open)
                {
                    return false;
                }
                if (DateTime.UtcNow - lastPong < new TimeSpan(0, 0, 0, 0, 300))
                {
                    return true; // don't ping if last ping was not too long ago
                    // Can disconnect at any time between IsAlive and actual message, so this is kinda useless anyway.
                }

                Random rnd = new Random();
                Byte[] b = new Byte[16];
                rnd.NextBytes(b);
                try {
                    pingBlocking(b);
                    return true;
                } catch (InvalidOperationException ex) {
                    Console.WriteLine(ex.ToString());
                    return true;
                } catch (TimeoutException) {
                    // fall through
                } catch (Exception ex) {
                    Console.WriteLine(ex.ToString());
                }

                return false;
            }
        }

        public bool IsSecure
        {
            get { return uri.Scheme.ToLower() == "wss"; }
        }

        public Uri Url
        {
            get { return uri; }
        }

        /// <summary>
        /// Gets a fake ClientSslConfiguration for compatibility reasons. Actual SSL behavior is hard-coded.
        /// </summary>
        public ClientSslConfiguration SslConfiguration
        {
            get { return new ClientSslConfiguration(); }
        }

        public void Send(string message)
        {
            if (ws == UIntPtr.Zero) {
                throw new ObjectDisposedException(GetType().FullName);
            }

            IntPtr p = StringToHGlobalUTF8(message);
            var res = (WsppRes) wspp_send_text(ws, p);
            Marshal.FreeHGlobal(p);
            if (res != WsppRes.OK)
                throw new Exception(Enum.GetName(typeof(WsppRes), res) ?? "Unknown error");
        }

        public void Send(byte[] data)
        {
            if (ws == UIntPtr.Zero) {
                throw new ObjectDisposedException(GetType().FullName);
            }

            var res = (WsppRes) wspp_send_binary(ws, data, (ulong)data.Length);
            if (res != WsppRes.OK)
                throw new Exception(Enum.GetName(typeof(WsppRes), res) ?? "Unknown error");
        }

        public void SendAsync(string message, Action<bool> onComplete = null)
        {
            // upper layer catches exceptions, so whatever
            Send(message);
            if (onComplete != null)
                onComplete(true);
        }

        public void SendAsync(byte[] data, Action<bool> onComplete = null)
        {
            // upper layer catches exceptions, so whatever
            Send(data);
            if (onComplete != null)
                onComplete(true);
        }

        public void Ping(byte[] data)
        {
            if (ws == UIntPtr.Zero) {
                throw new ObjectDisposedException(GetType().FullName);
            }

            var res = (WsppRes) wspp_ping(ws, data, (ulong)data.Length);
            if (res != WsppRes.OK)
                throw new Exception(Enum.GetName(typeof(WsppRes), res) ?? "Unknown error");
        }

        private void dispatchOnOpen(object sender, EventArgs e)
        {
            if (OnOpen != null)
                OnOpen(this, e);
        }

        private void dispatchOnClose(object sender, CloseEventArgs e)
        {
            lock (dispatcherLock)
            {
                WebSocketEventDispatcher tmp = dispatcher;
                if (tmp != null)
                {
                    dispatcher = null;
                    new Thread(new ThreadStart(delegate
                    {
                        tmp.Dispose();
                    })).Start();
                }
                else
                {
                    warn("duplicate close event");
                }
            }

            // TODO: set state to closed here instead (see comment in WebSocket.native.cs)

        #if !NO_WEBSOCKET_MULTI_THREADED_CLOSE
            if (OnError != null) {
                new Thread(new ThreadStart(delegate
                {
                    Thread.Sleep(1); // prefer disposing first
                    OnClose(this, e);
                })).Start();
            }
        #else
            if (OnClose != null)
                OnClose(this, e);
        #endif
        }

        private void dispatchOnError(object sender, ErrorEventArgs e)
        {
            if (readyState == WebSocketState.Closed)
            {
                // free resources if error left a closed socket
                lock (dispatcherLock)
                {
                    WebSocketEventDispatcher tmp = dispatcher;
                    if (tmp != null)
                    {
                        dispatcher = null;
                        new Thread(new ThreadStart(delegate
                        {
                            tmp.Dispose();
                        })).Start();
                    }
                    else
                    {
                        warn("duplicate close event");
                    }
                }
            }

            // TODO: set state to closed here instead (see comment in WebSocket.native.cs)
            // NOTE: this requires the if above to change - say to Disconnecting

        #if !NO_WEBSOCKET_MULTI_THREADED_CLOSE
            // run OnError in a new thread if readyState == Closed
            if (readyState == WebSocketState.Closed)
            {
                if (OnError != null) {
                    new Thread(new ThreadStart(delegate
                    {
                        Thread.Sleep(1); // prefer disposing first
                        OnError(this, e);
                    })).Start();
                }
            }
            else
        #endif
            if (OnError != null)
                OnError(this, e);
        }

        private void dispatchOnMessage(object sender, MessageEventArgs e)
        {
            if (OnMessage != null)
                OnMessage(this, e);
        }
    }
}


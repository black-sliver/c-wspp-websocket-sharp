// main library file. this implements the websocket sharp compatible WebSocket

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using WebSocketSharp.Net;

namespace WebSocketSharp
{
    public partial class WebSocket : IDisposable
    {
        internal enum State
        {
            Disconnected = 0,
            Connecting = 1,
            Open = 2,
        }

        private Uri uri;
        private WebSocketWorker worker = null;
        private WebSocketEventDispatcher dispatcher = null;
        private List<byte[]> pings = new List<byte[]>();
        private State state;

        private void error(string message, Exception exception = null)
        {
            // FIXME: on .net >=4.0 we could use an async task to fire from main thread
            ErrorEventArgs e = new ErrorEventArgs(message, exception);
            dispatcher.Enqueue(e);
        }

        public WebSocket(string uriString)
        {
            // TODO: automatic ws:// or wss://?
            uri = new Uri(uriString);
            ws = wspp_new_from(uriString, directory);
            setHandlers();
        }

        public WebSocket(string uriString, string[] protocols)
        {
            // TODO: automatic ws:// or wss://?
            uri = new Uri(uriString);
            ws = wspp_new_from(uriString, directory);
            setHandlers();
        }

        public void Dispose()
        {
            Console.WriteLine("dispose");
            Dispose(true);
            GC.SuppressFinalize(this); // no need to call finalizer
        }

        protected virtual void Dispose(bool disposing)
        {
            Console.WriteLine("Dispose");
            if (ws != UIntPtr.Zero)
            {
                clearHandlers();
                var old = ws;
                ws = UIntPtr.Zero;

                // need to close before disposing worker
                Console.WriteLine("- close");
                close(old, 1001, "Going away");

                if (worker != null) {
                    Console.WriteLine("- dispose worker");
                    worker.Dispose();
                    worker = null;
                }

                if (dispatcher != null) {
                    Console.WriteLine("- dispose dispatcher");
                    dispatcher.Dispose();
                    dispatcher = null;
                }

                Console.WriteLine("- wspp_delete");
                wspp_delete(old);

                openHandler = null;
                closeHandler = null;
                messageHandler = null;
                errorHandler = null;
                pongHandler = null;
            }
        }

        ~WebSocket()
        {
            Console.WriteLine("~");
            Dispose(false);
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
            Console.WriteLine("ping sent, waiting for pong");

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
                        Console.WriteLine("pong received");
                        return;
                    }
                }
            }
            lock(pings)
            {
                pings.Remove(data);
            }
            Console.WriteLine("pong timeout");
            throw new TimeoutException();
        }

        public void Connect()
        {
            connect();
            while (worker != null && worker.IsAlive && state != State.Open) {
                Thread.Sleep(1);
            }
            if (state != State.Open) {
                throw new Exception("Connect failed");
            }
        }

        private void connect()
        {
            if (state != State.Disconnected) {
                throw new InvalidOperationException("Invalid state");
            }

            state = State.Connecting;

            if (ws == UIntPtr.Zero) {
                throw new ObjectDisposedException(GetType().FullName);
            }

            if (worker != null && !worker.IsAlive) {
                // previous session finished
                worker = null;
            }

            if (dispatcher == null) {
                // we dispatch events from a separate thread to avoid deadlocks
                dispatcher = new WebSocketEventDispatcher(); // (this)
                dispatcher.OnOpen += dispatchOnOpen;
                dispatcher.OnClose += dispatchOnClose;
                dispatcher.OnError += dispatchOnError;
                dispatcher.OnMessage += dispatchOnMessage;
                dispatcher.Start();
            }

            wspp_connect(ws);

            if (worker == null) {
                // start worker after queing connect
                worker = new WebSocketWorker(ws);
                worker.Start();
            }
        }

        // public void ConnectAsync() -> return a Task

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
            if (ws == UIntPtr.Zero) {
                throw new ObjectDisposedException(GetType().FullName);
            }

            close(ws, code, reason);

            if (worker.IsCurrentThread) {
                throw new InvalidOperationException("Can't wait for reply from worker thread");
            }
            while (worker != null && worker.IsAlive && state == State.Open) {
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
            if (ws == UIntPtr.Zero) {
                throw new ObjectDisposedException(GetType().FullName);
            }

            close(ws, code, reason);
        }

        public bool IsAlive
        {
            get {
                // TODO: remember time of last ping and don't spam it.
                // Can disconnect at any time between IsAlive and actual message, so this is kinda useless anyway.
                if (state == State.Disconnected) {
                    Console.WriteLine("IsAlive=False (disconnected)");
                    return false;
                }
                Random rnd = new Random();
                Byte[] b = new Byte[16];
                rnd.NextBytes(b);
                try {
                    pingBlocking(b);
                    Console.WriteLine("IsAlive=True");
                    return true;
                } catch (InvalidOperationException ex) {
                    Console.WriteLine(ex.ToString());
                    return true;
                } catch (TimeoutException) {
                    // fall through
                } catch (Exception ex) {
                    Console.WriteLine(ex.ToString());
                }
                // for whatever reason, we sometimes don't receive the pong
                Console.WriteLine("IsAlive=False");
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

        public event EventHandler OnOpen;

        public event EventHandler<CloseEventArgs> OnClose;

        public event EventHandler<ErrorEventArgs> OnError;

        public event EventHandler<MessageEventArgs> OnMessage;

        private void dispatchOnOpen(object sender, EventArgs e)
        {
            if (OnOpen != null)
                OnOpen(this, e);
        }

        private void dispatchOnClose(object sender, CloseEventArgs e)
        {
            if (OnClose != null)
                OnClose(this, e);
        }

        private void dispatchOnError(object sender, ErrorEventArgs e)
        {
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


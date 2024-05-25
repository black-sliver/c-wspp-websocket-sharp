// Worker (thread) that polls the socket

using System;
using System.Threading;

namespace WebSocketSharp
{
    internal class WebSocketWorker : IDisposable
    {
        private IWSPP _wspp;
        private Thread _thread;
        private bool _stop;
        private int _id;
        static object _lastIdLock = new object();
        static int _lastId = 0;

        public WebSocketWorker(IWSPP wspp) {
            lock(_lastIdLock)
            {
                _id = _lastId + 1;
                _lastId = _id;
            }
            _wspp = wspp;
            _thread = new Thread(new ThreadStart(work));
            _stop = false;
        }

        public void Start()
        {
            _thread.Start();
        }

        public void Join()
        {
            _thread.Join();
        }

        public bool IsCurrentThread { get { return Thread.CurrentThread == _thread; } }

        public bool IsAlive { get { return _thread.IsAlive; } }

        private void debug(string msg)
        {
            if (msg == null)
            {
                msg = "<null>";
            }

            #if DEBUG
            Console.WriteLine("WebSocketWorker " + _id + ": " + msg);

            #if LOG_TO_FILE
            WebSocket.Log("WebSocketWorker " + _id + ": " + msg);
            #endif
            #endif
        }

        private void work()
        {
            while (!_stop && !_wspp.stopped())
            {
                // sadly we can't use wspp_run() because .net will not run finalizers then
                _wspp.poll();
                Thread.Sleep(1);
            }

            if (!_wspp.stopped())
                debug("stopping");

            // wait up to a second for closing handshake
            for (int i=0; i<1000; i++)
            {
                if (_wspp.stopped())
                {
                    break;
                }
                _wspp.poll();
                Thread.Sleep(1);
            }
            debug("stopped");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // no need to call finalizer
        }

        protected virtual void Dispose(bool disposing)
        {
            debug("disposing");
            if (!_stop)
            {
                _stop = true;
                _thread.Join();
                debug("joined");
            }
        }

        ~WebSocketWorker()
        {
            Dispose(false);
        }
    }
}


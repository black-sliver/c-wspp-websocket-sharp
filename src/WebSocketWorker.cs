// Worker (thread) that polls the socket

using System;
using System.Threading;

namespace WebSocketSharp
{
    internal class WebSocketWorker : IDisposable
    {
        private UIntPtr _ws;
        private Thread _thread;
        private bool _stop;

        public WebSocketWorker(UIntPtr ws) {
            _ws = ws;
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

        private void work()
        {
            while (!_stop && !WebSocket.wspp_stopped(_ws))
            {
                // sadly we can't use wspp_run() because .net will not run finalizers then
                WebSocket.wspp_poll(_ws);
                Thread.Sleep(1);
            }
            Console.WriteLine("WebSocketWorker: stopping or stopped");

            // wait up to a second for closing handshake
            for (int i=0; i<1000; i++)
            {
                if (WebSocket.wspp_stopped(_ws))
                {
                    break;
                }
                WebSocket.wspp_poll(_ws);
                Thread.Sleep(1);
            }
            Console.WriteLine("WebSocketWorker: done");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // no need to call finalizer
        }

        protected virtual void Dispose(bool disposing)
        {
            Console.WriteLine("WebSocketWorker: disposing");
            if (!_stop)
            {
                _stop = true;
                _thread.Join();
                Console.WriteLine("WebSocketWorker: joined");
            }
        }

        ~WebSocketWorker()
        {
            Dispose(false);
        }
    }
}


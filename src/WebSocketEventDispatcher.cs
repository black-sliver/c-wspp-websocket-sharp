// Worker (thread) that dispatches events

using System;
using System.Collections.Generic;
using System.Threading;

namespace WebSocketSharp
{
    internal class WebSocketEventDispatcher : IDisposable
    {
        private Thread _thread;
        private bool _stop;
        private Queue<EventArgs> _queue;
        private int _id;
        static object _lastIdLock = new object();
        static int _lastId = 0;

        public WebSocketEventDispatcher() {
            lock(_lastIdLock)
            {
                _id = _lastId + 1;
                _lastId = _id;
            }
            _thread = new Thread(new ThreadStart(work));
            _stop = false;
            _queue = new Queue<EventArgs>();
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
            Console.WriteLine("WebSocketEventDispatcher " + _id + ": " + msg);

            #if LOG_TO_FILE
            WebSocket.Log("WebSocketEventDispatcher " + _id + ": " + msg);
            #endif
            #endif
        }

        private void work()
        {
            debug("running");
            while (!_stop)
            {
                // dispatch events from here
                EventArgs e;
                lock(_queue)
                {
                    e = (_queue.Count > 0) ? _queue.Dequeue() : null;
                }
                if (e != null)
                {
                    try
                    {
                        debug("dispatching " + e);
                        if (e is MessageEventArgs)
                        {
                            if (OnMessage != null)
                                OnMessage(this, (MessageEventArgs)e);
                        }
                        else if (e is CloseEventArgs)
                        {
                            if (OnClose != null)
                                OnClose(this, (CloseEventArgs)e);
                        }
                        else if (e is ErrorEventArgs)
                        {
                            if (OnError != null)
                                OnError(this, (ErrorEventArgs)e);
                        }
                        else //if (e is OpenEventArgs)
                        {
                            if (OnOpen != null)
                                OnOpen(this, e);
                        }
                    }
                    catch (Exception ex)
                    {
                        debug(ex.Message);
                    }
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
            debug("stopped");
            _queue = null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // no need to call finalizer
        }

        protected virtual void Dispose(bool disposing)
        {
            if (IsCurrentThread)
            {
                throw new InvalidOperationException("Can't dispose self");
            }
            debug("disposing");
            if (!_stop)
            {
                _stop = true;
                _thread.Join();
                debug("joined");
                _queue = null;
            }
        }

        ~WebSocketEventDispatcher()
        {
            Dispose(false);
        }

        public void Enqueue(EventArgs e)
        {
            lock(_queue)
            {
                _queue.Enqueue(e);
            }
        }

        public event EventHandler OnOpen;

        public event EventHandler<CloseEventArgs> OnClose;

        public event EventHandler<ErrorEventArgs> OnError;

        public event EventHandler<MessageEventArgs> OnMessage;
    }
}


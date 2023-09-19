using System;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;

namespace WebSocketSharp
{
    internal class DllDirectory : IDisposable
    {
        private static object _lock = new object();

        private bool _disposed = false;

    #if OS_WINDOWS
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetDllDirectory(int nBufferLength, StringBuilder lpPathName);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        private static string _oldDllDirectory = null;

        private static void set(string newDllDirectory)
        {
            if (_oldDllDirectory != null)
            {
                throw new InvalidOperationException("Please reset dll directory before setting it again!");
            }
            StringBuilder tmpSB = new StringBuilder(10240);
            int len = GetDllDirectory(10240, tmpSB);
            if (len > 10240)
            {
                throw new Exception("Could not SetDllDirectory");
            }
            string tmpString = tmpSB.ToString(0, len);
            SetDllDirectory(newDllDirectory);
            _oldDllDirectory = tmpString;
        }

        private static void reset()
        {
            if (_oldDllDirectory != null)
            {
                SetDllDirectory(_oldDllDirectory);
                _oldDllDirectory = null;
            }
        }
    #else
        internal static void set(string newDllDirectory)
        {
            // not implemented
        }

        internal static void reset()
        {
            // not implemented
        }
    #endif

        internal static void Set(string newDllDirectory)
        {
            lock (_lock)
            {
                set(newDllDirectory);
            }
        }

        internal static void Reset()
        {
            lock (_lock)
            {
                reset();
            }
        }

        internal static DllDirectory Context(string newDllDirectory)
        {
            Monitor.Enter(_lock);
            try
            {
                set(newDllDirectory);
                return new DllDirectory(); // return IDisposable that resets the directory on Dispose
            }
            catch (Exception ex)
            {
                Monitor.Exit(_lock);
                throw ex;
            }
        }

        private DllDirectory()
        {
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // no need to call finalizer
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                try
                {
                    _disposed = true;
                    reset();
                }
                finally
                {
                    Monitor.Exit(_lock);
                }
            }
        }

        ~DllDirectory()
        {
            Dispose(false);
        }
    }
}

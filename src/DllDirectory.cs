using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;

namespace WebSocketSharp
{
    /// <summary>
    /// Helper to load a native DLL from a specific folder
    /// </summary>
    internal class DllDirectory : IDisposable
    {
        private static readonly object _lock = new object();
        private static string _new = null;
        private static Stack<String> _stack = new Stack<String>();

        private bool _disposed = false;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetDllDirectory(int nBufferLength, StringBuilder lpPathName);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        private static string GetDllDirectoryString()
        {
            StringBuilder tmpSB = new StringBuilder(10240);
            int len = GetDllDirectory(10240, tmpSB);
            if (len > 10240)
            {
                throw new Exception("Could not GetDllDirectory");
            }
            string tmpString = tmpSB.ToString(0, len);
            return tmpString;
        }

        private static bool IsWindows
        {
            get {
                int platformId = (int)Environment.OSVersion.Platform;
                return (platformId < 4 || platformId == 5);
            }
        }

        private static void SetInternal(string newDllDirectory)
        {
            bool isWindows = IsWindows;
            if (_new == null)
            {
                if (isWindows)
                {
                    _stack.Push(GetDllDirectoryString());
                }
                else
                {
                    _stack.Push("");
                }
            }
            else
            {
                _stack.Push(_new);
            }
            _new = newDllDirectory;

            if (isWindows)
            {
                SetDllDirectory(_new);
            }
        }

        private static void ResetInternal()
        {
            _new = _stack.Pop();
            if (IsWindows)
            {
                SetDllDirectory(_new);
                if (_stack.Count == 0)
                {
                    _new = null; // ask OS again next time
                }
            }
        }

        internal static void Set(string newDllDirectory)
        {
            lock (_lock)
            {
                SetInternal(newDllDirectory);
            }
        }

        internal static void Reset()
        {
            lock (_lock)
            {
                ResetInternal();
            }
        }

        internal static String Current {
            get {
                lock (_lock)
                {
                    if (_new == null)
                    {
                        if (IsWindows)
                        {
                            return GetDllDirectoryString();
                        }
                        else
                        {
                            return "";
                        }
                    }
                    return _new;
                }
            }
        }

        internal static DllDirectory Context(string newDllDirectory)
        {
            Monitor.Enter(_lock);
            try
            {
                SetInternal(newDllDirectory);
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
                    ResetInternal();
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

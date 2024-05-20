using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace WebSocketSharp
{
    /// <summary>
    /// Wrapper for native c-wspp on Non-Windows.
    /// </summary>
    internal class WSPPNix : IWSPP
    {
        internal const CallingConvention CALLING_CONVENTION = CallingConvention.Cdecl;

        internal const string DLL_NAME = "c-wspp";

        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate void OnOpenCallback();
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate void OnCloseCallback(); // TODO: code, reason
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate void OnErrorCallback(IntPtr msg); // TODO: errorCode
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate void OnMessageCallback(IntPtr data, ulong len, int opCode);
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate void OnPongCallback(IntPtr data, ulong len);

        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate UIntPtr Wspp_new(IntPtr uri);
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate void Wspp_delete(UIntPtr ws);
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate ulong Wspp_poll(UIntPtr ws);
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate ulong Wspp_run(UIntPtr ws);
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate bool Wspp_stopped(UIntPtr ws);
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate int Wspp_connect(UIntPtr ws);
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate int Wspp_close(UIntPtr ws, ushort code, IntPtr reason);
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate int Wspp_send_text(UIntPtr ws, IntPtr message);
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate int Wspp_send_binary(UIntPtr ws, byte[] data, ulong len);
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate int Wspp_ping(UIntPtr ws, byte[] data, ulong len);
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate void Wspp_set_open_handler(UIntPtr ws, OnOpenCallback f);
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate void Wspp_set_close_handler(UIntPtr ws, OnCloseCallback f);
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate void Wspp_set_error_handler(UIntPtr ws, OnErrorCallback f);
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate void Wspp_set_message_handler(UIntPtr ws, OnMessageCallback f);
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate void Wspp_set_pong_handler(UIntPtr ws, OnPongCallback f);

        UIntPtr _ws;

#pragma warning disable 0414
        OnOpenCallback _openHandler;
        OnCloseCallback _closeHandler;
        OnErrorCallback _errorHandler;
        OnMessageCallback _messageHandler;
        OnPongCallback _pongHandler;
#pragma warning disable 0414

        static Wspp_new wspp_new;
        static Wspp_delete wspp_delete;
        static Wspp_poll wspp_poll;
        static Wspp_run wspp_run;
        static Wspp_stopped wspp_stopped;
        static Wspp_connect wspp_connect;
        static Wspp_close wspp_close;
        static Wspp_send_text wspp_send_text;
        static Wspp_send_binary wspp_send_binary;
        static Wspp_ping wspp_ping;
        static Wspp_set_open_handler wspp_set_open_handler;
        static Wspp_set_close_handler wspp_set_close_handler;
        static Wspp_set_error_handler wspp_set_error_handler;
        static Wspp_set_message_handler wspp_set_message_handler;
        static Wspp_set_pong_handler wspp_set_pong_handler;

        static IntPtr _dll;

        private static readonly object _dllLock = new object();

        static public bool IsActivePlatform()
        {
            int platformId = (int)Environment.OSVersion.Platform;
            return (platformId >= 4 && platformId != 5);
        }

        public WSPPNix(string uriString)
        {
            lock (_dllLock)
            {
                if (_dll == IntPtr.Zero)
                {
                    // since we target old .net, there is only x86 and amd64 and we hope this works
                    string arch;
                    if (IntPtr.Size == 4)
                    {
                        arch = "x86";
                    }
                    else if (IntPtr.Size == 8)
                    {
                        arch = "amd64";
                    }
                    else
                    {
                        throw new PlatformNotSupportedException("Unknown architecture");
                    }

                    int platformId = (int)Environment.OSVersion.Platform;
                    if (platformId < 4 || platformId == 5)
                    {
                        throw new PlatformNotSupportedException("Use WSPPWin instead");
                    }

                    string attemptedPaths = "";
                    IDL dl = DynamicLinker.Create();

                    // try to guess which openssl to target (if openssl1 is available, try that, then openssl3)
                    string variant = "";
                    if (platformId != 6)
                    {
                        bool hasLibSSL = false;
                        bool hasOpenSSL1 = false;
                        bool hasOpenSSL3 = false;

                        IntPtr tmp;
                        tmp = dl.Open("libssl.so.3", 0x2);
                        if (tmp != IntPtr.Zero)
                        {
                            dl.Close(tmp);
                            hasOpenSSL3 = true;
                        }
                        else
                        {
                            tmp = dl.Open("libssl3.so", 0x2);
                            if (tmp != IntPtr.Zero)
                            {
                                dl.Close(tmp);
                                hasOpenSSL3 = true;
                            }
                        }
                        tmp = dl.Open("libssl.so.1", 0x2);
                        if (tmp != IntPtr.Zero)
                        {
                            dl.Close(tmp);
                            hasOpenSSL1 = true;
                        }
                        else
                        {
                            tmp = dl.Open("libssl.so.1.1", 0x2);
                            if (tmp != IntPtr.Zero)
                            {
                                dl.Close(tmp);
                                hasOpenSSL1 = true;
                            }
                        }
                        tmp = dl.Open("libssl.so", 0x2);
                        if (tmp != IntPtr.Zero)
                        {
                            dl.Close(tmp);
                            hasLibSSL = true;
                        }
                        else
                        {
                            tmp = dl.Open("ssl", 0x2);
                            if (tmp != IntPtr.Zero)
                            {
                                dl.Close(tmp);
                                hasLibSSL = true;
                            }
                        }

                        #if DEBUG
                        if (hasLibSSL)
                            Console.WriteLine("Found libssl");
                        if (hasOpenSSL1)
                            Console.WriteLine("Found libssl 1.x");
                        if (hasOpenSSL3)
                            Console.WriteLine("Found libssl 3.x");
                        if (!hasLibSSL && !hasOpenSSL1 && !hasOpenSSL3)
                            Console.WriteLine("WARNING: Didn't find any openssl. Loading certs may fail.");
                        #endif

                        if (hasOpenSSL1)
                        {
                            variant = "-openssl1";
                            #if DEBUG
                            Console.WriteLine("Using variant openssl1");
                            #endif
                        }
                    }

                    while (true)
                    {
                        // for unix, we try linux first and if that doesn't work it's probably macos
                        string ext = (platformId == 6) ? ".dylib" : ".so";
                        string platform = (platformId == 6) ? "macos" : "linux";
                        string dllPath = DllDirectory.Current;
                        if (dllPath == null)
                        {
                            dllPath  = "";
                        }
                        else if (dllPath != "")
                        {
                            dllPath += "/";
                        }
                        dllPath += DLL_NAME + variant + "-" + platform + "-" + arch + ext;

                        _dll = dl.Open(dllPath, 0x2);
                        if (_dll == IntPtr.Zero)
                        {
                            if (attemptedPaths.Length > 0)
                            {
                                attemptedPaths += ", ";
                            }
                            attemptedPaths += dllPath;

                            if (variant != "")
                            {
                                variant = "";
                                continue;
                            }
                            if (platformId != 6)
                            {
                                platformId = 6; // MacOSX
                                variant = ""; // there is currently only 1 variant on macos
                                continue;
                            }

                            throw new DllNotFoundException(attemptedPaths);
                        }

                        wspp_new = (Wspp_new)Marshal.GetDelegateForFunctionPointer(dl.Sym(_dll, "wspp_new"), typeof(Wspp_new));
                        wspp_delete = (Wspp_delete)Marshal.GetDelegateForFunctionPointer(dl.Sym(_dll, "wspp_delete"), typeof(Wspp_delete));
                        wspp_poll = (Wspp_poll)Marshal.GetDelegateForFunctionPointer(dl.Sym(_dll, "wspp_poll"), typeof(Wspp_poll));
                        wspp_run = (Wspp_run)Marshal.GetDelegateForFunctionPointer(dl.Sym(_dll, "wspp_run"), typeof(Wspp_run));
                        wspp_stopped = (Wspp_stopped)Marshal.GetDelegateForFunctionPointer(dl.Sym(_dll, "wspp_stopped"), typeof(Wspp_stopped));
                        wspp_connect = (Wspp_connect)Marshal.GetDelegateForFunctionPointer(dl.Sym(_dll, "wspp_connect"), typeof(Wspp_connect));
                        wspp_close = (Wspp_close)Marshal.GetDelegateForFunctionPointer(dl.Sym(_dll, "wspp_close"), typeof(Wspp_close));
                        wspp_send_text = (Wspp_send_text)Marshal.GetDelegateForFunctionPointer(dl.Sym(_dll, "wspp_send_text"), typeof(Wspp_send_text));
                        wspp_send_binary = (Wspp_send_binary)Marshal.GetDelegateForFunctionPointer(dl.Sym(_dll, "wspp_send_binary"), typeof(Wspp_send_binary));
                        wspp_ping = (Wspp_ping)Marshal.GetDelegateForFunctionPointer(dl.Sym(_dll, "wspp_ping"), typeof(Wspp_ping));
                        wspp_set_open_handler = (Wspp_set_open_handler)Marshal.GetDelegateForFunctionPointer(dl.Sym(_dll, "wspp_set_open_handler"), typeof(Wspp_set_open_handler));
                        wspp_set_close_handler = (Wspp_set_close_handler)Marshal.GetDelegateForFunctionPointer(dl.Sym(_dll, "wspp_set_close_handler"), typeof(Wspp_set_close_handler));
                        wspp_set_error_handler = (Wspp_set_error_handler)Marshal.GetDelegateForFunctionPointer(dl.Sym(_dll, "wspp_set_error_handler"), typeof(Wspp_set_error_handler));
                        wspp_set_message_handler = (Wspp_set_message_handler)Marshal.GetDelegateForFunctionPointer(dl.Sym(_dll, "wspp_set_message_handler"), typeof(Wspp_set_message_handler));
                        wspp_set_pong_handler = (Wspp_set_pong_handler)Marshal.GetDelegateForFunctionPointer(dl.Sym(_dll, "wspp_set_pong_handler"), typeof(Wspp_set_pong_handler));

                        if (wspp_run == null || wspp_new == null)
                        {
                            throw new ArgumentException("Wrong or incompatible " + DLL_NAME);
                        }
                        break;
                    }
                }
            }

            IntPtr uriUTF8 = Native.StringToHGlobalUTF8(uriString);
            try {
                _ws = wspp_new(uriUTF8);
            } finally {
                Marshal.FreeHGlobal(uriUTF8);
            }
        }

        private void validate()
        {
            if (_ws == UIntPtr.Zero)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        public void set_open_handler(OnOpenHandler callback)
        {
            _openHandler = null;
            if (callback != null)
            {
                _openHandler = delegate {
                    callback();
                };
            }
            wspp_set_open_handler(_ws, _openHandler);
        }

        public void set_close_handler(OnCloseHandler callback)
        {
            _closeHandler = null;
            if (callback != null)
            {
                _closeHandler = delegate {
                    callback();
                };
            }
            wspp_set_close_handler(_ws, _closeHandler);
        }

        public void set_error_handler(OnErrorHandler callback)
        {
            _errorHandler = null;
            if (callback != null)
            {
                _errorHandler = delegate (IntPtr data) {
                    if (_ws == UIntPtr.Zero)
                        return;

                    callback(Native.ToString(data, "Unknown"));
                };
            }
            wspp_set_error_handler(_ws, _errorHandler);
        }

        public void set_message_handler(OnMessageHandler callback)
        {
            _messageHandler = null;
            if (callback != null)
            {
                _messageHandler = delegate (IntPtr data, ulong len, int opCode) {
                    if (_ws == UIntPtr.Zero)
                        return;

                    if (len > Int32.MaxValue)
                        return;

                    callback(Native.ToByteArray(data, (int)len), opCode);
                };
            }
            wspp_set_message_handler(_ws, _messageHandler);
        }

        public void set_pong_handler(OnPongHandler callback)
        {
            _pongHandler = null;
            if (callback != null)
            {
                _pongHandler = delegate (IntPtr data, ulong len) {
                    if (_ws == UIntPtr.Zero)
                        return;

                    if (len > Int32.MaxValue)
                        return;

                    callback(Native.ToByteArray(data, (int)len));
                };
            }
            wspp_set_pong_handler(_ws, _pongHandler);
        }

        public WsppRes connect()
        {
            validate();
            return (WsppRes) wspp_connect(_ws);
        }

        public void delete()
        {
            validate();
            // TODO: finalizer/Dispose
            wspp_delete(_ws);
            _ws = UIntPtr.Zero;
        }

        public WsppRes close(ushort code, string reason)
        {
            validate();
            IntPtr reasonUTF8 = Native.StringToHGlobalUTF8(reason);
            var res = (WsppRes) wspp_close(_ws, code, reasonUTF8);
            Marshal.FreeHGlobal(reasonUTF8);
            return res;
        }

        public WsppRes send(string message)
        {
            validate();
            IntPtr p = Native.StringToHGlobalUTF8(message);
            var res = (WsppRes) wspp_send_text(_ws, p);
            Marshal.FreeHGlobal(p);
            return res;
        }

        public WsppRes send(byte[] data)
        {
            validate();
            return (WsppRes) wspp_send_binary(_ws, data, (ulong)data.Length);
        }

        public WsppRes ping(byte[] data)
        {
            validate();
            return (WsppRes) wspp_ping(_ws, data, (ulong)data.Length);
        }

        public ulong poll()
        {
            validate();
            return wspp_poll(_ws);
        }

        public bool stopped()
        {
            validate();
            return wspp_stopped(_ws);
        }

        public void clear_handlers()
        {
            // clear native callbacks
            set_open_handler(null);
            set_close_handler(null);
            set_error_handler(null);
            set_message_handler(null);
            set_pong_handler(null);
        }
    }
}

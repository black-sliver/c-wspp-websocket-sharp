using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace WebSocketSharp
{
    /// <summary>
    /// Wrapper for native c-wspp on Non-Windows.
    /// </summary>
    internal class WSPPWin32 : IWSPP
    {
#if WIN32_C_WSPP_CALLING_CONVENTION_CDECL
        internal const CallingConvention CALLING_CONVENTION = CallingConvention.Cdecl;
#else
        internal const CallingConvention CALLING_CONVENTION = CallingConvention.Winapi;
#endif

        internal const string DLL_NAME = "c-wspp-win32.dll";

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

        [DllImport(DLL_NAME, CharSet=CharSet.Ansi, CallingConvention=CALLING_CONVENTION)]
        internal static extern UIntPtr wspp_new(IntPtr uri);
        [DllImport(DLL_NAME, CallingConvention=CALLING_CONVENTION)]
        internal static extern void wspp_delete(UIntPtr ws);
        [DllImport(DLL_NAME, CallingConvention=CALLING_CONVENTION)]
        internal static extern ulong wspp_poll(UIntPtr ws);
        [DllImport(DLL_NAME, CallingConvention=CALLING_CONVENTION)]
        internal static extern ulong wspp_run(UIntPtr ws);
        [DllImport(DLL_NAME, CallingConvention=CALLING_CONVENTION)]
        internal static extern bool wspp_stopped(UIntPtr ws);
        [DllImport(DLL_NAME, CallingConvention=CALLING_CONVENTION)]
        internal static extern int wspp_connect(UIntPtr ws);
        [DllImport(DLL_NAME, CallingConvention=CALLING_CONVENTION)]
        internal static extern int wspp_close(UIntPtr ws, ushort code, IntPtr reason);
        [DllImport(DLL_NAME, CallingConvention=CALLING_CONVENTION)]
        internal static extern int wspp_send_text(UIntPtr ws, IntPtr message);
        [DllImport(DLL_NAME, CallingConvention=CALLING_CONVENTION)]
        internal static extern int wspp_send_binary(UIntPtr ws, byte[] data, ulong len);
        [DllImport(DLL_NAME, CallingConvention=CALLING_CONVENTION)]
        internal static extern int wspp_ping(UIntPtr ws, byte[] data, ulong len);
        [DllImport(DLL_NAME, CallingConvention=CALLING_CONVENTION)]
        internal static extern void wspp_set_open_handler(UIntPtr ws, OnOpenCallback f);
        [DllImport(DLL_NAME, CallingConvention=CALLING_CONVENTION)]
        internal static extern void wspp_set_close_handler(UIntPtr ws, OnCloseCallback f);
        [DllImport(DLL_NAME, CallingConvention=CALLING_CONVENTION)]
        internal static extern void wspp_set_error_handler(UIntPtr ws, OnErrorCallback f);
        [DllImport(DLL_NAME, CallingConvention=CALLING_CONVENTION)]
        internal static extern void wspp_set_message_handler(UIntPtr ws, OnMessageCallback f);
        [DllImport(DLL_NAME, CallingConvention=CALLING_CONVENTION)]
        internal static extern void wspp_set_pong_handler(UIntPtr ws, OnPongCallback f);

        UIntPtr _ws;

#pragma warning disable 0414
        OnOpenCallback _openHandler;
        OnCloseCallback _closeHandler;
        OnErrorCallback _errorHandler;
        OnMessageCallback _messageHandler;
        OnPongCallback _pongHandler;
#pragma warning disable 0414

        static public bool IsActivePlatform()
        {
            int platformId = (int)Environment.OSVersion.Platform;
            return (platformId < 4 || platformId == 5) && IntPtr.Size == 4;
        }

        public WSPPWin32(string uriString)
        {
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

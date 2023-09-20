// Wrapper for the native code/lib

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace WebSocketSharp
{
    internal enum WsppRes
    {
        OK = 0,
        InvalidState = 1,
        Unknown = -1,
    }

    public partial class WebSocket : IDisposable
    {
        private UIntPtr ws;
        private OnMessageCallback messageHandler;
        private OnOpenCallback openHandler;
        private OnCloseCallback closeHandler;
        private OnErrorCallback errorHandler;
        private OnPongCallback pongHandler;

    #if C_WSPP_CALLING_CONVENTION_CDECL
        internal const CallingConvention CALLING_CONVENTION = CallingConvention.Cdecl;
    #else
        internal const CallingConvention CALLING_CONVENTION = CallingConvention.Winapi;
    #endif

    #if OS_WINDOWS
        internal const string DLL_NAME = "c-wspp.dll";
    #elif OS_MAC
    #   error "Not implemented"
    #else
        internal const string DLL_NAME = "c-wspp.so";
    #endif

        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate void OnMessageCallback(IntPtr data, ulong len, int opCode);
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate void OnOpenCallback();
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate void OnCloseCallback(); // TODO: code, reason
        [UnmanagedFunctionPointer(CALLING_CONVENTION)]
        internal delegate void OnErrorCallback(); // TODO: message, errorCode
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
        internal static extern void wspp_set_message_handler(UIntPtr ws, OnMessageCallback f);
        [DllImport(DLL_NAME, CallingConvention=CALLING_CONVENTION)]
        internal static extern void wspp_set_error_handler(UIntPtr ws, OnErrorCallback f);
        [DllImport(DLL_NAME, CallingConvention=CALLING_CONVENTION)]
        internal static extern void wspp_set_pong_handler(UIntPtr ws, OnPongCallback f);

        // NOTE: currently we do string -> UTF8 in C#, but it might be better to change that.
        internal static IntPtr StringToHGlobalUTF8(string s, out int length)
        {
            if (s == null)
            {
                length = 0;
                return IntPtr.Zero;
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(s);
            var ptr = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            Marshal.WriteByte(ptr, bytes.Length, 0);
            length = bytes.Length;

            return ptr;
        }

        internal static IntPtr StringToHGlobalUTF8(string s)
        {
            int temp;
            return StringToHGlobalUTF8(s, out temp);
        }

        internal bool sequenceEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) {
                return false;
            }
            for (int i=0; i<a.Length; i++) {
                if (a[i] != b[i]) {
                    return false;
                }
            }
            return true;
        }

        private void OpenHandler()
        {
            // ignore events that happen during shutdown of the socket
            if (ws == UIntPtr.Zero)
                return;

            Console.WriteLine("open");
            state = State.Open;
            EventArgs e = new EventArgs();
            // FIXME: on .net >=4.0 we could use an async task to fire from main thread
            dispatcher.Enqueue(e);
        }

        private void CloseHandler()
        {
            Console.WriteLine("close");
            // ignore events that happen during shutdown of the socket
            if (ws == UIntPtr.Zero)
                return;

            CloseEventArgs e = new CloseEventArgs(0, ""); // TODO: code and reason
            // FIXME: on .net >=4.0 we could use an async task to fire from main thread
            dispatcher.Enqueue(e);
        }

        private void MessageHandler(IntPtr data, ulong len, int opCode)
        {
            // ignore events that happen during shutdown of the socket
            if (ws == UIntPtr.Zero)
                return;

            Console.WriteLine("message");
            if (len > Int32.MaxValue) {
                error("Received message that was too long");
                return;
            }
            byte[] bytes = new byte[(int)len];
            Marshal.Copy(data, bytes, 0, (int)len);
            MessageEventArgs e = new MessageEventArgs(bytes, (OpCode)opCode);
            // FIXME: on .net >=4.0 we could use an async task to fire from main thread
            dispatcher.Enqueue(e);
        }

        private void ErrorHandler()
        {
            // ignore events that happen during shutdown of the socket
            if (ws == UIntPtr.Zero)
                return;

            Console.WriteLine("error");

            if (state == State.Connecting) {
                // no need to close
                state = State.Disconnected;
            } else if (state == State.Open) {
                // this should never happen since we throw all exceptions in-line
                Close();
            }
            error("Connect error");
        }

        private void PongHandler(IntPtr data, ulong len)
        {
            Console.WriteLine("pong");
            byte[] bytes = new byte[(int)len];
            Marshal.Copy(data, bytes, 0, (int)len);

            // look for internal ping
            lock (pings)
            {
                foreach (byte[] b in pings) {
                    if (sequenceEqual(bytes, b)) {
                        pings.Remove(b);
                        Console.WriteLine("internal ping");
                        return;
                    }
                }
            }

            // emit event for external ping
            MessageEventArgs e = new MessageEventArgs(bytes, OpCode.Pong);
            // FIXME: on .net >=4.0 we could use an async task to fire from main thread
            dispatcher.Enqueue(e);
        }

        /// <summary>
        /// wspp_new with string -> utf8 conversion.
        /// Has its own function scope to ensure DllDirectory.Set is run before wspp_new.
        /// </summary>
        static private UIntPtr wspp_new(string uriString)
        {
            IntPtr uriUTF8 = StringToHGlobalUTF8(uriString);
            try {
                return wspp_new(uriUTF8);
            } finally {
                Marshal.FreeHGlobal(uriUTF8);
            }
        }

        /// <summary>
        /// Create new native wspp websocket with DLL from dllDirectory
        /// </summary>
        static private UIntPtr wspp_new_from(string uriString, string dllDirectory)
        {
            using (DllDirectory.Context(dllDirectory))
            {
                return wspp_new(uriString);
            }
        }

        static internal string directory {
            get {
                return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            }
        }

        static private void close(UIntPtr ws, ushort code, string reason)
        {
            IntPtr reasonUTF8 = StringToHGlobalUTF8(reason);
            wspp_close(ws, code, reasonUTF8);
            Marshal.FreeHGlobal(reasonUTF8);
        }

        private void setHandlers()
        {
            openHandler = new OnOpenCallback(OpenHandler);
            closeHandler = new OnCloseCallback(CloseHandler);
            messageHandler = new OnMessageCallback(MessageHandler);
            errorHandler = new OnErrorCallback(ErrorHandler);
            pongHandler = new OnPongCallback(PongHandler);

            wspp_set_open_handler(ws, openHandler);
            wspp_set_close_handler(ws, closeHandler);
            wspp_set_message_handler(ws, messageHandler);
            wspp_set_error_handler(ws, errorHandler);
            wspp_set_pong_handler(ws, pongHandler);
        }

        private void clearHandlers()
        {
            wspp_set_open_handler(ws, null);
            wspp_set_close_handler(ws, null);
            wspp_set_message_handler(ws, null);
            wspp_set_error_handler(ws, null);
            wspp_set_pong_handler(ws, null);
        }
    }
}


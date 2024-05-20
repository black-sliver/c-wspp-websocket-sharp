namespace WebSocketSharp
{
    // callback signatures in managed code
    internal delegate void OnOpenHandler();
    internal delegate void OnCloseHandler(); // TODO: code, reason
    internal delegate void OnErrorHandler(string msg); // TODO: errorCode
    internal delegate void OnMessageHandler(byte[] data, int opCode);
    internal delegate void OnPongHandler(byte[] data);

    /// <summary>
    /// c-wspp native call restult codes
    /// </summary>
    internal enum WsppRes
    {
        OK = 0,
        InvalidState = 1,
        Unknown = -1,
    }

    /// <summary>
    /// Abstract native interface. For Windows and Non-Windows implementations.
    /// </summary>
    internal interface IWSPP
    {
        WsppRes connect();
        void delete();
        WsppRes close(ushort code, string reason);
        WsppRes send(string message);
        WsppRes send(byte[] data);
        WsppRes ping(byte[] data);
        ulong poll();
        bool stopped();

        void set_open_handler(OnOpenHandler callback);
        void set_close_handler(OnCloseHandler callback);
        void set_error_handler(OnErrorHandler callback);
        void set_message_handler(OnMessageHandler callback);
        void set_pong_handler(OnPongHandler callback);

        /// <summary>
        /// Helper to set all handlers to null during shutdown.
        /// </summary>
        void clear_handlers();
    }
}

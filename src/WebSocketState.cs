// This should not be required, but someone might accidentally use this

namespace WebSocketSharp
{
    public enum WebSocketState : ushort
    {
        New = 0,
        Connecting = 1,
        Open = 2,
        Closing = 3,
        Closed = 4
    }
}

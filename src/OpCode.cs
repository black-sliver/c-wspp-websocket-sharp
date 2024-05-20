namespace WebSocketSharp
{
    internal enum OpCode
    {
        Text = 0x1,
        Binary = 0x2,
        Ping = 0x8,
        Pong = 0xA,
    }
}

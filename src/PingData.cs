// helper to keep track of ongoing internal pings

using System;
using System.Threading;

namespace WebSocketSharp
{
    internal class PingData {
        public byte[] data;
        public EventWaitHandle waitHandle;

        public PingData(byte[] data, EventWaitHandle waitHandle)
        {
            this.data = data;
            this.waitHandle = waitHandle;
        }
    }
}

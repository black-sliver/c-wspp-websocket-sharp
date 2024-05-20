using System;

namespace WebSocketSharp
{
    internal class WSPPFactory
    {
        public static IWSPP Create(string uri)
        {
            if (WSPPWin32.IsActivePlatform())
            {
                return new WSPPWin32(uri);
            }
            if (WSPPWin64.IsActivePlatform())
            {
                return new WSPPWin64(uri);
            }
            if (WSPPNix.IsActivePlatform())
            {
                return new WSPPNix(uri);
            }
            throw new PlatformNotSupportedException();
        }
    }
}

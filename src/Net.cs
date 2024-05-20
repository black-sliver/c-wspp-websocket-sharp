// Fake WebSocketSharp.Net to satisfy calls from MultiClient.Net

using System.Security.Authentication;

namespace WebSocketSharp
{
    namespace Net
    {
        public class ClientSslConfiguration
        {
            public SslProtocols EnabledSslProtocols
            {
                get { return 0; }
                set { }
            }
        }
    }
}

# c-wspp websocket-sharp

Fake websocket-sharp.dll that partially implements the API of
[websocket-sharp](https://github.com/sta/websocket-sharp),
but uses a native lib
[c-wspp](https://github.com/black-sliver/c-wspp),
which is a wrapper around
[WebSocket++](https://github.com/zaphoyd/websocketpp.git)
to get latest SSL/TLS support into builds that depend on or embed older Mono.

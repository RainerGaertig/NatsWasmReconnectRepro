# NATS.Net: Blazor WebAssembly freezes when a WebSocket connection is closed

Minimal repro for a freeze of the single browser thread in Blazor WebAssembly when a
`NatsConnection` over WebSocket is closed while its read loop is running.

## Versions
- NATS.Net 3.3.0
- .NET 10 (Blazor WebAssembly standalone, single-threaded runtime)
- nats-server 2.x with WebSocket enabled

## Run
1. Start a NATS server with WebSocket on port 8080:
	 ```
	 nats-server -c nats-server.conf
	 ```
	 or with Docker:
	 ```
	 docker run --rm -p 4222:4222 -p 8080:8080 -v ${PWD}/nats-server.conf:/nats.conf nats:latest -c /nats.conf
	 ```
2. Start the app:
	 ```
	 dotnet run --project src/NatsWasmRepro
	 ```
3. Open the printed URL in Chrome or Edge, and open the browser dev tools (console).

## Reproduce
1. Click **1. Connect + subscribe**. The app subscribes to `repro.ping` and publishes to it
	 every 20 ms, so the read loop is always receiving data.
2. Click **3a. Run 2a 20x** (`NatsConnection.ReconnectAsync()` in a loop), or
	 **3b. Run 2b 20x** (new connection + `DisposeAsync()` of the old one in a loop).
3. Watch the **Heartbeat** counter. It is incremented by a JS timer every 250 ms.

**Expected:** all iterations complete, the heartbeat keeps counting.

**Actual:** after one or a few iterations the heartbeat stops and the tab no longer responds.
The browser shows "Page unresponsive" after a while. When the debugger is attached, the only
thread is in one of:

```
System.Threading.SemaphoreSlim.Wait
NATS.Client.Core.Internal.SocketConnectionWrapper.SignalDisconnected
NATS.Client.Core.Internal.SocketReader.ReadAtLeastAsync
NATS.Client.Core.Internal.NatsReadProtocolProcessor.ReadLoopAsync
NATS.Client.Core.Internal.WebSocketConnection.ReceiveAsync
System.Net.WebSockets.BrowserWebSocket.ReceiveAsyncCore
```

```
ExceptionDispatchInfo.Throw
ValueTask<ValueWebSocketReceiveResult>.Result.get
NATS.Client.Core.Internal.WebSocketConnection.ReceiveAsync
NATS.Client.Core.Internal.SocketConnectionWrapper.ReceiveAsync
NATS.Client.Core.Internal.SocketReader.ReadAtLeastAsync
NATS.Client.Core.Internal.NatsReadProtocolProcessor.ReadLoopAsync
```

The timing varies: sometimes several iterations pass before it freezes. The same code
does not freeze on multi-threaded hosts (console app, ASP.NET Core).

## Suspected cause
- `SocketConnectionWrapper.DisposeAsync()` holds `_sem` across the WebSocket close
	handshake, which is always asynchronous in the browser.
- The close makes the pending receive in the read loop fail, and the read loop calls
	`SocketConnectionWrapper.SignalDisconnected()`, which calls the synchronous `_sem.Wait()`.
- On the single browser thread nothing can release `_sem`. If the wait throws instead of
	blocking, `NatsReadProtocolProcessor.ReadLoopAsync` catches it in `catch (Exception) { continue; }`
	and receives again on the closed socket, which fails immediately: a loop that never yields.

# Remote control WebSocket API

HandsLifted runs a small WebSocket server so another program on the same PC (a clicker redirector such as ClickerFixer, a Stream Deck script, a test harness) can move the live presentation forward and back.

It is one-way and fire-and-forget: you send a JSON message, the app acts on it, nothing is sent back.

Source: `HandsLiftedApp.Core/Services/RemoteControl/` (`RemoteControlServer.cs`, `RemoteControlMessageHandler.cs`). Design history: `docs/superpowers/specs/2026-09-21-remote-control-websocket-design.md`.

## Quick reference

| | |
|---|---|
| Transport | WebSocket (`ws://`, no TLS) |
| URL | `ws://127.0.0.1:8979/` |
| Bind address | `127.0.0.1` only (not reachable from the LAN) |
| Port | `8979`, fixed, not configurable |
| Auth | none |
| Subprotocol / handshake | none |
| Direction | client to server only; the server never replies |
| Library | WatsonWebsocket (`WatsonWsServer`) |

The path is not routed. `ws://127.0.0.1:8979/` is the documented URL.

## Message format

Send one JSON object per WebSocket message (text frame):

```json
{ "action": "NextSlide" }
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `action` | string | yes | One of the actions below. Case-sensitive, exact match. |

Other fields in the object are ignored.

### Actions

| `action` | Effect |
|---|---|
| `NextSlide` | Same as pressing Right / Page Down in the main window: advance to the next slide. |
| `PreviousSlide` | Same as pressing Left / Page Up: go back one slide. |
| `GotoLogo` | Show the logo (same as F12). Idempotent: sending it while the logo is already up leaves it up. |
| `GotoBlank` | Show a blank screen (same as F1). Idempotent. |
| `GotoSlides` | Return to the current slide: clears Logo, Blank and QuickShow. No-op if slides are already showing. |

All of them publish `ActionMessage { Action = ... }` on `MessageBus.Current`, the same message the keyboard shortcuts publish, so behaviour (crossing item boundaries, group handling, and so on) is identical to a keypress. `NextSlide` / `PreviousSlide` also scroll the playlist so the selected slide is in view, like the keyboard and the on-screen buttons.

Logo and Blank are states, not toggles: there is no "toggle" action, because the server never reports its state back and a toggle would drift out of sync. To leave Logo or Blank, send `GotoSlides`. `NextSlide` and `PreviousSlide` also leave Logo or Blank, but they only restore the current slide and do not advance, so a stray press while the screen is blank cannot skip a slide.

## Responses and errors

There are no responses. In particular there is no ack, no error payload, and no close frame for bad input.

| Situation | What happens |
|---|---|
| Valid action | Performed. Nothing sent back. |
| Unknown action string | Ignored. Warning logged (`unrecognized action "..."`). Connection stays open. |
| JSON object without `action` | Ignored. Warning logged. Connection stays open. |
| Not valid JSON | Ignored. Warning logged. Connection stays open. |
| Exception while handling | Caught and logged (`Error`). Connection stays open. |

A client that needs to know whether the action landed has to watch its own downstream effect (for example, the slide changing).

Messages are handled on the UI thread (posted via `Dispatcher.UIThread`), in the order received on a connection.

## Connection lifecycle

- No handshake or login message. Connect, send actions whenever needed.
- The client is responsible for reconnecting if the connection drops (for example when HandsLifted restarts).
- If HandsLifted is not running, the connection is refused.

## When the server is NOT running

- **Environment variable `HANDSLIFTED_NO_REMOTE_CONTROL`** set to any non-empty value: the server is not created. Log line: `Remote control server disabled by HANDSLIFTED_NO_REMOTE_CONTROL`. `tools\devtools.ps1 start` sets this by default (pass `-RemoteControl` to keep the server on), because the listener triggers a Windows Firewall prompt on a fresh build.
- **Port 8979 already in use** (typically a second HandsLifted instance): bind fails, the app logs a warning (`RemoteControlServer failed to bind to port 8979 - remote control will be unavailable this session`) and keeps running. The second instance has no remote control.
- Server starts during `Globals.OnStartup` and is disposed in `Globals.OnShutdown`.

To check the listener from PowerShell:

```powershell
Get-NetTCPConnection -LocalPort 8979 -State Listen
```

## Examples

Examples below were written from the source and have not been run against a live build.

### wscat

```powershell
npx wscat -c ws://127.0.0.1:8979/
> {"action":"NextSlide"}
> {"action":"PreviousSlide"}
```

### PowerShell

```powershell
$ws = [System.Net.WebSockets.ClientWebSocket]::new()
$ws.ConnectAsync([Uri]'ws://127.0.0.1:8979/', [Threading.CancellationToken]::None).Wait()

$bytes = [Text.Encoding]::UTF8.GetBytes('{"action":"NextSlide"}')
$ws.SendAsync([ArraySegment[byte]]::new($bytes),
              [System.Net.WebSockets.WebSocketMessageType]::Text,
              $true,
              [Threading.CancellationToken]::None).Wait()

$ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'done',
               [Threading.CancellationToken]::None).Wait()
```

### Python

```python
# pip install websockets
import asyncio, json, websockets

async def main():
    async with websockets.connect("ws://127.0.0.1:8979/") as ws:
        await ws.send(json.dumps({"action": "NextSlide"}))

asyncio.run(main())
```

### C# (`System.Net.WebSockets`)

```csharp
using var ws = new ClientWebSocket();
await ws.ConnectAsync(new Uri("ws://127.0.0.1:8979/"), CancellationToken.None);
var payload = Encoding.UTF8.GetBytes("""{"action":"NextSlide"}""");
await ws.SendAsync(payload, WebSocketMessageType.Text, true, CancellationToken.None);
```

## Security notes

- Loopback only, so other machines cannot connect.
- There is no authentication, and the server does not check the `Origin` header. Any process on the PC can send actions, and a web page open in a browser on the same PC can also open `ws://127.0.0.1:8979/` (browsers do not apply CORS to WebSockets). The only available actions are next/previous slide, so the impact is limited to a stray slide change during a service.
- No encryption; not intended to be exposed beyond localhost. Do not forward the port.

## Extending

To add an action:

1. Add a member to `ActionMessage.NavigateSlideAction` and handle it in the `ActionMessage` switch in `PlaylistInstance` (if it is not already handled there).
2. Add the case to the `switch` in `RemoteControlMessageHandler.ParseAction`. The string should match the enum member name.
3. Add a test in `HandsLiftedApp.Tests/Services/RemoteControl/RemoteControlMessageHandlerTests.cs`.
4. Update the Actions table above.

New actions are additive: clients on the old protocol keep working, and older servers ignore actions they do not know without closing the connection.

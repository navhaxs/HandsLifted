# Remote control WebSocket API — design

Date: 2026-09-21

## Problem

HandsLifted has no way for an external process to trigger slide navigation. `ClickerFixer` (a sibling project — a presentation-clicker redirector) has an `IClickerTarget` implementation for HandsLifted (`VisionScreens.cs`, named after HandsLifted's product name) left over from an earlier proof of concept: `IsActive()` correctly detects the `handsliftedapp` process, but `SendNext()`/`SendPrevious()` enqueue messages to a worker loop whose body is entirely commented out — nothing is actually sent. There is no server on the HandsLifted side to receive it anyway.

This spec defines a minimal remote-control protocol HandsLifted exposes, so ClickerFixer (or any future local remote) can drive slide navigation. Only the HandsLifted-side server is in scope for implementation here; reactivating ClickerFixer's client is a documented follow-on (see "Out of scope").

## Goals

- A local process can trigger `NextSlide` / `PreviousSlide` in a running HandsLifted instance.
- Minimal surface: reuse HandsLifted's existing slide-navigation message pipeline rather than inventing a parallel one.
- No new trust boundary: this is a same-machine integration (ClickerFixer.Desktop and HandsLifted both run on the presenter PC), so no auth, no LAN exposure.

## Non-goals (out of scope for this spec)

- Reactivating `ClickerFixer.Desktop/ClickerTargets/VisionScreens.cs` (the client side). Documented under "Client side (follow-on)" below so the contract is known, but not implemented here.
- `GotoBlank` / `GotoLogo` actions — `ActionMessage.NavigateSlideAction` already has these, but v1 only exposes Next/Previous (matches `IClickerTarget`'s `SendNext`/`SendPrevious`, all a physical clicker needs). Adding the other two later is additive — same message shape, new allowed `action` strings — not a breaking change.
- Settings UI / configurable port. Fixed port, always-on for v1.
- Any response/ack from server to client. Fire-and-forget.
- LAN or remote access, authentication, encryption.

## Protocol

- **Transport**: WebSocket.
- **Endpoint**: `ws://127.0.0.1:8979/remote`. Port matches ClickerFixer's existing `VisionScreensConfig.Port` default (already `8979` in that codebase) — no config change needed on the client side.
- **Bind address**: `127.0.0.1` only. Not `0.0.0.0` — this is a same-machine integration, not a network service.
- **Message format**: JSON, one field:
  ```json
  { "action": "NextSlide" }
  ```
  ```json
  { "action": "PreviousSlide" }
  ```
  `action` values are the exact names of `HandsLiftedApp.Core.Models.AppState.ActionMessage.NavigateSlideAction` members used in v1 (`NextSlide`, `PreviousSlide`) — not a separate wire vocabulary, so server and future clients can't drift from the enum.
- **Response**: none. No ack, no error payload. A client that cares whether the action landed watches its own downstream effect (e.g. ClickerFixer's ripple/log UI already fires on a successful `SendNext()` call, not on a server reply).
- **Malformed / unknown action**: server logs and ignores the message. Does not close the connection — a client on a future protocol version sending an action this server doesn't know yet should not get disconnected for it.
- **Connection lifecycle**: no handshake/auth message. A client connects, sends action messages whenever a button is pressed, reconnects on drop (client's responsibility, mirrors ClickerFixer's existing `WebsocketClient` usage for ProPresenter).

## Server implementation (HandsLifted side)

- **New file**: `HandsLiftedApp.Core/Services/RemoteControl/RemoteControlServer.cs`. Lives in `HandsLiftedApp.Core` (not a new project) specifically so it has direct access to the `internal` `ActionMessage` class/enum without changing its visibility or adding `InternalsVisibleTo`.
- **Library**: `WatsonWebsocket` (new package reference for `HandsLiftedApp.Core.csproj`). Not previously used in this repo, but already proven in the ClickerFixer repo — `ClickerFixer.Satellite/Services/MyWebServer.cs` is the reference pattern: construct `WatsonWsServer(bindAddress, port)`, subscribe `MessageReceived`, `Start()`.
- **Dispatch**: on `MessageReceived`, deserialize `{ "action": string }`, map the string to `ActionMessage.NavigateSlideAction` via a `switch`, and call:
  ```csharp
  MessageBus.Current.SendMessage(new ActionMessage { Action = mappedAction });
  ```
  This is the identical call `KeyboardSlideNavigation.OnKeyDown` already makes for `Key.Right`/`Key.PageDown` etc. — the remote-control path rides the same pipeline as a physical keypress, so there is exactly one place slide-navigation side effects live. (`KeyboardSlideNavigation` also sends a `FocusSelectedItem` message alongside `ActionMessage` for keyboard-specific UI focus; the remote-control path does not send this, since there's no keyboard focus context to restore.)
- **Startup**: constructed and started once in `HandsLiftedApp.Core/App.axaml.cs`'s `OnFrameworkInitializationCompleted`, alongside other app-wide singleton init. Always-on for v1 — no settings toggle.
- **Bind failure** (port already in use — realistically only a second HandsLifted instance on the same machine): catch, log a warning, continue app startup. Must not crash the app. Same defensive posture as `ClickerFixer.Satellite`'s `MyLogServer` (see that repo's CLAUDE.md: "harden against unguarded bind failure").
- **Shutdown**: dispose the `WatsonWsServer` on app exit (wherever other app-wide singletons are torn down, if anywhere — if there's no existing app-exit teardown hook, this is a new one and should be minimal).

## Client side (follow-on, not implemented in this task)

For context, so the contract above is exercised correctly whenever this is picked up:

- `ClickerFixer.Desktop/ClickerTargets/VisionScreens.cs`'s `WebsocketWorkerLoop` body is fully commented out (leftover decompiled POC). Reactivating it means replacing that commented block with the same live pattern `ProPresenter.cs`'s `WebsocketWorkerLoop` already uses (same `Websocket.Client` library, same task-queue/worker-thread structure) — connect to `ws://localhost:{Global.Config.TargetsConfig.VisionScreensConfig.Port}/remote`, and change `SendNext()`/`SendPrevious()`'s JSON payloads from the current no-op stub to `{"action":"NextSlide"}` / `{"action":"PreviousSlide"}`.
- `IClickerTarget.IsActive()` (`Process.GetProcessesByName("handsliftedapp").Length != 0`) already works today and needs no change.
- No ClickerFixer config changes needed — `VisionScreensConfig.Port` already defaults to `8979`.

## Testing

- **Unit**: a test around the message-parsing/dispatch function in isolation — feed a JSON string in, assert the expected `ActionMessage` was published on `MessageBus.Current` (subscribe before sending, or substitute a test scheduler). Does not need to exercise `WatsonWsServer` itself (external library).
- **Manual smoke test**: connect with any WebSocket client (e.g. `wscat -c ws://localhost:8979/remote`), send `{"action":"NextSlide"}`, confirm the live slide advances in a running HandsLifted instance.
- **End-to-end** with a real ClickerFixer client is exercised once the follow-on client work lands — not part of this task's verification.

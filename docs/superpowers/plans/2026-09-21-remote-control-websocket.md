# Remote Control WebSocket Server Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give HandsLifted a loopback WebSocket server that translates `{"action":"NextSlide"}` / `{"action":"PreviousSlide"}` JSON messages into the app's existing slide-navigation pipeline, so an external process (ClickerFixer) can drive slide navigation.

**Architecture:** A `RemoteControlServer` class in `HandsLiftedApp.Core/Services/RemoteControl/` wraps a `WatsonWsServer` bound to `127.0.0.1:8979`. Incoming message bytes are handed to a separate, pure `RemoteControlMessageHandler.Handle(string)` function that parses the JSON and publishes `HandsLiftedApp.Core.Models.AppState.ActionMessage` on `ReactiveUI.MessageBus.Current` — the identical call path `KeyboardSlideNavigation.OnKeyDown` already uses for PageDown/PageUp. Splitting parse/dispatch out from the socket wiring keeps the socket-free logic unit-testable. The server is started/stopped from `Globals.OnStartup`/`OnShutdown`, matching how every other app-wide singleton in this codebase is wired.

**Tech Stack:** C# / .NET 10, `WatsonWebsocket` 4.1.2 (new dependency, already proven in the sibling `ClickerFixer.Satellite` project), `Newtonsoft.Json` (already a dependency of `HandsLiftedApp.Core`), MSTest (existing test framework in `HandsLiftedApp.Tests`).

**Spec:** `docs/superpowers/specs/2026-09-21-remote-control-websocket-design.md`

## Global Constraints

- Endpoint is `ws://127.0.0.1:8979/` — root path, no sub-path, bind address `127.0.0.1` only (never `0.0.0.0`).
- Message schema: JSON object with one field, `{"action": "<name>"}`, where `<name>` is exactly `NextSlide` or `PreviousSlide` (matches `ActionMessage.NavigateSlideAction` member names) for v1.
- No response/ack is ever sent back to the client.
- Malformed or unrecognized messages are logged and ignored — the connection is never closed because of message content.
- The server must never crash the app: a bind failure (e.g. port already in use) is caught, logged as a warning, and the app continues starting up without remote control.
- v1 exposes only `NextSlide`/`PreviousSlide` — do not wire up `GotoBlank`/`GotoLogo` in this plan (out of scope per spec).

---

### Task 1: Add the `WatsonWebsocket` package reference

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`

**Interfaces:**
- Consumes: nothing (first task).
- Produces: `WatsonWebsocket` types (`WatsonWsServer`, `MessageReceivedEventArgs`, `ConnectionEventArgs`) available to `HandsLiftedApp.Core` for Task 2 onward.

This repo uses central package management (`Directory.Packages.props` pins versions; individual `.csproj` files reference packages without a `Version` attribute — see any existing `<PackageReference Include="..."/>` line in `HandsLiftedApp.Core.csproj`).

- [ ] **Step 1: Add the version pin**

In `Directory.Packages.props`, add a line inside the existing `<ItemGroup>` of `<PackageVersion>` entries (alphabetical among the existing entries is fine, doesn't need to be exact):

```xml
<PackageVersion Include="WatsonWebsocket" Version="4.1.2" />
```

- [ ] **Step 2: Add the project reference**

In `HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`, inside the `<ItemGroup>` that lists other `<PackageReference Include="..."/>` entries (the one containing `Serilog`, `Newtonsoft.Json`, etc.), add:

```xml
<PackageReference Include="WatsonWebsocket" />
```

- [ ] **Step 3: Verify it restores and builds**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`
Expected: builds with 0 errors (warnings from existing code are fine — don't fix unrelated warnings).

- [ ] **Step 4: Commit**

```bash
git add Directory.Packages.props HandsLiftedApp.Core/HandsLiftedApp.Core.csproj
git commit -m "build: add WatsonWebsocket dependency for remote control server"
```

---

### Task 2: `RemoteControlMessageHandler` — pure parse/dispatch logic + tests

**Files:**
- Create: `HandsLiftedApp.Core/Services/RemoteControl/RemoteControlMessageHandler.cs`
- Test: `HandsLiftedApp.Tests/Services/RemoteControl/RemoteControlMessageHandlerTests.cs`

**Interfaces:**
- Consumes: `HandsLiftedApp.Core.Models.AppState.ActionMessage` (existing, `internal class` with `public NavigateSlideAction Action { get; set; }` and nested `public enum NavigateSlideAction { NextSlide, PreviousSlide, GotoLogo, GotoBlank }`), `ReactiveUI.MessageBus.Current` (existing, used the same way in `HandsLiftedApp.Core/Controller/KeyboardSlideNavigation.cs`).
- Produces: `internal static class RemoteControlMessageHandler` with `public static void Handle(string json)` — takes a raw JSON message string and publishes an `ActionMessage` on `MessageBus.Current` if it parses to a known v1 action; logs and does nothing otherwise. Task 3 (`RemoteControlServer`) calls `RemoteControlMessageHandler.Handle(json)` directly.

This handler has zero socket dependency — it takes a `string`, does its thing, and can be tested by subscribing to `MessageBus.Current.Listen<ActionMessage>()` before calling `Handle`.

- [ ] **Step 1: Write the failing tests**

Create `HandsLiftedApp.Tests/Services/RemoteControl/RemoteControlMessageHandlerTests.cs`:

```csharp
using HandsLiftedApp.Core.Models.AppState;
using HandsLiftedApp.Core.Services.RemoteControl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReactiveUI;

namespace HandsLiftedApp.Tests.Services.RemoteControl;

[TestClass]
public class RemoteControlMessageHandlerTests
{
    [TestMethod]
    public void Handle_NextSlideAction_PublishesActionMessage()
    {
        ActionMessage? received = null;
        using var subscription = MessageBus.Current.Listen<ActionMessage>()
            .Subscribe(msg => received = msg);

        RemoteControlMessageHandler.Handle("{\"action\":\"NextSlide\"}");

        Assert.IsNotNull(received);
        Assert.AreEqual(ActionMessage.NavigateSlideAction.NextSlide, received!.Action);
    }

    [TestMethod]
    public void Handle_PreviousSlideAction_PublishesActionMessage()
    {
        ActionMessage? received = null;
        using var subscription = MessageBus.Current.Listen<ActionMessage>()
            .Subscribe(msg => received = msg);

        RemoteControlMessageHandler.Handle("{\"action\":\"PreviousSlide\"}");

        Assert.IsNotNull(received);
        Assert.AreEqual(ActionMessage.NavigateSlideAction.PreviousSlide, received!.Action);
    }

    [TestMethod]
    public void Handle_UnknownAction_DoesNotPublish()
    {
        ActionMessage? received = null;
        using var subscription = MessageBus.Current.Listen<ActionMessage>()
            .Subscribe(msg => received = msg);

        RemoteControlMessageHandler.Handle("{\"action\":\"GotoBlank\"}");

        Assert.IsNull(received);
    }

    [TestMethod]
    public void Handle_MalformedJson_DoesNotThrowAndDoesNotPublish()
    {
        ActionMessage? received = null;
        using var subscription = MessageBus.Current.Listen<ActionMessage>()
            .Subscribe(msg => received = msg);

        RemoteControlMessageHandler.Handle("not json");

        Assert.IsNull(received);
    }

    [TestMethod]
    public void Handle_EmptyString_DoesNotThrowAndDoesNotPublish()
    {
        ActionMessage? received = null;
        using var subscription = MessageBus.Current.Listen<ActionMessage>()
            .Subscribe(msg => received = msg);

        RemoteControlMessageHandler.Handle("");

        Assert.IsNull(received);
    }
}
```

Note: `ActionMessage` and `RemoteControlMessageHandler` are both `internal` — this compiles from the test project because `HandsLiftedApp.Core/AssemblyInfo.cs` already has `[assembly: InternalsVisibleTo("HandsLiftedApp.Tests")]`. No new attribute needed.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~RemoteControlMessageHandlerTests"`
Expected: build FAILS — `RemoteControlMessageHandler` doesn't exist yet. (This is the "red" step; a compile failure counts as the test failing.)

- [ ] **Step 3: Implement `RemoteControlMessageHandler`**

Create `HandsLiftedApp.Core/Services/RemoteControl/RemoteControlMessageHandler.cs`:

```csharp
using System;
using HandsLiftedApp.Core.Models.AppState;
using Newtonsoft.Json;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.Services.RemoteControl;

internal static class RemoteControlMessageHandler
{
    private class IncomingMessage
    {
        public string? Action { get; set; }
    }

    public static void Handle(string json)
    {
        var action = ParseAction(json);
        if (action == null)
        {
            return;
        }

        MessageBus.Current.SendMessage(new ActionMessage { Action = action.Value });
    }

    private static ActionMessage.NavigateSlideAction? ParseAction(string json)
    {
        IncomingMessage? message;
        try
        {
            message = JsonConvert.DeserializeObject<IncomingMessage>(json);
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "RemoteControlServer: failed to parse message {Json}", json);
            return null;
        }

        return message?.Action switch
        {
            "NextSlide" => ActionMessage.NavigateSlideAction.NextSlide,
            "PreviousSlide" => ActionMessage.NavigateSlideAction.PreviousSlide,
            null => LogAndReturnNull(json, "message had no \"action\" field or was not a JSON object"),
            _ => LogAndReturnNull(json, $"unrecognized action \"{message.Action}\"")
        };
    }

    private static ActionMessage.NavigateSlideAction? LogAndReturnNull(string json, string reason)
    {
        Log.Warning("RemoteControlServer: ignoring message {Json} - {Reason}", json, reason);
        return null;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj --filter "FullyQualifiedName~RemoteControlMessageHandlerTests"`
Expected: all 5 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Services/RemoteControl/RemoteControlMessageHandler.cs HandsLiftedApp.Tests/Services/RemoteControl/RemoteControlMessageHandlerTests.cs
git commit -m "feat: add remote-control message parse/dispatch logic"
```

---

### Task 3: `RemoteControlServer` — WatsonWsServer wiring

**Files:**
- Create: `HandsLiftedApp.Core/Services/RemoteControl/RemoteControlServer.cs`

**Interfaces:**
- Consumes: `RemoteControlMessageHandler.Handle(string)` (Task 2), `WatsonWebsocket.WatsonWsServer` (Task 1's new dependency).
- Produces: `public class RemoteControlServer : IDisposable` with a public parameterless constructor (does NOT auto-start — construction only wires the server object), `public void Start()` (binds and starts listening; catches and logs bind failure without throwing), and `public void Dispose()` (stops and disposes the underlying `WatsonWsServer`). Task 4 (`Globals`) calls `new RemoteControlServer()` then `.Start()` in `OnStartup`, and `.Dispose()` in `OnShutdown`.

No automated test for this class — it's a thin wrapper around a third-party socket library; the parse/dispatch logic it delegates to is already covered by Task 2's tests. It's covered by the manual smoke test in Task 5 instead.

- [ ] **Step 1: Implement `RemoteControlServer`**

Create `HandsLiftedApp.Core/Services/RemoteControl/RemoteControlServer.cs`:

```csharp
using System;
using System.Text;
using Serilog;
using WatsonWebsocket;

namespace HandsLiftedApp.Core.Services.RemoteControl;

public class RemoteControlServer : IDisposable
{
    public const int Port = 8979;

    private readonly WatsonWsServer _server;

    public RemoteControlServer()
    {
        _server = new WatsonWsServer("127.0.0.1", Port);
        _server.MessageReceived += OnMessageReceived;
    }

    public void Start()
    {
        try
        {
            _server.Start();
            Log.Information("RemoteControlServer listening on ws://127.0.0.1:{Port}/", Port);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "RemoteControlServer failed to bind to port {Port} - remote control will be unavailable this session", Port);
        }
    }

    private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        try
        {
            var json = Encoding.UTF8.GetString(e.Data);
            RemoteControlMessageHandler.Handle(json);
        }
        catch (Exception ex)
        {
            // This handler runs on WatsonWsServer's own background thread with nothing
            // above it to catch a fault - an uncaught exception here would take down that
            // thread (and, depending on the library's internals, possibly the server)
            // instead of just failing to process one bad message.
            Log.Error(ex, "RemoteControlServer: unhandled error processing incoming message");
        }
    }

    public void Dispose()
    {
        _server.MessageReceived -= OnMessageReceived;
        _server.Dispose();
    }
}
```

- [ ] **Step 2: Verify it builds**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`
Expected: builds with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add HandsLiftedApp.Core/Services/RemoteControl/RemoteControlServer.cs
git commit -m "feat: add RemoteControlServer WatsonWsServer wrapper"
```

---

### Task 4: Wire into `Globals.OnStartup` / `OnShutdown`

**Files:**
- Modify: `HandsLiftedApp.Core/Globals.cs`

**Interfaces:**
- Consumes: `RemoteControlServer` (Task 3) — `new RemoteControlServer()`, `.Start()`, `.Dispose()`.
- Produces: `Globals.Instance.RemoteControlServer` property, in case a future task needs to reach it (e.g. a settings screen). Not consumed by any task in this plan.

- [ ] **Step 1: Add the field/property**

In `HandsLiftedApp.Core/Globals.cs`, add alongside the other app-wide singleton properties (near `ImportWorkerThread`, `SlideRenderQueue`):

```csharp
public HandsLiftedApp.Core.Services.RemoteControl.RemoteControlServer? RemoteControlServer { get; private set; }
```

- [ ] **Step 2: Start it in `OnStartup`**

In `Globals.OnStartup`, after the `MainViewModel = new();` / `SlidePreloadService.Initialize(...)` block (i.e. once the app's core state exists — the remote control server has no dependency on it, but starting it last keeps the accept-connections-immediately behavior from racing anything earlier in startup), add:

```csharp
RemoteControlServer = new HandsLiftedApp.Core.Services.RemoteControl.RemoteControlServer();
RemoteControlServer.Start();
```

- [ ] **Step 3: Stop it in `OnShutdown`**

In `Globals.OnShutdown`, alongside the existing `ImportWorkerThread?.Dispose();` try/catch block, add:

```csharp
try
{
    RemoteControlServer?.Dispose();
}
catch (Exception ex)
{
    Log.Error(ex, "Error disposing RemoteControlServer");
}
```

- [ ] **Step 4: Verify it builds**

Run: `dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj`
Expected: builds with 0 errors.

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Core/Globals.cs
git commit -m "feat: start remote-control server on app startup"
```

---

### Task 5: Manual smoke test

**Files:** none (verification only, no code changes).

**Interfaces:**
- Consumes: the fully wired app from Tasks 1-4.
- Produces: confirmation the feature works end-to-end in a running app. Nothing later depends on this task's artifacts.

- [ ] **Step 1: Run the full test suite**

Run: `dotnet test HandsLiftedApp.Tests/HandsLiftedApp.Tests.csproj`
Expected: all tests pass, including the 5 from Task 2.

- [ ] **Step 2: Launch HandsLifted and open a playlist with at least 2 slides**

Run the Desktop app (however this repo's normal dev run is invoked — e.g. `dotnet run --project HandsLiftedApp.Desktop`) and open or create a playlist with at least two slides so `NextSlide`/`PreviousSlide` has somewhere to navigate to/from.

- [ ] **Step 3: Connect a WebSocket client and send a command**

Using any WebSocket CLI (e.g. `wscat`, install via `npm install -g wscat` if not present):

```bash
wscat -c ws://localhost:8979/
```

Then send:

```json
{"action":"NextSlide"}
```

Expected: the live slide in HandsLifted advances to the next slide.

- [ ] **Step 4: Send the reverse command**

In the same `wscat` session, send:

```json
{"action":"PreviousSlide"}
```

Expected: the live slide goes back to the previous slide.

- [ ] **Step 5: Send a malformed message and confirm the app doesn't crash or disconnect**

In the same session, send:

```json
{"action":"NotARealAction"}
```

Expected: nothing happens in the app (no navigation), the app doesn't crash, and the `wscat` connection stays open (send `{"action":"NextSlide"}` again afterward to confirm the connection is still live).

- [ ] **Step 6: No commit for this task** — it's verification only. If any step fails, return to the relevant earlier task, fix, and re-run this task's steps from Step 1.

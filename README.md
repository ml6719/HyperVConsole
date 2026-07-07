# HyperVConsoleKit

HyperVConsoleKit is a C# library for getting emergency console access to local Hyper-V virtual machines from the host.

It is for the awkward moments: RDP is broken, the guest agent is dead, the VM is sitting at a boot menu, Windows Recovery is waiting for input, a Linux appliance has no remote access, or a technician needs one last way in without relying on the guest OS being healthy.

It is not trying to replace RDP, VMConnect, ScreenConnect, TeamViewer, or a proper in-guest support agent. Think of it as a break-glass console toolkit that an MSP, backup vendor, DR product, or remote control agent can embed as a fallback.

## What It Can Do

- List local Hyper-V VMs.
- Read VM state and basic console capabilities.
- Capture the current VM console image from the host.
- Stream console frames to your own transport.
- Reduce stream bandwidth with lower resolution, lower color depth, bitrate caps, and changed tiles.
- Send keyboard input, special keys, chords, Ctrl+Alt+Del, and paste-as-keystrokes.
- Try mouse movement and clicks where Hyper-V exposes a synthetic mouse.
- Start, stop, and reset VMs.
- Detect when Enhanced Session is likely available and launch VMConnect.
- Apply policy limits for FPS, resolution, bandwidth, color depth, power control, and input.
- Emit audit events for capture, input, session, and VM power actions.
- Run structured diagnostics that can be uploaded by an agent.
- Fan out one capture loop to multiple viewers while slow viewers drop stale frames.
- Manage one shared console stream per VM with `HyperVConsoleSessionManager`.

The core library targets:

```xml
<TargetFrameworks>net46;netstandard2.0;net8.0-windows</TargetFrameworks>
```

That means it is compatible with modern .NET on Windows, including .NET Core/.NET services that can consume `netstandard2.0`, plus .NET 8 Windows apps and services. It is not cross-platform .NET because Hyper-V console capture and input are Windows-only WMI APIs.

## How It Works

The raw emergency console path uses the Hyper-V WMI provider:

```text
root\virtualization\v2
```

Screen capture uses Hyper-V thumbnail capture. Input uses Hyper-V virtual keyboard and mouse WMI devices where available.

Single captures return raw `Rgb565` bytes sized exactly:

```text
Width * Height * 2
```

Streaming can keep `Rgb565` or reduce to:

- `Rgb332`
- `Gray8`
- `Gray4`
- `Mono1`

The library does not force JPEG, PNG, WebSockets, SignalR, gRPC, or any particular remote-control protocol. You decide what to do with the frames.

## Install

From source:

```powershell
git clone https://github.com/ml6719/HyperVConsole.git
cd HyperVConsole
dotnet build HyperVConsoleKit.sln
```

From NuGet once published:

```powershell
dotnet add package HyperVConsoleKit
```

Your process must run on the Hyper-V host with administrator rights or as LocalSystem.

## Quick Start

```csharp
using HyperVConsoleKit;

var client = new HyperVConsoleClient();

foreach (var vm in client.GetVirtualMachines())
{
    Console.WriteLine($"{vm.Name} - {vm.Id} - {vm.State}");
}
```

## Agent Policy

If you are embedding this in an MSP agent, set policy at the client or session boundary. This keeps your service from accidentally opening an unlimited, full-resolution, full-input remote console.

```csharp
using HyperVConsoleKit;

var policy = new HyperVConsolePolicy
{
    MaxWidth = 1024,
    MaxHeight = 768,
    MaxFramesPerSecond = 5,
    MaxBytesPerSecond = 500_000,
    MaxColorDepth = ConsoleFramePixelFormat.Rgb332,
    MaxConcurrentViewers = 3,
    AllowKeyboardInput = true,
    AllowMouseInput = true,
    AllowPowerControl = false
};

var client = new HyperVConsoleClient(policy);
```

You can also override policy for a single session:

```csharp
using var session = client.OpenConsole(vm.Id, new HyperVConsoleOpenOptions
{
    Mode = HyperVConsoleMode.RawHostConsole,
    Policy = policy
});
```

## Audit Events

Hook audit events if you need an activity trail for technician sessions.

```csharp
client.Activity += (_, e) =>
{
    Console.WriteLine($"{e.TimestampUtc:o} {e.UserName} {e.VirtualMachineId} {e.Action} {e.Success} {e.Message}");
};

using var session = client.OpenConsole(vm.Id);
session.Activity += (_, e) =>
{
    Console.WriteLine($"{e.Action}: {e.Message}");
};

session.SendCtrlAltDel();
```

## Structured Diagnostics

Use diagnostics when an agent needs to explain why the console is not usable.

```csharp
var report = client.RunDiagnostics(vm.Id);

Console.WriteLine(report.OverallStatus);

foreach (var item in report.Items)
{
    Console.WriteLine($"{item.Status}: {item.Name} - {item.Message}");
}
```

This is deliberately JSON-friendly, so you can return it from an API endpoint or upload it with your agent telemetry.

Open a console session:

```csharp
var vm = client.GetVirtualMachines().First(v => v.IsRunning);

using var session = client.OpenConsole(
    vm.Id,
    new HyperVConsoleOpenOptions { Mode = HyperVConsoleMode.RawHostConsole });
```

## List VMs

```csharp
using HyperVConsoleKit;

var client = new HyperVConsoleClient();
var vms = client.GetVirtualMachines();

foreach (var vm in vms)
{
    Console.WriteLine($"Name:      {vm.Name}");
    Console.WriteLine($"Id:        {vm.Id}");
    Console.WriteLine($"State:     {vm.State}");
    Console.WriteLine($"Running:   {vm.IsRunning}");
    Console.WriteLine($"Capture:   {vm.SupportsConsoleCapture} / now: {vm.CanCaptureNow}");
    Console.WriteLine($"Keyboard:  {vm.SupportsKeyboardInput} / now: {vm.CanSendKeyboardInputNow}");
    Console.WriteLine($"Mouse:     {vm.SupportsMouseInput} / now: {vm.CanSendMouseInputNow}");
    Console.WriteLine($"Enhanced:  {vm.SupportsEnhancedSession}");
    Console.WriteLine($"Suggested: {vm.RecommendedConsoleMode}");
    Console.WriteLine();
}
```

## Check Capabilities

Use this before deciding what UI to show a technician.

```csharp
using HyperVConsoleKit;

var client = new HyperVConsoleClient();
var vm = client.GetVirtualMachines().First(v => v.Name == "Recovery VM");

var caps = client.GetConsoleCapabilities(vm.Id);

Console.WriteLine($"Raw capture:      {caps.SupportsRawCapture}");
Console.WriteLine($"Can capture now:  {caps.CanCaptureNow}");
Console.WriteLine($"Keyboard input:   {caps.SupportsKeyboardInput}");
Console.WriteLine($"Can type now:     {caps.CanSendKeyboardInputNow}");
Console.WriteLine($"Mouse input:      {caps.SupportsMouseInput}");
Console.WriteLine($"Can mouse now:    {caps.CanSendMouseInputNow}");
Console.WriteLine($"Enhanced session: {caps.SupportsEnhancedSession}");
Console.WriteLine($"Recommended mode: {caps.RecommendedMode}");

foreach (var limitation in caps.Limitations)
{
    Console.WriteLine($"Note: {limitation}");
}
```

Typical logic:

```csharp
var mode = caps.RecommendedMode == HyperVConsoleMode.EnhancedSession
    ? HyperVConsoleMode.EnhancedSession
    : HyperVConsoleMode.RawHostConsole;
```

Enhanced Session is better for normal interactive control when the guest is healthy enough. Raw host console is the fallback for boot, BIOS, recovery, and broken guest scenarios.

## Capture A Frame

```csharp
using HyperVConsoleKit;

var client = new HyperVConsoleClient();
var vm = client.GetVirtualMachines().First(v => v.IsRunning);

using var session = client.OpenConsole(vm.Id, new HyperVConsoleOpenOptions
{
    Mode = HyperVConsoleMode.RawHostConsole
});

var frame = session.CaptureFrame(new ConsoleFrameOptions
{
    Width = 1024,
    Height = 768
});

File.WriteAllBytes("console.rgb565", frame.RawBytes);

Console.WriteLine($"{frame.Width}x{frame.Height}");
Console.WriteLine(frame.PixelFormat);
Console.WriteLine($"{frame.RawBytes.Length} bytes");
```

That file is raw RGB565. If you ask for `1024x768`, it will be:

```text
1024 * 768 * 2 = 1,572,864 bytes
```

## Convert RGB565 To A Bitmap

The library deliberately does not depend on an image encoder. If your app wants PNG/JPEG, convert at the edge.

Example using `System.Drawing` on Windows:

```csharp
using System.Drawing;
using System.Drawing.Imaging;
using HyperVConsoleKit;

static Bitmap Rgb565ToBitmap(ConsoleFrame frame)
{
    var bitmap = new Bitmap(frame.Width, frame.Height, PixelFormat.Format24bppRgb);
    var index = 0;

    for (var y = 0; y < frame.Height; y++)
    {
        for (var x = 0; x < frame.Width; x++)
        {
            var value = frame.RawBytes[index] | (frame.RawBytes[index + 1] << 8);
            index += 2;

            var red = ((value >> 11) & 0x1F) * 255 / 31;
            var green = ((value >> 5) & 0x3F) * 255 / 63;
            var blue = (value & 0x1F) * 255 / 31;

            bitmap.SetPixel(x, y, Color.FromArgb(red, green, blue));
        }
    }

    return bitmap;
}

using var bmp = Rgb565ToBitmap(frame);
bmp.Save("console.png", ImageFormat.Png);
```

For production, use a faster pixel buffer approach rather than `SetPixel`.

## Stream Frames

The streaming API gives you frames and lets you choose the transport.

```csharp
using HyperVConsoleKit;

var client = new HyperVConsoleClient();
var vm = client.GetVirtualMachines().First(v => v.IsRunning);

using var session = client.OpenConsole(vm.Id, new HyperVConsoleOpenOptions
{
    Mode = HyperVConsoleMode.RawHostConsole
});

using var cts = new CancellationTokenSource();

await session.StreamFramesAsync(
    new ConsoleFrameStreamOptions
    {
        Width = 1024,
        Height = 768,
        PixelFormat = ConsoleFramePixelFormat.Rgb332,
        ActiveFramesPerSecond = 5,
        IdleFramesPerSecond = 1,
        UseAdaptiveFrameRate = true,
        SendChangedTilesOnly = true,
        MaxBytesPerSecond = 500_000
    },
    async (frame, cancellationToken) =>
    {
        if (frame.UpdateKind == ConsoleFrameUpdateKind.FullFrame)
        {
            Console.WriteLine($"Full frame: {frame.PayloadBytes} bytes");
            await SendFullFrameToYourClient(frame, cancellationToken);
        }
        else
        {
            Console.WriteLine($"Changed tiles: {frame.Tiles.Count}, {frame.PayloadBytes} bytes");
            await SendChangedTilesToYourClient(frame, cancellationToken);
        }
    },
    cts.Token);
```

Your callback is awaited. If `DropFramesWhenBehind` is `true`, HyperVConsoleKit uses latest-frame streaming internally: capture can keep moving while your sender is busy, and the sender receives the newest available frame instead of working through stale frames.

Placeholder methods from the example:

```csharp
static Task SendFullFrameToYourClient(ConsoleFrame frame, CancellationToken cancellationToken)
{
    // Send frame.RawBytes plus metadata: width, height, pixel format, sequence number.
    return Task.CompletedTask;
}

static Task SendChangedTilesToYourClient(ConsoleFrame frame, CancellationToken cancellationToken)
{
    // Send frame.Tiles. Each tile has X, Y, Width, Height, and RawBytes.
    return Task.CompletedTask;
}
```

For several viewers, use a frame hub so the VM is captured once and each viewer has its own latest-frame queue:

```csharp
using var session = client.OpenConsole(vm.Id);

var options = ConsoleFrameStreamOptions.CreatePreset(ConsoleStreamPreset.Balanced);
options.DropFramesWhenBehind = true;

var hub = new HyperVConsoleFrameHub(session, options, maxViewers: 3);
using var hubCts = new CancellationTokenSource();

var hubTask = hub.RunAsync(hubCts.Token);

var viewerTask = hub.AddViewerAsync(async (frame, cancellationToken) =>
{
    await SendFullFrameToYourClient(frame, cancellationToken);
}, requestAborted);
```

For an agent or web gateway, prefer the session manager. It keeps one stream hub per VM, reference-counts viewers, and shuts the stream down when the last viewer disconnects:

```csharp
var manager = new HyperVConsoleSessionManager(client, policy);

await manager.AddViewerAsync(
    vm.Id,
    ConsoleFrameStreamOptions.CreatePreset(ConsoleStreamPreset.Balanced),
    async (frame, cancellationToken) =>
    {
        await SendFullFrameToYourClient(frame, cancellationToken);
    },
    requestAborted);
```

## Streaming Presets

If you do not want to tune every knob yourself:

```csharp
var options = ConsoleFrameStreamOptions.CreatePreset(ConsoleStreamPreset.LowBandwidth);
```

Available presets:

- `Latency`
- `Balanced`
- `LowBandwidth`
- `Quality`

You can still override anything:

```csharp
var options = ConsoleFrameStreamOptions.CreatePreset(ConsoleStreamPreset.LowBandwidth);
options.Width = 800;
options.Height = 600;
options.MaxBytesPerSecond = 250_000;
```

## Bandwidth Controls

For a technician on a poor connection, use something like:

```csharp
var options = new ConsoleFrameStreamOptions
{
    Width = 640,
    Height = 480,
    PixelFormat = ConsoleFramePixelFormat.Gray8,
    ActiveFramesPerSecond = 3,
    IdleFramesPerSecond = 0.5,
    UseAdaptiveFrameRate = true,
    SendChangedTilesOnly = true,
    TileWidth = 64,
    TileHeight = 64,
    MaxBytesPerSecond = 180_000
};
```

For better quality on a LAN:

```csharp
var options = new ConsoleFrameStreamOptions
{
    Width = 1024,
    Height = 768,
    PixelFormat = ConsoleFramePixelFormat.Rgb565,
    ActiveFramesPerSecond = 5,
    IdleFramesPerSecond = 1,
    UseAdaptiveFrameRate = true,
    SendChangedTilesOnly = false
};
```

## Send Keyboard Input

```csharp
using var session = client.OpenConsole(vm.Id);

session.SendText("Administrator");
session.SendKey(ConsoleKeyCode.Enter);
```

Common recovery keys:

```csharp
session.SendKey(ConsoleKeyCode.Tab);
session.SendKey(ConsoleKeyCode.Escape);
session.SendKey(ConsoleKeyCode.F8);
session.SendKey(ConsoleKeyCode.F12);
session.SendKey(ConsoleKeyCode.Up);
session.SendKey(ConsoleKeyCode.Down);
session.SendKey(ConsoleKeyCode.Left);
session.SendKey(ConsoleKeyCode.Right);
```

Ctrl+Alt+Del:

```csharp
session.SendCtrlAltDel();
```

Key down/up:

```csharp
session.SendKeyDown(ConsoleKeyCode.Shift);
session.SendKey(ConsoleKeyCode.F8);
session.SendKeyUp(ConsoleKeyCode.Shift);
```

## Send Chords

```csharp
session.SendChord(ConsoleKeyCode.Alt, ConsoleKeyCode.Tab);
session.SendChord(ConsoleKeyCode.Control, ConsoleKeyCode.Shift, ConsoleKeyCode.Escape);
```

There are helpers for the common support actions:

```csharp
session.SendAltTab();
session.SendWinR();
session.SendCtrlShiftEsc();
```

This is useful for support tools that expose a toolbar of recovery actions.

## Paste Text As Keystrokes

For long strings, passwords, commands, or recovery scripts, use throttled paste-as-input:

```csharp
session.PasteTextAsKeystrokes(
    "ipconfig /all\r\n",
    new ConsolePasteOptions
    {
        DelayBetweenCharactersMs = 10,
        DelayAfterNewLineMs = 25,
        ConvertLineEndingsToEnter = true
    });
```

Async version:

```csharp
await session.PasteTextAsKeystrokesAsync(
    "sudo systemctl status ssh\n",
    new ConsolePasteOptions(),
    cancellationToken);
```

This sends keystrokes through Hyper-V. It is not a guest clipboard.

## Mouse Input

Mouse support is best-effort. Some VMs expose a synthetic mouse, some do not, and guest behavior varies.

```csharp
var caps = client.GetConsoleCapabilities(vm.Id);

if (caps.CanSendMouseInputNow)
{
    using var session = client.OpenConsole(vm.Id);

    // Coordinates are absolute Hyper-V mouse coordinates, 0 to 32767.
    var moved = session.TrySendMouseMove(16000, 16000);
    var clicked = session.TrySendMouseClick(16000, 16000, MouseButton.Left);

    Console.WriteLine($"Moved: {moved}, Clicked: {clicked}");
}
```

In a browser viewer, map canvas coordinates to Hyper-V absolute coordinates:

```csharp
static int ToHyperVMouseCoordinate(double canvasPosition, double canvasSize)
{
    return (int)Math.Round((canvasPosition / canvasSize) * 32767);
}
```

If mouse methods return `false`, keep the technician UI keyboard-first.

## Power Controls

```csharp
client.StartVirtualMachine(vm.Id);
client.StopVirtualMachine(vm.Id, force: false);
client.ResetVirtualMachine(vm.Id);
```

Async:

```csharp
await client.StartVirtualMachineAsync(vm.Id, cancellationToken);
await client.StopVirtualMachineAsync(vm.Id, force: false, cancellationToken);
await client.ResetVirtualMachineAsync(vm.Id, cancellationToken);
```

Treat these like serious remote-control actions. Your product should require explicit confirmation and log who did what.

## Enhanced Session

Hyper-V Enhanced Session is VMConnect using RDP over VMBus. When it is available, it is usually a better interactive experience than raw framebuffer polling.

HyperVConsoleKit can detect and launch it:

```csharp
var caps = client.GetConsoleCapabilities(vm.Id);

if (caps.SupportsEnhancedSession)
{
    var launch = client.GetEnhancedSessionLaunchInfo(vm.Id);

    Console.WriteLine(launch.VmConnectPath);
    Console.WriteLine(launch.Arguments);

    client.TryLaunchEnhancedSession(vm.Id);
}
```

Important caveat: Enhanced Session is not exposed by this library as a headless frame/input stream. For automated/web gateway scenarios, use `RawHostConsole`. For local interactive technician use, launch VMConnect when Enhanced Session is available.

## Async Usage

Most operations have async equivalents:

```csharp
var vms = await client.GetVirtualMachinesAsync(cancellationToken);
var vm = await client.GetVirtualMachineAsync(id, cancellationToken);

using var session = client.OpenConsole(vm.Id);

var frame = await session.CaptureFrameAsync(
    new ConsoleFrameOptions { Width = 1024, Height = 768 },
    cancellationToken);

await session.SendTextAsync("Administrator", cancellationToken);
await session.SendKeyAsync(ConsoleKeyCode.Enter, cancellationToken);
```

Internally, WMI calls are serialized per `HyperVConsoleClient` instance to avoid concurrent access to WMI COM objects from multiple callers.

## Sample Apps

This repository includes several small samples.

List VMs:

```powershell
dotnet run --project samples\ConsoleListVms\ConsoleListVms.csproj
```

Capture a raw frame:

```powershell
dotnet run --project samples\ConsoleScreenshot\ConsoleScreenshot.csproj -- "My VM" console.rgb565
```

Send keyboard input:

```powershell
dotnet run --project samples\ConsoleKeyboardInput\ConsoleKeyboardInput.csproj -- "My VM" tab enter
dotnet run --project samples\ConsoleKeyboardInput\ConsoleKeyboardInput.csproj -- "My VM" ctrlaltdel
dotnet run --project samples\ConsoleKeyboardInput\ConsoleKeyboardInput.csproj -- "My VM" paste:"ipconfig /all"
```

Run diagnostics:

```powershell
dotnet run --project samples\ConsoleDiagnostics\ConsoleDiagnostics.csproj -- "My VM"
```

Diagnostics does not send keyboard input by default. Add `--send-input` to include a simple Enter key smoke test:

```powershell
dotnet run --project samples\ConsoleDiagnostics\ConsoleDiagnostics.csproj -- "My VM" --send-input
```

Run the browser sample:

```powershell
dotnet run --project samples\AspNetCoreWebConsole\AspNetCoreWebConsole.csproj --urls http://localhost:5088
```

Then open:

```text
http://localhost:5088
```

The web sample is intentionally just a sample. It is not production-ready remote access software.

It does include practical gateway hooks in `appsettings.json`:

```json
{
  "HyperVConsole": {
    "ApiKey": "",
    "AllowedVmIds": [],
    "MaxWidth": 1024,
    "MaxHeight": 768,
    "MaxFramesPerSecond": 5,
    "MaxBytesPerSecond": 500000,
    "MaxColorDepth": "Rgb332",
    "MaxConcurrentViewers": 3,
    "AllowKeyboardInput": true,
    "AllowMouseInput": true,
    "AllowPowerControl": false
  }
}
```

If `ApiKey` is set, pass it as `?apiKey=...` in the browser or as `X-Console-Api-Key` for API calls. `AllowedVmIds` lets you expose only specific VMs.

## Tests

The test project covers logic that does not require Hyper-V:

```powershell
dotnet test HyperVConsoleKit.sln
```

Current tests cover pixel conversion, tile diffing, and policy clamping.

## WebSocket Frame Shape Used By The Sample

The ASP.NET Core sample sends a small binary envelope:

```text
4 bytes: little-endian JSON header length
N bytes: UTF-8 JSON header
N bytes: raw full-frame or tile payload
```

The JSON header includes:

- sequence number
- width and height
- pixel format
- update kind
- keyframe flag
- payload byte count
- tile metadata

You can copy this idea, replace it with your own protocol, or send `ConsoleFrame` through SignalR, gRPC, WebRTC data channels, a relay, or your existing agent transport.

## Building A Real MSP/Remote-Control Integration

The library does not expose remote access by itself. That is deliberate.

If you build a service on top of it, add:

- strong authentication
- role-based access
- per-VM authorization
- technician identity
- audit logging
- customer approval where required
- encrypted transport
- session timeout
- rate limiting
- explicit confirmation for power actions
- read-only mode
- break-glass mode for reset, stop, and Ctrl+Alt+Del

For many products, the sensible flow is:

```text
Normal agent/RDP works  -> use that
Enhanced Session works -> launch VMConnect for local technician
Guest is broken        -> use RawHostConsole fallback
```

## Requirements

- Windows.
- Hyper-V installed.
- Administrator privileges or LocalSystem.
- Hyper-V WMI provider at `root\virtualization\v2`.
- A running VM for meaningful capture/input tests.

## Current Limitations

- Raw console capture is polling-based. It is good for emergency work, not high-FPS remote desktop.
- Enhanced Session is detected and launchable, but not headlessly streamed.
- Mouse input depends on Hyper-V mouse device availability and guest behavior.
- International keyboard layouts and IME scenarios may need extra mapping work.
- The sample web console is not secured and should not be exposed as-is.

## License

MIT.

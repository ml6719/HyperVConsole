# HyperVConsoleKit

A C# library and sample service for host-side emergency Hyper-V VM console access.

This toolkit is intended for recovery workflows where RDP, guest agents, or normal remote support tools are unavailable. It polls Hyper-V thumbnail images through WMI and sends keyboard input through the virtual keyboard device. It is not a replacement for RDP, VMConnect, or a full in-guest remote support agent.

Single captures are returned as raw RGB565 bytes exactly sized to `Width * Height * 2`. Streaming can keep RGB565 or reduce the payload to RGB332, Gray8, Gray4, or Mono1. The core library does not encode JPEG or PNG so latency-sensitive consumers can choose their own compression, transport, diffing, or GPU upload strategy.

## Projects

- `src/HyperVConsoleKit` - multi-targeted library for `net46` and `net8.0-windows`.
- `samples/ConsoleListVms` - lists local Hyper-V VMs.
- `samples/ConsoleScreenshot` - captures one raw RGB565 console frame.
- `samples/ConsoleKeyboardInput` - sends text and common recovery keys.
- `samples/ConsoleDiagnostics` - runs safe capability, capture, stream, and mouse tests.
- `samples/AspNetCoreWebConsole` - sample browser console over WebSockets.

## Requirements

- Windows with Hyper-V installed.
- Administrator privileges or LocalSystem.
- Hyper-V WMI provider at `root\virtualization\v2`.

## Quick Start

```powershell
dotnet build HyperVConsoleKit.sln
dotnet run --project samples\ConsoleListVms\ConsoleListVms.csproj
dotnet run --project samples\ConsoleScreenshot\ConsoleScreenshot.csproj -- <vm-name-or-id> console.rgb565
dotnet run --project samples\ConsoleKeyboardInput\ConsoleKeyboardInput.csproj -- <vm-name-or-id> tab enter
dotnet run --project samples\ConsoleDiagnostics\ConsoleDiagnostics.csproj -- <vm-name-or-id>
dotnet run --project samples\AspNetCoreWebConsole\AspNetCoreWebConsole.csproj --urls http://localhost:5088
```

`ConsoleDiagnostics` does not send keyboard input by default. Add `--send-input` to include a simple Enter key smoke test.

The web sample streams raw frames at 5 FPS by default. End users can configure stream timing, color depth, bitrate, and changed-tile mode with query parameters:

```text
ws://localhost:5088/ws/console/<vm-id>?width=1024&height=768&fps=5&idleFps=1&format=Rgb332&maxBps=500000&tiles=true
```

Supported stream performance controls:

- Capture size: request lower `Width` and `Height` from Hyper-V to reduce CPU and transport cost.
- Pixel format: `Rgb565`, `Rgb332`, `Gray8`, `Gray4`, or `Mono1`.
- Adaptive FPS: separate active and idle frame rates based on changed bytes.
- Bitrate cap: `MaxBytesPerSecond` skips non-key frames when the byte budget is exhausted.
- Dirty tiles: first frame is a full keyframe, then only changed tiles are sent until the next periodic keyframe.
- Backpressure: frame callbacks are awaited and stale frames are not queued.
- Presets: `Latency`, `Balanced`, `LowBandwidth`, and `Quality`.

## Async and Thread Safety

The library exposes synchronous and `Task`-based async methods for VM operations, frame capture, streaming, and keyboard input. Hyper-V WMI calls are serialized per `HyperVConsoleClient` instance to avoid concurrent access to WMI COM objects from multiple callers.

## Console Modes

The library reports capabilities for each VM:

- `RawHostConsole` - WMI thumbnail capture, virtual keyboard, and synthetic mouse where present.
- `EnhancedSession` - detected when host policy and VM transport are compatible, with a VMConnect launch helper.
- `Auto` - recommends Enhanced Session when available, otherwise raw host console.

Enhanced Session uses VMConnect/RDP-over-VMBus. It is better for normal interactive control, but this library does not currently expose a headless Enhanced Session frame/input stream. Raw host console remains the emergency fallback for BIOS, boot, recovery, and broken guest scenarios.

## Input

Keyboard input uses `Msvm_Keyboard` methods such as `TypeText`, `TypeKey`, `PressKey`, `ReleaseKey`, `TypeScancodes`, and `TypeCtrlAltDel`.

Additional helpers include:

- `SendChord(...)` for modifier combinations.
- `PasteTextAsKeystrokes(...)` for throttled paste-as-input.
- `TrySendMouseMove(...)`, `TrySendMouseClick(...)`, and `TrySendMouseDoubleClick(...)` through `Msvm_SyntheticMouse` or `Msvm_Ps2Mouse` when available.

Mouse support remains best-effort because Hyper-V device availability and guest behavior vary.

## Security

The library does not expose remote access by itself. Any service built on top of it must add strong authentication, role-based access, audit logging, encrypted transport, per-VM permission checks, session timeouts, explicit technician identity, and customer approval where required.

The ASP.NET Core sample is intentionally a sample only and is not production-ready.

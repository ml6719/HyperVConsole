using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HyperVConsoleKit;
using Xunit;

namespace HyperVConsoleKit.Tests;

public sealed class FrameHubTests
{
    [Fact]
    public async Task AddViewerAsyncCompletesWhenHubProducerStops()
    {
        var session = new CompletingSession();
        var hub = new HyperVConsoleFrameHub(session, new ConsoleFrameStreamOptions());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var viewerTask = hub.AddViewerAsync((_, _) => Task.CompletedTask, cts.Token);
        await hub.RunAsync(cts.Token);
        await viewerTask;
    }

    private sealed class CompletingSession : IHyperVConsoleSession
    {
        public event EventHandler<HyperVConsoleAuditEvent> Activity { add { } remove { } }
        public Guid VirtualMachineId { get; } = Guid.NewGuid();
        public ConsoleFrame CaptureFrame(ConsoleFrameOptions options) => throw new NotSupportedException();
        public Task<ConsoleFrame> CaptureFrameAsync(ConsoleFrameOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StreamFramesAsync(ConsoleFrameStreamOptions options, Func<ConsoleFrame, CancellationToken, Task> onFrame, CancellationToken cancellationToken) => Task.CompletedTask;
        public void SendText(string text) => throw new NotSupportedException();
        public Task SendTextAsync(string text, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void SendKey(ConsoleKeyCode key) => throw new NotSupportedException();
        public Task SendKeyAsync(ConsoleKeyCode key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void SendKeyDown(ConsoleKeyCode key) => throw new NotSupportedException();
        public Task SendKeyDownAsync(ConsoleKeyCode key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void SendKeyUp(ConsoleKeyCode key) => throw new NotSupportedException();
        public Task SendKeyUpAsync(ConsoleKeyCode key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void SendChord(params ConsoleKeyCode[] keys) => throw new NotSupportedException();
        public Task SendChordAsync(CancellationToken cancellationToken, params ConsoleKeyCode[] keys) => throw new NotSupportedException();
        public void PasteTextAsKeystrokes(string text, ConsolePasteOptions options) => throw new NotSupportedException();
        public Task PasteTextAsKeystrokesAsync(string text, ConsolePasteOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void SendScancodes(byte[] scancodes) => throw new NotSupportedException();
        public Task SendScancodesAsync(byte[] scancodes, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void SendCtrlAltDel() => throw new NotSupportedException();
        public Task SendCtrlAltDelAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public void SendAltTab() => throw new NotSupportedException();
        public Task SendAltTabAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public void SendWinR() => throw new NotSupportedException();
        public Task SendWinRAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public void SendCtrlShiftEsc() => throw new NotSupportedException();
        public Task SendCtrlShiftEscAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public bool TrySendMouseMove(int x, int y) => throw new NotSupportedException();
        public Task<bool> TrySendMouseMoveAsync(int x, int y, CancellationToken cancellationToken) => throw new NotSupportedException();
        public bool TrySendMouseClick(int x, int y, MouseButton button) => throw new NotSupportedException();
        public Task<bool> TrySendMouseClickAsync(int x, int y, MouseButton button, CancellationToken cancellationToken) => throw new NotSupportedException();
        public bool TrySendMouseDoubleClick(int x, int y, MouseButton button) => throw new NotSupportedException();
        public Task<bool> TrySendMouseDoubleClickAsync(int x, int y, MouseButton button, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Dispose() { }
    }
}

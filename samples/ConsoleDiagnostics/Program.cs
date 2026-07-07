using HyperVConsoleKit;

var vmSelector = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.OrdinalIgnoreCase));
var sendInput = args.Any(arg => string.Equals(arg, "--send-input", StringComparison.OrdinalIgnoreCase));
var client = new HyperVConsoleClient();
var vms = client.GetVirtualMachines();

if (vms.Count == 0)
{
    Console.Error.WriteLine("FAIL: no Hyper-V VMs found.");
    return 1;
}

var vm = ResolveVm(vmSelector, vms);
var failures = 0;

Console.WriteLine($"HyperVConsoleKit diagnostics");
Console.WriteLine($"Target: {vm.Name} ({vm.Id})");
Console.WriteLine($"State: {vm.State}");
Console.WriteLine();

Run("Capabilities", () =>
{
    var capabilities = client.GetConsoleCapabilities(vm.Id);
    Console.WriteLine($"  RawCapture: {capabilities.SupportsRawCapture}");
    Console.WriteLine($"  Keyboard: {capabilities.SupportsKeyboardInput}");
    Console.WriteLine($"  Mouse: {capabilities.SupportsMouseInput}");
    Console.WriteLine($"  EnhancedSession: {capabilities.SupportsEnhancedSession}");
    Console.WriteLine($"  HostEnhancedSessionPolicy: {capabilities.HostEnhancedSessionPolicyEnabled}");
    Console.WriteLine($"  EnhancedTransport: {capabilities.EnhancedSessionTransportType}");
    Console.WriteLine($"  Recommended: {capabilities.RecommendedMode}");
    foreach (var limitation in capabilities.Limitations)
    {
        Console.WriteLine($"  Limitation: {limitation}");
    }
});

using var session = client.OpenConsole(vm.Id, new HyperVConsoleOpenOptions { Mode = HyperVConsoleMode.RawHostConsole });

Run("Capture 1024x768 RGB565", () =>
{
    var frame = session.CaptureFrame(new ConsoleFrameOptions { Width = 1024, Height = 768 });
    Expect(frame.RawBytes.Length == 1024 * 768 * 2, $"expected {1024 * 768 * 2} bytes, got {frame.RawBytes.Length}");
    Console.WriteLine($"  Bytes: {frame.RawBytes.Length}");
    Console.WriteLine($"  PixelFormat: {frame.PixelFormat}");
});

Run("Stream Rgb332 tile mode", () =>
{
    var frames = new List<ConsoleFrame>();
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
    try
    {
        session.StreamFramesAsync(new ConsoleFrameStreamOptions
        {
            Width = 1024,
            Height = 768,
            PixelFormat = ConsoleFramePixelFormat.Rgb332,
            SendChangedTilesOnly = true,
            ActiveFramesPerSecond = 5,
            IdleFramesPerSecond = 1,
            MaxBytesPerSecond = 900000
        }, (frame, cancellationToken) =>
        {
            frames.Add(frame);
            if (frames.Count >= 2)
            {
                cts.Cancel();
            }

            return Task.CompletedTask;
        }, cts.Token).GetAwaiter().GetResult();
    }
    catch (OperationCanceledException)
    {
    }

    Expect(frames.Count > 0, "no stream frames received");
    Console.WriteLine($"  Frames: {frames.Count}");
    foreach (var frame in frames)
    {
        Console.WriteLine($"  #{frame.SequenceNumber}: {frame.UpdateKind}, payload={frame.PayloadBytes}, tiles={frame.Tiles.Count}, fps={frame.TargetFramesPerSecond}");
    }
});

Run("Mouse move", () =>
{
    if (!vm.SupportsMouseInput)
    {
        Console.WriteLine("  SKIP: no mouse device reported.");
        return;
    }

    var moved = session.TrySendMouseMove(16000, 16000);
    Expect(moved, "TrySendMouseMove returned false");
    Console.WriteLine($"  MoveResult: {moved}");
});

Run("Keyboard input", () =>
{
    if (!sendInput)
    {
        Console.WriteLine("  SKIP: pass --send-input to send Enter.");
        return;
    }

    session.SendKey(ConsoleKeyCode.Enter);
    Console.WriteLine("  Sent: Enter");
});

Console.WriteLine();
Console.WriteLine(failures == 0 ? "PASS" : $"FAILURES: {failures}");
return failures == 0 ? 0 : 1;

void Run(string name, Action action)
{
    try
    {
        Console.WriteLine($"[{name}]");
        action();
        Console.WriteLine("  Result: PASS");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"  Result: FAIL");
        Console.WriteLine($"  {ex.GetType().Name}: {ex.Message}");
    }

    Console.WriteLine();
}

static HyperVVirtualMachine ResolveVm(string? selector, IReadOnlyList<HyperVVirtualMachine> vms)
{
    if (string.IsNullOrWhiteSpace(selector))
    {
        return vms.FirstOrDefault(vm => vm.IsRunning) ?? vms[0];
    }

    if (Guid.TryParse(selector, out var id))
    {
        return vms.FirstOrDefault(vm => vm.Id == id) ?? throw new HyperVVirtualMachineNotFoundException(id);
    }

    return vms.FirstOrDefault(vm => string.Equals(vm.Name, selector, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException("VM name was not found: " + selector);
}

static void Expect(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

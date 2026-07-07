using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HyperVConsoleKit
{
    /// <summary>
    /// Pixel formats that can be returned by the console streaming pipeline.
    /// </summary>
    /// <remarks>
    /// Single-frame capture always starts as raw RGB565 from Hyper-V. Streaming can keep RGB565
    /// or reduce the frame to a lower color depth to trade quality for latency and bandwidth.
    /// </remarks>
    public enum ConsoleFramePixelFormat
    {
        Rgb565 = 0,
        Rgb332 = 1,
        Gray8 = 2,
        Gray4 = 3,
        Mono1 = 4
    }

    /// <summary>
    /// Describes whether a streamed console frame contains a complete image or changed tiles only.
    /// </summary>
    public enum ConsoleFrameUpdateKind
    {
        FullFrame = 0,
        ChangedTiles = 1
    }

    /// <summary>
    /// Named streaming presets for common latency, bandwidth, and quality tradeoffs.
    /// </summary>
    public enum ConsoleStreamPreset
    {
        Custom = 0,
        Latency = 1,
        Balanced = 2,
        LowBandwidth = 3,
        Quality = 4
    }

    /// <summary>
    /// Console transport mode requested when opening a VM console session.
    /// </summary>
    public enum HyperVConsoleMode
    {
        Auto = 0,
        RawHostConsole = 1,
        EnhancedSession = 2
    }

    /// <summary>
    /// Hyper-V Enhanced Session transport advertised by the VM configuration.
    /// </summary>
    public enum HyperVEnhancedSessionTransportType
    {
        Vmbus = 0,
        HvSocket = 1,
        Unknown = 65535
    }

    /// <summary>
    /// Common Hyper-V virtual machine states reported by Msvm_ComputerSystem.EnabledState.
    /// </summary>
    public enum HyperVVirtualMachineState
    {
        Unknown = 0,
        Other = 1,
        Running = 2,
        Off = 3,
        Stopping = 4,
        Saved = 6,
        Paused = 9,
        Starting = 10,
        Reset = 11,
        Saving = 32773,
        Pausing = 32776,
        Resuming = 32777,
        FastSaved = 32779,
        FastSaving = 32780
    }

    /// <summary>
    /// Windows virtual-key codes accepted by the Hyper-V virtual keyboard WMI device.
    /// </summary>
    public enum ConsoleKeyCode : uint
    {
        Backspace = 0x08,
        Tab = 0x09,
        Enter = 0x0D,
        Shift = 0x10,
        Control = 0x11,
        Alt = 0x12,
        Pause = 0x13,
        CapsLock = 0x14,
        Escape = 0x1B,
        Space = 0x20,
        PageUp = 0x21,
        PageDown = 0x22,
        End = 0x23,
        Home = 0x24,
        Left = 0x25,
        Up = 0x26,
        Right = 0x27,
        Down = 0x28,
        Insert = 0x2D,
        Delete = 0x2E,
        LeftWindows = 0x5B,
        RightWindows = 0x5C,
        D0 = 0x30,
        D1 = 0x31,
        D2 = 0x32,
        D3 = 0x33,
        D4 = 0x34,
        D5 = 0x35,
        D6 = 0x36,
        D7 = 0x37,
        D8 = 0x38,
        D9 = 0x39,
        A = 0x41,
        B = 0x42,
        C = 0x43,
        D = 0x44,
        E = 0x45,
        F = 0x46,
        G = 0x47,
        H = 0x48,
        I = 0x49,
        J = 0x4A,
        K = 0x4B,
        L = 0x4C,
        M = 0x4D,
        N = 0x4E,
        O = 0x4F,
        P = 0x50,
        Q = 0x51,
        R = 0x52,
        S = 0x53,
        T = 0x54,
        U = 0x55,
        V = 0x56,
        W = 0x57,
        X = 0x58,
        Y = 0x59,
        Z = 0x5A,
        F1 = 0x70,
        F2 = 0x71,
        F3 = 0x72,
        F4 = 0x73,
        F5 = 0x74,
        F6 = 0x75,
        F7 = 0x76,
        F8 = 0x77,
        F9 = 0x78,
        F10 = 0x79,
        F11 = 0x7A,
        F12 = 0x7B
    }

    /// <summary>
    /// Mouse button identifiers used by the synthetic mouse helpers.
    /// </summary>
    public enum MouseButton
    {
        Left = 0,
        Right = 1,
        Middle = 2
    }

    /// <summary>
    /// High-level action names emitted by client and session audit events.
    /// </summary>
    public enum HyperVConsoleAuditAction
    {
        SessionOpened = 0,
        SessionDisposed = 1,
        FrameCaptured = 2,
        FrameStreamed = 3,
        TextSent = 4,
        KeySent = 5,
        ChordSent = 6,
        ScancodesSent = 7,
        CtrlAltDelSent = 8,
        MouseMoved = 9,
        MouseClicked = 10,
        MouseDoubleClicked = 11,
        VirtualMachineStarted = 12,
        VirtualMachineStopped = 13,
        VirtualMachineReset = 14,
        EnhancedSessionLaunchRequested = 15
    }

    /// <summary>
    /// Status values used by structured diagnostics.
    /// </summary>
    public enum HyperVConsoleDiagnosticStatus
    {
        Pass = 0,
        Warning = 1,
        Fail = 2,
        Skipped = 3
    }

    /// <summary>
    /// Snapshot of a local Hyper-V VM and its console-related capabilities.
    /// </summary>
    public sealed class HyperVVirtualMachine
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public HyperVVirtualMachineState State { get; set; }
        public string HostName { get; set; }
        public bool IsRunning { get; set; }
        public bool IsPaused { get; set; }
        public bool SupportsConsoleCapture { get; set; }
        public bool CanCaptureNow { get; set; }
        public bool SupportsKeyboardInput { get; set; }
        public bool CanSendKeyboardInputNow { get; set; }
        public bool SupportsMouseInput { get; set; }
        public bool CanSendMouseInputNow { get; set; }
        public bool SupportsEnhancedSession { get; set; }
        public HyperVConsoleMode RecommendedConsoleMode { get; set; }
    }

    /// <summary>
    /// Options used when opening a raw Hyper-V console session.
    /// </summary>
    public sealed class HyperVConsoleOpenOptions
    {
        public HyperVConsoleMode Mode { get; set; } = HyperVConsoleMode.Auto;
        public HyperVConsolePolicy Policy { get; set; }
    }

    /// <summary>
    /// Describes supported and currently available console features for a VM.
    /// </summary>
    /// <remarks>
    /// Support flags describe whether a feature exists in principle. The corresponding
    /// Can...Now flags describe whether the feature is usable at this moment, for example
    /// only while the VM is running.
    /// </remarks>
    public sealed class HyperVConsoleCapabilities
    {
        public Guid VirtualMachineId { get; set; }
        public string VirtualMachineName { get; set; }
        public bool SupportsRawCapture { get; set; }
        public bool CanCaptureNow { get; set; }
        public bool SupportsKeyboardInput { get; set; }
        public bool CanSendKeyboardInputNow { get; set; }
        public bool SupportsMouseInput { get; set; }
        public bool CanSendMouseInputNow { get; set; }
        public bool SupportsEnhancedSession { get; set; }
        public bool HostEnhancedSessionPolicyEnabled { get; set; }
        public HyperVEnhancedSessionTransportType EnhancedSessionTransportType { get; set; }
        public HyperVConsoleMode RecommendedMode { get; set; }
        public string[] Limitations { get; set; }
    }

    /// <summary>
    /// Information needed to launch VMConnect for an Enhanced Session.
    /// </summary>
    public sealed class HyperVEnhancedSessionLaunchInfo
    {
        public Guid VirtualMachineId { get; set; }
        public string VirtualMachineName { get; set; }
        public string ServerName { get; set; }
        public string VmConnectPath { get; set; }
        public string Arguments { get; set; }
        public bool CanLaunchFromCurrentProcess { get; set; }
        public string Limitation { get; set; }
    }

    /// <summary>
    /// Policy limits and permissions applied to console sessions and agent-style gateways.
    /// </summary>
    /// <remarks>
    /// Policy can disable capture, input, or power control and can clamp stream options such as
    /// resolution, frame rate, byte rate, color depth, and concurrent viewers.
    /// </remarks>
    public sealed class HyperVConsolePolicy
    {
        public bool AllowCapture { get; set; } = true;
        public bool AllowKeyboardInput { get; set; } = true;
        public bool AllowMouseInput { get; set; } = true;
        public bool AllowPowerControl { get; set; } = true;
        public int? MaxWidth { get; set; }
        public int? MaxHeight { get; set; }
        public double? MaxFramesPerSecond { get; set; }
        public long? MaxBytesPerSecond { get; set; }
        public ConsoleFramePixelFormat? MaxColorDepth { get; set; }
        public int? MaxConcurrentViewers { get; set; }
        public TimeSpan? IdleTimeout { get; set; }

        /// <summary>
        /// Applies resolution limits to single-frame capture options.
        /// </summary>
        public void ApplyTo(ConsoleFrameOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            if (MaxWidth.HasValue && options.Width > MaxWidth.Value)
            {
                options.Width = MaxWidth.Value;
            }

            if (MaxHeight.HasValue && options.Height > MaxHeight.Value)
            {
                options.Height = MaxHeight.Value;
            }
        }

        /// <summary>
        /// Applies resolution, frame-rate, byte-rate, and color-depth limits to stream options.
        /// </summary>
        public void ApplyTo(ConsoleFrameStreamOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            if (MaxWidth.HasValue && options.Width > MaxWidth.Value)
            {
                options.Width = MaxWidth.Value;
            }

            if (MaxHeight.HasValue && options.Height > MaxHeight.Value)
            {
                options.Height = MaxHeight.Value;
            }
            if (MaxFramesPerSecond.HasValue)
            {
                options.FramesPerSecond = Math.Min(options.FramesPerSecond, MaxFramesPerSecond.Value);
                options.ActiveFramesPerSecond = Math.Min(options.ActiveFramesPerSecond, MaxFramesPerSecond.Value);
                options.IdleFramesPerSecond = Math.Min(options.IdleFramesPerSecond, MaxFramesPerSecond.Value);
            }

            if (MaxBytesPerSecond.HasValue)
            {
                options.MaxBytesPerSecond = options.MaxBytesPerSecond.HasValue
                    ? Math.Min(options.MaxBytesPerSecond.Value, MaxBytesPerSecond.Value)
                    : MaxBytesPerSecond.Value;
            }

            if (MaxColorDepth.HasValue && GetColorDepthRank(options.PixelFormat) > GetColorDepthRank(MaxColorDepth.Value))
            {
                options.PixelFormat = MaxColorDepth.Value;
            }
        }

        internal void EnsureCaptureAllowed()
        {
            if (!AllowCapture)
            {
                throw new HyperVConsoleException("Console capture is disabled by policy.");
            }
        }

        internal void EnsureKeyboardAllowed()
        {
            if (!AllowKeyboardInput)
            {
                throw new HyperVConsoleException("Keyboard input is disabled by policy.");
            }
        }

        internal void EnsureMouseAllowed()
        {
            if (!AllowMouseInput)
            {
                throw new HyperVConsoleException("Mouse input is disabled by policy.");
            }
        }

        internal void EnsurePowerControlAllowed()
        {
            if (!AllowPowerControl)
            {
                throw new HyperVConsoleException("VM power control is disabled by policy.");
            }
        }

        private static int GetColorDepthRank(ConsoleFramePixelFormat format)
        {
            switch (format)
            {
                case ConsoleFramePixelFormat.Mono1: return 1;
                case ConsoleFramePixelFormat.Gray4: return 4;
                case ConsoleFramePixelFormat.Rgb332:
                case ConsoleFramePixelFormat.Gray8: return 8;
                case ConsoleFramePixelFormat.Rgb565: return 16;
                default: throw new ArgumentOutOfRangeException("format");
            }
        }
    }

    /// <summary>
    /// Audit event emitted for console session, frame, input, and power actions.
    /// </summary>
    public sealed class HyperVConsoleAuditEvent
    {
        public DateTime TimestampUtc { get; set; }
        public Guid VirtualMachineId { get; set; }
        public HyperVConsoleAuditAction Action { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public long? Bytes { get; set; }
        public string UserName { get; set; }
    }

    /// <summary>
    /// One diagnostic check result in a console diagnostics report.
    /// </summary>
    public sealed class HyperVConsoleDiagnosticItem
    {
        public string Name { get; set; }
        public HyperVConsoleDiagnosticStatus Status { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Structured diagnostics for a VM console, suitable for JSON APIs and agent telemetry.
    /// </summary>
    public sealed class HyperVConsoleDiagnosticReport
    {
        public Guid VirtualMachineId { get; set; }
        public string VirtualMachineName { get; set; }
        public HyperVVirtualMachineState State { get; set; }
        public DateTime CheckedUtc { get; set; }
        public HyperVConsoleCapabilities Capabilities { get; set; }
        public IReadOnlyList<HyperVConsoleDiagnosticItem> Items { get; set; }
        public HyperVConsoleDiagnosticStatus OverallStatus { get; set; }
    }

    /// <summary>
    /// Timing and line-ending options for paste-as-keystrokes input.
    /// </summary>
    public sealed class ConsolePasteOptions
    {
        public int DelayBetweenCharactersMs { get; set; } = 10;
        public int DelayAfterNewLineMs { get; set; } = 25;
        public bool ConvertLineEndingsToEnter { get; set; } = true;
    }

    /// <summary>
    /// Options for a single console frame capture.
    /// </summary>
    public sealed class ConsoleFrameOptions
    {
        public int Width { get; set; } = 1024;
        public int Height { get; set; } = 768;
    }

    /// <summary>
    /// Options for continuous console frame streaming.
    /// </summary>
    public sealed class ConsoleFrameStreamOptions
    {
        public int Width { get; set; } = 1024;
        public int Height { get; set; } = 768;
        public double FramesPerSecond { get; set; } = 5;
        public double IdleFramesPerSecond { get; set; } = 1;
        public double ActiveFramesPerSecond { get; set; } = 5;
        public bool UseAdaptiveFrameRate { get; set; } = true;
        public long? MaxBytesPerSecond { get; set; }
        public ConsoleFramePixelFormat PixelFormat { get; set; } = ConsoleFramePixelFormat.Rgb565;
        public bool SendChangedTilesOnly { get; set; } = true;
        public int TileWidth { get; set; } = 64;
        public int TileHeight { get; set; } = 64;
        public bool DropFramesWhenBehind { get; set; } = true;
        public int FullFrameInterval { get; set; } = 60;
        public int ActiveChangeThresholdBytes { get; set; } = 4096;
        public ConsoleStreamPreset Preset { get; set; } = ConsoleStreamPreset.Custom;

        /// <summary>
        /// Creates a stream options instance from one of the built-in presets.
        /// </summary>
        public static ConsoleFrameStreamOptions CreatePreset(ConsoleStreamPreset preset)
        {
            var options = new ConsoleFrameStreamOptions { Preset = preset };
            switch (preset)
            {
                case ConsoleStreamPreset.Latency:
                    options.Width = 800;
                    options.Height = 600;
                    options.ActiveFramesPerSecond = 8;
                    options.IdleFramesPerSecond = 2;
                    options.PixelFormat = ConsoleFramePixelFormat.Rgb332;
                    options.MaxBytesPerSecond = 900000;
                    options.TileWidth = 64;
                    options.TileHeight = 64;
                    break;
                case ConsoleStreamPreset.LowBandwidth:
                    options.Width = 640;
                    options.Height = 480;
                    options.ActiveFramesPerSecond = 3;
                    options.IdleFramesPerSecond = 0.5;
                    options.PixelFormat = ConsoleFramePixelFormat.Gray8;
                    options.MaxBytesPerSecond = 180000;
                    options.TileWidth = 64;
                    options.TileHeight = 64;
                    break;
                case ConsoleStreamPreset.Quality:
                    options.Width = 1024;
                    options.Height = 768;
                    options.ActiveFramesPerSecond = 5;
                    options.IdleFramesPerSecond = 1;
                    options.PixelFormat = ConsoleFramePixelFormat.Rgb565;
                    options.MaxBytesPerSecond = null;
                    options.SendChangedTilesOnly = false;
                    break;
                default:
                    options.Width = 1024;
                    options.Height = 768;
                    options.ActiveFramesPerSecond = 5;
                    options.IdleFramesPerSecond = 1;
                    options.PixelFormat = ConsoleFramePixelFormat.Rgb332;
                    options.MaxBytesPerSecond = 500000;
                    break;
            }

            options.FramesPerSecond = options.ActiveFramesPerSecond;
            return options;
        }
    }

    /// <summary>
    /// A captured or streamed console frame.
    /// </summary>
    public sealed class ConsoleFrame
    {
        public Guid VirtualMachineId { get; set; }
        public DateTime CapturedUtc { get; set; }
        public long SequenceNumber { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public ConsoleFramePixelFormat PixelFormat { get; set; }
        public ConsoleFrameUpdateKind UpdateKind { get; set; }
        public int BytesPerPixelNumerator { get; set; }
        public int BytesPerPixelDenominator { get; set; }
        public byte[] RawBytes { get; set; }
        public IReadOnlyList<ConsoleFrameTile> Tiles { get; set; }
        public bool IsKeyFrame { get; set; }
        public long PayloadBytes { get; set; }
        public double TargetFramesPerSecond { get; set; }
    }

    /// <summary>
    /// A changed rectangular region in a tiled console frame update.
    /// </summary>
    public sealed class ConsoleFrameTile
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] RawBytes { get; set; }
    }

    /// <summary>
    /// Active raw Hyper-V console session for capture, streaming, keyboard input, and mouse input.
    /// </summary>
    public interface IHyperVConsoleSession : IDisposable
    {
        /// <summary>
        /// Raised for auditable actions performed through this session.
        /// </summary>
        event EventHandler<HyperVConsoleAuditEvent> Activity;

        /// <summary>
        /// Gets the VM id this console session controls.
        /// </summary>
        Guid VirtualMachineId { get; }

        /// <summary>
        /// Captures one raw console frame from Hyper-V.
        /// </summary>
        ConsoleFrame CaptureFrame(ConsoleFrameOptions options);

        /// <summary>
        /// Captures one raw console frame from Hyper-V on a worker thread.
        /// </summary>
        Task<ConsoleFrame> CaptureFrameAsync(ConsoleFrameOptions options, CancellationToken cancellationToken);

        /// <summary>
        /// Streams console frames until cancellation is requested or the frame callback fails.
        /// </summary>
        Task StreamFramesAsync(ConsoleFrameStreamOptions options, Func<ConsoleFrame, CancellationToken, Task> onFrame, CancellationToken cancellationToken);

        /// <summary>
        /// Sends text through the Hyper-V virtual keyboard TypeText method.
        /// </summary>
        void SendText(string text);

        /// <summary>
        /// Sends text asynchronously through the Hyper-V virtual keyboard TypeText method.
        /// </summary>
        Task SendTextAsync(string text, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a single key press and release.
        /// </summary>
        void SendKey(ConsoleKeyCode key);

        /// <summary>
        /// Sends a single key press and release asynchronously.
        /// </summary>
        Task SendKeyAsync(ConsoleKeyCode key, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a key down event.
        /// </summary>
        void SendKeyDown(ConsoleKeyCode key);

        /// <summary>
        /// Sends a key down event asynchronously.
        /// </summary>
        Task SendKeyDownAsync(ConsoleKeyCode key, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a key up event.
        /// </summary>
        void SendKeyUp(ConsoleKeyCode key);

        /// <summary>
        /// Sends a key up event asynchronously.
        /// </summary>
        Task SendKeyUpAsync(ConsoleKeyCode key, CancellationToken cancellationToken);

        /// <summary>
        /// Sends a key chord by pressing keys in order and releasing them in reverse order.
        /// </summary>
        void SendChord(params ConsoleKeyCode[] keys);

        /// <summary>
        /// Sends a key chord asynchronously.
        /// </summary>
        Task SendChordAsync(CancellationToken cancellationToken, params ConsoleKeyCode[] keys);

        /// <summary>
        /// Sends text as a sequence of keystrokes with optional delays and newline conversion.
        /// </summary>
        void PasteTextAsKeystrokes(string text, ConsolePasteOptions options);

        /// <summary>
        /// Sends text as a sequence of keystrokes asynchronously.
        /// </summary>
        Task PasteTextAsKeystrokesAsync(string text, ConsolePasteOptions options, CancellationToken cancellationToken);

        /// <summary>
        /// Sends raw keyboard scancodes through the Hyper-V virtual keyboard.
        /// </summary>
        void SendScancodes(byte[] scancodes);

        /// <summary>
        /// Sends raw keyboard scancodes asynchronously.
        /// </summary>
        Task SendScancodesAsync(byte[] scancodes, CancellationToken cancellationToken);

        /// <summary>
        /// Sends Ctrl+Alt+Del through the Hyper-V virtual keyboard.
        /// </summary>
        void SendCtrlAltDel();

        /// <summary>
        /// Sends Ctrl+Alt+Del asynchronously.
        /// </summary>
        Task SendCtrlAltDelAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Sends Alt+Tab.
        /// </summary>
        void SendAltTab();

        /// <summary>
        /// Sends Alt+Tab asynchronously.
        /// </summary>
        Task SendAltTabAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Sends Windows+R.
        /// </summary>
        void SendWinR();

        /// <summary>
        /// Sends Windows+R asynchronously.
        /// </summary>
        Task SendWinRAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Sends Ctrl+Shift+Esc.
        /// </summary>
        void SendCtrlShiftEsc();

        /// <summary>
        /// Sends Ctrl+Shift+Esc asynchronously.
        /// </summary>
        Task SendCtrlShiftEscAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Attempts to move the synthetic mouse to an absolute Hyper-V coordinate.
        /// </summary>
        bool TrySendMouseMove(int x, int y);

        /// <summary>
        /// Attempts to move the synthetic mouse asynchronously.
        /// </summary>
        Task<bool> TrySendMouseMoveAsync(int x, int y, CancellationToken cancellationToken);

        /// <summary>
        /// Attempts to move the mouse and click the specified button.
        /// </summary>
        bool TrySendMouseClick(int x, int y, MouseButton button);

        /// <summary>
        /// Attempts to move the mouse and click asynchronously.
        /// </summary>
        Task<bool> TrySendMouseClickAsync(int x, int y, MouseButton button, CancellationToken cancellationToken);

        /// <summary>
        /// Attempts to move the mouse and double-click the specified button.
        /// </summary>
        bool TrySendMouseDoubleClick(int x, int y, MouseButton button);

        /// <summary>
        /// Attempts to move the mouse and double-click asynchronously.
        /// </summary>
        Task<bool> TrySendMouseDoubleClickAsync(int x, int y, MouseButton button, CancellationToken cancellationToken);
    }
}

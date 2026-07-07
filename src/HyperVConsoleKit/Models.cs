using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HyperVConsoleKit
{
    public enum ConsoleFramePixelFormat
    {
        Rgb565 = 0,
        Rgb332 = 1,
        Gray8 = 2,
        Gray4 = 3,
        Mono1 = 4
    }

    public enum ConsoleFrameUpdateKind
    {
        FullFrame = 0,
        ChangedTiles = 1
    }

    public enum ConsoleStreamPreset
    {
        Custom = 0,
        Latency = 1,
        Balanced = 2,
        LowBandwidth = 3,
        Quality = 4
    }

    public enum HyperVConsoleMode
    {
        Auto = 0,
        RawHostConsole = 1,
        EnhancedSession = 2
    }

    public enum HyperVEnhancedSessionTransportType
    {
        Vmbus = 0,
        HvSocket = 1,
        Unknown = 65535
    }

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

    public enum MouseButton
    {
        Left = 0,
        Right = 1,
        Middle = 2
    }

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

    public enum HyperVConsoleDiagnosticStatus
    {
        Pass = 0,
        Warning = 1,
        Fail = 2,
        Skipped = 3
    }

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

    public sealed class HyperVConsoleOpenOptions
    {
        public HyperVConsoleMode Mode { get; set; } = HyperVConsoleMode.Auto;
        public HyperVConsolePolicy Policy { get; set; }
    }

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

    public sealed class HyperVConsoleDiagnosticItem
    {
        public string Name { get; set; }
        public HyperVConsoleDiagnosticStatus Status { get; set; }
        public string Message { get; set; }
    }

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

    public sealed class ConsolePasteOptions
    {
        public int DelayBetweenCharactersMs { get; set; } = 10;
        public int DelayAfterNewLineMs { get; set; } = 25;
        public bool ConvertLineEndingsToEnter { get; set; } = true;
    }

    public sealed class ConsoleFrameOptions
    {
        public int Width { get; set; } = 1024;
        public int Height { get; set; } = 768;
    }

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

    public sealed class ConsoleFrameTile
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] RawBytes { get; set; }
    }

    public interface IHyperVConsoleSession : IDisposable
    {
        event EventHandler<HyperVConsoleAuditEvent> Activity;
        Guid VirtualMachineId { get; }
        ConsoleFrame CaptureFrame(ConsoleFrameOptions options);
        Task<ConsoleFrame> CaptureFrameAsync(ConsoleFrameOptions options, CancellationToken cancellationToken);
        Task StreamFramesAsync(ConsoleFrameStreamOptions options, Func<ConsoleFrame, CancellationToken, Task> onFrame, CancellationToken cancellationToken);
        void SendText(string text);
        Task SendTextAsync(string text, CancellationToken cancellationToken);
        void SendKey(ConsoleKeyCode key);
        Task SendKeyAsync(ConsoleKeyCode key, CancellationToken cancellationToken);
        void SendKeyDown(ConsoleKeyCode key);
        Task SendKeyDownAsync(ConsoleKeyCode key, CancellationToken cancellationToken);
        void SendKeyUp(ConsoleKeyCode key);
        Task SendKeyUpAsync(ConsoleKeyCode key, CancellationToken cancellationToken);
        void SendChord(params ConsoleKeyCode[] keys);
        Task SendChordAsync(CancellationToken cancellationToken, params ConsoleKeyCode[] keys);
        void PasteTextAsKeystrokes(string text, ConsolePasteOptions options);
        Task PasteTextAsKeystrokesAsync(string text, ConsolePasteOptions options, CancellationToken cancellationToken);
        void SendScancodes(byte[] scancodes);
        Task SendScancodesAsync(byte[] scancodes, CancellationToken cancellationToken);
        void SendCtrlAltDel();
        Task SendCtrlAltDelAsync(CancellationToken cancellationToken);
        void SendAltTab();
        Task SendAltTabAsync(CancellationToken cancellationToken);
        void SendWinR();
        Task SendWinRAsync(CancellationToken cancellationToken);
        void SendCtrlShiftEsc();
        Task SendCtrlShiftEscAsync(CancellationToken cancellationToken);
        bool TrySendMouseMove(int x, int y);
        Task<bool> TrySendMouseMoveAsync(int x, int y, CancellationToken cancellationToken);
        bool TrySendMouseClick(int x, int y, MouseButton button);
        Task<bool> TrySendMouseClickAsync(int x, int y, MouseButton button, CancellationToken cancellationToken);
        bool TrySendMouseDoubleClick(int x, int y, MouseButton button);
        Task<bool> TrySendMouseDoubleClickAsync(int x, int y, MouseButton button, CancellationToken cancellationToken);
    }
}

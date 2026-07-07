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

    public sealed class HyperVVirtualMachine
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public HyperVVirtualMachineState State { get; set; }
        public string HostName { get; set; }
        public bool IsRunning { get; set; }
        public bool IsPaused { get; set; }
        public bool SupportsConsoleCapture { get; set; }
        public bool SupportsKeyboardInput { get; set; }
        public bool SupportsMouseInput { get; set; }
        public bool SupportsEnhancedSession { get; set; }
        public HyperVConsoleMode RecommendedConsoleMode { get; set; }
    }

    public sealed class HyperVConsoleOpenOptions
    {
        public HyperVConsoleMode Mode { get; set; } = HyperVConsoleMode.Auto;
    }

    public sealed class HyperVConsoleCapabilities
    {
        public Guid VirtualMachineId { get; set; }
        public string VirtualMachineName { get; set; }
        public bool SupportsRawCapture { get; set; }
        public bool SupportsKeyboardInput { get; set; }
        public bool SupportsMouseInput { get; set; }
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
        bool TrySendMouseMove(int x, int y);
        Task<bool> TrySendMouseMoveAsync(int x, int y, CancellationToken cancellationToken);
        bool TrySendMouseClick(int x, int y, MouseButton button);
        Task<bool> TrySendMouseClickAsync(int x, int y, MouseButton button, CancellationToken cancellationToken);
        bool TrySendMouseDoubleClick(int x, int y, MouseButton button);
        Task<bool> TrySendMouseDoubleClickAsync(int x, int y, MouseButton button, CancellationToken cancellationToken);
    }
}

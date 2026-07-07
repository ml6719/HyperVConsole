using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace HyperVConsoleKit
{
    public sealed class HyperVConsoleClient
    {
        internal const string NamespacePath = @"root\virtualization\v2";
        private readonly ManagementScope _scope;
        private readonly object _wmiLock = new object();

        public HyperVConsoleClient() : this(NamespacePath)
        {
        }

        public HyperVConsoleClient(string namespacePath)
        {
            _scope = new ManagementScope(namespacePath);
            _scope.Connect();
        }

        public IReadOnlyList<HyperVVirtualMachine> GetVirtualMachines()
        {
            try
            {
                lock (_wmiLock)
                {
                    using (var searcher = new ManagementObjectSearcher(_scope, new ObjectQuery("SELECT * FROM Msvm_ComputerSystem WHERE Caption = 'Virtual Machine'")))
                    using (var results = searcher.Get())
                    {
                        return results.Cast<ManagementObject>().Select(ToVirtualMachine).ToList();
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new HyperVAccessDeniedException("Access denied while enumerating Hyper-V virtual machines. Run elevated or as LocalSystem.", ex);
            }
            catch (ManagementException ex)
            {
                throw new HyperVConsoleException("Unable to enumerate Hyper-V virtual machines through WMI.", ex);
            }
        }

        public Task<IReadOnlyList<HyperVVirtualMachine>> GetVirtualMachinesAsync(CancellationToken cancellationToken)
        {
            return RunAsync(() => GetVirtualMachines(), cancellationToken);
        }

        public HyperVVirtualMachine GetVirtualMachine(Guid id)
        {
            lock (_wmiLock)
            {
                using (var vm = GetVirtualMachineObject(id))
                {
                    return ToVirtualMachine(vm);
                }
            }
        }

        public Task<HyperVVirtualMachine> GetVirtualMachineAsync(Guid id, CancellationToken cancellationToken)
        {
            return RunAsync(() => GetVirtualMachine(id), cancellationToken);
        }

        public IHyperVConsoleSession OpenConsole(Guid virtualMachineId)
        {
            return OpenConsole(virtualMachineId, new HyperVConsoleOpenOptions());
        }

        public IHyperVConsoleSession OpenConsole(Guid virtualMachineId, HyperVConsoleOpenOptions options)
        {
            if (options == null)
            {
                options = new HyperVConsoleOpenOptions();
            }

            var capabilities = GetConsoleCapabilities(virtualMachineId);
            var mode = options.Mode == HyperVConsoleMode.Auto ? capabilities.RecommendedMode : options.Mode;
            if (mode == HyperVConsoleMode.EnhancedSession)
            {
                throw new HyperVConsoleException("Enhanced Session is detected and can be launched through VMConnect, but programmatic frame streaming and input injection are only implemented for RawHostConsole mode.");
            }

            return new HyperVConsoleSession(_scope, _wmiLock, virtualMachineId);
        }

        public HyperVConsoleCapabilities GetConsoleCapabilities(Guid virtualMachineId)
        {
            lock (_wmiLock)
            {
                using (var vm = GetVirtualMachineObject(virtualMachineId))
                {
                    return GetConsoleCapabilities(vm);
                }
            }
        }

        public HyperVEnhancedSessionLaunchInfo GetEnhancedSessionLaunchInfo(Guid virtualMachineId)
        {
            var vm = GetVirtualMachine(virtualMachineId);
            var vmConnectPath = FindVmConnectPath();
            return new HyperVEnhancedSessionLaunchInfo
            {
                VirtualMachineId = virtualMachineId,
                VirtualMachineName = vm.Name,
                ServerName = Environment.MachineName,
                VmConnectPath = vmConnectPath,
                Arguments = string.Format("{0} \"{1}\"", Environment.MachineName, vm.Name),
                CanLaunchFromCurrentProcess = !string.IsNullOrEmpty(vmConnectPath) && Environment.UserInteractive,
                Limitation = "VMConnect Enhanced Session is interactive and RDP-over-VMBus brokered; this library can launch it, but cannot currently stream or remote-control that session headlessly."
            };
        }

        public bool TryLaunchEnhancedSession(Guid virtualMachineId)
        {
            var launchInfo = GetEnhancedSessionLaunchInfo(virtualMachineId);
            if (!launchInfo.CanLaunchFromCurrentProcess)
            {
                return false;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = launchInfo.VmConnectPath,
                Arguments = launchInfo.Arguments,
                UseShellExecute = false
            });
            return true;
        }

        public void StartVirtualMachine(Guid virtualMachineId)
        {
            RequestStateChange(virtualMachineId, 2);
        }

        public Task StartVirtualMachineAsync(Guid virtualMachineId, CancellationToken cancellationToken)
        {
            return RunAsync(() => StartVirtualMachine(virtualMachineId), cancellationToken);
        }

        public void StopVirtualMachine(Guid virtualMachineId, bool force)
        {
            RequestStateChange(virtualMachineId, force ? (ushort)3 : (ushort)4);
        }

        public Task StopVirtualMachineAsync(Guid virtualMachineId, bool force, CancellationToken cancellationToken)
        {
            return RunAsync(() => StopVirtualMachine(virtualMachineId, force), cancellationToken);
        }

        public void ResetVirtualMachine(Guid virtualMachineId)
        {
            RequestStateChange(virtualMachineId, 11);
        }

        public Task ResetVirtualMachineAsync(Guid virtualMachineId, CancellationToken cancellationToken)
        {
            return RunAsync(() => ResetVirtualMachine(virtualMachineId), cancellationToken);
        }

        internal ManagementObject GetVirtualMachineObject(Guid id)
        {
            var query = "SELECT * FROM Msvm_ComputerSystem WHERE Caption = 'Virtual Machine' AND Name = '" + id.ToString("D").ToUpperInvariant() + "'";
            using (var searcher = new ManagementObjectSearcher(_scope, new ObjectQuery(query)))
            using (var results = searcher.Get())
            {
                foreach (ManagementObject vm in results)
                {
                    return vm;
                }
            }

            throw new HyperVVirtualMachineNotFoundException(id);
        }

        internal static void EnsureCompleted(string wmiClass, string methodName, ManagementBaseObject outParams, ManagementScope scope)
        {
            var returnCode = Convert.ToUInt32(outParams["ReturnValue"]);
            if (returnCode == WmiReturnCode.Completed)
            {
                return;
            }

            if (returnCode == WmiReturnCode.Started)
            {
                WaitForJob(wmiClass, methodName, outParams, scope);
                return;
            }

            if (returnCode == WmiReturnCode.AccessDenied)
            {
                throw new HyperVAccessDeniedException(string.Format("Access denied calling {0}.{1}.", wmiClass, methodName));
            }

            throw new HyperVWmiException(wmiClass, methodName, returnCode);
        }

        private static void WaitForJob(string wmiClass, string methodName, ManagementBaseObject outParams, ManagementScope scope)
        {
            var jobPath = outParams["Job"] as string;
            if (string.IsNullOrEmpty(jobPath))
            {
                throw new HyperVWmiException(wmiClass, methodName, WmiReturnCode.Started);
            }

            using (var job = new ManagementObject(scope, new ManagementPath(jobPath), null))
            {
                while (true)
                {
                    job.Get();
                    var state = Convert.ToUInt16(job["JobState"]);
                    if (state == 7)
                    {
                        return;
                    }

                    if (state != 3 && state != 4)
                    {
                        var errorCode = job["ErrorCode"] == null ? 32768 : Convert.ToUInt32(job["ErrorCode"]);
                        throw new HyperVWmiException(wmiClass, methodName, errorCode);
                    }

                    System.Threading.Thread.Sleep(250);
                }
            }
        }

        private HyperVVirtualMachine ToVirtualMachine(ManagementObject vm)
        {
            var stateValue = Convert.ToUInt16(vm["EnabledState"]);
            var state = Enum.IsDefined(typeof(HyperVVirtualMachineState), (int)stateValue)
                ? (HyperVVirtualMachineState)stateValue
                : HyperVVirtualMachineState.Unknown;
            var capabilities = GetConsoleCapabilities(vm);

            return new HyperVVirtualMachine
            {
                Id = Guid.Parse((string)vm["Name"]),
                Name = (string)vm["ElementName"],
                State = state,
                HostName = Environment.MachineName,
                IsRunning = stateValue == 2,
                IsPaused = stateValue == 9 || stateValue == 32776,
                SupportsConsoleCapture = capabilities.SupportsRawCapture,
                SupportsKeyboardInput = capabilities.SupportsKeyboardInput,
                SupportsMouseInput = capabilities.SupportsMouseInput,
                SupportsEnhancedSession = capabilities.SupportsEnhancedSession,
                RecommendedConsoleMode = capabilities.RecommendedMode
            };
        }

        private HyperVConsoleCapabilities GetConsoleCapabilities(ManagementObject vm)
        {
            var vmId = Guid.Parse((string)vm["Name"]);
            var vmName = (string)vm["ElementName"];
            var enhancedTransport = GetEnhancedSessionTransportType(vm);
            var hostEnhancedSessionEnabled = GetHostEnhancedSessionPolicy();
            var hasSyntheticMouse = GetFirstRelatedObject(vm, "Msvm_SyntheticMouse", "Msvm_SystemDevice", "PartComponent", "GroupComponent") != null;
            var limitations = new List<string>();
            if (!hostEnhancedSessionEnabled)
            {
                limitations.Add("Host Enhanced Session Mode policy is disabled.");
            }

            if (enhancedTransport == HyperVEnhancedSessionTransportType.Unknown)
            {
                limitations.Add("VM EnhancedSessionTransportType could not be determined.");
            }

            limitations.Add("Enhanced Session capture/control is VMConnect/RDP-over-VMBus brokered, not exposed as raw frames by this library.");

            var supportsEnhanced = hostEnhancedSessionEnabled && enhancedTransport != HyperVEnhancedSessionTransportType.Unknown;
            return new HyperVConsoleCapabilities
            {
                VirtualMachineId = vmId,
                VirtualMachineName = vmName,
                SupportsRawCapture = true,
                SupportsKeyboardInput = true,
                SupportsMouseInput = hasSyntheticMouse,
                SupportsEnhancedSession = supportsEnhanced,
                HostEnhancedSessionPolicyEnabled = hostEnhancedSessionEnabled,
                EnhancedSessionTransportType = enhancedTransport,
                RecommendedMode = supportsEnhanced ? HyperVConsoleMode.EnhancedSession : HyperVConsoleMode.RawHostConsole,
                Limitations = limitations.ToArray()
            };
        }

        private HyperVEnhancedSessionTransportType GetEnhancedSessionTransportType(ManagementObject vm)
        {
            using (var settings = GetFirstRelatedObject(vm, "Msvm_VirtualSystemSettingData", "Msvm_SettingsDefineState", "SettingData", "ManagedElement"))
            {
                if (settings == null || settings["EnhancedSessionTransportType"] == null)
                {
                    return HyperVEnhancedSessionTransportType.Unknown;
                }

                var value = Convert.ToUInt16(settings["EnhancedSessionTransportType"]);
                return value == 0 ? HyperVEnhancedSessionTransportType.Vmbus :
                    value == 1 ? HyperVEnhancedSessionTransportType.HvSocket :
                    HyperVEnhancedSessionTransportType.Unknown;
            }
        }

        private static bool GetHostEnhancedSessionPolicy()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Virtualization"))
                {
                    var value = key == null ? null : key.GetValue("EnhancedSessionMode");
                    return value != null && Convert.ToInt32(value) != 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string FindVmConnectPath()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "vmconnect.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "vmconnect.exe")
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private static ManagementObject GetFirstRelatedObject(ManagementObject source, string relatedClass, string associationClass, string resultRole, string thisRole)
        {
            using (var related = source.GetRelated(relatedClass, associationClass, null, null, resultRole, thisRole, false, null))
            {
                foreach (ManagementObject item in related)
                {
                    return item;
                }
            }

            return null;
        }

        private void RequestStateChange(Guid virtualMachineId, ushort requestedState)
        {
            lock (_wmiLock)
            {
                using (var vm = GetVirtualMachineObject(virtualMachineId))
                using (var inParams = vm.GetMethodParameters("RequestStateChange"))
                {
                    inParams["RequestedState"] = requestedState;
                    using (var outParams = vm.InvokeMethod("RequestStateChange", inParams, null))
                    {
                        EnsureCompleted("Msvm_ComputerSystem", "RequestStateChange", outParams, _scope);
                    }
                }
            }
        }

        private static Task RunAsync(Action action, CancellationToken cancellationToken)
        {
            return Task.Run(action, cancellationToken);
        }

        private static Task<T> RunAsync<T>(Func<T> action, CancellationToken cancellationToken)
        {
            return Task.Run(action, cancellationToken);
        }
    }

    internal sealed class HyperVConsoleSession : IHyperVConsoleSession
    {
        private readonly ManagementScope _scope;
        private readonly object _wmiLock;
        private readonly Guid _virtualMachineId;
        private bool _disposed;

        public HyperVConsoleSession(ManagementScope scope, object wmiLock, Guid virtualMachineId)
        {
            _scope = scope;
            _wmiLock = wmiLock;
            _virtualMachineId = virtualMachineId;
            lock (_wmiLock)
            {
                using (GetVirtualMachineObject())
                {
                }
            }
        }

        public Guid VirtualMachineId
        {
            get { return _virtualMachineId; }
        }

        public ConsoleFrame CaptureFrame(ConsoleFrameOptions options)
        {
            ThrowIfDisposed();
            if (options == null)
            {
                options = new ConsoleFrameOptions();
            }

            ValidateFrameOptions(options);

            lock (_wmiLock)
            {
                using (var vm = GetVirtualMachineObject())
                using (var settings = GetFirstRelatedObject(vm, "Msvm_VirtualSystemSettingData", "Msvm_SettingsDefineState", "SettingData", "ManagedElement"))
                using (var service = GetManagementService())
                using (var inParams = service.GetMethodParameters("GetVirtualSystemThumbnailImage"))
                {
                    if (settings == null)
                    {
                        throw new HyperVConsoleCaptureNotSupportedException("No Hyper-V virtual system settings object was found for this virtual machine.");
                    }

                    inParams["TargetSystem"] = settings.Path.Path;
                    inParams["WidthPixels"] = (ushort)options.Width;
                    inParams["HeightPixels"] = (ushort)options.Height;

                    using (var outParams = service.InvokeMethod("GetVirtualSystemThumbnailImage", inParams, null))
                    {
                        var returnCode = Convert.ToUInt32(outParams["ReturnValue"]);
                        if (returnCode == WmiReturnCode.NotSupported)
                        {
                            throw new HyperVConsoleCaptureNotSupportedException("Hyper-V console thumbnail capture is not supported for this virtual machine.");
                        }

                        HyperVConsoleClient.EnsureCompleted("Msvm_VirtualSystemManagementService", "GetVirtualSystemThumbnailImage", outParams, _scope);

                        var rawRgb565 = NormalizeRawRgb565((byte[])outParams["ImageData"], options.Width, options.Height);
                        return new ConsoleFrame
                        {
                            VirtualMachineId = _virtualMachineId,
                            CapturedUtc = DateTime.UtcNow,
                            Width = options.Width,
                            Height = options.Height,
                            PixelFormat = ConsoleFramePixelFormat.Rgb565,
                            RawBytes = rawRgb565
                        };
                    }
                }
            }
        }

        public Task<ConsoleFrame> CaptureFrameAsync(ConsoleFrameOptions options, CancellationToken cancellationToken)
        {
            return Task.Run(() => CaptureFrame(options), cancellationToken);
        }

        public async Task StreamFramesAsync(ConsoleFrameStreamOptions options, Func<ConsoleFrame, CancellationToken, Task> onFrame, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (onFrame == null)
            {
                throw new ArgumentNullException("onFrame");
            }

            if (options == null)
            {
                options = new ConsoleFrameStreamOptions();
            }

            ValidateStreamOptions(options);
            var captureOptions = new ConsoleFrameOptions { Width = options.Width, Height = options.Height };
            byte[] previousPayload = null;
            long sequenceNumber = 0;
            var framesSinceKeyFrame = 0;
            var byteWindowStarted = DateTime.UtcNow;
            long byteWindowTotal = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var started = DateTime.UtcNow;
                var captured = await CaptureFrameAsync(captureOptions, cancellationToken).ConfigureAwait(false);
                var converted = PixelCodec.ConvertRgb565(captured.RawBytes, options.PixelFormat);
                var forceKeyFrame = previousPayload == null || !options.SendChangedTilesOnly || framesSinceKeyFrame >= options.FullFrameInterval;
                var streamFrame = BuildStreamFrame(captured, converted, previousPayload, options, forceKeyFrame, sequenceNumber + 1);
                var changedBytes = streamFrame.UpdateKind == ConsoleFrameUpdateKind.FullFrame
                    ? streamFrame.PayloadBytes
                    : streamFrame.Tiles.Sum(t => (long)t.RawBytes.Length);
                var targetFps = !options.UseAdaptiveFrameRate
                    ? options.FramesPerSecond
                    : changedBytes < options.ActiveChangeThresholdBytes
                        ? options.IdleFramesPerSecond
                        : options.ActiveFramesPerSecond;

                streamFrame.TargetFramesPerSecond = targetFps;

                var shouldSend = CanSpendBytes(streamFrame.PayloadBytes, options.MaxBytesPerSecond, ref byteWindowStarted, ref byteWindowTotal, forceKeyFrame);
                if (shouldSend)
                {
                    await onFrame(streamFrame, cancellationToken).ConfigureAwait(false);
                    previousPayload = converted;
                    sequenceNumber++;
                    framesSinceKeyFrame = streamFrame.IsKeyFrame ? 0 : framesSinceKeyFrame + 1;
                }

                var elapsedMs = (int)(DateTime.UtcNow - started).TotalMilliseconds;
                var delayMs = Math.Max(1, (int)Math.Round(1000.0 / targetFps));
                var remainingMs = delayMs - elapsedMs;
                if (remainingMs > 0)
                {
                    await Task.Delay(remainingMs, cancellationToken).ConfigureAwait(false);
                }
                else if (!options.DropFramesWhenBehind)
                {
                    await Task.Yield();
                }
            }
        }

        public void SendText(string text)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            InvokeKeyboardMethod("TypeText", "asciiText", text);
        }

        public Task SendTextAsync(string text, CancellationToken cancellationToken)
        {
            return Task.Run(() => SendText(text), cancellationToken);
        }

        public void SendKey(ConsoleKeyCode key)
        {
            InvokeKeyboardMethod("TypeKey", "keyCode", (uint)key);
        }

        public Task SendKeyAsync(ConsoleKeyCode key, CancellationToken cancellationToken)
        {
            return Task.Run(() => SendKey(key), cancellationToken);
        }

        public void SendKeyDown(ConsoleKeyCode key)
        {
            InvokeKeyboardMethod("PressKey", "keyCode", (uint)key);
        }

        public Task SendKeyDownAsync(ConsoleKeyCode key, CancellationToken cancellationToken)
        {
            return Task.Run(() => SendKeyDown(key), cancellationToken);
        }

        public void SendKeyUp(ConsoleKeyCode key)
        {
            InvokeKeyboardMethod("ReleaseKey", "keyCode", (uint)key);
        }

        public Task SendKeyUpAsync(ConsoleKeyCode key, CancellationToken cancellationToken)
        {
            return Task.Run(() => SendKeyUp(key), cancellationToken);
        }

        public void SendChord(params ConsoleKeyCode[] keys)
        {
            ThrowIfDisposed();
            if (keys == null || keys.Length == 0)
            {
                return;
            }

            foreach (var key in keys)
            {
                SendKeyDown(key);
            }

            for (var index = keys.Length - 1; index >= 0; index--)
            {
                SendKeyUp(keys[index]);
            }
        }

        public Task SendChordAsync(CancellationToken cancellationToken, params ConsoleKeyCode[] keys)
        {
            return Task.Run(() => SendChord(keys), cancellationToken);
        }

        public void PasteTextAsKeystrokes(string text, ConsolePasteOptions options)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (options == null)
            {
                options = new ConsolePasteOptions();
            }

            for (var index = 0; index < text.Length; index++)
            {
                var character = text[index];
                if (options.ConvertLineEndingsToEnter && character == '\r')
                {
                    if (index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        index++;
                    }

                    SendKey(ConsoleKeyCode.Enter);
                    SleepIfPositive(options.DelayAfterNewLineMs);
                    continue;
                }

                if (options.ConvertLineEndingsToEnter && character == '\n')
                {
                    SendKey(ConsoleKeyCode.Enter);
                    SleepIfPositive(options.DelayAfterNewLineMs);
                    continue;
                }

                SendText(character.ToString());
                SleepIfPositive(options.DelayBetweenCharactersMs);
            }
        }

        public Task PasteTextAsKeystrokesAsync(string text, ConsolePasteOptions options, CancellationToken cancellationToken)
        {
            return Task.Run(() => PasteTextAsKeystrokes(text, options), cancellationToken);
        }

        public void SendScancodes(byte[] scancodes)
        {
            ThrowIfDisposed();
            if (scancodes == null || scancodes.Length == 0)
            {
                return;
            }

            InvokeKeyboardMethod("TypeScancodes", "scanCodes", scancodes);
        }

        public Task SendScancodesAsync(byte[] scancodes, CancellationToken cancellationToken)
        {
            return Task.Run(() => SendScancodes(scancodes), cancellationToken);
        }

        public void SendCtrlAltDel()
        {
            InvokeKeyboardMethod("TypeCtrlAltDel", null, null);
        }

        public Task SendCtrlAltDelAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() => SendCtrlAltDel(), cancellationToken);
        }

        public bool TrySendMouseMove(int x, int y)
        {
            ThrowIfDisposed();
            try
            {
                using (var mouse = GetMouseObject())
                {
                    if (mouse == null)
                    {
                        return false;
                    }

                    using (var inParams = mouse.GetMethodParameters("SetAbsolutePosition"))
                    {
                        inParams["HorizontalPosition"] = ClampMousePosition(x);
                        inParams["VerticalPosition"] = ClampMousePosition(y);
                        using (var outParams = mouse.InvokeMethod("SetAbsolutePosition", inParams, null))
                        {
                            return IsSuccessfulMouseReturn(outParams);
                        }
                    }
                }
            }
            catch (ManagementException)
            {
                return false;
            }
        }

        public Task<bool> TrySendMouseMoveAsync(int x, int y, CancellationToken cancellationToken)
        {
            return Task.FromResult(TrySendMouseMove(x, y));
        }

        public bool TrySendMouseClick(int x, int y, MouseButton button)
        {
            ThrowIfDisposed();
            try
            {
                if (!TrySendMouseMove(x, y))
                {
                    return false;
                }

                using (var mouse = GetMouseObject())
                {
                    if (mouse == null)
                    {
                        return false;
                    }

                    using (var inParams = mouse.GetMethodParameters("ClickButton"))
                    {
                        inParams["ButtonIndex"] = (ushort)button;
                        using (var outParams = mouse.InvokeMethod("ClickButton", inParams, null))
                        {
                            return IsSuccessfulMouseReturn(outParams);
                        }
                    }
                }
            }
            catch (ManagementException)
            {
                return false;
            }
        }

        public Task<bool> TrySendMouseClickAsync(int x, int y, MouseButton button, CancellationToken cancellationToken)
        {
            return Task.FromResult(TrySendMouseClick(x, y, button));
        }

        public bool TrySendMouseDoubleClick(int x, int y, MouseButton button)
        {
            if (!TrySendMouseClick(x, y, button))
            {
                return false;
            }

            System.Threading.Thread.Sleep(100);
            return TrySendMouseClick(x, y, button);
        }

        public Task<bool> TrySendMouseDoubleClickAsync(int x, int y, MouseButton button, CancellationToken cancellationToken)
        {
            return Task.FromResult(TrySendMouseDoubleClick(x, y, button));
        }

        public void Dispose()
        {
            _disposed = true;
        }

        private void InvokeKeyboardMethod(string methodName, string parameterName, object value)
        {
            ThrowIfDisposed();
            lock (_wmiLock)
            {
                using (var vm = GetVirtualMachineObject())
                using (var keyboard = GetFirstRelatedObject(vm, "Msvm_Keyboard", "Msvm_SystemDevice", "PartComponent", "GroupComponent"))
                {
                    if (keyboard == null)
                    {
                        throw new HyperVKeyboardNotSupportedException("No Hyper-V keyboard device was found for this virtual machine.");
                    }

                    using (var inParams = string.IsNullOrEmpty(parameterName) ? null : keyboard.GetMethodParameters(methodName))
                    {
                        if (inParams != null)
                        {
                            inParams[parameterName] = value;
                        }

                        using (var outParams = keyboard.InvokeMethod(methodName, inParams, null))
                        {
                            var returnCode = Convert.ToUInt32(outParams["ReturnValue"]);
                            if (returnCode == WmiReturnCode.NotSupported)
                            {
                                throw new HyperVKeyboardNotSupportedException("Hyper-V keyboard input is not supported for this virtual machine.");
                            }

                            HyperVConsoleClient.EnsureCompleted("Msvm_Keyboard", methodName, outParams, _scope);
                        }
                    }
                }
            }
        }

        private ManagementObject GetMouseObject()
        {
            lock (_wmiLock)
            {
                using (var vm = GetVirtualMachineObject())
                {
                    return GetFirstRelatedObject(vm, "Msvm_SyntheticMouse", "Msvm_SystemDevice", "PartComponent", "GroupComponent")
                        ?? GetFirstRelatedObject(vm, "Msvm_Ps2Mouse", "Msvm_SystemDevice", "PartComponent", "GroupComponent");
                }
            }
        }

        private static ushort ClampMousePosition(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            if (value > 32767)
            {
                return 32767;
            }

            return (ushort)value;
        }

        private static bool IsSuccessfulMouseReturn(ManagementBaseObject outParams)
        {
            var returnCode = Convert.ToUInt32(outParams["ReturnValue"]);
            return returnCode == WmiReturnCode.Completed || returnCode == WmiReturnCode.Started;
        }

        private static void SleepIfPositive(int delayMs)
        {
            if (delayMs > 0)
            {
                System.Threading.Thread.Sleep(delayMs);
            }
        }

        private ManagementObject GetVirtualMachineObject()
        {
            var query = "SELECT * FROM Msvm_ComputerSystem WHERE Caption = 'Virtual Machine' AND Name = '" + _virtualMachineId.ToString("D").ToUpperInvariant() + "'";
            using (var searcher = new ManagementObjectSearcher(_scope, new ObjectQuery(query)))
            using (var results = searcher.Get())
            {
                foreach (ManagementObject vm in results)
                {
                    return vm;
                }
            }

            throw new HyperVVirtualMachineNotFoundException(_virtualMachineId);
        }

        private ManagementObject GetManagementService()
        {
            using (var searcher = new ManagementObjectSearcher(_scope, new ObjectQuery("SELECT * FROM Msvm_VirtualSystemManagementService")))
            using (var results = searcher.Get())
            {
                foreach (ManagementObject service in results)
                {
                    return service;
                }
            }

            throw new HyperVConsoleException("Msvm_VirtualSystemManagementService was not found.");
        }

        private static ManagementObject GetFirstRelatedObject(ManagementObject source, string relatedClass, string associationClass, string resultRole, string thisRole)
        {
            using (var related = source.GetRelated(relatedClass, associationClass, null, null, resultRole, thisRole, false, null))
            {
                foreach (ManagementObject item in related)
                {
                    return item;
                }
            }

            return null;
        }

        private static void ValidateFrameOptions(ConsoleFrameOptions options)
        {
            if (options.Width < 1 || options.Width > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException("options", "Width must be between 1 and 65535.");
            }

            if (options.Height < 1 || options.Height > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException("options", "Height must be between 1 and 65535.");
            }
        }

        private static byte[] NormalizeRawRgb565(byte[] imageData, int width, int height)
        {
            if (imageData == null)
            {
                throw new HyperVConsoleException("Hyper-V returned no image data for the requested frame.");
            }

            var expectedLength = checked(width * height * 2);
            if (imageData.Length < expectedLength)
            {
                throw new HyperVConsoleException("Hyper-V returned fewer RGB565 bytes than expected for the requested frame size.");
            }

            if (imageData.Length == expectedLength)
            {
                return imageData;
            }

            var trimmed = new byte[expectedLength];
            Buffer.BlockCopy(imageData, 0, trimmed, 0, expectedLength);
            return trimmed;
        }

        private static void ValidateStreamOptions(ConsoleFrameStreamOptions options)
        {
            ValidateFrameOptions(new ConsoleFrameOptions { Width = options.Width, Height = options.Height });
            if (options.FramesPerSecond <= 0 || options.FramesPerSecond > 60)
            {
                throw new ArgumentOutOfRangeException("options", "FramesPerSecond must be greater than 0 and no more than 60.");
            }

            if (options.ActiveFramesPerSecond <= 0 || options.ActiveFramesPerSecond > 60)
            {
                throw new ArgumentOutOfRangeException("options", "ActiveFramesPerSecond must be greater than 0 and no more than 60.");
            }

            if (options.IdleFramesPerSecond <= 0 || options.IdleFramesPerSecond > 60)
            {
                throw new ArgumentOutOfRangeException("options", "IdleFramesPerSecond must be greater than 0 and no more than 60.");
            }

            if (options.MaxBytesPerSecond.HasValue && options.MaxBytesPerSecond.Value <= 0)
            {
                throw new ArgumentOutOfRangeException("options", "MaxBytesPerSecond must be greater than 0 when specified.");
            }

            if (options.TileWidth < 1 || options.TileHeight < 1)
            {
                throw new ArgumentOutOfRangeException("options", "TileWidth and TileHeight must be greater than 0.");
            }

            if (options.FullFrameInterval < 1)
            {
                throw new ArgumentOutOfRangeException("options", "FullFrameInterval must be greater than 0.");
            }
        }

        private ConsoleFrame BuildStreamFrame(ConsoleFrame captured, byte[] converted, byte[] previousPayload, ConsoleFrameStreamOptions options, bool forceKeyFrame, long sequenceNumber)
        {
            if (forceKeyFrame)
            {
                return new ConsoleFrame
                {
                    VirtualMachineId = _virtualMachineId,
                    CapturedUtc = captured.CapturedUtc,
                    SequenceNumber = sequenceNumber,
                    Width = captured.Width,
                    Height = captured.Height,
                    PixelFormat = options.PixelFormat,
                    UpdateKind = ConsoleFrameUpdateKind.FullFrame,
                    BytesPerPixelNumerator = PixelCodec.GetBytesPerPixelNumerator(options.PixelFormat),
                    BytesPerPixelDenominator = PixelCodec.GetBytesPerPixelDenominator(options.PixelFormat),
                    RawBytes = converted,
                    Tiles = new ConsoleFrameTile[0],
                    IsKeyFrame = true,
                    PayloadBytes = converted.Length
                };
            }

            var tiles = PixelCodec.GetChangedTiles(previousPayload, converted, captured.Width, captured.Height, options.PixelFormat, options.TileWidth, options.TileHeight);
            var payloadBytes = tiles.Sum(t => (long)t.RawBytes.Length);
            return new ConsoleFrame
            {
                VirtualMachineId = _virtualMachineId,
                CapturedUtc = captured.CapturedUtc,
                SequenceNumber = sequenceNumber,
                Width = captured.Width,
                Height = captured.Height,
                PixelFormat = options.PixelFormat,
                UpdateKind = ConsoleFrameUpdateKind.ChangedTiles,
                BytesPerPixelNumerator = PixelCodec.GetBytesPerPixelNumerator(options.PixelFormat),
                BytesPerPixelDenominator = PixelCodec.GetBytesPerPixelDenominator(options.PixelFormat),
                RawBytes = null,
                Tiles = tiles,
                IsKeyFrame = false,
                PayloadBytes = payloadBytes
            };
        }

        private static bool CanSpendBytes(long payloadBytes, long? maxBytesPerSecond, ref DateTime windowStarted, ref long windowTotal, bool forceKeyFrame)
        {
            if (!maxBytesPerSecond.HasValue)
            {
                return true;
            }

            var now = DateTime.UtcNow;
            if ((now - windowStarted).TotalSeconds >= 1)
            {
                windowStarted = now;
                windowTotal = 0;
            }

            if (forceKeyFrame && windowTotal == 0)
            {
                windowTotal += payloadBytes;
                return true;
            }

            if (windowTotal + payloadBytes > maxBytesPerSecond.Value)
            {
                return false;
            }

            windowTotal += payloadBytes;
            return true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("HyperVConsoleSession");
            }
        }
    }

    internal static class PixelCodec
    {
        public static byte[] ConvertRgb565(byte[] rgb565, ConsoleFramePixelFormat format)
        {
            if (format == ConsoleFramePixelFormat.Rgb565)
            {
                var copy = new byte[rgb565.Length];
                Buffer.BlockCopy(rgb565, 0, copy, 0, rgb565.Length);
                return copy;
            }

            var pixelCount = rgb565.Length / 2;
            var output = new byte[GetPackedLength(pixelCount, format)];
            for (var pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
            {
                var sourceIndex = pixelIndex * 2;
                var value = rgb565[sourceIndex] | (rgb565[sourceIndex + 1] << 8);
                var red = ((value >> 11) & 0x1F) * 255 / 31;
                var green = ((value >> 5) & 0x3F) * 255 / 63;
                var blue = (value & 0x1F) * 255 / 31;

                switch (format)
                {
                    case ConsoleFramePixelFormat.Rgb332:
                        output[pixelIndex] = (byte)((red & 0xE0) | ((green & 0xE0) >> 3) | (blue >> 6));
                        break;
                    case ConsoleFramePixelFormat.Gray8:
                        output[pixelIndex] = ToGray(red, green, blue);
                        break;
                    case ConsoleFramePixelFormat.Gray4:
                        SetPacked4(output, pixelIndex, ToGray(red, green, blue) >> 4);
                        break;
                    case ConsoleFramePixelFormat.Mono1:
                        SetPacked1(output, pixelIndex, ToGray(red, green, blue) >= 128 ? 1 : 0);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("format");
                }
            }

            return output;
        }

        public static IReadOnlyList<ConsoleFrameTile> GetChangedTiles(byte[] previous, byte[] current, int width, int height, ConsoleFramePixelFormat format, int tileWidth, int tileHeight)
        {
            var tiles = new List<ConsoleFrameTile>();
            for (var y = 0; y < height; y += tileHeight)
            {
                var actualHeight = Math.Min(tileHeight, height - y);
                for (var x = 0; x < width; x += tileWidth)
                {
                    var actualWidth = Math.Min(tileWidth, width - x);
                    if (!TileEquals(previous, current, width, x, y, actualWidth, actualHeight, format))
                    {
                        tiles.Add(new ConsoleFrameTile
                        {
                            X = x,
                            Y = y,
                            Width = actualWidth,
                            Height = actualHeight,
                            RawBytes = ExtractTile(current, width, x, y, actualWidth, actualHeight, format)
                        });
                    }
                }
            }

            return tiles;
        }

        public static int GetBytesPerPixelNumerator(ConsoleFramePixelFormat format)
        {
            switch (format)
            {
                case ConsoleFramePixelFormat.Rgb565: return 2;
                case ConsoleFramePixelFormat.Rgb332:
                case ConsoleFramePixelFormat.Gray8: return 1;
                case ConsoleFramePixelFormat.Gray4: return 1;
                case ConsoleFramePixelFormat.Mono1: return 1;
                default: throw new ArgumentOutOfRangeException("format");
            }
        }

        public static int GetBytesPerPixelDenominator(ConsoleFramePixelFormat format)
        {
            switch (format)
            {
                case ConsoleFramePixelFormat.Gray4: return 2;
                case ConsoleFramePixelFormat.Mono1: return 8;
                default: return 1;
            }
        }

        private static bool TileEquals(byte[] previous, byte[] current, int frameWidth, int x, int y, int width, int height, ConsoleFramePixelFormat format)
        {
            for (var row = 0; row < height; row++)
            {
                for (var col = 0; col < width; col++)
                {
                    var pixelIndex = (y + row) * frameWidth + x + col;
                    if (GetPixelValue(previous, pixelIndex, format) != GetPixelValue(current, pixelIndex, format))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static byte[] ExtractTile(byte[] source, int frameWidth, int x, int y, int width, int height, ConsoleFramePixelFormat format)
        {
            var output = new byte[GetPackedLength(width * height, format)];
            var targetPixel = 0;
            for (var row = 0; row < height; row++)
            {
                for (var col = 0; col < width; col++)
                {
                    var sourcePixel = (y + row) * frameWidth + x + col;
                    SetPixelValue(output, targetPixel++, format, GetPixelValue(source, sourcePixel, format));
                }
            }

            return output;
        }

        private static int GetPackedLength(int pixelCount, ConsoleFramePixelFormat format)
        {
            switch (format)
            {
                case ConsoleFramePixelFormat.Rgb565: return pixelCount * 2;
                case ConsoleFramePixelFormat.Rgb332:
                case ConsoleFramePixelFormat.Gray8: return pixelCount;
                case ConsoleFramePixelFormat.Gray4: return (pixelCount + 1) / 2;
                case ConsoleFramePixelFormat.Mono1: return (pixelCount + 7) / 8;
                default: throw new ArgumentOutOfRangeException("format");
            }
        }

        private static int GetPixelValue(byte[] source, int pixelIndex, ConsoleFramePixelFormat format)
        {
            switch (format)
            {
                case ConsoleFramePixelFormat.Rgb565:
                    return source[pixelIndex * 2] | (source[pixelIndex * 2 + 1] << 8);
                case ConsoleFramePixelFormat.Rgb332:
                case ConsoleFramePixelFormat.Gray8:
                    return source[pixelIndex];
                case ConsoleFramePixelFormat.Gray4:
                    return GetPacked4(source, pixelIndex);
                case ConsoleFramePixelFormat.Mono1:
                    return GetPacked1(source, pixelIndex);
                default:
                    throw new ArgumentOutOfRangeException("format");
            }
        }

        private static void SetPixelValue(byte[] target, int pixelIndex, ConsoleFramePixelFormat format, int value)
        {
            switch (format)
            {
                case ConsoleFramePixelFormat.Rgb565:
                    target[pixelIndex * 2] = (byte)(value & 0xFF);
                    target[pixelIndex * 2 + 1] = (byte)((value >> 8) & 0xFF);
                    break;
                case ConsoleFramePixelFormat.Rgb332:
                case ConsoleFramePixelFormat.Gray8:
                    target[pixelIndex] = (byte)value;
                    break;
                case ConsoleFramePixelFormat.Gray4:
                    SetPacked4(target, pixelIndex, value);
                    break;
                case ConsoleFramePixelFormat.Mono1:
                    SetPacked1(target, pixelIndex, value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("format");
            }
        }

        private static byte ToGray(int red, int green, int blue)
        {
            return (byte)((red * 299 + green * 587 + blue * 114) / 1000);
        }

        private static int GetPacked4(byte[] source, int pixelIndex)
        {
            var value = source[pixelIndex / 2];
            return pixelIndex % 2 == 0 ? (value >> 4) & 0x0F : value & 0x0F;
        }

        private static void SetPacked4(byte[] target, int pixelIndex, int value)
        {
            var index = pixelIndex / 2;
            if (pixelIndex % 2 == 0)
            {
                target[index] = (byte)((target[index] & 0x0F) | ((value & 0x0F) << 4));
            }
            else
            {
                target[index] = (byte)((target[index] & 0xF0) | (value & 0x0F));
            }
        }

        private static int GetPacked1(byte[] source, int pixelIndex)
        {
            return (source[pixelIndex / 8] >> (7 - (pixelIndex % 8))) & 1;
        }

        private static void SetPacked1(byte[] target, int pixelIndex, int value)
        {
            if (value != 0)
            {
                target[pixelIndex / 8] = (byte)(target[pixelIndex / 8] | (1 << (7 - (pixelIndex % 8))));
            }
        }
    }
}

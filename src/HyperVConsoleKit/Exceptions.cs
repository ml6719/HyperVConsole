using System;

namespace HyperVConsoleKit
{
    /// <summary>
    /// Base exception for HyperVConsoleKit failures.
    /// </summary>
    public class HyperVConsoleException : Exception
    {
        public HyperVConsoleException(string message) : base(message) { }
        public HyperVConsoleException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Thrown when Hyper-V WMI access is denied.
    /// </summary>
    public sealed class HyperVAccessDeniedException : HyperVConsoleException
    {
        public HyperVAccessDeniedException(string message, Exception innerException = null) : base(message, innerException) { }
    }

    /// <summary>
    /// Thrown when the requested VM id cannot be found on the local Hyper-V host.
    /// </summary>
    public sealed class HyperVVirtualMachineNotFoundException : HyperVConsoleException
    {
        public HyperVVirtualMachineNotFoundException(Guid virtualMachineId)
            : base("Hyper-V virtual machine was not found: " + virtualMachineId)
        {
            VirtualMachineId = virtualMachineId;
        }

        public Guid VirtualMachineId { get; private set; }
    }

    /// <summary>
    /// Thrown when Hyper-V console frame capture is not available.
    /// </summary>
    public sealed class HyperVConsoleCaptureNotSupportedException : HyperVConsoleException
    {
        public HyperVConsoleCaptureNotSupportedException(string message) : base(message) { }
    }

    /// <summary>
    /// Thrown when Hyper-V virtual keyboard input is not available.
    /// </summary>
    public sealed class HyperVKeyboardNotSupportedException : HyperVConsoleException
    {
        public HyperVKeyboardNotSupportedException(string message) : base(message) { }
    }

    /// <summary>
    /// Thrown when Hyper-V virtual mouse input is not available.
    /// </summary>
    public sealed class HyperVMouseNotSupportedException : HyperVConsoleException
    {
        public HyperVMouseNotSupportedException(string message) : base(message) { }
    }

    /// <summary>
    /// Thrown when a Hyper-V WMI method returns a failure code.
    /// </summary>
    public sealed class HyperVWmiException : HyperVConsoleException
    {
        public HyperVWmiException(string wmiClass, string methodName, uint returnCode)
            : base(string.Format("Hyper-V WMI call failed. Class={0}, Method={1}, ReturnCode={2} ({3}).", wmiClass, methodName, returnCode, WmiReturnCode.ToText(returnCode)))
        {
            WmiClass = wmiClass;
            MethodName = methodName;
            ReturnCode = returnCode;
        }

        public string WmiClass { get; private set; }
        public string MethodName { get; private set; }
        public uint ReturnCode { get; private set; }
    }
}

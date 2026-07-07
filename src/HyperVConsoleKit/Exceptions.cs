using System;

namespace HyperVConsoleKit
{
    public class HyperVConsoleException : Exception
    {
        public HyperVConsoleException(string message) : base(message) { }
        public HyperVConsoleException(string message, Exception innerException) : base(message, innerException) { }
    }

    public sealed class HyperVAccessDeniedException : HyperVConsoleException
    {
        public HyperVAccessDeniedException(string message, Exception innerException = null) : base(message, innerException) { }
    }

    public sealed class HyperVVirtualMachineNotFoundException : HyperVConsoleException
    {
        public HyperVVirtualMachineNotFoundException(Guid virtualMachineId)
            : base("Hyper-V virtual machine was not found: " + virtualMachineId)
        {
            VirtualMachineId = virtualMachineId;
        }

        public Guid VirtualMachineId { get; private set; }
    }

    public sealed class HyperVConsoleCaptureNotSupportedException : HyperVConsoleException
    {
        public HyperVConsoleCaptureNotSupportedException(string message) : base(message) { }
    }

    public sealed class HyperVKeyboardNotSupportedException : HyperVConsoleException
    {
        public HyperVKeyboardNotSupportedException(string message) : base(message) { }
    }

    public sealed class HyperVMouseNotSupportedException : HyperVConsoleException
    {
        public HyperVMouseNotSupportedException(string message) : base(message) { }
    }

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

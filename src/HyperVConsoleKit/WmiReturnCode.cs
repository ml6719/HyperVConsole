namespace HyperVConsoleKit
{
    internal static class WmiReturnCode
    {
        public const uint Completed = 0;
        public const uint Started = 4096;
        public const uint AccessDenied = 32769;
        public const uint NotSupported = 32770;

        public static string ToText(uint code)
        {
            switch (code)
            {
                case 0: return "Completed";
                case 4096: return "Job Started";
                case 32768: return "Failed";
                case 32769: return "Access Denied";
                case 32770: return "Not Supported";
                case 32771: return "Unknown Status";
                case 32772: return "Timeout";
                case 32773: return "Invalid Parameter";
                case 32774: return "System In Use";
                case 32775: return "Invalid State";
                case 32776: return "Incorrect Data Type";
                case 32777: return "System Not Available";
                case 32778: return "Out Of Memory";
                default: return "Unknown";
            }
        }
    }
}

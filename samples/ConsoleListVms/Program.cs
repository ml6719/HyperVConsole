using HyperVConsoleKit;

var client = new HyperVConsoleClient();
var vms = client.GetVirtualMachines();

Console.WriteLine("Name\tId\tState\tCanCapture\tCanKeyboard\tCanMouse\tEnhanced\tRecommended");
foreach (var vm in vms)
{
    Console.WriteLine($"{vm.Name}\t{vm.Id}\t{vm.State}\t{vm.CanCaptureNow}\t{vm.CanSendKeyboardInputNow}\t{vm.CanSendMouseInputNow}\t{vm.SupportsEnhancedSession}\t{vm.RecommendedConsoleMode}");
}

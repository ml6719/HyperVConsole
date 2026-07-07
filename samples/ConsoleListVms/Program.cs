using HyperVConsoleKit;

var client = new HyperVConsoleClient();
var vms = client.GetVirtualMachines();

Console.WriteLine("Name\tId\tState\tConsole\tKeyboard\tMouse\tEnhanced\tRecommended");
foreach (var vm in vms)
{
    Console.WriteLine($"{vm.Name}\t{vm.Id}\t{vm.State}\t{vm.SupportsConsoleCapture}\t{vm.SupportsKeyboardInput}\t{vm.SupportsMouseInput}\t{vm.SupportsEnhancedSession}\t{vm.RecommendedConsoleMode}");
}

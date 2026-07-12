using HyperVConsoleKit;

var client = new HyperVConsoleClient();
var vms = client.GetVirtualMachines();
if (vms.Count == 0)
{
    Console.Error.WriteLine("No Hyper-V virtual machines were found.");
    return 1;
}

var vm = ResolveVm(args, vms);
var outputPath = args.Length >= 2 ? args[1] : Path.Combine(Environment.CurrentDirectory, $"{Sanitize(vm.Name)}-{DateTime.UtcNow:yyyyMMddHHmmss}.rgb565");

using (var session = client.OpenConsole(vm.Id))
{
    var frame = await session.CaptureFrameAsync(new ConsoleFrameOptions(), CancellationToken.None);

    File.WriteAllBytes(outputPath, frame.RawBytes);
    Console.WriteLine($"Captured {frame.Width}x{frame.Height} RGB565 frame.");
}

Console.WriteLine($"Captured raw RGB565 {vm.Name} frame to {outputPath}");
return 0;

static HyperVVirtualMachine ResolveVm(string[] args, IReadOnlyList<HyperVVirtualMachine> vms)
{
    if (args.Length == 0)
    {
        return vms.FirstOrDefault(vm => vm.IsRunning) ?? vms[0];
    }

    if (Guid.TryParse(args[0], out var id))
    {
        return vms.FirstOrDefault(vm => vm.Id == id) ?? throw new HyperVVirtualMachineNotFoundException(id);
    }

    return vms.FirstOrDefault(vm => string.Equals(vm.Name, args[0], StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException("VM name was not found: " + args[0]);
}

static string Sanitize(string value)
{
    foreach (var invalid in Path.GetInvalidFileNameChars())
    {
        value = value.Replace(invalid, '_');
    }

    return value;
}

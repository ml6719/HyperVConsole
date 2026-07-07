using HyperVConsoleKit;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: ConsoleKeyboardInput <vm-id-or-name> <text|enter|escape|tab|f8|f12|ctrlaltdel|alt+tab|ctrl+shift+esc|paste:<text>>");
    return 1;
}

var client = new HyperVConsoleClient();
var vm = ResolveVm(args[0], client.GetVirtualMachines());

using (var session = client.OpenConsole(vm.Id))
{
    foreach (var command in args.Skip(1))
    {
        Send(session, command);
    }
}

Console.WriteLine($"Sent {args.Length - 1} keyboard command(s) to {vm.Name}.");
return 0;

static void Send(IHyperVConsoleSession session, string command)
{
    switch (command.ToLowerInvariant())
    {
        case "enter":
            session.SendKey(ConsoleKeyCode.Enter);
            return;
        case "escape":
        case "esc":
            session.SendKey(ConsoleKeyCode.Escape);
            return;
        case "tab":
            session.SendKey(ConsoleKeyCode.Tab);
            return;
        case "f8":
            session.SendKey(ConsoleKeyCode.F8);
            return;
        case "f12":
            session.SendKey(ConsoleKeyCode.F12);
            return;
        case "ctrlaltdel":
        case "cad":
            session.SendCtrlAltDel();
            return;
        case "alt+tab":
            session.SendChord(ConsoleKeyCode.Alt, ConsoleKeyCode.Tab);
            return;
        case "ctrl+shift+esc":
            session.SendChord(ConsoleKeyCode.Control, ConsoleKeyCode.Shift, ConsoleKeyCode.Escape);
            return;
        default:
            if (command.StartsWith("paste:", StringComparison.OrdinalIgnoreCase))
            {
                session.PasteTextAsKeystrokes(command.Substring("paste:".Length), new ConsolePasteOptions());
                return;
            }

            session.SendText(command);
            return;
    }
}

static HyperVVirtualMachine ResolveVm(string value, IReadOnlyList<HyperVVirtualMachine> vms)
{
    if (Guid.TryParse(value, out var id))
    {
        return vms.FirstOrDefault(vm => vm.Id == id) ?? throw new HyperVVirtualMachineNotFoundException(id);
    }

    return vms.FirstOrDefault(vm => string.Equals(vm.Name, value, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException("VM name was not found: " + value);
}

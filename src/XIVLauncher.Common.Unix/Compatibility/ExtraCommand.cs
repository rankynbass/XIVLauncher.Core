namespace XIVLauncher.Common.Unix.Compatibility;

public struct ExtraCommand
{
    public string Command { get; private set; }

    public string Arguments { get; private set; }
    public ExtraCommand(string command, string arguments)
    {
        Command = command;
        Arguments = arguments;
    }
}
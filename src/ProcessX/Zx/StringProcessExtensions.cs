using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Zx;

public static class StringProcessExtensions
{
    public static TaskAwaiter<string> GetAwaiter(this string command)
    {
        return ProcessCommand(command).GetAwaiter();
    }

    public static TaskAwaiter GetAwaiter(this string[] commands)
    {
        async Task ProcessCommands()
        {
            await Task.WhenAll(commands.Select(ProcessCommand));
        }

        return ProcessCommands().GetAwaiter();
    }

    private static Task<string> ProcessCommand(string command)
    {
        if (TryChangeDirectory(command))
        {
            return Task.FromResult("");
        }

        return Env.process(command);
    }

    private static bool TryChangeDirectory(string command)
    {
        if (command.StartsWith("cd ") || command.StartsWith("chdir "))
        {
            var path = Regex.Replace(command, "^cd|^chdir", "").Trim();
            Environment.CurrentDirectory = Path.Combine(Environment.CurrentDirectory, path);
            return true;
        }

        return false;
    }
}

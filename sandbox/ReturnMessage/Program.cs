﻿using Microsoft.Extensions.Hosting;

namespace ReturnMessage;

public class Program : ConsoleAppBase
{
    private static async Task Main(string[] args) => await Host.CreateDefaultBuilder().RunConsoleAppFrameworkAsync<Program>(args);

    [Command("str")]
    public static void StringWrite([Option("m")]string echoMesage, [Option("c")]int repeatCount)
    {
        for (var i = 0; i < repeatCount; i++)
        {
            Console.WriteLine(echoMesage);
        }
    }

    [Command("bin")]
    public static async Task BinaryWrite([Option("s")]int writeSize, [Option("c")]int repeatCount, [Option("w")]int waitMilliseconds)
    {
        var stdOut = Console.OpenStandardOutput();
        for (var i = 0; i < repeatCount; i++)
        {
            var bin = new byte[writeSize];
            Array.Fill<byte>(bin, unchecked((byte)(i + 1)));
            await stdOut.WriteAsync(bin);

            await Task.Delay(waitMilliseconds);
        }
    }
}

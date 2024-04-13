#pragma warning disable CA2012  // Use ValueTasks correctly.
#pragma warning disable CS8603  // Possible null reference return.
#pragma warning disable IDE1006 // Naming Styles.
#pragma warning disable MA0042  // Do not use blocking calls in an async method.

using System.Diagnostics;
using System.Threading.Channels;

namespace Cysharp.Diagnostics;

internal sealed class ProcessAsyncEnumerator : IAsyncEnumerator<string>
{
    private readonly Process? process;
    private readonly ChannelReader<string> channel;
    private readonly CancellationToken cancellationToken;
    private readonly CancellationTokenRegistration cancellationTokenRegistration;
    private string? current;
    private bool disposed;

    public ProcessAsyncEnumerator(Process? process, ChannelReader<string> channel, CancellationToken cancellationToken)
    {
        // Process is not null, kill when canceled.
        this.process = process;
        this.channel = channel;
        this.cancellationToken = cancellationToken;
        if (cancellationToken.CanBeCanceled)
        {
            cancellationTokenRegistration = cancellationToken.Register(() => _ = DisposeAsync());
        }
    }

    public string Current => current; // When call after MoveNext, current always not null.

    public async ValueTask<bool> MoveNextAsync() =>
        channel.TryRead(out current) ||
        (await channel.WaitToReadAsync(cancellationToken).ConfigureAwait(false) && channel.TryRead(out current));

    public ValueTask DisposeAsync()
    {
        if (!disposed)
        {
            disposed = true;
            try
            {
                cancellationTokenRegistration.Dispose();
                if (process is not null)
                {
                    process.EnableRaisingEvents = false;
                    if (!process.HasExited) process.Kill();
                }
            }
            finally { process?.Dispose(); }
        }

        return default;
    }
}

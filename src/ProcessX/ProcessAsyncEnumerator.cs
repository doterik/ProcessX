using System.Diagnostics;
using System.Threading.Channels;

namespace Cysharp.Diagnostics;

internal class ProcessAsyncEnumerator : IAsyncEnumerator<string>
{
    private readonly Process? process;
    private readonly ChannelReader<string> channel;
    private readonly CancellationToken cancellationToken;
    private readonly CancellationTokenRegistration cancellationTokenRegistration;
    private string? current;
    private bool disposed;

    public ProcessAsyncEnumerator(Process? process, ChannelReader<string> channel, CancellationToken cancellationToken)
    {
        // process is not null, kill when canceled.
        this.process = process;
        this.channel = channel;
        this.cancellationToken = cancellationToken;
        if (cancellationToken.CanBeCanceled)
        {
            cancellationTokenRegistration = cancellationToken.Register(() =>
            {
                _ = DisposeAsync();
            });
        }
    }

#pragma warning disable CS8603
    // when call after MoveNext, current always not null.
    public string Current => current;
#pragma warning restore CS8603

    public async ValueTask<bool> MoveNextAsync()
    {
        if (channel.TryRead(out current))
        {
            return true;
        }
        else
        {
            if (await channel.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (channel.TryRead(out current))
                {
                    return true;
                }
            }

            return false;
        }
    }

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
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
            }
            finally
            {
                if (process is not null)
                {
                    process.Dispose();
                }
            }
        }

        return default;
    }
}

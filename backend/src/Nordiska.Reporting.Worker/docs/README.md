using System.Threading.Channels;

public sealed class JobBuffer
{
private readonly Channel<JobEnvelope> _channel;

    public JobBuffer(int capacity)
    {
        _channel = Channel.CreateBounded<JobEnvelope>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });
    }

    public async Task EnqueueAsync(
        JobEnvelope job,
        CancellationToken ct)
    {
        await _channel.Writer.WriteAsync(job, ct);
    }

    public async ValueTask<JobEnvelope> DequeueAsync(
        CancellationToken ct)
    {
        return await _channel.Reader.ReadAsync(ct);
    }
}
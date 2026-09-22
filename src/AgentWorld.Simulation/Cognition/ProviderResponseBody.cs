using System.Text;

namespace AgentWorld.Simulation.Cognition;

/// <summary>Caps remote response bytes before JSON parsing or full buffering.</summary>
public static class ProviderResponseBody
{
    public const int MaximumBytes = 256 * 1024;

    public static async Task<string> ReadAsync(HttpContent content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();
        if (content.Headers.ContentLength > MaximumBytes)
        {
            throw new InvalidDataException("The provider response exceeds the byte limit.");
        }

        using var source = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var body = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var count = await source.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, MaximumBytes + 1 - checked((int)body.Length))), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                return Encoding.UTF8.GetString(body.GetBuffer(), 0, checked((int)body.Length));
            }
            if (body.Length + count > MaximumBytes)
            {
                throw new InvalidDataException("The provider response exceeds the byte limit.");
            }
            body.Write(buffer, 0, count);
        }
    }
}

namespace SharpOMatic.AGUI;

public sealed class AgUiOptions
{
    public static readonly TimeSpan DefaultKeepAliveInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// How long the AG-UI SSE response may go without writing anything before a keep-alive comment is sent.
    /// Proxies and load balancers close connections that stay silent, and a long model call can be silent for minutes.
    /// Must be greater than zero and no more than 24 days.
    /// </summary>
    public TimeSpan KeepAliveInterval { get; set; } = DefaultKeepAliveInterval;
}

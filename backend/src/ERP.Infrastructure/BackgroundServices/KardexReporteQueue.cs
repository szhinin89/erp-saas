using System.Threading.Channels;

namespace ERP.Infrastructure.BackgroundServices;

/// <summary>
/// Canal en memoria para encolar IDs de reportes de Kardex pendientes.
/// Singleton: compartido entre el controller (produce) y el processor (consume).
/// </summary>
public sealed class KardexReporteQueue
{
    private readonly Channel<Guid> _channel =
        Channel.CreateBounded<Guid>(new BoundedChannelOptions(500)
        {
            FullMode    = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = true,
        });

    public ChannelWriter<Guid>  Writer => _channel.Writer;
    public ChannelReader<Guid>  Reader => _channel.Reader;
}

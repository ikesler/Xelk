using System.Threading.Channels;
using Microsoft.SqlServer.XEvent.XELite;
using Nest;

namespace Xelk;

public class XelkIndexer
{
    public event Action<int> BatchIndexed = _ => { };
    public event Action<int, string> ReadingFile = (_, _) => { };

    private readonly IEnumerable<string> _files;
    private readonly string _elasticIndex;
    private readonly int _batchSize;
    private readonly ElasticClient _elasticClient;
    private readonly Channel<IXEvent> _channel;

    private int _totalIndexed;

    public XelkIndexer(IEnumerable<string> files, string elasticUrl, string elasticIndex, int batchSize)
    {
        _files = files;
        _elasticIndex = elasticIndex;
        _batchSize = batchSize;

        _elasticClient = new ElasticClient(new ConnectionSettings(new Uri(elasticUrl)));
        _channel = Channel.CreateBounded<IXEvent>(new BoundedChannelOptions(_batchSize));
    }

    public Task Index() => Task.WhenAll(ReadEvents(), WriteEvents());

    private async Task ReadEvents()
    {
        foreach (var (file, i) in _files.Select((f, i) => (f, i)))
        {
            ReadingFile(i, file);
            var xeStream = new XEFileEventStreamer(file);
            await xeStream.ReadEventStream(() => Task.CompletedTask, async xevent => await _channel.Writer.WriteAsync(xevent),
                CancellationToken.None);
        }
        _channel.Writer.Complete();
    }

    private async Task WriteEvents()
    {
        while (await _channel.Reader.WaitToReadAsync())
        {
            await IndexBatch(await _channel.Reader.ReadAllAsync().Take(_batchSize).ToArrayAsync());
        }
    }

    private async Task IndexBatch(IXEvent[] batch)
    {
        await _elasticClient.BulkAsync(x => x.Index(_elasticIndex).IndexMany(batch));
        _totalIndexed += batch.Length;
        BatchIndexed(_totalIndexed);
    }
}

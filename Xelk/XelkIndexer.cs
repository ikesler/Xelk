using System.Threading.Channels;
using Microsoft.SqlServer.XEvent.XELite;
using Nest;

namespace Xelk;

public class XelkIndexer
{
    private readonly IEnumerable<string> _files;
    private readonly string _elasticIndex;
    private readonly int _batchSize;
    private readonly ElasticClient _elasticClient;
    public event Action<int> BatchIndexed = _ => { };

    public XelkIndexer(IEnumerable<string> files, string elasticUrl, string elasticIndex, int batchSize)
    {
        _files = files;
        _elasticIndex = elasticIndex;
        _batchSize = batchSize;

        _elasticClient = new ElasticClient(new ConnectionSettings(new Uri(elasticUrl)));
    }

    public async Task Index()
    {
        var totalIndexed = 0;
        async Task IndexBatch(IEnumerable<IXEvent> batch)
        {
            await _elasticClient.BulkAsync(x => x.Index(_elasticIndex).IndexMany(batch));
            Interlocked.Add(ref totalIndexed, batch.Count());
            BatchIndexed(totalIndexed);
        }

        foreach (var file in _files)
        {
            var channel = Channel.CreateBounded<IXEvent>(new BoundedChannelOptions(_batchSize));
            var indexingCompletion = Task.Run(async () =>
            {
                while (await channel.Reader.WaitToReadAsync())
                {
                    await IndexBatch(channel.Reader.ReadAllAsync().Take(_batchSize).ToEnumerable());
                }
            });

            var xeStream = new XEFileEventStreamer(file);

            await xeStream.ReadEventStream(() => Task.CompletedTask, async xevent => await channel.Writer.WriteAsync(xevent),
                CancellationToken.None);
            channel.Writer.Complete();
            await indexingCompletion;
        }
    }
}
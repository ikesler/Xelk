using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using Microsoft.SqlServer.XEvent.XELite;
using Nest;
using System.Threading.Tasks.Dataflow;

namespace Xelk
{
    [Command]
    public class XelkCommand : ICommand
    {
        [CommandOption("source", 's', IsRequired = true, Description = "Path to a single *.xel file or a directory contining *.xel files.")]
        public string Source { get; init; }

        [CommandOption("target-url", 't', IsRequired = true, Description = "Elasticsearch API endpoint, including port and credentials.")]
        public string TargetUrl { get; init; }

        [CommandOption("target-index", 'i', IsRequired = true, Description = "Name of the target Elasticsearch index.")]
        public string TargetIndex { get; init; }

        [CommandOption("batch-size", Description = "Number of events sent to the Elastic per one Index request")]
        public int BatchSize { get; init; } = 10000;

        public async ValueTask ExecuteAsync(IConsole console)
        {
            string[] files;
            if (File.Exists(Source))
            {
                files = new[] { Source };
            }
            else if (Directory.Exists(Source))
            {
                files = Directory.GetFiles(Source, "*.xel");
                if (files.Length == 0)
                {
                    throw new CommandException("No *.xel files found in Source path.");
                }
            }
            else
            {
                throw new CommandException("Source path does not exist.");
            }

            var settings = new ConnectionSettings(new Uri(TargetUrl));
            var client = new ElasticClient(settings);

            var totalIndexed = 0;
            async Task IndexBatch(IEnumerable<IXEvent> batch)
            {
                await client.BulkAsync(x => x.Index(TargetIndex).IndexMany(batch));
                Interlocked.Add(ref totalIndexed, BatchSize);
                console.Output.WriteLine($"Total indexed {totalIndexed}");
            }

            foreach (var file in files)
            {
                var buffer = new BatchBlock<IXEvent>(BatchSize);
                var actionBlock = new ActionBlock<IEnumerable<IXEvent>>(IndexBatch, new ExecutionDataflowBlockOptions { BoundedCapacity = 1 });
                buffer.LinkTo(actionBlock);

                var xeStream = new XEFileEventStreamer(file);

                await xeStream.ReadEventStream(() => Task.CompletedTask, xevent =>
                {
                    buffer.Post(xevent);
                    return Task.CompletedTask;
                },
                CancellationToken.None);

                buffer.Complete();
                await buffer.Completion;
                actionBlock.Complete();
                await actionBlock.Completion;
            }
        }
    }
}

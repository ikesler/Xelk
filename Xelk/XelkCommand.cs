﻿using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;

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

            var indexer = new XelkIndexer(files, TargetUrl, TargetIndex, BatchSize);
            indexer.BatchIndexed += totalIndexed => console.Output.WriteLine($"Total indexed {totalIndexed}");
            await indexer.Index();
        }
    }
}

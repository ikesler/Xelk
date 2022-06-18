# Xelk - Uploading SQLServer Extended Events (*.xel) files to Elasticsearch (ELK stack)

Usage: `dotnet run -s E:\xel\2022_06_14\3 -t http://localhost:9200/ -i xelk_test`

More info: `dotnet run -- --help`

```
Xelk v1.0.0

USAGE
  Xelk --source <value> --target-url <value> --target-index <value> [options]

OPTIONS
* -s|--source       Path to a single *.xel file or a directory containing *.xel files.
* -t|--target-url   Elasticsearch API endpoint, including port and credentials.
* -i|--target-index  Name of the target Elasticsearch index.
  --batch-size      Number of events sent to the Elastic per one Index request. Default: "10000".
  -h|--help         Shows help text.
  --version         Shows version information.


```

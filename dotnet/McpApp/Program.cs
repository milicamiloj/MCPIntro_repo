using dotenv.net;
using McpApp;
using McpApp.Core;
using Microsoft.Extensions.Logging;

DotEnv.Load();

var model = Environment.GetEnvironmentVariable("CLAUDE_MODEL");
if (string.IsNullOrWhiteSpace(model))
{
    throw new InvalidOperationException("CLAUDE_MODEL cannot be empty. Update .env.");
}

if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
{
    throw new InvalidOperationException("ANTHROPIC_API_KEY cannot be empty. Update .env.");
}

using var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole());
var logger = loggerFactory.CreateLogger<CliApp>();

var claudeService = new ClaudeService(model);
var clients = new Dictionary<string, McpClient>(StringComparer.OrdinalIgnoreCase);

await using var docClient = new McpClient();
await docClient.ConnectAsync(
    command: "dotnet",
    args:
    [
        "run",
        "--project",
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../McpServer/McpServer.csproj")),
    ]);
clients["doc_client"] = docClient;

for (var i = 0; i < args.Length; i++)
{
    var client = new McpClient();
    await client.ConnectAsync("dotnet", ["run", "--project", args[i]]);
    clients[$"client_{i}_{args[i]}"] = client;
}

var chat = new CliChat(docClient, clients, claudeService);
var app = new CliApp(chat, logger);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await app.InitializeAsync(cts.Token);
await app.RunAsync(cts.Token);

foreach (var (key, client) in clients.ToList())
{
    if (ReferenceEquals(client, docClient))
    {
        continue;
    }

    await client.DisposeAsync();
    clients.Remove(key);
}

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace McpApp;

public sealed class McpClient : IAsyncDisposable
{
    private ModelContextProtocol.Client.McpClient? _client;
    private StdioClientTransport? _transport;

    public async Task ConnectAsync(
        string command,
        IEnumerable<string> args,
        IDictionary<string, string?>? env = null,
        CancellationToken cancellationToken = default)
    {
        var options = new StdioClientTransportOptions
        {
            Command = command,
            Arguments = args.ToList(),
            EnvironmentVariables = env is null
                ? new Dictionary<string, string?>()
                : new Dictionary<string, string?>(env),
        };

        _transport = new StdioClientTransport(options, loggerFactory: null);
        _client = await ModelContextProtocol.Client.McpClient.CreateAsync(
            _transport,
            cancellationToken: cancellationToken);
    }

    private ModelContextProtocol.Client.McpClient Client =>
        _client ?? throw new InvalidOperationException("MCP client is not connected.");

    public ValueTask<IList<McpClientTool>> ListToolsAsync(CancellationToken cancellationToken = default) =>
        Client.ListToolsAsync(cancellationToken: cancellationToken);

    public ValueTask<CallToolResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? toolInput,
        CancellationToken cancellationToken = default) =>
        Client.CallToolAsync(toolName, toolInput, cancellationToken: cancellationToken);

    public ValueTask<IList<McpClientPrompt>> ListPromptsAsync(CancellationToken cancellationToken = default) =>
        Client.ListPromptsAsync(cancellationToken: cancellationToken);

    public ValueTask<GetPromptResult> GetPromptAsync(
        string promptName,
        IReadOnlyDictionary<string, object?>? args,
        CancellationToken cancellationToken = default) =>
        Client.GetPromptAsync(promptName, args, cancellationToken: cancellationToken);

    public async Task<object?> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
    {
        var result = await Client.ReadResourceAsync(uri, cancellationToken: cancellationToken);

        if (result.Contents.Count == 1 && result.Contents[0] is TextResourceContents textResource)
        {
            return textResource.Text;
        }

        return result.Contents;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is IAsyncDisposable asyncClient)
        {
            await asyncClient.DisposeAsync();
        }
        else if (_client is IDisposable disposableClient)
        {
            disposableClient.Dispose();
        }

        _client = null;
        _transport = null;
    }
}

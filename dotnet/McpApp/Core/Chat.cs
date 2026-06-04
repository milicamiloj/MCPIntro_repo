using Anthropic.SDK.Messaging;
using McpApp;

namespace McpApp.Core;

public class Chat
{
    private readonly ClaudeService _claudeService;
    private readonly IReadOnlyDictionary<string, McpClient> _clients;

    protected readonly List<Message> Messages = [];

    public Chat(ClaudeService claudeService, IReadOnlyDictionary<string, McpClient> clients)
    {
        _claudeService = claudeService;
        _clients = clients;
    }

    public async Task<string> RunAsync(string query, CancellationToken cancellationToken = default)
    {
        await ProcessQueryAsync(query, cancellationToken);

        while (true)
        {
            var response = await _claudeService.ChatAsync(
                Messages,
                tools: await ToolManager.GetAllToolsAsync(_clients, cancellationToken),
                cancellationToken: cancellationToken);

            _claudeService.AddAssistantMessage(Messages, response.Content);

            if (string.Equals(response.StopReason, "tool_use", StringComparison.OrdinalIgnoreCase))
            {
                var toolResults = await ToolManager.ExecuteToolRequestsAsync(_clients, response, cancellationToken);
                _claudeService.AddUserMessage(Messages, toolResults.Cast<ContentBase>().ToList());
                continue;
            }

            return _claudeService.TextFromMessage(response);
        }
    }

    protected virtual Task ProcessQueryAsync(string query, CancellationToken cancellationToken = default)
    {
        _claudeService.AddUserMessage(Messages, query);
        return Task.CompletedTask;
    }
}

using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Messaging;
using AnthropicTool = Anthropic.SDK.Common.Tool;

namespace McpApp.Core;

public class ClaudeService
{
    private readonly AnthropicClient _client;
    private readonly string _model;

    public ClaudeService(string model)
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("ANTHROPIC_API_KEY cannot be empty.");
        }

        _client = new AnthropicClient(new APIAuthentication(apiKey));
        _model = model;
    }

    public void AddUserMessage(IList<Message> messages, object content)
    {
        messages.Add(new Message { Role = RoleType.User, Content = ToContentList(content) });
    }

    public void AddAssistantMessage(IList<Message> messages, object content)
    {
        messages.Add(new Message { Role = RoleType.Assistant, Content = ToContentList(content) });
    }

    public string TextFromMessage(MessageResponse message)
    {
        return string.Join(
            "\n",
            message.Content
                .OfType<TextContent>()
                .Select(block => block.Text)
                .Where(static text => !string.IsNullOrWhiteSpace(text)));
    }

    public Task<MessageResponse> ChatAsync(
        IList<Message> messages,
        string? system = null,
        decimal? temperature = 1m,
        IEnumerable<string>? stopSequences = null,
        IList<AnthropicTool>? tools = null,
        bool thinking = false,
        int thinkingBudget = 1024,
        CancellationToken cancellationToken = default)
    {
        var parameters = new MessageParameters
        {
            Model = _model,
            MaxTokens = 8000,
            Messages = messages.ToList(),
            Temperature = temperature,
            StopSequences = stopSequences?.ToArray(),
            Tools = tools,
        };

        if (!string.IsNullOrWhiteSpace(system))
        {
            parameters.System =
            [
                new SystemMessage(system),
            ];
        }

        if (thinking)
        {
            parameters.Thinking = new ThinkingParameters
            {
                Type = ThinkingType.enabled,
                BudgetTokens = thinkingBudget,
            };
        }

        return _client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);
    }

    private static List<ContentBase> ToContentList(object content)
    {
        if (content is List<ContentBase> blocks)
        {
            return blocks;
        }

        if (content is IEnumerable<ContentBase> enumerable)
        {
            return enumerable.ToList();
        }

        return
        [
            new TextContent
            {
                Text = content?.ToString() ?? string.Empty,
            },
        ];
    }
}

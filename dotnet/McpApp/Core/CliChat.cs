using System.Text.Json;
using Anthropic.SDK.Messaging;
using McpApp;
using ModelContextProtocol.Protocol;

namespace McpApp.Core;

public sealed class CliChat : Chat
{
    private readonly McpClient _docClient;

    public CliChat(McpClient docClient, IReadOnlyDictionary<string, McpClient> clients, ClaudeService claudeService)
        : base(claudeService, clients)
    {
        _docClient = docClient;
    }

    public async Task<List<Prompt>> ListPromptsAsync(CancellationToken cancellationToken = default)
    {
        var prompts = await _docClient.ListPromptsAsync(cancellationToken);
        return prompts.Select(p => p.ProtocolPrompt).ToList();
    }

    public async Task<List<string>> ListDocIdsAsync(CancellationToken cancellationToken = default)
    {
        var resource = await _docClient.ReadResourceAsync("docs://documents", cancellationToken);
        var text = resource?.ToString();

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<string>>(text) ?? [];
    }

    public async Task<string> GetDocContentAsync(string docId, CancellationToken cancellationToken = default)
    {
        var resource = await _docClient.ReadResourceAsync($"docs://documents/{docId}", cancellationToken);
        return resource?.ToString() ?? string.Empty;
    }

    public async Task<List<PromptMessage>> GetPromptAsync(
        string command,
        string docId,
        CancellationToken cancellationToken = default)
    {
        var prompt = await _docClient.GetPromptAsync(
            command,
            new Dictionary<string, object?> { ["doc_id"] = docId },
            cancellationToken);

        return prompt.Messages.ToList();
    }

    public async Task<string> ExtractResourcesAsync(string query, CancellationToken cancellationToken = default)
    {
        var mentions = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.StartsWith('@'))
            .Select(word => word[1..])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var docIds = await ListDocIdsAsync(cancellationToken);
        var blocks = new List<string>();

        foreach (var docId in docIds)
        {
            if (!mentions.Contains(docId))
            {
                continue;
            }

            var content = await GetDocContentAsync(docId, cancellationToken);
            blocks.Add($"\n<document id=\"{docId}\">\n{content}\n</document>\n");
        }

        return string.Join(string.Empty, blocks);
    }

    public async Task<bool> ProcessCommandAsync(string query, CancellationToken cancellationToken = default)
    {
        if (!query.StartsWith('/'))
        {
            return false;
        }

        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2)
        {
            return true;
        }

        var command = words[0][1..];
        var docId = words[1];
        var promptMessages = await GetPromptAsync(command, docId, cancellationToken);

        foreach (var promptMessage in promptMessages)
        {
            Messages.Add(ConvertPromptMessage(promptMessage));
        }

        return true;
    }

    protected override async Task ProcessQueryAsync(string query, CancellationToken cancellationToken = default)
    {
        if (await ProcessCommandAsync(query, cancellationToken))
        {
            return;
        }

        var resources = await ExtractResourcesAsync(query, cancellationToken);
        var prompt = $"""
                     The user has a question:
                     <query>
                     {query}
                     </query>

                     The following context may be useful in answering their question:
                     <context>
                     {resources}
                     </context>

                     Note the user's query might contain references to documents like "@report.docx". The "@" is only
                     included as a way of mentioning the doc. The actual name of the document would be "report.docx".
                     If the document content is included in this prompt, you don't need to use an additional tool to read the document.
                     Answer the user's question directly and concisely. Start with the exact information they need.
                     Don't refer to or mention the provided context in any way - just use it to inform your answer.
                     """;

        Messages.Add(new Message(RoleType.User, prompt));
    }

    private static Message ConvertPromptMessage(PromptMessage promptMessage)
    {
        var role = promptMessage.Role == Role.Assistant ? RoleType.Assistant : RoleType.User;
        var text = promptMessage.Content switch
        {
            TextContentBlock textBlock => textBlock.Text,
            EmbeddedResourceBlock embedded when embedded.Resource is TextResourceContents textResource => textResource.Text,
            _ => string.Empty,
        };

        return new Message(role, text);
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.SDK.Common;
using Anthropic.SDK.Messaging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using McpApp;
using AnthropicTool = Anthropic.SDK.Common.Tool;

namespace McpApp.Core;

public static class ToolManager
{
    public static async Task<List<AnthropicTool>> GetAllToolsAsync(
        IReadOnlyDictionary<string, McpClient> clients,
        CancellationToken cancellationToken = default)
    {
        var tools = new List<AnthropicTool>();

        foreach (var client in clients.Values)
        {
            var mcpTools = await client.ListToolsAsync(cancellationToken);
            tools.AddRange(mcpTools.Select(ToAnthropicTool));
        }

        return tools;
    }

    public static async Task<McpClient?> FindClientWithToolAsync(
        IReadOnlyDictionary<string, McpClient> clients,
        string toolName,
        CancellationToken cancellationToken = default)
    {
        foreach (var client in clients.Values)
        {
            var tools = await client.ListToolsAsync(cancellationToken);
            if (tools.Any(t => string.Equals(t.Name, toolName, StringComparison.Ordinal)))
            {
                return client;
            }
        }

        return null;
    }

    public static async Task<List<ToolResultContent>> ExecuteToolRequestsAsync(
        IReadOnlyDictionary<string, McpClient> clients,
        MessageResponse message,
        CancellationToken cancellationToken = default)
    {
        var toolRequests = message.Content.OfType<ToolUseContent>().ToList();
        var toolResults = new List<ToolResultContent>();

        foreach (var request in toolRequests)
        {
            var client = await FindClientWithToolAsync(clients, request.Name, cancellationToken);
            if (client is null)
            {
                toolResults.Add(CreateErrorResult(request.Id, "Could not find that tool."));
                continue;
            }

            try
            {
                var input = request.Input is null
                    ? null
                    : request.Input.Deserialize<Dictionary<string, object?>>();

                var output = await client.CallToolAsync(request.Name, input, cancellationToken);
                var textPayload = JsonSerializer.Serialize(ExtractTextOutput(output));

                toolResults.Add(
                    new ToolResultContent
                    {
                        ToolUseId = request.Id,
                        IsError = output.IsError,
                        Content =
                        [
                            new TextContent
                            {
                                Text = textPayload,
                            },
                        ],
                    });
            }
            catch (Exception ex)
            {
                toolResults.Add(CreateErrorResult(request.Id, $"Error executing tool '{request.Name}': {ex.Message}"));
            }
        }

        return toolResults;
    }

    private static AnthropicTool ToAnthropicTool(McpClientTool mcpTool)
    {
        var schemaJson = mcpTool.ProtocolTool.InputSchema.GetRawText();

        return new AnthropicTool(
            new Function(
                mcpTool.Name,
                mcpTool.Description ?? string.Empty,
                JsonNode.Parse(schemaJson) ?? new JsonObject()));
    }

    private static List<string> ExtractTextOutput(CallToolResult toolResult)
    {
        var outputs = new List<string>();

        foreach (var content in toolResult.Content)
        {
            if (content is TextContentBlock text)
            {
                outputs.Add(text.Text);
            }
            else
            {
                outputs.Add(JsonSerializer.Serialize(content));
            }
        }

        return outputs;
    }

    private static ToolResultContent CreateErrorResult(string toolUseId, string error)
    {
        return new ToolResultContent
        {
            ToolUseId = toolUseId,
            IsError = true,
            Content =
            [
                new TextContent
                {
                    Text = JsonSerializer.Serialize(new { error }),
                },
            ],
        };
    }
}

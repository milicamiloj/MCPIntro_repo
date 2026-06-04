using Microsoft.Extensions.Logging;

namespace McpApp.Core;

public sealed class CliApp
{
    private readonly CliChat _chat;
    private readonly ILogger<CliApp> _logger;

    public CliApp(CliChat chat, ILogger<CliApp> logger)
    {
        _chat = chat;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _ = await _chat.ListDocIdsAsync(cancellationToken);
        _ = await _chat.ListPromptsAsync(cancellationToken);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
        };

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("> ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            var response = await _chat.RunAsync(input, cancellationToken);
            Console.WriteLine($"\nResponse:\n{response}");
        }

        _logger.LogInformation("CLI stopped.");
    }
}

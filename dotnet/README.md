# MCPIntro .NET verzija

Ovaj folder sadrži C#/.NET 10 ekvivalent originalnog Python MCP projekta.

## Preduslovi

- .NET 10 SDK
- `ANTHROPIC_API_KEY`
- `CLAUDE_MODEL`

## Pokretanje

Iz foldera `dotnet/`:

```bash
dotnet run --project McpApp
```

Aplikacija automatski startuje `McpServer` preko stdio transporta.

## `.env` konfiguracija

U root-u repozitorijuma postavite:

```env
ANTHROPIC_API_KEY=...
CLAUDE_MODEL=...
```

`McpApp` učitava `.env` preko `dotenv.net`.

## Arhitektura

- `McpServer` – MCP server sa tools/resources/prompts (dokumenti)
- `McpApp` – CLI chat aplikacija i MCP klijent
  - `McpClient` – wrapper oko `ModelContextProtocol.Client.McpClient`
  - `Core/ClaudeService` – Anthropic poruke/chat
  - `Core/ToolManager` – agregacija i izvršavanje MCP tool poziva
  - `Core/Chat` – tool-use petlja
  - `Core/CliChat` – obrada `@doc` i `/command docId`
  - `Core/CliApp` – interaktivna konzola

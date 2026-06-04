using System.Text.Json;
using ModelContextProtocol.Server;

namespace McpServer;

[McpServerToolType]
[McpServerPromptType]
[McpServerResourceType]
public class DocumentTools
{
    private readonly Dictionary<string, string> _docs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["deposition.md"] = "This deposition covers the testimony of Angela Smith, P.E.",
        ["report.pdf"] = "The report details the state of a 20m condenser tower.",
        ["financials.docx"] = "These financials outline the project's budget and expenditures.",
        ["outlook.pdf"] = "This document presents the projected future performance of the system.",
        ["plan.md"] = "The plan outlines the steps for the project's implementation.",
        ["spec.txt"] = "These specifications define the technical requirements for the equipment.",
    };

    [McpServerTool(Name = "read_doc", ReadOnly = true)]
    public string ReadDoc(string doc_id)
    {
        if (!_docs.TryGetValue(doc_id, out var content))
        {
            throw new ArgumentException($"Document '{doc_id}' was not found.", nameof(doc_id));
        }

        return content;
    }

    [McpServerTool(Name = "edit_doc")]
    public string EditDoc(string doc_id, string content)
    {
        if (string.IsNullOrWhiteSpace(doc_id))
        {
            throw new ArgumentException("Document id is required.", nameof(doc_id));
        }

        _docs[doc_id] = content ?? string.Empty;
        return $"Updated '{doc_id}'.";
    }

    [McpServerResource(UriTemplate = "docs://documents", Name = "documents")]
    public string ListDocuments()
    {
        var ids = _docs.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        return JsonSerializer.Serialize(ids);
    }

    [McpServerResource(UriTemplate = "docs://documents/{doc_id}", Name = "document")]
    public string GetDocumentResource(string doc_id)
    {
        return ReadDoc(doc_id);
    }

    [McpServerPrompt(Name = "rewrite_as_markdown")]
    public string RewriteAsMarkdown(string doc_id)
    {
        var content = ReadDoc(doc_id);
        return $"Rewrite the following document as well-structured Markdown while preserving all key meaning.\n\nDocument ID: {doc_id}\n\n{content}";
    }

    [McpServerPrompt(Name = "summarize")]
    public string Summarize(string doc_id)
    {
        var content = ReadDoc(doc_id);
        return $"Provide a concise summary of this document in 3-5 bullet points.\n\nDocument ID: {doc_id}\n\n{content}";
    }
}

namespace DevKnowledgeHub.Models;

public sealed class Topic
{
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string Icon { get; init; }
    public int Order { get; init; }
    public List<CaseSummary> Cases { get; init; } = new();
}

public sealed class CaseSummary
{
    public required string Slug { get; init; }
    public required string TopicSlug { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public int Order { get; init; }
}

public sealed class ParentCase
{
    public required string Slug { get; init; }
    public required string TopicSlug { get; init; }
    public required string TopicTitle { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public int Order { get; init; }
    public List<string> Sources { get; init; } = new();
    public bool IsFlagged { get; init; }
    public string? FlagNote { get; init; }

    public string ProblemHtml { get; set; } = string.Empty;
    public string WhenItAppliesHtml { get; set; } = string.Empty;
    public string SolutionHtml { get; set; } = string.Empty;
    public List<Example> Examples { get; init; } = new();
    public string TradeoffsHtml { get; set; } = string.Empty;
    public string AlternativesHtml { get; set; } = string.Empty;

    public List<string> RelatedCaseSlugs { get; init; } = new();
}

public sealed class Example
{
    public required string Title { get; init; }
    public required string ExplanationHtml { get; init; }
}

public sealed class ReferenceArticle
{
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public List<string> Sources { get; init; } = new();
    public string BodyHtml { get; set; } = string.Empty;
    public bool IsFlagged { get; init; }
    public string? FlagNote { get; init; }
}

public sealed class SearchEntry
{
    public required string Title { get; init; }
    public required string Url { get; init; }
    public required string Kind { get; init; } // "Case" or "Reference"
    public required string TopicTitle { get; init; }
    public required string SearchText { get; init; }
}

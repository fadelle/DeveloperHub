using System.Text.Json;
using System.Text.RegularExpressions;
using DevKnowledgeHub.Models;
using Markdig;

namespace DevKnowledgeHub.Services;

/// <summary>
/// Loads every topic/case/reference markdown file under Content/ at startup and
/// keeps the parsed result in memory. Content is plain Markdown with a small
/// front-matter block and a fixed set of "## " section headings, so new cases
/// can be added as new files without touching any C# or Razor code.
/// </summary>
public sealed class ContentRepository
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UsePipeTables()
        .Build();

    private readonly List<Topic> _topics = new();
    private readonly Dictionary<string, ParentCase> _casesBySlug = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ParentCase>> _casesByTopic = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ReferenceArticle> _referenceBySlug = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SearchEntry> _searchIndex = new();

    public IReadOnlyList<Topic> Topics => _topics;
    public IReadOnlyList<ReferenceArticle> ReferenceArticles => _referenceBySlug.Values.OrderBy(r => r.Title).ToList();
    public IReadOnlyList<SearchEntry> SearchIndex => _searchIndex;

    public ContentRepository(string contentRootPath)
    {
        Load(contentRootPath);
    }

    public ParentCase? GetCase(string topicSlug, string caseSlug) =>
        _casesBySlug.TryGetValue(Key(topicSlug, caseSlug), out var c) ? c : null;

    /// <summary>Finds a case by its slug alone, regardless of topic — used to resolve "related" links.</summary>
    public ParentCase? FindCaseBySlug(string caseSlug) =>
        _casesBySlug.Values.FirstOrDefault(c => c.Slug.Equals(caseSlug, StringComparison.OrdinalIgnoreCase));

    public Topic? GetTopic(string topicSlug) =>
        _topics.FirstOrDefault(t => t.Slug.Equals(topicSlug, StringComparison.OrdinalIgnoreCase));

    public ReferenceArticle? GetReference(string slug) =>
        _referenceBySlug.TryGetValue(slug, out var r) ? r : null;

    public List<ParentCase> GetCasesForTopic(string topicSlug) =>
        _casesByTopic.TryGetValue(topicSlug, out var list) ? list : new List<ParentCase>();

    private static string Key(string topicSlug, string caseSlug) => $"{topicSlug}::{caseSlug}";

    private void Load(string contentRootPath)
    {
        var topicsFile = Path.Combine(contentRootPath, "topics.json");
        var topicDtos = JsonSerializer.Deserialize<List<TopicDto>>(File.ReadAllText(topicsFile), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new();

        foreach (var dto in topicDtos.OrderBy(t => t.Order))
        {
            _topics.Add(new Topic
            {
                Slug = dto.Slug,
                Title = dto.Title,
                Summary = dto.Summary,
                Icon = dto.Icon,
                Order = dto.Order
            });
        }

        foreach (var topic in _topics)
        {
            var topicDir = Path.Combine(contentRootPath, topic.Slug);
            if (!Directory.Exists(topicDir)) continue;

            var cases = new List<ParentCase>();
            foreach (var file in Directory.GetFiles(topicDir, "*.md").OrderBy(f => f))
            {
                var parsed = ParseCaseFile(File.ReadAllText(file), topic.Slug, topic.Title);
                cases.Add(parsed);
                _casesBySlug[Key(topic.Slug, parsed.Slug)] = parsed;

                topic.Cases.Add(new CaseSummary
                {
                    Slug = parsed.Slug,
                    TopicSlug = topic.Slug,
                    Title = parsed.Title,
                    Summary = parsed.Summary,
                    Order = parsed.Order
                });

                _searchIndex.Add(new SearchEntry
                {
                    Title = parsed.Title,
                    Url = $"/topics/{topic.Slug}/{parsed.Slug}",
                    Kind = "Case",
                    TopicTitle = topic.Title,
                    SearchText = BuildSearchText(parsed)
                });
            }

            cases = cases.OrderBy(c => c.Order).ToList();
            topic.Cases.Sort((a, b) => a.Order.CompareTo(b.Order));
            _casesByTopic[topic.Slug] = cases;
        }

        var refDir = Path.Combine(contentRootPath, "reference");
        if (Directory.Exists(refDir))
        {
            foreach (var file in Directory.GetFiles(refDir, "*.md").OrderBy(f => f))
            {
                var article = ParseReferenceFile(File.ReadAllText(file));
                _referenceBySlug[article.Slug] = article;
                _searchIndex.Add(new SearchEntry
                {
                    Title = article.Title,
                    Url = $"/reference/{article.Slug}",
                    Kind = "Reference",
                    TopicTitle = "Reference",
                    SearchText = $"{article.Title} {article.Summary} {StripHtml(article.BodyHtml)}"
                });
            }
        }
    }

    private static string BuildSearchText(ParentCase c)
    {
        var examples = string.Join(" ", c.Examples.Select(e => e.Title + " " + StripHtml(e.ExplanationHtml)));
        return string.Join(" ", new[]
        {
            c.Title, c.Summary,
            StripHtml(c.ProblemHtml), StripHtml(c.WhenItAppliesHtml), StripHtml(c.SolutionHtml),
            examples, StripHtml(c.TradeoffsHtml), StripHtml(c.AlternativesHtml)
        });
    }

    private static string StripHtml(string html) => Regex.Replace(html ?? string.Empty, "<.*?>", " ");

    // --- Front matter -------------------------------------------------

    private static (Dictionary<string, string> meta, string body) SplitFrontMatter(string raw)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var trimmed = raw.TrimStart('﻿', '\r', '\n');
        if (!trimmed.StartsWith("---"))
            return (meta, raw);

        var endIndex = trimmed.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endIndex < 0) return (meta, raw);

        var frontMatterBlock = trimmed.Substring(3, endIndex - 3).Trim();
        var body = trimmed.Substring(endIndex + 4).TrimStart('\r', '\n');

        foreach (var line in frontMatterBlock.Split('\n'))
        {
            var idx = line.IndexOf(':');
            if (idx < 0) continue;
            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                value = value[1..^1];
            meta[key] = value;
        }

        return (meta, body);
    }

    private static List<string> SplitList(string value) =>
        value.Trim('[', ']').Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();

    // --- Case parsing ---------------------------------------------------

    private ParentCase ParseCaseFile(string raw, string topicSlug, string topicTitle)
    {
        var (meta, body) = SplitFrontMatter(raw);

        var sections = SplitSections(body);

        var parentCase = new ParentCase
        {
            Slug = meta.GetValueOrDefault("slug", "case"),
            TopicSlug = topicSlug,
            TopicTitle = topicTitle,
            Title = meta.GetValueOrDefault("title", "Untitled Case"),
            Summary = meta.GetValueOrDefault("summary", string.Empty),
            Order = int.TryParse(meta.GetValueOrDefault("order", "0"), out var o) ? o : 0,
            Sources = meta.TryGetValue("sources", out var s) ? SplitList(s) : new List<string>(),
            RelatedCaseSlugs = meta.TryGetValue("related", out var rel) ? SplitList(rel) : new List<string>(),
            IsFlagged = meta.GetValueOrDefault("flagged", "false").Equals("true", StringComparison.OrdinalIgnoreCase),
            FlagNote = meta.TryGetValue("flagNote", out var flagNote) ? flagNote : null
        };

        foreach (var (heading, content) in sections)
        {
            var normalized = heading.Trim().ToLowerInvariant();
            var html = Markdown.ToHtml(content.Trim(), _pipeline);

            if (normalized == "problem") parentCase.ProblemHtml = html;
            else if (normalized == "when it applies") parentCase.WhenItAppliesHtml = html;
            else if (normalized == "solution") parentCase.SolutionHtml = html;
            else if (normalized == "tradeoffs & common mistakes" || normalized == "tradeoffs and common mistakes") parentCase.TradeoffsHtml = html;
            else if (normalized == "alternatives") parentCase.AlternativesHtml = html;
            else if (heading.StartsWith("Example:", StringComparison.OrdinalIgnoreCase))
            {
                var title = heading[8..].Trim();
                parentCase.Examples.Add(new Example { Title = title, ExplanationHtml = html });
            }
        }

        return parentCase;
    }

    private ReferenceArticle ParseReferenceFile(string raw)
    {
        var (meta, body) = SplitFrontMatter(raw);
        var html = Markdown.ToHtml(body.Trim(), _pipeline);

        return new ReferenceArticle
        {
            Slug = meta.GetValueOrDefault("slug", "reference"),
            Title = meta.GetValueOrDefault("title", "Reference"),
            Summary = meta.GetValueOrDefault("summary", string.Empty),
            Sources = meta.TryGetValue("sources", out var s) ? SplitList(s) : new List<string>(),
            BodyHtml = html,
            IsFlagged = meta.GetValueOrDefault("flagged", "false").Equals("true", StringComparison.OrdinalIgnoreCase),
            FlagNote = meta.TryGetValue("flagNote", out var flagNote) ? flagNote : null
        };
    }

    /// <summary>
    /// Splits a case's markdown body on top-level "## " headings. Each
    /// resulting (heading, content) pair is rendered as its own HTML fragment,
    /// which is what lets Problem / When It Applies / Solution / each Example /
    /// Tradeoffs / Alternatives be addressed and styled independently.
    /// </summary>
    private static List<(string Heading, string Content)> SplitSections(string body)
    {
        var lines = body.Replace("\r\n", "\n").Split('\n');
        var result = new List<(string Heading, string Content)>();
        string? currentHeading = null;
        var currentContent = new System.Text.StringBuilder();

        foreach (var line in lines)
        {
            if (line.StartsWith("## "))
            {
                if (currentHeading != null)
                    result.Add((currentHeading, currentContent.ToString()));

                currentHeading = line[3..].Trim();
                currentContent = new System.Text.StringBuilder();
            }
            else
            {
                currentContent.AppendLine(line);
            }
        }

        if (currentHeading != null)
            result.Add((currentHeading, currentContent.ToString()));

        return result;
    }

    private sealed class TopicDto
    {
        public string Slug { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public int Order { get; set; }
    }
}

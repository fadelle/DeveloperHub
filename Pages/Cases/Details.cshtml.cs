using DevKnowledgeHub.Models;
using DevKnowledgeHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DevKnowledgeHub.Pages.Cases;

public class DetailsModel : PageModel
{
    private readonly ContentRepository _content;
    public DetailsModel(ContentRepository content) => _content = content;

    public ParentCase Case { get; private set; } = default!;
    public Topic Topic { get; private set; } = default!;
    public List<ParentCase> RelatedCases { get; } = new();
    public CaseSummary? PrevCase { get; private set; }
    public CaseSummary? NextCase { get; private set; }

    public IActionResult OnGet(string topicSlug, string caseSlug)
    {
        var topic = _content.GetTopic(topicSlug);
        var parentCase = _content.GetCase(topicSlug, caseSlug);
        if (topic is null || parentCase is null) return NotFound();

        Topic = topic;
        Case = parentCase;

        foreach (var relatedSlug in parentCase.RelatedCaseSlugs)
        {
            var related = _content.FindCaseBySlug(relatedSlug);
            if (related is not null) RelatedCases.Add(related);
        }

        var orderedCases = topic.Cases;
        var idx = orderedCases.FindIndex(c => c.Slug.Equals(caseSlug, StringComparison.OrdinalIgnoreCase));
        if (idx > 0) PrevCase = orderedCases[idx - 1];
        if (idx >= 0 && idx < orderedCases.Count - 1) NextCase = orderedCases[idx + 1];

        return Page();
    }
}

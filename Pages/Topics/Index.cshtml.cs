using DevKnowledgeHub.Models;
using DevKnowledgeHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DevKnowledgeHub.Pages.Topics;

public class IndexModel : PageModel
{
    private readonly ContentRepository _content;
    public IndexModel(ContentRepository content) => _content = content;

    public Topic Topic { get; private set; } = default!;

    public IActionResult OnGet(string slug)
    {
        var topic = _content.GetTopic(slug);
        if (topic is null) return NotFound();
        Topic = topic;
        return Page();
    }
}

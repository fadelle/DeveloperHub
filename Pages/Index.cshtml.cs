using DevKnowledgeHub.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DevKnowledgeHub.Pages;

public class IndexModel : PageModel
{
    private readonly ContentRepository _content;
    public IndexModel(ContentRepository content) => _content = content;

    public IReadOnlyList<Models.Topic> Topics => _content.Topics;
    public int TotalCases => _content.Topics.Sum(t => t.Cases.Count);

    public void OnGet() { }
}

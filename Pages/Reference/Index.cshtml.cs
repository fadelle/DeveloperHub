using DevKnowledgeHub.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DevKnowledgeHub.Pages.Reference;

public class IndexModel : PageModel
{
    private readonly ContentRepository _content;
    public IndexModel(ContentRepository content) => _content = content;

    public IReadOnlyList<Models.ReferenceArticle> Articles => _content.ReferenceArticles;

    public void OnGet() { }
}

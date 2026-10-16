using DevKnowledgeHub.Models;
using DevKnowledgeHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DevKnowledgeHub.Pages.Reference;

public class DetailsModel : PageModel
{
    private readonly ContentRepository _content;
    public DetailsModel(ContentRepository content) => _content = content;

    public ReferenceArticle Article { get; private set; } = default!;

    public IActionResult OnGet(string slug)
    {
        var article = _content.GetReference(slug);
        if (article is null) return NotFound();
        Article = article;
        return Page();
    }
}

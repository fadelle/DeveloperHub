using System.Text.Json;
using DevKnowledgeHub.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var contentRoot = Path.Combine(builder.Environment.ContentRootPath, "Content");
builder.Services.AddSingleton(new ContentRepository(contentRoot));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.MapGet("/api/search", (string? q, ContentRepository repo) =>
{
    if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        return Results.Json(Array.Empty<object>());

    var terms = q.Trim();
    var results = repo.SearchIndex
        .Where(e => e.SearchText.Contains(terms, StringComparison.OrdinalIgnoreCase)
                 || e.Title.Contains(terms, StringComparison.OrdinalIgnoreCase))
        .Select(e => new { e.Title, e.Url, e.Kind, e.TopicTitle })
        .Take(20)
        .ToList();

    return Results.Json(results, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
});

app.Run();

using System.Text.Json;
using DevKnowledgeHub.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var contentRoot = Path.Combine(builder.Environment.ContentRootPath, "Content");
var contentRepository = new ContentRepository(contentRoot);
builder.Services.AddSingleton(contentRepository);

var app = builder.Build();

// Written once at startup so the search index is a plain static file —
// served identically whether this app is running live or exported to static HTML.
var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var searchIndexJson = JsonSerializer.Serialize(
    contentRepository.SearchIndex.Select(e => new { e.Title, e.Url, e.Kind, e.TopicTitle, e.SearchText }),
    jsonOptions);
var wwwrootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(wwwrootPath);
File.WriteAllText(Path.Combine(wwwrootPath, "search-index.json"), searchIndexJson);

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

// Lets the static-export tool discover every page without hardcoding routes —
// always in sync with whatever ContentRepository actually loaded.
app.MapGet("/__routes.json", (ContentRepository repo) =>
{
    var routes = new List<string> { "/", "/reference" };
    foreach (var topic in repo.Topics)
    {
        routes.Add($"/topics/{topic.Slug}");
        foreach (var c in topic.Cases)
            routes.Add($"/topics/{topic.Slug}/{c.Slug}");
    }
    foreach (var article in repo.ReferenceArticles)
        routes.Add($"/reference/{article.Slug}");

    return Results.Json(routes);
});

app.Run();

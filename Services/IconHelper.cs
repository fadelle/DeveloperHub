namespace DevKnowledgeHub.Services;

/// <summary>Tiny inline-SVG icon set so the site needs no icon font or extra asset requests.</summary>
public static class IconHelper
{
    private const string Shell = "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\">{0}</svg>";

    public static string Get(string key) => string.Format(Shell, Path(key));

    private static string Path(string key) => key switch
    {
        "stream" => "<path d=\"M3 7c3 0 3 3 6 3s3-3 6-3 3 3 6 3\"/><path d=\"M3 14c3 0 3 3 6 3s3-3 6-3 3 3 6 3\"/>",
        "concurrency" => "<circle cx=\"6\" cy=\"6\" r=\"2.6\"/><circle cx=\"18\" cy=\"6\" r=\"2.6\"/><circle cx=\"12\" cy=\"18\" r=\"2.6\"/><path d=\"M8.3 7.5 10 15.3M15.7 7.5 14 15.3M8.6 6h6.8\"/>",
        "oop" => "<rect x=\"4\" y=\"4\" width=\"7\" height=\"7\" rx=\"1.4\"/><rect x=\"13\" y=\"13\" width=\"7\" height=\"7\" rx=\"1.4\"/><path d=\"M11 7.5h4a2 2 0 0 1 2 2V13\"/>",
        "performance" => "<path d=\"M4 19V11M10 19V5M16 19v-7M21 19H3\"/>",
        "database" => "<ellipse cx=\"12\" cy=\"5.5\" rx=\"7\" ry=\"2.8\"/><path d=\"M5 5.5v6.5c0 1.5 3.1 2.8 7 2.8s7-1.3 7-2.8V5.5\"/><path d=\"M5 12v6.5c0 1.5 3.1 2.8 7 2.8s7-1.3 7-2.8V12\"/>",
        "testing" => "<path d=\"m9 11 2.2 2.2L20 4.6\"/><path d=\"M20 12.5V18a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h9.5\"/>",
        "architecture" => "<path d=\"M12 3 3 8l9 5 9-5-9-5Z\"/><path d=\"m3 13 9 5 9-5M3 8v10M21 8v10\"/>",
        "security" => "<path d=\"M12 3 5 6v5.5c0 4.2 2.9 7.5 7 8.5 4.1-1 7-4.3 7-8.5V6l-7-3Z\"/><path d=\"M9.3 12.2l1.9 1.9 3.5-3.9\"/>",
        "reference" => "<path d=\"M5 4h11l3 3v13H5z\"/><path d=\"M9 9h7M9 13h7M9 17h4\"/>",
        "search" => "<circle cx=\"11\" cy=\"11\" r=\"6.5\"/><path d=\"m20 20-3.6-3.6\"/>",
        "sun" => "<circle cx=\"12\" cy=\"12\" r=\"4.2\"/><path d=\"M12 2.5v2.4M12 19.1v2.4M4.2 4.2l1.7 1.7M18.1 18.1l1.7 1.7M2.5 12h2.4M19.1 12h2.4M4.2 19.8l1.7-1.7M18.1 5.9l1.7-1.7\"/>",
        "moon" => "<path d=\"M20 14.5A8.5 8.5 0 1 1 9.5 4a7 7 0 0 0 10.5 10.5Z\"/>",
        "menu" => "<path d=\"M4 6h16M4 12h16M4 18h16\"/>",
        "arrow-right" => "<path d=\"M5 12h14M13 6l6 6-6 6\"/>",
        "arrow-left" => "<path d=\"M19 12H5M11 6l-6 6 6 6\"/>",
        "flag" => "<path d=\"M6 3v18\"/><path d=\"M6 4h11l-3 4 3 4H6\"/>",
        _ => "<circle cx=\"12\" cy=\"12\" r=\"8\"/>"
    };
}

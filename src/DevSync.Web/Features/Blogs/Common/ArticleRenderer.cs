using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace DevSync.Web.Features.Blogs.Common;

// Deliberately small Markdown dialect. Every user-controlled fragment is encoded;
// only this renderer emits HTML. Raw HTML is displayed as plain text.
public static class ArticleRenderer
{
    public static string Render(string markdown, string? applicationOrigin = null)
    {
        var html = new StringBuilder();
        var code = new StringBuilder();
        var fenced = false;
        var list = false;
        foreach (var line in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                if (list) { html.Append("</ul>"); list = false; }
                if (fenced) { html.Append("<pre><code>").Append(WebUtility.HtmlEncode(code.ToString())).Append("</code></pre>"); code.Clear(); }
                fenced = !fenced;
                continue;
            }
            if (fenced) { code.AppendLine(line); continue; }
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                if (!list) { html.Append("<ul>"); list = true; }
                html.Append("<li>").Append(Inline(line[2..], applicationOrigin)).Append("</li>");
                continue;
            }
            if (list) { html.Append("</ul>"); list = false; }
            if (string.IsNullOrWhiteSpace(line)) continue;
            var heading = Regex.Match(line, "^(#{1,3}) (.+)$");
            if (heading.Success)
            {
                var level = heading.Groups[1].Length + 1;
                html.Append("<h").Append(level).Append('>').Append(Inline(heading.Groups[2].Value, applicationOrigin)).Append("</h").Append(level).Append('>');
            }
            else html.Append("<p>").Append(Inline(line, applicationOrigin)).Append("</p>");
        }
        if (list) html.Append("</ul>");
        if (fenced) html.Append("<pre><code>").Append(WebUtility.HtmlEncode(code.ToString())).Append("</code></pre>");
        return html.ToString();
    }

    private static bool IsAssetPath(string path) => Regex.IsMatch(path, @"^/media/images/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$");
    public static string? ImageSource(string source, string? applicationOrigin)
    {
        if (IsAssetPath(source)) return source;
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.UserInfo.Length != 0 || source.Length > 2048) return null;
        if (Uri.TryCreate(applicationOrigin, UriKind.Absolute, out var origin)
            && uri.Scheme == origin.Scheme && uri.Authority.Equals(origin.Authority, StringComparison.OrdinalIgnoreCase)
            && uri.Query.Length == 0 && uri.Fragment.Length == 0 && IsAssetPath(uri.AbsolutePath))
            return uri.AbsolutePath;
        return uri.Scheme == "https" && uri.HostNameType == UriHostNameType.Dns && uri.Host.Contains('.')
            && !uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            && !uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ? uri.AbsoluteUri : null;
    }

    private static string Inline(string text, string? applicationOrigin)
    {
        var html = new StringBuilder();
        var offset = 0;
        foreach (Match match in Regex.Matches(text, @"`(?<code>[^`]+)`|(?<image>!)?\[(?<label>[^\]]*)\]\((?<url>[^\s)]+)\)"))
        {
            html.Append(WebUtility.HtmlEncode(text[offset..match.Index]));
            if (match.Groups["code"].Success)
                html.Append("<code>").Append(WebUtility.HtmlEncode(match.Groups["code"].Value)).Append("</code>");
            else if (match.Groups["image"].Success)
            {
                var imageSource = ImageSource(match.Groups["url"].Value, applicationOrigin);
                if (imageSource is not null)
                    html.Append("<img src=\"").Append(WebUtility.HtmlEncode(imageSource))
                        .Append("\" alt=\"").Append(WebUtility.HtmlEncode(match.Groups["label"].Value))
                        .Append("\" loading=\"lazy\" decoding=\"async\" referrerpolicy=\"no-referrer\" />");
                else html.Append(WebUtility.HtmlEncode(match.Value));
            }
            else if (Uri.TryCreate(match.Groups["url"].Value, UriKind.Absolute, out var uri) &&
                     (uri.Scheme == "https" || uri.Scheme == "http"))
                html.Append("<a rel=\"nofollow noopener noreferrer\" href=\"").Append(WebUtility.HtmlEncode(uri.AbsoluteUri))
                    .Append("\">").Append(WebUtility.HtmlEncode(match.Groups["label"].Value)).Append("</a>");
            else html.Append(WebUtility.HtmlEncode(match.Value));
            offset = match.Index + match.Length;
        }
        return html.Append(WebUtility.HtmlEncode(text[offset..])).ToString();
    }
}

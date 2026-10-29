using System.Net;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DevSync.Web.Features.Blogs.Common;

public static class ArticleRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables().UseTaskLists().UseEmphasisExtras()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub).DisableHtml().Build();

    public static string Render(string markdown, string? applicationOrigin = null)
    {
        var document = Markdown.Parse(markdown ?? "", Pipeline);
        // The page already has its own H1. Preserve the existing article heading hierarchy.
        foreach (var heading in document.Descendants<HeadingBlock>()) heading.Level = Math.Min(6, heading.Level + 1);
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.ObjectRenderers.ReplaceOrAdd<Markdig.Renderers.Html.Inlines.LinkInlineRenderer>(new SafeLinkRenderer(applicationOrigin));
        renderer.ObjectRenderers.ReplaceOrAdd<Markdig.Renderers.Html.Inlines.AutolinkInlineRenderer>(new SafeAutoLinkRenderer());
        renderer.Render(document);
        return writer.ToString();
    }

    private static string? LinkSource(string? source)
    {
        if (string.IsNullOrEmpty(source) || source.Any(char.IsControl) || source.Contains('\\')) return null;
        if (source.StartsWith('#') || source.StartsWith('/') && !source.StartsWith("//")) return source;
        return Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.UserInfo.Length == 0 && uri.Scheme is "http" or "https" ? uri.AbsoluteUri : null;
    }

    private sealed class SafeLinkRenderer(string? origin) : HtmlObjectRenderer<LinkInline>
    {
        protected override void Write(HtmlRenderer renderer, LinkInline link)
        {
            var target = link.GetDynamicUrl?.Invoke() ?? link.Url ?? "";
            if (link.IsImage)
            {
                var source = ImageSource(target, origin);
                var alt = string.Concat(link.Descendants().Select(node => node switch { LiteralInline text => text.Content.ToString(), CodeInline code => code.Content, _ => "" }));
                if (source is null) { renderer.WriteEscape("![" + alt + "](" + target + ")"); return; }
                renderer.Write("<img src=\"").Write(WebUtility.HtmlEncode(source)).Write("\" alt=\"").Write(WebUtility.HtmlEncode(alt))
                    .Write("\" loading=\"lazy\" decoding=\"async\" referrerpolicy=\"no-referrer\" />");
                return;
            }
            var url = LinkSource(target);
            if (url is null) { renderer.WriteChildren(link); return; }
            renderer.Write("<a rel=\"nofollow noopener noreferrer\" href=\"").Write(WebUtility.HtmlEncode(url)).Write("\">");
            renderer.WriteChildren(link); renderer.Write("</a>");
        }
    }
    private sealed class SafeAutoLinkRenderer : HtmlObjectRenderer<AutolinkInline>
    {
        protected override void Write(HtmlRenderer renderer, AutolinkInline link)
        {
            var url = link.IsEmail ? null : LinkSource(link.Url);
            if (url is null) { renderer.WriteEscape(link.Url); return; }
            renderer.Write("<a rel=\"nofollow noopener noreferrer\" href=\"").Write(WebUtility.HtmlEncode(url)).Write("\">")
                .WriteEscape(link.Url).Write("</a>");
        }
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

}

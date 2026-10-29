using DevSync.Web.Features.Blogs.Common;

namespace DevSync.Tests;

public sealed class ArticleRendererTests
{
    [Fact]
    public void Https_images_render_with_encoded_attributes_and_code_stays_literal()
    {
        var source="![Pipeline \"diagram\"](https://learn.microsoft.com/pipeline.png?a=1&b=2)";
        var html=ArticleRenderer.Render(source);
        Assert.Contains("<img src=\"https://learn.microsoft.com/pipeline.png?a=1&amp;b=2\"",html);
        Assert.Contains("alt=\"Pipeline &quot;diagram&quot;\"",html);
        Assert.Contains("referrerpolicy=\"no-referrer\"",html);
        Assert.DoesNotContain("<img",ArticleRenderer.Render("`"+source+"`"));
        Assert.DoesNotContain("<img",ArticleRenderer.Render("```\n"+source+"\n```"));
    }

    [Theory]
    [InlineData("javascript:alert")]
    [InlineData("data:image/svg+xml,test")]
    [InlineData("http://example.com/image.png")]
    [InlineData("https://user:password@example.com/image.png")]
    [InlineData("https://127.0.0.1/image.png")]
    [InlineData("https://localhost/image.png")]
    [InlineData("https://internal.local/image.png")]
    public void Unsafe_image_targets_remain_text(string url)
    {
        var html=ArticleRenderer.Render("![Image]("+url+")");
        Assert.DoesNotContain("<img",html);
        Assert.DoesNotContain("<a ",html);
    }

    [Theory]
    [InlineData("<svg onload=alert(1)>")]
    [InlineData("[x](javascript:alert)")]
    [InlineData("[x](data:text/html,<script>)")]
    [InlineData("[x](//attacker.invalid)")]
    public void Untrusted_html_and_unsafe_urls_are_never_emitted_as_active_content(string source)
    {
        var html = ArticleRenderer.Render(source);
        Assert.DoesNotContain("<svg", html);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<a ", html);
    }

    [Fact]
    public void Headings_lists_code_and_http_links_render_and_unclosed_code_fences_are_safe()
    {
        var html = ArticleRenderer.Render("# Heading\n- item\n\n`<inline>` [Docs](https://example.com/?a=1&b=2)\n```\n<script>");
        Assert.Contains("<h2 id=\"heading\">Heading</h2>", html);
        Assert.Contains("<li>item</li>", html);
        Assert.Contains("<code>&lt;inline&gt;</code>", html);
        Assert.Contains("a=1&amp;b=2", html);
        Assert.Contains("<pre><code>&lt;script&gt;", html);
        Assert.EndsWith("</code></pre>", html.TrimEnd());
    }
    [Fact]
    public void Common_markdown_tables_emphasis_nested_lists_quotes_and_toc_links_render()
    {
        var html=ArticleRenderer.Render("## 1. What is RxJS?\n\n**Bold** and *italic* and ~~old~~.\n\n[Read section](#1-what-is-rxjs)\n\n| Feature | Value |\n| --- | --- |\n| **State** | `active` |\n\n1. First\n2. Second\n   - Nested\n\n> A quote\n\n---");
        Assert.Contains("<strong>Bold</strong>",html);Assert.Contains("<em>italic</em>",html);
        Assert.Contains("<table>",html);Assert.Contains("<th>Feature</th>",html);
        Assert.Contains("<strong>State</strong>",html);Assert.Contains("<ol>",html);Assert.Contains("<blockquote>",html);
        Assert.Contains("id=\"1-what-is-rxjs\"",html);Assert.Contains("href=\"#1-what-is-rxjs\"",html);
        Assert.Contains("<hr",html);
    }

    [Fact]
    public void Encoded_urls_html_and_autolinks_cannot_bypass_link_policy()
    {
        var html=ArticleRenderer.Render("<javascript:alert> [x](javascript&#58;alert) ![x](data:image/png;base64,AAAA)\n\n<img src=x onerror=alert(1)>");
        Assert.DoesNotContain("<a ",html);Assert.DoesNotContain("<img",html);
    }
}

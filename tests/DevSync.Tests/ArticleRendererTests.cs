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
        var html = ArticleRenderer.Render("# Heading\n- item\n`<inline>` [Docs](https://example.com/?a=1&b=2)\n```\n<script>");
        Assert.Contains("<h2>Heading</h2>", html);
        Assert.Contains("<ul><li>item</li></ul>", html);
        Assert.Contains("<code>&lt;inline&gt;</code>", html);
        Assert.Contains("a=1&amp;b=2", html);
        Assert.Contains("<pre><code>&lt;script&gt;", html);
        Assert.EndsWith("</code></pre>", html);
    }
}

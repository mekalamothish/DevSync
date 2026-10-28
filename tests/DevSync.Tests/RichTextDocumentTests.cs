using System.Text.Json;
using DevSync.Web.Common.Content;

namespace DevSync.Tests;

public sealed class RichTextDocumentTests
{
    public static string Document(string text="Formatted text") => JsonSerializer.Serialize(new {type="doc",content=new object[]{
        new{type="heading",attrs=new{level=2},content=new[]{new{type="text",text="A heading"}}},
        new{type="paragraph",content=new[]{new{type="text",text,marks=new[]{new{type="bold"},new{type="italic"},new{type="underline"}}}}},
        new{type="orderedList",attrs=new{start=3},content=new[]{new{type="listItem",content=new[]{new{type="paragraph",content=new[]{new{type="text",text="First item"}}}}}}},
        new{type="taskList",content=new[]{new{type="taskItem",attrs=new{@checked=true},content=new[]{new{type="paragraph",content=new[]{new{type="text",text="Completed task"}}}}}}}
    }});

    [Fact]
    public void Formatting_lists_and_task_state_are_rendered_from_json_without_raw_html()
    {
        var json=Document("<img src=x onerror=alert(1)>");Assert.Null(RichTextDocument.Validate(json));
        var html=RichTextDocument.Render(json);
        Assert.Contains("<h2>A heading</h2>",html);Assert.Contains("<ol start=\"3\">",html);
        Assert.Contains("<u><em><strong>&lt;img",html);Assert.Contains("data-checked=\"true\"",html);
        Assert.DoesNotContain("<img src=x",html);Assert.Contains("Completed task",RichTextDocument.PlainText(json));
    }
    [Theory]
    [InlineData("{\"type\":\"doc\",\"content\":[{\"type\":\"script\"}]}")]
    [InlineData("{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"bad\",\"marks\":[{\"type\":\"link\",\"attrs\":{\"href\":\"javascript:alert(1)\"}}]}]}]}")]
    [InlineData("{\"type\":\"doc\",\"content\":[{\"type\":\"image\",\"attrs\":{\"src\":\"data:image/svg+xml,evil\"}}]}")]
    [InlineData("{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"doc\"}]}]}")]
    [InlineData("not json")]
    public void Unsafe_or_malformed_documents_are_rejected_and_never_rendered(string json)
    {Assert.NotNull(RichTextDocument.Validate(json));Assert.Equal("",RichTextDocument.Render(json));}
    [Fact]
    public void Library_images_keep_access_control_and_attributes_are_encoded()
    {
        var json=JsonSerializer.Serialize(new{type="doc",content=new[]{new{type="image",attrs=new{src="/media/images/95d0821e-0636-4ee6-9ce5-a6fd9855099a",alt="\" onerror=alert(1)",onclick="alert(1)"}}}});
        Assert.Null(RichTextDocument.Validate(json));var html=RichTextDocument.Render(json);
        Assert.Contains("/media/images/95d0821e-0636-4ee6-9ce5-a6fd9855099a",html);Assert.DoesNotContain("onclick=",html);Assert.Contains("alt=\"&quot;",html);
    }
    [Fact]
    public void Excessive_document_size_or_depth_is_rejected()
    {
        Assert.NotNull(RichTextDocument.Validate(Document(new string('x',60001))));
        var json="{\"type\":\"paragraph\"}";for(var i=0;i<60;i++)json="{\"type\":\"blockquote\",\"content\":["+json+"]}";
        Assert.NotNull(RichTextDocument.Validate("{\"type\":\"doc\",\"content\":["+json+"]}"));
    }
}

using System.Net;
using System.Text.Json;
using DevSync.Web.Features.Blogs.Common;

namespace DevSync.Web.Common.Content;

// Tiptap JSON is data, never trusted HTML. Only this allowlist produces markup.
public static class RichTextDocument
{
    private static readonly HashSet<string> Nodes = ["doc","paragraph","heading","text","hardBreak","bulletList","orderedList","listItem","blockquote","codeBlock","horizontalRule","image","taskList","taskItem"];
    private static readonly HashSet<string> Marks = ["bold","italic","strike","underline","code","link"];
    private static string Text(JsonElement e,string name) => e.TryGetProperty(name,out var value) && value.ValueKind==JsonValueKind.String ? value.GetString()! : "";
    private static string E(string text) => WebUtility.HtmlEncode(text);
    private static JsonElement Attributes(JsonElement e) => e.TryGetProperty("attrs",out var a) && a.ValueKind==JsonValueKind.Object ? a : default;
    private static string Attribute(JsonElement e,string name) {var a=Attributes(e);return a.ValueKind==JsonValueKind.Object ? Text(a,name) : "";}
    private static int Number(JsonElement e,string name,int fallback)
    {var a=Attributes(e);return a.ValueKind==JsonValueKind.Object && a.TryGetProperty(name,out var n) && n.TryGetInt32(out var number) ? number : fallback;}
    private static bool Checked(JsonElement e) {var a=Attributes(e);return a.ValueKind==JsonValueKind.Object && a.TryGetProperty("checked",out var c) && c.ValueKind==JsonValueKind.True;}
    public static bool SafeLink(string url) => url.Length<=2048 && Uri.TryCreate(url,UriKind.Absolute,out var u) && u.UserInfo.Length==0 && u.Scheme is "https" or "http";

    public static string? Validate(string? json,string? origin=null)
    {
        if(string.IsNullOrWhiteSpace(json)) return null;
        if(json.Length>60000) return "Rich text must stay within 60,000 characters, including formatting.";
        try
        {
            using var document=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=48});
            if(Text(document.RootElement,"type")!="doc") return "Rich text must contain a Tiptap document.";
            var count=0;
            Visit(document.RootElement);
            return null;
            void Visit(JsonElement node)
            {
                if(++count>5000 || node.ValueKind!=JsonValueKind.Object || !Nodes.Contains(Text(node,"type"))) throw new FormatException();
                var type=Text(node,"type");
                if(type=="text" && (!node.TryGetProperty("text",out var text) || text.ValueKind!=JsonValueKind.String)) throw new FormatException();
                if(type=="text" && Text(node,"text").Length==0) throw new FormatException();
                if(type=="heading" && Number(node,"level",0) is <1 or >3) throw new FormatException();
                if(type=="orderedList" && Number(node,"start",1) is <1 or >1000000) throw new FormatException();
                if(type=="image" && ArticleRenderer.ImageSource(Attribute(node,"src"),origin) is null) throw new FormatException();
                if(node.TryGetProperty("marks",out var marks))
                {
                    if(marks.ValueKind!=JsonValueKind.Array || marks.GetArrayLength()>8) throw new FormatException();
                    foreach(var mark in marks.EnumerateArray())
                        if(mark.ValueKind!=JsonValueKind.Object || !Marks.Contains(Text(mark,"type")) || Text(mark,"type")=="link" && !SafeLink(Attribute(mark,"href"))) throw new FormatException();
                }
                if(node.TryGetProperty("content",out var content))
                {
                    if(content.ValueKind!=JsonValueKind.Array) throw new FormatException();
                    if(type is "doc" or "bulletList" or "orderedList" or "taskList" or "listItem" or "taskItem" or "blockquote" && content.GetArrayLength()==0) throw new FormatException();
                    if(type is "listItem" or "taskItem" && Text(content[0],"type")!="paragraph") throw new FormatException();
                    foreach(var child in content.EnumerateArray())
                    {
                        var childType=Text(child,"type");
                        var allowed=type switch
                        {
                            "paragraph" or "heading"=>childType is "text" or "hardBreak",
                            "codeBlock"=>childType=="text" && !child.TryGetProperty("marks",out _),
                            "bulletList" or "orderedList"=>childType=="listItem",
                            "taskList"=>childType=="taskItem",
                            "text" or "hardBreak" or "horizontalRule" or "image"=>false,
                            _=>childType is "paragraph" or "heading" or "bulletList" or "orderedList" or "blockquote" or "codeBlock" or "horizontalRule" or "image" or "taskList"
                        };
                        if(!allowed) throw new FormatException();
                        Visit(child);
                    }
                }
                else if(type is "doc" or "bulletList" or "orderedList" or "taskList" or "listItem" or "taskItem" or "blockquote") throw new FormatException();
            }
        }
        catch(Exception ex) when(ex is JsonException or FormatException or InvalidOperationException)
        {return "Rich text contains invalid or unsupported formatting, links, or images. Your saved content has not changed.";}
    }

    public static string Render(string? json,string? origin=null)
    {
        if(string.IsNullOrWhiteSpace(json) || Validate(json,origin) is not null) return "";
        using var document=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=48});
        return Node(document.RootElement);
        string Node(JsonElement node)
        {
            var inner=node.TryGetProperty("content",out var children) ? string.Concat(children.EnumerateArray().Select(Node)) : "";
            string Wrap(string tag) => $"<{tag}>{inner}</{tag}>";
            switch(Text(node,"type"))
            {
                case "text":
                    var text=E(Text(node,"text"));
                    if(node.TryGetProperty("marks",out var marks)) foreach(var mark in marks.EnumerateArray())
                    {
                        var tag=Text(mark,"type") switch {"bold"=>"strong","italic"=>"em","strike"=>"s","underline"=>"u","code"=>"code",_=>"a"};
                        text=tag=="a" ? $"<a href=\"{E(Attribute(mark,"href"))}\" rel=\"nofollow noopener noreferrer\">{text}</a>" : $"<{tag}>{text}</{tag}>";
                    }
                    return text;
                case "paragraph":return Wrap("p");
                case "heading":return Wrap("h"+Number(node,"level",2));
                case "hardBreak":return "<br />";
                case "horizontalRule":return "<hr />";
                case "bulletList":return Wrap("ul");
                case "orderedList":return $"<ol start=\"{Number(node,"start",1)}\">{inner}</ol>";
                case "listItem":return Wrap("li");
                case "blockquote":return Wrap("blockquote");
                case "codeBlock":return "<pre data-highlight-code=\""+E(Attribute(node,"language"))+"\"><code>"+E(Plain(node)[..^1])+"</code></pre>";
                case "image":return $"<img src=\"{E(ArticleRenderer.ImageSource(Attribute(node,"src"),origin)!)}\" alt=\"{E(Attribute(node,"alt"))}\" title=\"{E(Attribute(node,"title"))}\" loading=\"lazy\" decoding=\"async\" referrerpolicy=\"no-referrer\" />";
                case "taskList":return "<ul data-type=\"taskList\">"+inner+"</ul>";
                case "taskItem":return $"<li data-type=\"taskItem\" data-checked=\"{Checked(node).ToString().ToLowerInvariant()}\"><input type=\"checkbox\" disabled aria-label=\"{(Checked(node)?"Completed":"Open")}\" {(Checked(node)?"checked":"")} /><div>{inner}</div></li>";
                default:return inner;
            }
        }
    }
    public static string PlainText(string? json)
    {
        if(string.IsNullOrWhiteSpace(json)) return "";
        try {using var d=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=48});return Plain(d.RootElement);}
        catch(JsonException) {return "";}
    }
    public static bool HasContent(string? json)
    {
        if(string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var document=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=48});
            return !string.IsNullOrWhiteSpace(Plain(document.RootElement)) || HasImage(document.RootElement);
            static bool HasImage(JsonElement node) => Text(node,"type")=="image" ||
                node.TryGetProperty("content",out var children) && children.ValueKind==JsonValueKind.Array && children.EnumerateArray().Any(HasImage);
        }
        catch(Exception ex) when(ex is JsonException or InvalidOperationException) {return false;}
    }
    private static string Plain(JsonElement node)
    {
        if(Text(node,"type")=="text") return Text(node,"text");
        if(Text(node,"type")=="hardBreak") return "\n";
        var value=node.TryGetProperty("content",out var children) && children.ValueKind==JsonValueKind.Array ? string.Concat(children.EnumerateArray().Select(Plain)) : "";
        return value+(Text(node,"type") is "paragraph" or "heading" or "listItem" or "taskItem" or "codeBlock" ? "\n" : "");
    }
}

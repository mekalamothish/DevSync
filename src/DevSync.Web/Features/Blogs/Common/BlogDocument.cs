using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevSync.Infrastructure.Persistence;
using DevSync.Web.Common.Content;
using DevSync.Web.Features.Notes.Edit;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Web.Features.Blogs.Common;

public static class BlogDocument
{
    public static List<BlockCommand> ReadBlocks(string json)
    {
        try {return JsonSerializer.Deserialize<List<BlockCommand>>(json,new JsonSerializerOptions{MaxDepth=48}) ?? [];}
        catch(JsonException) {return [];}
    }
    public static async Task<string> RenderAsync(AppDbContext db,string format,string markdown,string blocksJson,string richText,
        Guid? previewOwner,string? origin,CancellationToken ct)
    {
        if(format=="richtext") return RichTextDocument.Render(richText,origin);
        if(format!="blocks") return ArticleRenderer.Render(markdown,origin);
        var blocks=ReadBlocks(blocksJson);
        var ids=blocks.Where(b=>b.Kind=="article" && b.LinkedPostId!=null).Select(b=>b.LinkedPostId!.Value).Distinct().ToArray();
        var links=await db.BlogPosts.AsNoTracking().Where(p=>ids.Contains(p.Id) && (p.AuthorId==previewOwner || p.PublishedAt!=null && !p.IsHidden))
            .Select(p=>new{p.Id,Title=p.AuthorId==previewOwner?p.Title:p.PublishedTitle,Url=p.AuthorId==previewOwner?"/blogs/"+p.Id+"/preview":"/blogs/"+p.Slug}).ToListAsync(ct);
        return string.Concat(blocks.Select(block=>
        {
            var text=WebUtility.HtmlEncode(block.Content ?? "");
            return block.Kind switch
            {
                "heading"=>"<h2>"+text+"</h2>",
                "richtext"=>RichTextDocument.Render(block.Content,origin),
                "code"=>$"<pre data-highlight-code=\"{WebUtility.HtmlEncode(block.Language ?? "")}\"><code>{text}</code></pre>",
                "checklist"=>"<ul class=\"content-checklist\">"+string.Concat((block.Content ?? "").Split('\n').Where(l=>!string.IsNullOrWhiteSpace(l)).Select(line=>
                {
                    var item=line.Trim();var done=item.StartsWith("[x]",StringComparison.OrdinalIgnoreCase);
                    var label=item.StartsWith("[ ]")||done ? item[3..].TrimStart():item;
                    return $"<li><span aria-label=\"{(done?"Completed":"Open")}\">{(done?"☑":"☐")}</span> {WebUtility.HtmlEncode(label)}</li>";
                }))+"</ul>",
                "article"=>links.FirstOrDefault(p=>p.Id==block.LinkedPostId) is {} link ? $"<p><a href=\"{WebUtility.HtmlEncode(link.Url)}\">{WebUtility.HtmlEncode(link.Title)}</a></p><p class=\"whitespace-pre-wrap\">{text}</p>" : "<p>Linked article is unavailable.</p>",
                _=>"<p class=\"whitespace-pre-wrap\">"+text+"</p>"
            };
        }));
    }
    public static string ToRichText(IEnumerable<BlockCommand> blocks)
    {
        var content=new JsonArray();
        JsonObject Paragraph(string text)
        {
            var inline=new JsonArray();var lines=text.Split('\n');
            for(var i=0;i<lines.Length;i++){if(i>0)inline.Add(new JsonObject{["type"]="hardBreak"});if(lines[i].Length>0)inline.Add(new JsonObject{["type"]="text",["text"]=lines[i]});}
            return new JsonObject{["type"]="paragraph",["content"]=inline};
        }
        foreach(var block in blocks.Where(b=>b.Kind!="article"))
        {
            var text=block.Content??"";
            if(block.Kind=="richtext")
            {
                if(!string.IsNullOrWhiteSpace(text) && JsonNode.Parse(text)?["content"] is JsonArray rich)
                    foreach(var node in rich) if(node is not null) content.Add(node.DeepClone());
            }
            else if(block.Kind=="heading")content.Add(new JsonObject{["type"]="heading",["attrs"]=new JsonObject{["level"]=2},["content"]=text.Length==0?new JsonArray():new JsonArray(new JsonObject{["type"]="text",["text"]=text})});
            else if(block.Kind=="code")content.Add(new JsonObject{["type"]="codeBlock",["attrs"]=new JsonObject{["language"]=block.Language},["content"]=text.Length==0?new JsonArray():new JsonArray(new JsonObject{["type"]="text",["text"]=text})});
            else if(block.Kind=="checklist")
            {
                var items=new JsonArray();foreach(var line in text.Split('\n').Where(l=>!string.IsNullOrWhiteSpace(l)))
                {var item=line.Trim();var done=item.StartsWith("[x]",StringComparison.OrdinalIgnoreCase);items.Add(new JsonObject{["type"]="taskItem",["attrs"]=new JsonObject{["checked"]=done},["content"]=new JsonArray(Paragraph(done||item.StartsWith("[ ]")?item[3..].TrimStart():item))});}
                if(items.Count>0)content.Add(new JsonObject{["type"]="taskList",["content"]=items});
            }
            else content.Add(Paragraph(text));
        }
        if(content.Count==0)content.Add(Paragraph(""));
        return new JsonObject{["type"]="doc",["content"]=content}.ToJsonString();
    }
}

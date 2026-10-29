using System.Text.RegularExpressions;
using DevSync.Domain.Blogs;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using DevSync.Web.Common.Content;
using DevSync.Web.Features.Blogs.Common;

namespace DevSync.Web.Features.Blogs.Edit;

public sealed record SaveResult(int Status, string? Error, Guid Version, string? PublishedUrl);

public sealed class Handler(AppDbContext db, DevSync.Web.Features.Notes.Edit.Handler noteEditor)
{
    public async Task<Command?> LoadAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var command=await db.BlogPosts.AsNoTracking().Where(p => p.Id == id && p.AuthorId == userId)
            .Select(p => new Command { Id = p.Id, Version = p.Version, Title = p.Title, CoverImageId = p.CoverImageId, Summary = p.Summary,
                Markdown = p.Markdown, EditorFormat=p.EditorFormat,RichText=p.RichText,
                Blocks=BlogDocument.ReadBlocks(p.BlocksJson), Tags = p.DraftTags, IsHidden = p.IsHidden, PublishedUrl = p.Slug == null || p.IsHidden ? null : "/blogs/" + p.Slug })
            .SingleOrDefaultAsync(ct);
        if(command is not null)
        {
            var choices=new DevSync.Web.Features.Notes.Edit.Command{Blocks=command.Blocks};
            await noteEditor.PopulateChoicesAsync(choices,userId,ct);command.Articles=choices.Articles;
        }
        return command;
    }

    public async Task<SaveResult> HandleAsync(Guid id, Guid userId, Command command, bool publish, CancellationToken ct,
        string? operation=null,Guid? blockId=null,string? kind=null)
    {
        var post = await db.BlogPosts.SingleOrDefaultAsync(p => p.Id == id && p.AuthorId == userId, ct);
        if (post is null) return new(404, "Article not found.", Guid.Empty, null);
        if (publish && post.IsHidden) return new(422, "This article is hidden by moderation. You can save private edits while awaiting review.", post.Version, null);
        var url = post.Slug == null ? null : "/blogs/" + post.Slug;
        if (post.Version != command.Version) return new(409, "This article changed in another tab. Copy your work before reloading the latest version.", post.Version, url);
        if(command.EditorFormat is not ("markdown" or "blocks" or "richtext")) return new(422,"Choose Markdown, blocks, or rich text.",post.Version,url);
        if(command.Blocks.Count>50 || command.Blocks.Sum(b=>(long)(b.Content?.Length??0))>60000 || command.Blocks.Any(b=>!DevSync.Web.Features.Notes.Edit.Handler.Kinds.Contains(b.Kind) || (b.Language?.Length??0)>30))
            return new(422,"Keep articles within 50 supported blocks and 60,000 content characters per editor.",post.Version,url);
        if(RichTextDocument.Validate(command.RichText) is string richError) return new(422,richError,post.Version,url);
        foreach(var rich in command.Blocks.Where(b=>b.Kind=="richtext"))
            if(RichTextDocument.Validate(rich.Content) is string blockError) return new(422,blockError,post.Version,url);
        Guid? placeholder=null;
        if(operation is not null)
        {
            var index=command.Blocks.FindIndex(b=>b.Id==blockId);
            switch(operation)
            {
                case "add" when DevSync.Web.Features.Notes.Edit.Handler.Kinds.Contains(kind??"") && command.Blocks.Count<50:
                    placeholder=Guid.NewGuid(); command.Blocks.Add(new(){Id=placeholder.Value,Kind=kind!});break;
                case "remove" when index>=0:command.Blocks.RemoveAt(index);break;
                case "up" when index>0:(command.Blocks[index-1],command.Blocks[index])=(command.Blocks[index],command.Blocks[index-1]);break;
                case "down" when index>=0 && index+1<command.Blocks.Count:(command.Blocks[index+1],command.Blocks[index])=(command.Blocks[index],command.Blocks[index+1]);break;
                default:return new(422,"Choose a valid block action.",post.Version,url);
            }
        }
        foreach(var block in command.Blocks.Where(b=>b.Id==Guid.Empty)) block.Id=Guid.NewGuid();
        if(command.Blocks.Select(b=>b.Id).Distinct().Count()!=command.Blocks.Count) return new(422,"Each article block must appear only once.",post.Version,url);
        var linked=command.Blocks.Where(b=>b.Kind=="article" && b.LinkedPostId!=null).Select(b=>b.LinkedPostId!.Value).Distinct().ToArray();
        var permitted=await db.BlogPosts.Where(p=>linked.Contains(p.Id) && (p.AuthorId==userId || p.PublishedAt!=null && !p.IsHidden)).Select(p=>p.Id).ToArrayAsync(ct);
        if(linked.Except(permitted).Any() || command.Blocks.Any(b=>b.Kind=="article" && b.LinkedPostId==null && b.Id!=placeholder))
            return new(422,"Link a published article or one of your own drafts.",post.Version,url);
        if(command.CoverImageId is not null && !await db.ImageAssets.AnyAsync(image=>image.Id==command.CoverImageId && image.OwnerId==userId,ct))
            return new(422,"Choose a cover from your personal image library or upload a new image.",post.Version,url);
        var tags = (command.Tags ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.ToLowerInvariant()).Distinct().Order().ToArray();
        if (tags.Length > 5 || tags.Any(t => !Regex.IsMatch(t, "^[a-z0-9][a-z0-9+#.-]{0,29}$")))
            return new(422, "Use up to five comma-separated tags, each 1–30 letters, numbers, +, #, dot, or hyphen.", post.Version, url);
        var hasContent=command.EditorFormat switch
        {
            "richtext"=>RichTextDocument.HasContent(command.RichText),
            "blocks"=>command.Blocks.Any(b=>b.Kind=="richtext"?RichTextDocument.HasContent(b.Content):!string.IsNullOrWhiteSpace(b.Content)||b.LinkedPostId!=null),
            _=>!string.IsNullOrWhiteSpace(command.Markdown)
        };
        if (publish && (!hasContent || string.IsNullOrWhiteSpace(command.Summary)))
            return new(422, "Add an article and a short summary before publishing.", post.Version, url);
        post.CoverImageId = command.CoverImageId;
        post.Title = command.Title.Trim();
        post.Summary = command.Summary?.Trim() ?? "";
        post.Markdown = command.Markdown ?? "";
        post.EditorFormat=command.EditorFormat;post.RichText=command.RichText??"";post.BlocksJson=JsonSerializer.Serialize(command.Blocks);
        post.DraftTags = string.Join(',', tags);
        post.UpdatedAt = DateTimeOffset.UtcNow;
        post.Version = Guid.NewGuid();
        if (publish)
        {
            if (post.Slug is null)
            {
                var stem = Regex.Replace(post.Title.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
                stem = stem.Length == 0 ? "article" : stem[..Math.Min(stem.Length, 100)].TrimEnd('-');
                post.Slug = stem + "-" + post.Id.ToString("N");
            }
            post.PublishedCoverImageId = post.CoverImageId;
            post.PublishedTitle = post.Title;
            post.PublishedSummary = post.Summary;
            post.PublishedMarkdown = post.EditorFormat=="markdown" ? post.Markdown : "";
            post.PublishedEditorFormat=post.EditorFormat;
            post.PublishedBlocksJson=post.EditorFormat=="blocks" ? post.BlocksJson : "[]";
            post.PublishedRichText=post.EditorFormat=="richtext" ? post.RichText : "";
            post.PublishedAt ??= DateTimeOffset.UtcNow;
            var oldTags = await db.BlogTags.Where(t => t.PostId == id).ToListAsync(ct);
            db.BlogTags.RemoveRange(oldTags.Where(t => !tags.Contains(t.Name)));
            db.BlogTags.AddRange(tags.Except(oldTags.Select(t => t.Name)).Select(t => new BlogTag { PostId = id, Name = t }));
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { return new(409, "This article changed in another tab. Copy your work before reloading the latest version.", Guid.Empty, url); }
        return new(200, null, post.Version, post.Slug == null ? null : "/blogs/" + post.Slug);
    }
}

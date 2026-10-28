using DevSync.Domain.Images;
using DevSync.Infrastructure.Persistence;
using DevSync.Web.Features.Teams;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Web.Features.Images;

public sealed record Card(Guid Id, string Name, string Description, int Width, int Height, bool IsPublic, Guid Version, DateTimeOffset CreatedAt);
public sealed record Library(Guid? TeamId, string Title, bool CanUpload, bool CanManage, Card[] Images, int Total, int Page, int Pages, string BaseUrl)
{
    public string Path => TeamId is null ? "/images" : $"/teams/{TeamId}/images";
}
public sealed record Result(int Status, string? Error = null);

public sealed class Handler(AppDbContext db, IConfiguration config, TimeProvider clock)
{
    public async Task<Library?> Load(Guid actor, Guid? teamId, int page, CancellationToken ct)
    {
        string? role = null; var title = "My images";
        if (teamId is not null)
        {
            var team = await (from t in db.Teams join m in db.TeamMemberships on t.Id equals m.TeamId
                where t.Id == teamId && m.UserId == actor select new {t.Name,m.Role}).SingleOrDefaultAsync(ct);
            if(team is null) return null;
            role=team.Role; title=team.Name + " · Images";
        }
        var query=db.ImageAssets.AsNoTracking().Where(x => teamId == null ? x.OwnerId == actor : x.TeamId == teamId);
        var total=await query.CountAsync(ct); var pages=Math.Max(1,(total+23)/24); page=Math.Clamp(page,1,pages);
        var cards=await query.OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id).Skip((page-1)*24).Take(24)
            .Select(x=>new Card(x.Id,x.Name,x.Description,x.Width,x.Height,x.IsPublic,x.Version,x.CreatedAt)).ToArrayAsync(ct);
        return new(teamId,title,role==null || Access.Write(role),role==null || Access.Manage(role),cards,total,page,pages,
            (config["Account:PublicBaseUrl"] ?? "http://localhost:5180").TrimEnd('/'));
    }
    public async Task<Result> Upload(Guid actor, Guid? teamId, IFormFile? file, string? description, CancellationToken ct)
    {
        // Authorize before reading or processing potentially expensive uploaded bytes.
        if(teamId is not null)
        {
            var role=await db.TeamMemberships.Where(x=>x.TeamId==teamId && x.UserId==actor).Select(x=>x.Role).SingleOrDefaultAsync(ct);
            if(role is null) return new(404);
            if(!Access.Write(role)) return new(403);
        }
        description=description?.Trim();
        if(string.IsNullOrWhiteSpace(description) || description.Length>160) return new(422,"Add an image description of 1–160 characters.");
        if(file is null || file.Length is <=0 or > ImageFormat.MaxBytes) return new(422,"Choose a PNG or JPEG image up to 5 MB.");
        using var bytes=new MemoryStream();
        await using(var input=file.OpenReadStream())
        {
            var buffer=new byte[8192]; int read;
            while((read=await input.ReadAsync(buffer,ct))>0)
            {
                if(bytes.Length+read>ImageFormat.MaxBytes) return new(422,"Choose an image up to 5 MB.");
                await bytes.WriteAsync(buffer.AsMemory(0,read),ct);
            }
        }
        var data=bytes.ToArray(); var format=ImageFormat.Inspect(data);
        if(format is null) return new(422,"This file is not a supported PNG or JPEG. Use a non-interlaced PNG or a baseline/progressive JPEG, up to 20 million pixels.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        if(teamId is not null)
        {
            var (team,role)=await Access.LockAsync(db,teamId.Value,actor,ct);
            if(team is null) return new(404);
            if(!Access.Write(role!)) return new(403);
            Access.Record(db,team,actor,"Uploaded an image to the team library.",clock);
        }
        else
        {
            var user=await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {actor} FOR UPDATE").SingleOrDefaultAsync(ct);
            if(user is null) return new(404);
        }
        if(await db.ImageAssets.CountAsync(x=>teamId==null ? x.OwnerId==actor : x.TeamId==teamId,ct)>=100)
            return new(422,"This library has reached its 100-image limit.");
        var name=Path.GetFileName(file.FileName.Replace('\\','/'));
        if(string.IsNullOrWhiteSpace(name)) name="Image";
        db.ImageAssets.Add(new ImageAsset {OwnerId=teamId==null ? actor : null,TeamId=teamId,Name=name[..Math.Min(200,name.Length)],
            Description=description,Data=data,ContentType=format.ContentType,Width=format.Width,Height=format.Height,CreatedAt=clock.GetUtcNow().ToUniversalTime()});
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(200);
    }
    public async Task<Result> Visibility(Guid actor, Guid? teamId, Guid id, Guid version, bool isPublic, CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        if(teamId is not null)
        {
            var (team,role)=await Access.LockAsync(db,teamId.Value,actor,ct);
            if(team is null) return new(404);
            if(!Access.Manage(role!)) return new(403);
            Access.Record(db,team,actor,isPublic ? "Enabled an image public link." : "Disabled an image public link.",clock);
        }
        var image=await db.ImageAssets.SingleOrDefaultAsync(x=>x.Id==id && (teamId==null ? x.OwnerId==actor : x.TeamId==teamId),ct);
        if(image is null) return new(404);
        if(image.Version!=version) return new(409,"Image settings changed. Reload the library before trying again.");
        image.IsPublic=isPublic; image.Version=Guid.NewGuid();
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch(DbUpdateConcurrencyException) { return new(409,"Image settings changed. Reload the library before trying again."); }
        return new(200);
    }
    public async Task<(byte[] Data,string ContentType)?> Read(Guid id, Guid? actor, CancellationToken ct)
    {
        var image=await db.ImageAssets.AsNoTracking().Where(x=>x.Id==id && (x.IsPublic || actor!=null &&
            (x.OwnerId==actor || x.TeamId!=null && db.TeamMemberships.Any(m=>m.TeamId==x.TeamId && m.UserId==actor))))
            .Select(x=>new {x.Data,x.ContentType}).SingleOrDefaultAsync(ct);
        return image is null ? null : (image.Data,image.ContentType);
    }
}

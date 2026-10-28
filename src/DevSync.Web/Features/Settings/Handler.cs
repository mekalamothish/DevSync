using System.ComponentModel.DataAnnotations;
using DevSync.Domain.Governance;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.ModelBinding;
namespace DevSync.Web.Features.Settings;
public sealed class Command
{
    public Guid Version { get; set; }
    public bool PublicProfile { get; set; }
    public bool ShowBio { get; set; }
    public bool ReduceMotion { get; set; }
    [Range(12,24)] public int ArticlesPageSize { get; set; } = 12;
    [BindNever] public string Handle { get; set; } = "";
}
public sealed class Handler(AppDbContext db)
{
    private Command? cached;
    public async Task<Command> LoadAsync(Guid userId, CancellationToken ct)
    {
        if(cached is not null) return cached;
        var prefs=await db.AccountPreferences.AsNoTracking().SingleOrDefaultAsync(x=>x.UserId==userId,ct);
        var handle=await db.DeveloperProfiles.Where(p=>p.UserId==userId).Select(p=>p.Handle).SingleOrDefaultAsync(ct) ?? "";
        return cached=new Command{Version=prefs?.Version??Guid.Empty,PublicProfile=prefs?.PublicProfile??false,ShowBio=prefs?.ShowBio??false,
            ReduceMotion=prefs?.ReduceMotion??false,ArticlesPageSize=prefs?.ArticlesPageSize??12,Handle=handle};
    }
    public async Task<(int Status,string? Error)> SaveAsync(Guid userId,Command c,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var user=await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if(user is null) return(404,"Account not found.");
        var prefs=await db.AccountPreferences.SingleOrDefaultAsync(x=>x.UserId==userId,ct);
        if((prefs?.Version??Guid.Empty)!=c.Version) return(409,"Settings changed in another tab. Reload before saving.");
        if(c.ArticlesPageSize is not (12 or 24)) return(422,"Choose 12 or 24 articles per page.");
        if(prefs is null){prefs=new AccountPreferences{UserId=userId};db.AccountPreferences.Add(prefs);}
        prefs.PublicProfile=c.PublicProfile;prefs.ShowBio=c.ShowBio;prefs.ReduceMotion=c.ReduceMotion;prefs.ArticlesPageSize=c.ArticlesPageSize;prefs.Version=Guid.NewGuid();
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return(200,null);
    }
}

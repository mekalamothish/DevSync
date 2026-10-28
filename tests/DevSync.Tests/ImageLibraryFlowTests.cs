using System.Net;
using System.Text.RegularExpressions;
using DevSync.Domain.Profiles;
using DevSync.Domain.Teams;
using DevSync.Infrastructure.Identity;
using DevSync.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevSync.Tests;

[Collection("PostgreSQL")]
[Trait("Category","Database")]
public sealed class ImageLibraryFlowTests(AccountFixture fixture) : IClassFixture<AccountFixture>
{
    private static string Field(string html,string name) => WebUtility.HtmlDecode(Regex.Match(html,$"name=\"{name}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    private async Task<Guid> User(HttpClient client,string suffix)
    {
        using var scope=fixture.Factory.Services.CreateScope();
        var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user=new ApplicationUser {Email=fixture.Email(suffix),UserName=fixture.Email(suffix),EmailConfirmed=true};
        Assert.True((await users.CreateAsync(user,"Images-Test-Password42!")).Succeeded);
        Assert.True((await users.AddToRoleAsync(user,ApplicationRoles.Member)).Succeeded);
        var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.DeveloperProfiles.Add(new DeveloperProfile {UserId=user.Id,Handle=fixture.RunId+"_"+suffix.ToLowerInvariant(),DisplayName="Image tester"});
        await db.SaveChangesAsync();
        var page=await client.GetStringAsync("/account/sign-in");
        var login=await client.PostAsync("/account/sign-in",new FormUrlEncodedContent(new Dictionary<string,string> {
            ["Email"]=user.Email,["Password"]="Images-Test-Password42!",["__RequestVerificationToken"]=Field(page,"__RequestVerificationToken")}));
        Assert.Equal(HttpStatusCode.Redirect,login.StatusCode); return user.Id;
    }
    private static async Task<HttpResponseMessage> Upload(HttpClient client,string path,byte[]? bytes=null)
    {
        var page=await client.GetStringAsync(path);
        using var form=new MultipartFormDataContent();
        form.Add(new StringContent(Field(page,"__RequestVerificationToken")),"__RequestVerificationToken");
        form.Add(new StringContent("Pipeline diagram"),"description");
        var file=new ByteArrayContent(bytes??ImageLibraryTests.Png);
        file.Headers.ContentType=new("text/html"); // Server derives the MIME type from bytes.
        form.Add(file,"image","../../diagram.png");
        return await client.PostAsync(path+"/upload",form);
    }
    private static async Task<HttpResponseMessage> Visibility(HttpClient client,string path,Guid image,Guid version,bool publish)
    {
        var html=await client.GetStringAsync(path);
        return await client.PostAsync(path+$"/{image}/visibility",new FormUrlEncodedContent(new Dictionary<string,string> {
            ["version"]=version.ToString(),["isPublic"]=publish.ToString(),["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken")}));
    }
    [Fact]
    public async Task Personal_library_is_private_public_links_are_explicit_and_revocable()
    {
        using var owner=fixture.Client(); using var other=fixture.Client(); using var anonymous=fixture.Client();
        var user=await User(owner,"imageowner"); await User(other,"imageother");
        Assert.Equal(HttpStatusCode.Redirect,(await Upload(owner,"/images")).StatusCode);
        Guid id,version;
        using(var scope=fixture.Factory.Services.CreateScope())
        {
            var image=await scope.ServiceProvider.GetRequiredService<AppDbContext>().ImageAssets.SingleAsync(x=>x.OwnerId==user);
            id=image.Id; version=image.Version; Assert.False(image.IsPublic); Assert.Equal("diagram.png",image.Name); Assert.Equal("image/png",image.ContentType);
        }
        var path=$"/media/images/{id}";
        Assert.Equal(HttpStatusCode.NotFound,(await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync(path)).StatusCode);
        var content=await owner.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK,content.StatusCode); Assert.Equal(ImageLibraryTests.Png,await content.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/png",content.Content.Headers.ContentType!.MediaType); Assert.True(content.Headers.CacheControl!.NoStore);
        Assert.Contains("nosniff",content.Headers.GetValues("X-Content-Type-Options"));
        Assert.Equal(HttpStatusCode.NotFound,(await Visibility(other,"/images",id,version,true)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await Visibility(owner,"/images",id,version,true)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Visibility(owner,"/images",id,version,false)).StatusCode);
        using(var scope=fixture.Factory.Services.CreateScope()) version=await scope.ServiceProvider.GetRequiredService<AppDbContext>().ImageAssets.Where(x=>x.Id==id).Select(x=>x.Version).SingleAsync();
        Assert.Equal(HttpStatusCode.Redirect,(await Visibility(owner,"/images",id,version,false)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Upload(owner,"/images",System.Text.Encoding.UTF8.GetBytes("<svg>bad</svg>"))).StatusCode);
        using(var scope=fixture.Factory.Services.CreateScope()) Assert.Equal(1,await scope.ServiceProvider.GetRequiredService<AppDbContext>().ImageAssets.CountAsync(x=>x.OwnerId==user));
    }
    [Fact]
    public async Task Personal_quota_is_atomic_under_concurrent_uploads_and_gallery_is_paged()
    {
        using var owner=fixture.Client(); var user=await User(owner,"imagequota");
        using(var scope=fixture.Factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ImageAssets.AddRange(Enumerable.Range(0,99).Select(i=>new DevSync.Domain.Images.ImageAsset {
                OwnerId=user,Name="test.png",Description="Seed image "+i,Data=ImageLibraryTests.Png,ContentType="image/png",Width=1,Height=1}));
            await db.SaveChangesAsync();
        }
        var outcomes=await Task.WhenAll(Upload(owner,"/images"),Upload(owner,"/images"));
        Assert.Single(outcomes,x=>x.StatusCode==HttpStatusCode.Redirect);
        Assert.Single(outcomes,x=>x.StatusCode==HttpStatusCode.UnprocessableEntity);
        var first=await owner.GetStringAsync("/images");
        Assert.Equal(24,Regex.Matches(first,"<article ").Count);
        Assert.Contains("Page 1 of 5",first);
        Assert.Equal(4,Regex.Matches(await owner.GetStringAsync("/images?page=5"),"<article ").Count);
        using(var missingToken=new MultipartFormDataContent()) {
            missingToken.Add(new StringContent("test"),"description");
            Assert.Equal(HttpStatusCode.BadRequest,(await owner.PostAsync("/images/upload",missingToken)).StatusCode);
        }
        using(var scope=fixture.Factory.Services.CreateScope()) Assert.Equal(100,await scope.ServiceProvider.GetRequiredService<AppDbContext>().ImageAssets.CountAsync(x=>x.OwnerId==user));
    }
    [Fact]
    public async Task Team_images_follow_membership_writer_roles_and_owner_control_of_public_links()
    {
        using var owner=fixture.Client(); using var viewer=fixture.Client(); using var writer=fixture.Client(); using var other=fixture.Client();
        var ownerId=await User(owner,"imageTo");var viewerId=await User(viewer,"imageTv");var writerId=await User(writer,"imageTw"); await User(other,"imageTx");
        var team=new Team {OwnerId=ownerId,Name="Image team"};
        using(var scope=fixture.Factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.Teams.Add(team);
            db.TeamMemberships.AddRange(new TeamMembership {TeamId=team.Id,UserId=ownerId,Role="owner"},new TeamMembership {TeamId=team.Id,UserId=viewerId,Role="viewer"},new TeamMembership {TeamId=team.Id,UserId=writerId,Role="member"});
            await db.SaveChangesAsync();
        }
        var library=$"/teams/{team.Id}/images";
        Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync(library)).StatusCode);
        Assert.DoesNotContain("Upload an image",await viewer.GetStringAsync(library));
        // Viewer uses a CSRF token from another form because the read-only library has no form.
        var viewerHtml=await viewer.GetStringAsync("/teams");
        using(var form=new MultipartFormDataContent()) {
            form.Add(new StringContent(Field(viewerHtml,"__RequestVerificationToken")),"__RequestVerificationToken");
            form.Add(new StringContent("test"),"description");form.Add(new ByteArrayContent(ImageLibraryTests.Png),"image","test.png");
            Assert.Equal(HttpStatusCode.Forbidden,(await viewer.PostAsync(library+"/upload",form)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Redirect,(await Upload(writer,library)).StatusCode);
        Guid id,version;
        using(var scope=fixture.Factory.Services.CreateScope()) {var image=await scope.ServiceProvider.GetRequiredService<AppDbContext>().ImageAssets.SingleAsync(x=>x.TeamId==team.Id); id=image.Id;version=image.Version; Assert.Null(image.OwnerId);}
        var path=$"/media/images/{id}";
        Assert.Equal(HttpStatusCode.OK,(await viewer.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync(path)).StatusCode);
        var token=Field(await writer.GetStringAsync("/teams"),"__RequestVerificationToken");
        Assert.Equal(HttpStatusCode.Forbidden,(await writer.PostAsync(library+$"/{id}/visibility",new FormUrlEncodedContent(new Dictionary<string,string>{["version"]=version.ToString(),["isPublic"]="true",["__RequestVerificationToken"]=token}))).StatusCode);
        using(var scope=fixture.Factory.Services.CreateScope()) { var db=scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.TeamMemberships.Where(x=>x.TeamId==team.Id && x.UserId==viewerId).ExecuteDeleteAsync(); }
        Assert.Equal(HttpStatusCode.NotFound,(await viewer.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await viewer.GetAsync(library)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await Visibility(owner,library,id,version,true)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await other.GetAsync(path)).StatusCode);
    }
}

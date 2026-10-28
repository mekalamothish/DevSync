using DevSync.Web.Features.Images;
using DevSync.Web.Features.Blogs.Common;

namespace DevSync.Tests;

public sealed class ImageLibraryTests
{
    // A complete non-interlaced 1x1 PNG with valid CRCs and zlib stream.
    public static byte[] Png => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    [Fact]
    public void Png_structure_pixels_and_dimensions_are_verified()
    {
        var metadata=ImageFormat.Inspect(Png);
        Assert.NotNull(metadata); Assert.Equal("image/png",metadata.ContentType); Assert.Equal(1,metadata.Width);
        var corrupt=Png; corrupt[45]^=1;
        Assert.Null(ImageFormat.Inspect(corrupt));
        Assert.Null(ImageFormat.Inspect(Png[..^1]));
        Assert.Null(ImageFormat.Inspect(Png.Concat(new byte[]{0}).ToArray()));
        Assert.Null(ImageFormat.Inspect(System.Text.Encoding.UTF8.GetBytes("<svg onload='alert(1)'/>")));
        Assert.Null(ImageFormat.Inspect(new byte[ImageFormat.MaxBytes+1]));
    }
    [Fact]
    public void Jpeg_dimensions_and_complete_file_boundaries_are_verified()
    {
        var jpeg=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fixtures/library-example.jpg"));
        var metadata=ImageFormat.Inspect(jpeg);
        Assert.NotNull(metadata); Assert.Equal("image/jpeg",metadata.ContentType); Assert.Equal(480,metadata.Width); Assert.Equal(240,metadata.Height);
        Assert.Null(ImageFormat.Inspect(jpeg[..^2]));
        Assert.Null(ImageFormat.Inspect(jpeg.Concat(new byte[]{0}).ToArray()));
    }
    [Fact]
    public void Uploaded_images_support_relative_and_configured_absolute_urls_only()
    {
        var path="/media/images/72382eab-a55f-445e-b303-e64b8903f086";
        Assert.Contains("<img src=\""+path+"\"",ArticleRenderer.Render("![Diagram]("+path+")"));
        Assert.Contains("<img src=\""+path+"\"",ArticleRenderer.Render("![Diagram](http://localhost:5180"+path+")","http://localhost:5180"));
        foreach(var url in new[]{"//localhost:5180"+path,"http://localhost:5181"+path,path+"?x=1","/media/images/../foo",path+"/other"})
            Assert.DoesNotContain("<img",ArticleRenderer.Render("![Diagram]("+url+")","http://localhost:5180"));
    }
}

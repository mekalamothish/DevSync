using DevSync.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;

namespace DevSync.Tests;

public sealed class EmailDeliveryTests
{
    [Theory]
    [InlineData("Development",null,false)]
    [InlineData("Production",null,true)]
    [InlineData("Development","Smtp",true)]
    [InlineData("Development","File",false)]
    [InlineData("Production","Smtp",true)]
    public void Delivery_mode_can_enable_real_mail_without_switching_environment(string environment,string? mode,bool smtp)
        => Assert.Equal(smtp,EmailDelivery.UsesSmtp(Config(mode),new EnvironmentStub(environment)));

    [Theory]
    [InlineData("Production","File")]
    [InlineData("Development","Typo")]
    public void Invalid_or_production_file_delivery_is_rejected(string environment,string mode)
        => Assert.Throws<InvalidOperationException>(()=>EmailDelivery.UsesSmtp(Config(mode),new EnvironmentStub(environment)));

    private static IConfiguration Config(string? mode) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string,string?>{["Email:Delivery"]=mode}).Build();
    internal sealed class EnvironmentStub(string name):IHostEnvironment
    {
        public string EnvironmentName {get;set;}=name;
        public string ApplicationName {get;set;}="DevSync.Tests";
        public string ContentRootPath {get;set;}=Path.GetTempPath();
        public IFileProvider ContentRootFileProvider {get;set;}=new NullFileProvider();
    }
}

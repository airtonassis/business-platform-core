using Business.Platform.Core.Api.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using Xunit;

namespace Business.Platform.Core.IntegrationTests;

public sealed class WebConfigurationTests
{
    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void Backchannel_rejects_disabled_web_or_memory_store(string enabled)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["WebAuthentication:Enabled"]=enabled,["WebAuthentication:Authority"]="https://synthetic.invalid",["WebAuthentication:ClientId"]="local",
            ["WebAuthentication:BackChannelLogout:Enabled"]="true"}).Build();
        Assert.Throws<InvalidOperationException>(()=>WebComposition.Add(new ServiceCollection(),config,new HostingEnvironment{EnvironmentName="Development"}));
    }
    [Theory]
    [InlineData("Postgres")]
    [InlineData("Unknown")]
    public void Persistent_or_unknown_profile_never_falls_back_when_configuration_is_missing(string mode)
    {
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["WebAuthentication:Enabled"]="true",["WebAuthentication:Authority"]="https://synthetic.invalid",["WebAuthentication:ClientId"]="local",
            ["WebAuthentication:SessionStore"]=mode}).Build();
        var services=new ServiceCollection();
        Assert.Throws<InvalidOperationException>(()=>WebComposition.Add(services,configuration,new HostingEnvironment{EnvironmentName="Development"}));
        Assert.DoesNotContain(services,s=>s.ServiceType==typeof(WebTicketStore));
    }
    [Theory]
    [InlineData("shared.invalid","bpc_sessions_local","bpc_session_runtime")]
    [InlineData("127.0.0.1","core","bpc_session_runtime")]
    [InlineData("127.0.0.1","bpc_sessions_local","postgres")]
    public void Technical_store_refuses_remote_business_or_privileged_target(string host,string database,string username)
    {
        Assert.Throws<ArgumentException>(()=>new Business.Platform.Core.Infrastructure.Persistence.PostgresSessionStore(new(){Host=host,Name=database,Username=username,Password="synthetic"}));
    }
    [Fact]
    public void Http_authority_is_never_accepted_even_on_loopback()
    {
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["WebAuthentication:Enabled"]="true",["WebAuthentication:Authority"]="http://127.0.0.1:12345/realm",["WebAuthentication:ClientId"]="local"}).Build();
        Assert.Throws<InvalidOperationException>(()=>WebComposition.Add(new ServiceCollection(),configuration,new HostingEnvironment{EnvironmentName="Development"}));
    }
    [Fact]
    public void Local_session_store_is_not_silently_enabled_in_production()
    {
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["WebAuthentication:Enabled"]="true",["WebAuthentication:Authority"]="https://synthetic.invalid",["WebAuthentication:ClientId"]="local"}).Build();
        Assert.Throws<InvalidOperationException>(()=>WebComposition.Add(new ServiceCollection(),configuration,new HostingEnvironment{EnvironmentName="Production"}));
    }
    [Fact]
    public void Web_disabled_adds_no_cookie_session_store()
    {
        var services=new ServiceCollection();WebComposition.Add(services,new ConfigurationBuilder().Build(),new HostingEnvironment{EnvironmentName="Development"});
        Assert.DoesNotContain(services,s=>s.ServiceType==typeof(WebTicketStore));
    }
}

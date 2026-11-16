using Business.Platform.Core.Api.Web;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Xunit;

namespace Business.Platform.Core.IntegrationTests;

public sealed class WebSessionTests
{
    private sealed class Clock:TimeProvider { public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now; }
    private static AuthenticationTicket Ticket()=>new(new ClaimsPrincipal(new ClaimsIdentity([new("iss","https://synthetic.invalid"),new("sub","a")],"Web")),new AuthenticationProperties(),"Web");
    [Fact]
    public async Task Revocation_and_expiration_are_server_side()
    {
        var clock=new Clock();var store=new WebTicketStore(clock);var key=await store.StoreAsync(Ticket());
        Assert.NotNull(await store.RetrieveAsync(key));await store.RemoveAsync(key);Assert.Null(await store.RetrieveAsync(key));
        key=await store.StoreAsync(Ticket());clock.Now+=TimeSpan.FromMinutes(31);Assert.Null(await store.RetrieveAsync(key));
    }
    [Fact]
    public async Task Renewal_never_resurrects_revoked_or_absolute_expired_session()
    {
        var clock=new Clock();var store=new WebTicketStore(clock);var ticket=Ticket();var key=await store.StoreAsync(ticket);
        await store.RemoveAsync(key);await store.RenewAsync(key,ticket);Assert.Null(await store.RetrieveAsync(key));
        key=await store.StoreAsync(ticket);clock.Now+=TimeSpan.FromHours(25);await store.RenewAsync(key,ticket);Assert.Null(await store.RetrieveAsync(key));
    }
    [Fact]
    public async Task Renewed_ticket_cannot_change_identity()
    {
        var store=new WebTicketStore(TimeProvider.System);var key=await store.StoreAsync(Ticket());
        var other=new AuthenticationTicket(new(new ClaimsIdentity([new("iss","https://synthetic.invalid"),new("sub","b")],"Web")),"Web");
        await store.RenewAsync(key,other);Assert.Null(await store.RetrieveAsync(key));
    }
    [Fact]
    public async Task Human_extensions_keep_idle_alive_but_never_extend_absolute_limit()
    {
        var clock=new Clock();var store=new WebTicketStore(clock);var ticket=Ticket();var key=await store.StoreAsync(ticket);
        for(var i=0;i<71;i++){clock.Now+=TimeSpan.FromMinutes(20);await store.RenewAsync(key,ticket);Assert.NotNull(await store.RetrieveAsync(key));}
        clock.Now+=TimeSpan.FromMinutes(20);await store.RenewAsync(key,ticket);Assert.Null(await store.RetrieveAsync(key));
    }
}

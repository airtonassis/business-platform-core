using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Business.Platform.Core.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

namespace Business.Platform.Core.Api.Web;

public sealed class PostgresWebTicketStore(PostgresSessionStore database,IDataProtectionProvider protection,string clientId="local",bool requireCorrelation=false):ITicketStore
{
    private static AuthenticationTicket Copy(AuthenticationTicket ticket)=>TicketSerializer.Default.Deserialize(TicketSerializer.Default.Serialize(ticket))!;
    private static byte[] Identity(AuthenticationTicket ticket)
    {
        var issuer=ticket.Principal.FindAll("iss").ToArray();var subject=ticket.Principal.FindAll("sub").ToArray();
        if(issuer.Length!=1 || subject.Length!=1 || string.IsNullOrWhiteSpace(issuer[0].Value) || string.IsNullOrWhiteSpace(subject[0].Value))
            throw new InvalidOperationException("Session identity unavailable.");
        return SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new[]{issuer[0].Value,subject[0].Value}));
    }
    private IDataProtector Protector(string key)=>protection.CreateProtector("bpc.web.ticket.v1",key);
    private static bool ValidKey(string key)=>key.Length==64 && key.All(c=>c is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static bool Correlated(AuthenticationTicket ticket)=>ticket.Principal.FindAll("iat").Count()==1 && long.TryParse(ticket.Principal.FindFirst("iat")?.Value,out _) &&
        ticket.Principal.FindAll("sid").Count()==1 && ticket.Principal.FindFirst("sid") is {} sid && !string.IsNullOrWhiteSpace(sid.Value) && sid.Value.Length<=512;
    private static void Stamp(AuthenticationTicket ticket,string key)
    {
        var identity=(ClaimsIdentity)ticket.Principal.Identity!;
        foreach(var claim in ticket.Principal.FindAll(WebTicketStore.SessionClaim).ToArray())
            foreach(var item in ticket.Principal.Identities) item.TryRemoveClaim(claim);
        identity.AddClaim(new(WebTicketStore.SessionClaim,key));
    }
    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var copy=Copy(ticket);var identity=Identity(copy);
        if(requireCorrelation && !Correlated(copy))throw new InvalidOperationException("Session correlation unavailable.");
        var old=copy.Principal.FindFirst(WebTicketStore.SessionClaim)?.Value;
        if(old is not null && !ValidKey(old))throw new InvalidOperationException("Session unavailable.");
        var key=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));Stamp(copy,key);
        var issuer=copy.Principal.FindFirst("iss")!.Value;var subject=copy.Principal.FindFirst("sub")!.Value;
        var sid=copy.Principal.FindFirst("sid")?.Value;
        // Callback validation supplies iat in the back-channel-enabled profile; local legacy fixtures lack it.
        var issued=long.TryParse(copy.Principal.FindFirst("iat")?.Value,out var iat)?iat:DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if(copy.Principal.FindFirst("iat") is null)((ClaimsIdentity)copy.Principal.Identity!).AddClaim(new("iat",issued.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        await database.StoreAsync(key,old,identity,Protector(key).Protect(TicketSerializer.Default.Serialize(copy)),
            LogoutCorrelation.Hash("sub",issuer,clientId,subject),sid is null?null:LogoutCorrelation.Hash("sid",issuer,clientId,sid),
            sid is null?null:LogoutCorrelation.Hash("pair",issuer,clientId,subject,sid),issued);
        return key;
    }
    public async Task RenewAsync(string key,AuthenticationTicket ticket)
    {
        if(!ValidKey(key))return;
        var copy=Copy(ticket);Stamp(copy,key);
        await database.RenewAsync(key,Identity(copy),Protector(key).Protect(TicketSerializer.Default.Serialize(copy)));
    }
    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        if(!ValidKey(key))return null;
        var entry=await database.RetrieveAsync(key);if(entry is null)return null;
        try {
            var ticket=TicketSerializer.Default.Deserialize(Protector(key).Unprotect(entry.Value.Ticket));
            if(ticket is null || ticket.Principal.FindFirst(WebTicketStore.SessionClaim)?.Value!=key)return null;
            if(requireCorrelation && !Correlated(ticket))return null;
            ticket.Properties.ExpiresUtc=entry.Value.Expires;return ticket;
        } catch(CryptographicException) {return null;}
    }
    public Task RemoveAsync(string key)=>ValidKey(key)?database.RemoveAsync(key):Task.CompletedTask;
}

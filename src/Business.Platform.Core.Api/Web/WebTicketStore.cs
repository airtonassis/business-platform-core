using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Business.Platform.Core.Api.Web;

// Single-process local adapter. Restart revokes all sessions; never a production distributed store.
public sealed class WebTicketStore(TimeProvider clock):ITicketStore
{
    public const string SessionClaim="bpc.web.session";
    private sealed record Entry(byte[] Ticket,DateTimeOffset Absolute,DateTimeOffset Idle,string Identity);
    private readonly Dictionary<string,Entry> entries=[];
    private readonly object gate=new();
    private static string Identity(AuthenticationTicket t)=>t.Principal.FindFirst("iss")?.Value+"\n"+t.Principal.FindFirst("sub")?.Value;
    private bool Active(Entry e)=>e.Absolute>clock.GetUtcNow() && e.Idle>clock.GetUtcNow();
    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        lock(gate) {
            var copy=TicketSerializer.Default.Deserialize(TicketSerializer.Default.Serialize(ticket))!;
            var old=copy.Principal.FindFirst(SessionClaim)?.Value;
            var absolute=clock.GetUtcNow().AddHours(24);
            if(old is not null) {
                if(!entries.Remove(old,out var prior) || !Active(prior) || prior.Identity!=Identity(copy)) throw new InvalidOperationException("Session unavailable.");
                absolute=prior.Absolute;
            }
            var key=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var identity=(ClaimsIdentity)copy.Principal.Identity!;
            foreach(var claim in identity.FindAll(SessionClaim).ToArray()) identity.RemoveClaim(claim);
            identity.AddClaim(new(SessionClaim,key));
            var idle=clock.GetUtcNow().AddMinutes(30);if(idle>absolute)idle=absolute;
            copy.Properties.ExpiresUtc=idle;
            // Opportunistic expiry pruning, not persistent data purge.
            foreach(var expired in entries.Where(x=>!Active(x.Value)).Select(x=>x.Key).ToArray())entries.Remove(expired);
            entries[key]=new(TicketSerializer.Default.Serialize(copy),absolute,idle,Identity(copy));
            return Task.FromResult(key);
        }
    }
    public Task RenewAsync(string key,AuthenticationTicket ticket)
    {
        lock(gate) {
            if(entries.TryGetValue(key,out var entry) && Active(entry) && entry.Identity==Identity(ticket)) {
                var idle=clock.GetUtcNow().AddMinutes(30);if(idle>entry.Absolute)idle=entry.Absolute;
                var copy=TicketSerializer.Default.Deserialize(TicketSerializer.Default.Serialize(ticket))!;copy.Properties.ExpiresUtc=idle;
                entries[key]=entry with{Ticket=TicketSerializer.Default.Serialize(copy),Idle=idle};
            }
            else entries.Remove(key);
            return Task.CompletedTask;
        }
    }
    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        lock(gate) {
            if(entries.TryGetValue(key,out var entry) && Active(entry))return Task.FromResult(TicketSerializer.Default.Deserialize(entry.Ticket));
            entries.Remove(key);return Task.FromResult<AuthenticationTicket?>(null);
        }
    }
    public Task RemoveAsync(string key) { lock(gate)entries.Remove(key);return Task.CompletedTask; }
}

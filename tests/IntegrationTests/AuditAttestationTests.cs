using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Tenancy;
using Xunit;

namespace Business.Platform.Core.IntegrationTests;

public sealed class AuditAttestationTests
{
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow()=>DateTimeOffset.FromUnixTimeSeconds(1800000000); }
    [Fact]
    public void Canonical_encoding_is_deterministic_and_ttl_is_exactly_30()
    {
        var key=RandomNumberGenerator.GetBytes(32);var issuer=new AuditAttestationIssuer("key-1",key,new Clock());
        var subject=new OidcSubject("https://issuer.example/a.b", "ç|.\"subject");var org=Guid.NewGuid();
        var first=issuer.Issue(subject,org,25,null).Value;var second=issuer.Issue(subject,org,25,null).Value;
        Assert.True(first.GetPayload()==second.GetPayload());Assert.True(CryptographicOperations.FixedTimeEquals(first.GetMac(),second.GetMac()));
        var fields=first.GetPayload().Split('.');Assert.Equal(10,fields.Length);Assert.Equal("~",fields[7]);
        Assert.Equal(30,long.Parse(fields[9])-long.Parse(fields[8]));
        Assert.True(CryptographicOperations.FixedTimeEquals(first.GetMac(),HMACSHA256.HashData(key,Encoding.UTF8.GetBytes(first.GetPayload()))));
    }
    [Theory][InlineData(null,32)][InlineData("bad.kid",32)][InlineData("key",0)][InlineData("key",31)]
    public void Invalid_key_configuration_fails_closed(string? kid,int size)
    {
        var issuer=new AuditAttestationIssuer(kid,RandomNumberGenerator.GetBytes(size),new Clock());
        Assert.Equal("Audit.AttestationUnavailable",issuer.Issue(new("https://issuer","subject"),Guid.NewGuid(),25,null).Error.Code);
    }
    [Fact]
    public void Capability_is_redacted_and_cursor_is_bound_exactly()
    {
        var issuer=new AuditAttestationIssuer("key",RandomNumberGenerator.GetBytes(32),new Clock());
        var subject=new OidcSubject("https://issuer","subject");var org=Guid.NewGuid();
        var cursor=new TenantAuditPosition(DateTimeOffset.FromUnixTimeSeconds(1700000000),Guid.NewGuid()).Encode();
        var cap=issuer.Issue(subject,org,100,cursor).Value;
        Assert.Equal(cursor,cap.GetPayload().Split('.')[7]);Assert.Equal("{}",JsonSerializer.Serialize(cap));
        Assert.True(!cap.ToString().Contains(cap.GetPayload()));Assert.True(!issuer.ToString().Contains(cap.GetPayload()));
        Assert.True(issuer.Issue(subject,org,25,"").IsFailure);
    }
}

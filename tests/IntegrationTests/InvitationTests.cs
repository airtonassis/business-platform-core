using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Business.Platform.Core.Application.Platform;
using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Business.Platform.Core.IntegrationTests;

public sealed class InvitationTests
{
    [Fact]
    public void Secret_request_diagnostics_are_redacted()
    {
        var secret=OwnerReferenceCodec.Generate();
        Assert.True(!new Business.Platform.Core.Api.InvitationEndpoints.CodeRequest(secret).ToString().Contains(secret));
        Assert.True(!new Business.Platform.Core.Api.InvitationEndpoints.IssueRequest(Guid.NewGuid(),secret).ToString().Contains(secret));
    }
    private static readonly Guid Org=Guid.NewGuid(),User=Guid.NewGuid(),Identity=Guid.NewGuid(),Account=Guid.NewGuid();
    private static InvitationDetails Details()=>new(Guid.NewGuid(),Org,User,"Member","Pending",1,"Synthetic",DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch.AddHours(24),null,null);
    private sealed class Auth:IAuthenticatedSubject
    {
        public bool Active=true;
        public OidcSubject? GetSubject()=>Active?new("https://synthetic.invalid","manager"):null;
    }
    private sealed class Reader:IEnterpriseContextReader
    {
        public string Role="Owner";public bool Active=true;public bool NeverLinked;public int Calls;
        public bool IsProvisioned=>true;
        public Task<Result<EnterpriseContextSnapshot>> ReadAsync(OidcSubject subject,CancellationToken token)
        {
            Calls++;return Task.FromResult(Result.Success(new EnterpriseContextSnapshot(Identity,
                NeverLinked?new(BindingHistoryState.NeverConsumed,null):new(BindingHistoryState.Consumed,Org),
                NeverLinked?[]:[new(Identity,Account,Org,Account,Org,Active,Active,Active,Role)])));
        }
    }
    private sealed class Store:IInvitationStore
    {
        public InvitationDetails Value=Details();public bool Replay;public int ManagerCalls,RecipientCalls,Mutations;public InvitationCommand? Last;public string? ConflictState,FailureCode;
        public Task<Result<InvitationOutcome>> ManageAsync(OidcSubject subject,InvitationCommand request,CancellationToken token)
        {
            ManagerCalls++;Last=request;
            if(FailureCode is not null)return Task.FromResult(Result.Failure<InvitationOutcome>(new(FailureCode,"Unavailable")));
            if(request.Action is "Cancel" or "Replace" && ConflictState is not null){Mutations++;Value=Value with{State=ConflictState};return Task.FromResult(Result.Failure<InvitationOutcome>(new("Invitation.Conflict","Unavailable")));}
            return Task.FromResult(Result.Success(new InvitationOutcome(Value,Replay)));
        }
        public Task<Result<IReadOnlyList<InvitationDetails>>> ListAsync(OidcSubject subject,InvitationCommand request,CancellationToken token)
        {
            ManagerCalls++;Last=request;return Task.FromResult(Result.Success<IReadOnlyList<InvitationDetails>>([Value,Value with{InvitationId=Guid.NewGuid()}]));
        }
        public Task<Result<InvitationOutcome>> RecipientAsync(OidcSubject subject,Guid id,byte[] hash,bool accept,long version,CancellationToken token)
        {
            RecipientCalls++;
            if(accept && ConflictState is not null){Mutations++;Value=Value with{State=ConflictState};return Task.FromResult(Result.Failure<InvitationOutcome>(new("Invitation.Conflict","Unavailable")));}
            if(accept)Value=Value with{State="Accepted",AccountId=Guid.NewGuid(),MembershipId=Guid.NewGuid(),Version=2};
            return Task.FromResult(Result.Success(new InvitationOutcome(Value,Replay)));
        }
    }
    [Theory]
    [InlineData("Owner",true)][InlineData("Admin",true)][InlineData("Member",false)][InlineData("PlatformOperator",false)][InlineData("",false)]
    public async Task Issue_authorization_comes_from_server_context(string role,bool allowed)
    {
        var auth=new Auth();var reader=new Reader{Role=role};var store=new Store();var useCase=new Invitations(new(auth,reader),auth,store);
        var result=await useCase.IssueAsync(Org,User,OwnerReferenceCodec.Generate(),Guid.NewGuid().ToString());
        Assert.Equal(allowed,result.IsSuccess);Assert.Equal(allowed?1:0,store.ManagerCalls);
        if(allowed){Assert.True(store.Last!.ReferenceHash!.Length==32 && store.Last.SecretHash!.Length==32);Assert.Equal("Issue",store.Last.Action);}
    }
    [Fact]
    public async Task Foreign_inactive_and_never_linked_actors_cannot_issue()
    {
        var auth=new Auth();var reader=new Reader();var store=new Store();var useCase=new Invitations(new(auth,reader),auth,store);
        Assert.True((await useCase.IssueAsync(Guid.NewGuid(),User,OwnerReferenceCodec.Generate(),Guid.NewGuid().ToString())).IsFailure);
        reader.Active=false;Assert.True((await useCase.IssueAsync(Org,User,OwnerReferenceCodec.Generate(),Guid.NewGuid().ToString())).IsFailure);
        reader.Active=true;reader.NeverLinked=true;Assert.True((await useCase.IssueAsync(Org,User,OwnerReferenceCodec.Generate(),Guid.NewGuid().ToString())).IsFailure);
        Assert.Equal(0,store.ManagerCalls);
    }
    [Fact]
    public async Task Missing_identity_reference_and_key_fail_before_store()
    {
        var auth=new Auth{Active=false};var store=new Store();var query=new Invitations(new(auth,new Reader()),auth,store);
        Assert.Equal("Authentication.Required",(await query.IssueAsync(Org,User,"invalid",null)).Error.Code);
        auth.Active=true;Assert.Equal("Recipient.Unavailable",(await query.IssueAsync(Org,User,"invalid",null)).Error.Code);
        Assert.Equal("Invitation.InvalidRequest",(await query.IssueAsync(Org,User,OwnerReferenceCodec.Generate(),null)).Error.Code);
        Assert.Equal(0,store.ManagerCalls);
    }
    [Fact]
    public async Task Replay_does_not_return_a_new_or_recovered_secret()
    {
        var auth=new Auth();var store=new Store();var query=new Invitations(new(auth,new Reader()),auth,store);
        var proof=OwnerReferenceCodec.Generate();var key=Guid.NewGuid().ToString();
        var first=await query.IssueAsync(Org,User,proof,key);Assert.True(first.Value.Code?.Length==43);
        Assert.True(!first.Value.ToString().Contains(first.Value.Code!));
        store.Replay=true;var replay=await query.IssueAsync(Org,User,proof,key);
        Assert.Null(replay.Value.Code);Assert.True(replay.Value.SecretUnavailable);Assert.True(replay.Value.Replayed);
    }
    [Fact]
    public async Task Version_preconditions_follow_authorization_and_bind_item()
    {
        var auth=new Auth();var reader=new Reader();var store=new Store();var query=new Invitations(new(auth,reader),auth,store);
        Assert.Equal("Invitation.VersionRequired",(await query.ChangeAsync(Org,store.Value.InvitationId,false,null)).Error.Code);
        foreach(var tag in new[]{"*","W/"+InvitationVersion.Encode(store.Value),InvitationVersion.Encode(store.Value with{InvitationId=Guid.NewGuid()}),InvitationVersion.Encode(store.Value)+","+InvitationVersion.Encode(store.Value)})
            Assert.Equal("Invitation.InvalidRequest",(await query.ChangeAsync(Org,store.Value.InvitationId,false,tag)).Error.Code);
        reader.Role="Member";Assert.Equal("Access.Forbidden",(await query.ChangeAsync(Org,store.Value.InvitationId,false,null)).Error.Code);
        reader.Role="Owner";Assert.True((await query.ChangeAsync(Org,store.Value.InvitationId,true,InvitationVersion.Encode(store.Value),Guid.NewGuid().ToString())).IsSuccess);
        Assert.Equal("Replace",store.Last!.Action);
    }
    [Fact]
    public async Task Recipient_does_not_require_a_membership_but_does_require_identity_and_code()
    {
        var auth=new Auth();var reader=new Reader{NeverLinked=true};var store=new Store();var query=new Invitations(new(auth,reader),auth,store);
        var code=OwnerReferenceCodec.Generate();Assert.True((await query.RecipientAsync(store.Value.InvitationId,code)).IsSuccess);Assert.Equal(0,reader.Calls);
        Assert.Equal("Invitation.VersionRequired",(await query.RecipientAsync(store.Value.InvitationId,code,true)).Error.Code);
        Assert.True((await query.RecipientAsync(store.Value.InvitationId,code,true,InvitationVersion.Encode(store.Value))).IsSuccess);
        Assert.True((await query.RecipientAsync(store.Value.InvitationId,code,true)).IsSuccess);
        var calls=store.RecipientCalls;
        Assert.Equal("Invitation.Unavailable",(await query.RecipientAsync(store.Value.InvitationId,"wrong")).Error.Code);
        auth.Active=false;Assert.Equal("Authentication.Required",(await query.RecipientAsync(store.Value.InvitationId,code)).Error.Code);Assert.Equal(calls,store.RecipientCalls);
    }
    [Theory]
    [InlineData(0)][InlineData(101)]
    public async Task Pagination_limits_fail_closed(int limit)
    {
        var auth=new Auth();var store=new Store();var query=new Invitations(new(auth,new Reader()),auth,store);
        Assert.True((await query.ListAsync(Org,limit)).IsFailure);Assert.Equal(0,store.ManagerCalls);
    }
    [Fact]
    public async Task Cursor_and_invitation_etag_are_canonical()
    {
        var auth=new Auth();var store=new Store();var query=new Invitations(new(auth,new Reader()),auth,store);
        var page=await query.ListAsync(Org,1);Assert.Single(page.Value.Items);Assert.NotNull(page.Value.NextCursor);
        Assert.True(TenantAuditPosition.TryDecode(page.Value.NextCursor,out _));
        Assert.True((await query.ListAsync(Org,25,"malformed")).IsFailure);
        Assert.True((await query.ListAsync(Org,state:"Rejected")).IsFailure);
        Assert.True((await query.ListAsync(Org,state:"Expired")).IsSuccess);Assert.Equal("Expired",store.Last!.StateFilter);
        Assert.True(InvitationVersion.TryDecode(InvitationVersion.Encode(store.Value),store.Value,out var version));Assert.Equal(1,version);
        Assert.False(InvitationVersion.TryDecode(MembershipVersion.Encode(new(store.Value.InvitationId,Account,Org,"Member","Active",true,1)),store.Value,out _));
    }
    private sealed class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()=>Request.Headers.ContainsKey("Test-Anonymous")?Task.FromResult(AuthenticateResult.NoResult()):Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new("iss","https://synthetic.invalid"),new("sub","manager"),new("role","Owner"),new("organization_id",Guid.NewGuid().ToString())],Scheme.Name)),Scheme.Name)));
    }
    private sealed class Host(Reader reader,Store store):WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{["Database:Host"]="localhost",["Database:Name"]="synthetic",["Database:Username"]="synthetic",["Database:Password"]="synthetic-only"}));
            builder.ConfigureServices(s=>{
                s.RemoveAll<IEnterpriseContextReader>();s.AddSingleton<IEnterpriseContextReader>(reader);s.RemoveAll<IInvitationStore>();s.AddSingleton<IInvitationStore>(store);
                s.AddAuthentication(o=>{o.DefaultAuthenticateScheme="test";o.DefaultChallengeScheme="test";}).AddScheme<AuthenticationSchemeOptions,Handler>("test",_=>{});
            });
        }
    }
    [Fact]
    public async Task Http_contract_exposes_secret_once_and_requires_authorization()
    {
        var reader=new Reader();var store=new Store();await using var host=new Host(reader,store);using var client=host.CreateClient();
        var path=$"/api/v1/organizations/{Org}/invitations";client.DefaultRequestHeaders.Add("Idempotency-Key",Guid.NewGuid().ToString());
        var body=new{recipientUserId=User,recipientReference=OwnerReferenceCodec.Generate(),role="Owner",organizationId=Guid.NewGuid()};
        var created=await client.PostAsJsonAsync(path,body);Assert.Equal(HttpStatusCode.Created,created.StatusCode);Assert.NotNull(created.Headers.ETag);
        var json=JsonDocument.Parse(await created.Content.ReadAsStringAsync());Assert.Equal("Member",json.RootElement.GetProperty("invitation").GetProperty("role").GetString());
        var code=json.RootElement.GetProperty("code").GetString()!;Assert.True(code.Length==43);Assert.True(!json.RootElement.GetProperty("url").GetString()!.Contains(code));
        store.Replay=true;var replay=await client.PostAsJsonAsync(path,body);Assert.Equal(HttpStatusCode.OK,replay.StatusCode);
        var replayJson=JsonDocument.Parse(await replay.Content.ReadAsStringAsync());Assert.Equal(JsonValueKind.Null,replayJson.RootElement.GetProperty("code").ValueKind);Assert.True(replayJson.RootElement.GetProperty("secretUnavailable").GetBoolean());
        var item=path+"/"+store.Value.InvitationId;var get=await client.GetAsync(item);Assert.Equal(HttpStatusCode.OK,get.StatusCode);
        var metadata=await get.Content.ReadAsStringAsync();Assert.True(!metadata.Contains(code));Assert.DoesNotContain("hash",metadata,StringComparison.OrdinalIgnoreCase);
        Assert.Equal((HttpStatusCode)428,(await client.PostAsync(item+"/cancel",null)).StatusCode);
        using var invalid=new HttpRequestMessage(HttpMethod.Post,item+"/cancel");invalid.Headers.TryAddWithoutValidation("If-Match","*");Assert.Equal(HttpStatusCode.BadRequest,(await client.SendAsync(invalid)).StatusCode);
        using var cancel=new HttpRequestMessage(HttpMethod.Post,item+"/cancel");cancel.Headers.TryAddWithoutValidation("If-Match",get.Headers.ETag!.ToString());Assert.Equal(HttpStatusCode.OK,(await client.SendAsync(cancel)).StatusCode);
        reader.Role="Member";Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(path)).StatusCode);
        reader.Role="Owner";Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(path.Replace(Org.ToString(),Guid.NewGuid().ToString()))).StatusCode);
        client.DefaultRequestHeaders.Add("Test-Anonymous","true");Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync($"/api/v1/me/invitations/{store.Value.InvitationId}/accept",new{code})).StatusCode);
    }
    [Fact]
    public async Task Http_recipient_preview_accept_and_replay_without_membership()
    {
        var reader=new Reader{NeverLinked=true};var store=new Store();await using var host=new Host(reader,store);using var client=host.CreateClient();
        var code=OwnerReferenceCodec.Generate();var path=$"/api/v1/me/invitations/{store.Value.InvitationId}";
        var preview=await client.PostAsJsonAsync(path+"/preview",new{code});Assert.Equal(HttpStatusCode.OK,preview.StatusCode);Assert.NotNull(preview.Headers.ETag);
        Assert.Equal((HttpStatusCode)428,(await client.PostAsJsonAsync(path+"/accept",new{code})).StatusCode);
        using var accept=new HttpRequestMessage(HttpMethod.Post,path+"/accept"){Content=JsonContent.Create(new{code})};accept.Headers.TryAddWithoutValidation("If-Match",preview.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.OK,(await client.SendAsync(accept)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync(path+"/accept",new{code})).StatusCode);Assert.Equal(0,reader.Calls);
    }
    [Fact]
    public async Task Http_reuses_existing_rate_limit_and_returns_retry_after_without_store_access()
    {
        var reader=new Reader{NeverLinked=true};var store=new Store();await using var host=new Host(reader,store);using var client=host.CreateClient();
        var code=OwnerReferenceCodec.Generate();var path=$"/api/v1/me/invitations/{store.Value.InvitationId}/preview";
        for(var i=0;i<10;i++)Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync(path,new{code})).StatusCode);
        var rejected=await client.PostAsJsonAsync(path,new{code});Assert.Equal((HttpStatusCode)429,rejected.StatusCode);Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.Equal(10,store.RecipientCalls);
    }
    [Theory]
    [InlineData("Expired","Invitation.Expired")][InlineData("Cancelled","Invitation.Cancelled")][InlineData("Replaced","Invitation.Replaced")]
    public async Task State_changed_after_preview_is_rechecked_without_retrying_accept(string state,string error)
    {
        var auth=new Auth();var store=new Store{ConflictState=state};var query=new Invitations(new(auth,new Reader()),auth,store);
        var result=await query.RecipientAsync(store.Value.InvitationId,OwnerReferenceCodec.Generate(),true,InvitationVersion.Encode(store.Value));
        Assert.Equal(error,result.Error.Code);Assert.Equal(1,store.Mutations);Assert.Equal(3,store.RecipientCalls);
    }
    [Fact]
    public async Task Manager_conflict_refreshes_authorized_state_without_retrying_cancel()
    {
        var auth=new Auth();var store=new Store{ConflictState="Accepted"};var query=new Invitations(new(auth,new Reader()),auth,store);
        var result=await query.ChangeAsync(Org,store.Value.InvitationId,false,InvitationVersion.Encode(store.Value));
        Assert.Equal("Invitation.AlreadyAccepted",result.Error.Code);Assert.Equal(1,store.Mutations);Assert.Equal(3,store.ManagerCalls);
    }
    [Fact]
    public async Task Cancellation_generates_its_own_nonempty_audit_operation_identifier()
    {
        var auth=new Auth();var store=new Store();var query=new Invitations(new(auth,new Reader()),auth,store);
        Assert.True((await query.ChangeAsync(Org,store.Value.InvitationId,false,InvitationVersion.Encode(store.Value))).IsSuccess);
        var first=store.Last!.Key;Assert.NotEqual(Guid.Empty,first);
        Assert.True((await query.ChangeAsync(Org,store.Value.InvitationId,false,InvitationVersion.Encode(store.Value))).IsSuccess);
        Assert.NotEqual(first,store.Last!.Key);Assert.NotEqual(Guid.Empty,store.Last.Key);
    }
    [Theory]
    [InlineData("Invitation.Unavailable")][InlineData("Recipient.Unavailable")]
    public async Task Issue_recipient_failure_does_not_disclose_identity_existence(string storedError)
    {
        var auth=new Auth();var store=new Store{FailureCode=storedError};var query=new Invitations(new(auth,new Reader()),auth,store);
        var result=await query.IssueAsync(Org,User,OwnerReferenceCodec.Generate(),Guid.NewGuid().ToString());
        Assert.Equal("Recipient.Unavailable",result.Error.Code);Assert.Equal(1,store.ManagerCalls);
    }
}

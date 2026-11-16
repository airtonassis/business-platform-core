using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Application.Platform;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Business.Platform.Core.Api.Pages.Tenant;

public sealed class IndexModel(ResolveEnterpriseContext resolve,ManageDemoRecords records,ListTenantMemberships members,
    ManageTenantMembership membership,Invitations invitations,PlatformOperations platform,IAuthenticatedSubject authentication):PageModel
{
    public EnterpriseContext? Context {get;private set;}
    public string State {get;private set;}="Unavailable";
    public string? Notice {get;private set;}
    public string? IssuedCode {get;private set;}
    public IssuedOwnerReference? Reference {get;private set;}
    public DemoRecordPage? Records {get;private set;}
    public TenantMembershipPage? Members {get;private set;}
    public InvitationPage? Invitations {get;private set;}
    public DemoRecord? Selected {get;private set;}
    public TenantMembership? SelectedMember {get;private set;}
    public InvitationDetails? SelectedInvitation {get;private set;}
    [BindProperty(SupportsGet=true)]public string? Section {get;set;}="records";
    [BindProperty(SupportsGet=true)]public int Offset {get;set;}
    [BindProperty(SupportsGet=true)]public int Limit {get;set;}=25;
    [BindProperty(SupportsGet=true)]public string? Cursor {get;set;}
    [BindProperty]public Guid Id {get;set;}
    [BindProperty]public string? Title {get;set;}
    [BindProperty]public string? Version {get;set;}
    [BindProperty]public Guid Key {get;set;}=Guid.NewGuid();
    [BindProperty]public Guid Recipient {get;set;}
    [BindProperty]public string? RecipientReference {get;set;}
    [BindProperty]public string? Code {get;set;}
    private async Task<bool> LoadContext()
    {
        var result=await resolve.ExecuteAsync(null,HttpContext.RequestAborted);
        if(result.IsFailure){Error(result.Error);return false;}
        State=result.Value.State.ToString();Context=result.Value.Context;
        if(Context is null && result.Value.State!=EnterpriseContextState.NotLinked)Response.StatusCode=403;
        return Context is not null;
    }
    private void Error(Error error)
    {
        Response.StatusCode=error.Code switch{
            "Access.Forbidden" or "Access.Unavailable"=>403,"Resource.NotFound" or "Invitation.Unavailable" or "Recipient.Unavailable"=>404,
            "DemoRecord.VersionRequired" or "Membership.VersionRequired" or "Invitation.VersionRequired"=>428,
            "DemoRecord.VersionConflict" or "Membership.VersionConflict" or "Invitation.VersionConflict"=>412,
            "Idempotency.PayloadMismatch" or "Idempotency.Expired" or "Invitation.Expired" or "Invitation.Cancelled" or "Invitation.Replaced" or "Invitation.Conflict" or "Invitation.AlreadyAccepted"=>409,
            "DemoRecord.InvalidInput" or "DemoRecord.InvalidPage" or "DemoRecord.InvalidVersion" or "Membership.InvalidPage" or "Membership.InvalidVersion" or "Invitation.InvalidRequest"=>400,
            "Authentication.Required"=>401,_=>503};
        var message=Response.StatusCode switch{
            400=>"Confira os campos e os limites informados.",401=>"Sua sessão expirou. Entre novamente.",403=>"Esta ação não está disponível para seu acesso.",404=>"O recurso não está disponível para esta conta.",
            409=>"A operação está em conflito ou expirou. Confira o estado atual antes de continuar.",412=>"Outra alteração foi gravada. Recarregue o registro e revise seu título; nada foi sobrescrito.",428=>"Recarregue o item para obter sua versão antes de alterar.",_=>"Serviço indisponível. O resultado pode ser desconhecido; não repita a operação com uma nova chave."};
        ModelState.AddModelError("",message);
    }
    public async Task OnGetAsync(Guid? item=null)
    {
        Section??="records";
        if(!await LoadContext())return;
        if(Section=="records") {
            var result=await records.ListAsync(Context!.OrganizationId,Limit,Offset,HttpContext.RequestAborted);
            if(result.IsFailure)Error(result.Error);else Records=result.Value;
            if(item.HasValue){var selected=await records.ReadAsync(Context.OrganizationId,item.Value,HttpContext.RequestAborted);if(selected.IsFailure)Error(selected.Error);else{Selected=selected.Value;Id=Selected.Id;Title=Selected.Title;Version=DemoRecordVersion.Encode(Selected);}}
        } else if(Section=="members") {
            var result=await members.ExecuteAsync(Context!.OrganizationId,Limit,Offset,HttpContext.RequestAborted);if(result.IsFailure)Error(result.Error);else Members=result.Value;
            if(item.HasValue){var selected=await membership.ExecuteAsync(Context.OrganizationId,item.Value,MembershipAction.Read,token:HttpContext.RequestAborted);if(selected.IsFailure)Error(selected.Error);else SelectedMember=selected.Value;}
        } else if(Section=="invitations") {
            var result=await invitations.ListAsync(Context!.OrganizationId,Limit,Cursor,token:HttpContext.RequestAborted);if(result.IsFailure)Error(result.Error);else Invitations=result.Value;
            if(item.HasValue){var selected=await invitations.GetAsync(Context.OrganizationId,item.Value,HttpContext.RequestAborted);if(selected.IsFailure)Error(selected.Error);else SelectedInvitation=selected.Value.Invitation;}
        } else if(Section!="recipient") {Response.StatusCode=404;ModelState.AddModelError("","Página indisponível.");}
    }
    public async Task<IActionResult> OnPostCreateAsync()
    {
        if(await LoadContext()) {var result=await records.CreateAsync(Context!.OrganizationId,Title,Key,HttpContext.RequestAborted);if(result.IsFailure)Error(result.Error);else return RedirectToPage(new{section="records",item=result.Value.Item.Id});}
        return Page();
    }
    public async Task<IActionResult> OnPostUpdateAsync()
    {
        if(await LoadContext()) {var result=await records.UpdateAsync(Context!.OrganizationId,Id,Title,Version,HttpContext.RequestAborted);if(result.IsFailure)Error(result.Error);else return RedirectToPage(new{section="records",item=result.Value.Id});}
        return Page();
    }
    public async Task<IActionResult> OnPostMembershipAsync(MembershipAction action)
    {
        Section="members";
        if(await LoadContext()) {var result=await membership.ExecuteAsync(Context!.OrganizationId,Id,action,Version,HttpContext.RequestAborted);if(result.IsFailure)Error(result.Error);else return RedirectToPage(new{section="members",item=Id});}
        return Page();
    }
    public async Task<IActionResult> OnPostIssueAsync()
    {
        Section="invitations";
        if(await LoadContext()) {var result=await invitations.IssueAsync(Context!.OrganizationId,Recipient,RecipientReference??"",Key.ToString("D"),HttpContext.RequestAborted);if(result.IsFailure)Error(result.Error);else{IssuedCode=result.Value.Code;SelectedInvitation=result.Value.Invitation;Notice=result.Value.SecretUnavailable?"Convite já emitido. Código indisponível no replay.":"Convite emitido. Compartilhe o ID e o código por canal controlado.";}}
        RecipientReference=null;ModelState.Remove(nameof(RecipientReference));return Page();
    }
    public async Task<IActionResult> OnPostChangeInvitationAsync(bool replace)
    {
        Section="invitations";
        if(await LoadContext()){var result=await invitations.ChangeAsync(Context!.OrganizationId,Id,replace,Version,Key.ToString("D"),HttpContext.RequestAborted);if(result.IsFailure)Error(result.Error);else{IssuedCode=result.Value.Code;SelectedInvitation=result.Value.Invitation;Notice="Estado do convite atualizado.";}}
        return Page();
    }
    public async Task<IActionResult> OnPostRecipientAsync(bool accept)
    {
        Section="recipient";
        await LoadContext();
        var result=await invitations.RecipientAsync(Id,Code??"",accept,Version,HttpContext.RequestAborted);
        Code=null;ModelState.Remove(nameof(Code));
        if(result.IsFailure)Error(result.Error);else{SelectedInvitation=result.Value.Invitation;Id=SelectedInvitation.InvitationId;Version=InvitationVersion.Encode(SelectedInvitation);Notice=accept?"Convite aceito. Acesse o início para revalidar o contexto.":"Prévia conferida. Aceitar exige confirmação e reapresentação do código.";}
        return Page();
    }
    public async Task<IActionResult> OnPostReferenceAsync()
    {
        Section="recipient";
        await LoadContext();var actor=authentication.GetSubject();
        if(actor is null){Response.StatusCode=401;return Page();}
        var result=await platform.IssueAsync(actor,User.FindFirst("name")?.Value??"Recipient",HttpContext.RequestAborted);
        if(result.IsFailure)Error(result.Error);else Reference=result.Value;
        return Page();
    }
}

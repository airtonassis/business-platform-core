namespace Business.Platform.Core.Domain.Shared;

/// <summary>Natureza do ator responsável por uma operação auditada.</summary>
public enum AuditActorKind
{
    /// <summary>Usuário (pessoa ou conta de usuário).</summary>
    User = 1,

    /// <summary>Processo de sistema (job, integração, migração).</summary>
    System = 2
}

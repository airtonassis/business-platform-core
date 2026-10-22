namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Representa, de forma imutável e com igualdade estrutural, os metadados de auditoria de criação e da última atualização.
/// </summary>
/// <remarks>
/// Não acessa o relógio do sistema, não resolve identidade, não gera eventos de domínio, não mantém histórico
/// e não oferece controle de concorrência. Os instantes devem ser informados em UTC (offset zero).
/// </remarks>
public sealed class AuditMetadata : IEquatable<AuditMetadata>
{
    private AuditMetadata(
        AuditActor createdBy,
        DateTimeOffset createdAt,
        AuditActor? updatedBy,
        DateTimeOffset? updatedAt)
    {
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        UpdatedBy = updatedBy;
        UpdatedAt = updatedAt;
    }

    /// <summary>Ator que criou o registro.</summary>
    public AuditActor CreatedBy { get; }

    /// <summary>Instante UTC de criação.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Ator da última atualização, ou nulo se nunca atualizado.</summary>
    public AuditActor? UpdatedBy { get; }

    /// <summary>Instante UTC da última atualização, ou nulo se nunca atualizado.</summary>
    public DateTimeOffset? UpdatedAt { get; }

    /// <summary>
    /// Cria os metadados de criação. Retorna somente o primeiro erro, na ordem: instante não definido, offset não UTC.
    /// </summary>
    /// <param name="createdBy">Ator da criação.</param>
    /// <param name="createdAt">Instante UTC da criação.</param>
    /// <returns>Sucesso com os metadados ou falha de validação.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="createdBy"/> é nulo.</exception>
    public static Result<AuditMetadata> Create(AuditActor createdBy, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(createdBy);

        if (createdAt == default)
        {
            return new Error(
                "Audit.CreatedAt.NotSet",
                "O instante de criação não foi definido.",
                ErrorType.Validation);
        }

        if (createdAt.Offset != TimeSpan.Zero)
        {
            return new Error(
                "Audit.CreatedAt.NonUtcOffset",
                "O instante de criação deve estar em UTC (offset zero).",
                ErrorType.Validation);
        }

        return new AuditMetadata(createdBy, createdAt, null, null);
    }

    /// <summary>
    /// Retorna uma nova instância com os dados de atualização, preservando os de criação. Retorna somente o primeiro erro,
    /// na ordem: instante não definido, offset não UTC, anterior à criação, anterior à última atualização.
    /// </summary>
    /// <param name="updatedBy">Ator da atualização.</param>
    /// <param name="updatedAt">Instante UTC da atualização.</param>
    /// <returns>Sucesso com os novos metadados ou falha de validação.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="updatedBy"/> é nulo.</exception>
    public Result<AuditMetadata> WithUpdate(AuditActor updatedBy, DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(updatedBy);

        if (updatedAt == default)
        {
            return new Error(
                "Audit.UpdatedAt.NotSet",
                "O instante de atualização não foi definido.",
                ErrorType.Validation);
        }

        if (updatedAt.Offset != TimeSpan.Zero)
        {
            return new Error(
                "Audit.UpdatedAt.NonUtcOffset",
                "O instante de atualização deve estar em UTC (offset zero).",
                ErrorType.Validation);
        }

        if (updatedAt < CreatedAt)
        {
            return new Error(
                "Audit.UpdatedAt.BeforeCreation",
                "O instante de atualização não pode ser anterior ao de criação.",
                ErrorType.Validation);
        }

        if (UpdatedAt is { } lastUpdate && updatedAt < lastUpdate)
        {
            return new Error(
                "Audit.UpdatedAt.BeforeLastUpdate",
                "O instante de atualização não pode ser anterior ao da última atualização.",
                ErrorType.Validation);
        }

        return new AuditMetadata(CreatedBy, CreatedAt, updatedBy, updatedAt);
    }

    /// <summary>Indica se dois metadados são iguais estruturalmente.</summary>
    public static bool operator ==(AuditMetadata? left, AuditMetadata? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Indica se dois metadados são diferentes estruturalmente.</summary>
    public static bool operator !=(AuditMetadata? left, AuditMetadata? right) => !(left == right);

    /// <inheritdoc />
    public bool Equals(AuditMetadata? other) =>
        other is not null
        && CreatedBy.Equals(other.CreatedBy)
        && CreatedAt == other.CreatedAt
        && Equals(UpdatedBy, other.UpdatedBy)
        && UpdatedAt == other.UpdatedAt;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is AuditMetadata other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(CreatedBy, CreatedAt, UpdatedBy, UpdatedAt);
}

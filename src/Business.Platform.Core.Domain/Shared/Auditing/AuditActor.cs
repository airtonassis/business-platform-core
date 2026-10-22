namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Representa, de forma imutável e com igualdade por valor, o ator responsável por uma operação auditada.
/// </summary>
/// <remarks>
/// O identificador é preservado sem normalização e comparado de forma ordinal (case-sensitive).
/// Não resolve identidade nem autenticação.
/// </remarks>
public sealed class AuditActor : IEquatable<AuditActor>
{
    /// <summary>Tamanho máximo do identificador, em unidades UTF-16.</summary>
    public const int MaxIdLength = 256;

    private AuditActor(AuditActorKind kind, string id)
    {
        Kind = kind;
        Id = id;
    }

    /// <summary>Natureza do ator.</summary>
    public AuditActorKind Kind { get; }

    /// <summary>Identificador do ator, exatamente como recebido.</summary>
    public string Id { get; }

    /// <summary>
    /// Cria um ator validado. Retorna somente o primeiro erro, na ordem: tipo indefinido, identificador obrigatório,
    /// espaços nas extremidades, tamanho excedido, caractere de controle.
    /// </summary>
    /// <param name="kind">Natureza do ator (<see cref="AuditActorKind.User"/> ou <see cref="AuditActorKind.System"/>).</param>
    /// <param name="id">Identificador do ator.</param>
    /// <returns>Sucesso com o ator ou falha de validação.</returns>
    public static Result<AuditActor> Create(AuditActorKind kind, string? id)
    {
        if (kind is not (AuditActorKind.User or AuditActorKind.System))
        {
            return new Error(
                "Audit.Actor.KindUndefined",
                "O tipo do ator de auditoria é indefinido.",
                ErrorType.Validation);
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return new Error(
                "Audit.Actor.IdRequired",
                "O identificador do ator de auditoria é obrigatório.",
                ErrorType.Validation);
        }

        if (char.IsWhiteSpace(id[0]) || char.IsWhiteSpace(id[^1]))
        {
            return new Error(
                "Audit.Actor.IdSurroundingWhitespace",
                "O identificador do ator de auditoria não pode conter espaços no início ou no final.",
                ErrorType.Validation);
        }

        if (id.Length > MaxIdLength)
        {
            return new Error(
                "Audit.Actor.IdTooLong",
                "O identificador do ator de auditoria excede o máximo permitido de 256 caracteres.",
                ErrorType.Validation);
        }

        foreach (var character in id)
        {
            if (char.IsControl(character))
            {
                return new Error(
                    "Audit.Actor.IdInvalidCharacter",
                    "O identificador do ator de auditoria não pode conter caracteres de controle.",
                    ErrorType.Validation);
            }
        }

        return new AuditActor(kind, id);
    }

    /// <summary>Indica se dois atores são iguais por valor.</summary>
    public static bool operator ==(AuditActor? left, AuditActor? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Indica se dois atores são diferentes por valor.</summary>
    public static bool operator !=(AuditActor? left, AuditActor? right) => !(left == right);

    /// <inheritdoc />
    public bool Equals(AuditActor? other) =>
        other is not null && Kind == other.Kind && string.Equals(Id, other.Id, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is AuditActor other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Kind, Id);
}

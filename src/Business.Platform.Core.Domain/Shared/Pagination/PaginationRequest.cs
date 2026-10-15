namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Representa, de forma imutável e com igualdade por valor, uma solicitação de página
/// (numeração baseada em 1) com tamanho validado.
/// </summary>
/// <remarks>
/// Não executa consultas, não aplica ordenação e não realiza clamp automático de valores.
/// A futura camada de consulta deve garantir ordenação determinística com desempate único.
/// </remarks>
public sealed class PaginationRequest : IEquatable<PaginationRequest>
{
    /// <summary>Tamanho de página padrão.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Tamanho máximo de página permitido.</summary>
    public const int MaxPageSize = 100;

    private PaginationRequest(int pageNumber, int pageSize)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
        Offset = (long)(pageNumber - 1) * pageSize;
    }

    /// <summary>Solicitação padrão, equivalente a <c>Create(1, 20)</c>.</summary>
    public static PaginationRequest Default { get; } = new(1, DefaultPageSize);

    /// <summary>Número da página, a partir de 1.</summary>
    public int PageNumber { get; }

    /// <summary>Tamanho da página, entre 1 e <see cref="MaxPageSize"/>.</summary>
    public int PageSize { get; }

    /// <summary>Quantidade de itens a ignorar: <c>(PageNumber - 1) * PageSize</c>, calculada em <see cref="long"/>.</summary>
    public long Offset { get; }

    /// <summary>
    /// Cria uma solicitação de paginação validada. Retorna somente o primeiro erro, na ordem:
    /// número da página, tamanho mínimo, tamanho máximo.
    /// </summary>
    /// <param name="pageNumber">Número da página, a partir de 1.</param>
    /// <param name="pageSize">Tamanho da página, de 1 a <see cref="MaxPageSize"/>.</param>
    /// <returns>Sucesso com a solicitação ou falha de validação.</returns>
    public static Result<PaginationRequest> Create(int pageNumber, int pageSize = DefaultPageSize)
    {
        if (pageNumber < 1)
        {
            return new Error(
                "Pagination.PageNumber.LessThanOne",
                "O número da página deve ser maior ou igual a 1.",
                ErrorType.Validation);
        }

        if (pageSize < 1)
        {
            return new Error(
                "Pagination.PageSize.LessThanOne",
                "O tamanho da página deve ser maior ou igual a 1.",
                ErrorType.Validation);
        }

        if (pageSize > MaxPageSize)
        {
            return new Error(
                "Pagination.PageSize.ExceedsMaximum",
                "O tamanho da página excede o máximo permitido de 100.",
                ErrorType.Validation);
        }

        return new PaginationRequest(pageNumber, pageSize);
    }

    /// <summary>Indica se duas solicitações são iguais por valor.</summary>
    public static bool operator ==(PaginationRequest? left, PaginationRequest? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Indica se duas solicitações são diferentes por valor.</summary>
    public static bool operator !=(PaginationRequest? left, PaginationRequest? right) => !(left == right);

    /// <inheritdoc />
    public bool Equals(PaginationRequest? other) =>
        other is not null && PageNumber == other.PageNumber && PageSize == other.PageSize;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PaginationRequest other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(PageNumber, PageSize);
}

namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Contrato de consulta paginada de raízes de agregado por especificação, independente de infraestrutura.
/// </summary>
/// <typeparam name="TAggregate">Tipo da raiz de agregado.</typeparam>
/// <typeparam name="TId">Tipo do identificador da raiz de agregado.</typeparam>
/// <remarks>
/// Contrato aditivo: não herda <see cref="IRepository{TAggregate, TId}"/>. Não há consulta sem paginação.
/// Falhas esperadas são comunicadas por <see cref="Result"/>; cancelamento por <see cref="OperationCanceledException"/>.
/// A contagem total é best-effort. A tradução, o limite operacional de deslocamento (<c>MaxOffset</c>) e a
/// classificação de falhas pertencem à Infrastructure (ADR-0004 e ADR-0006).
/// </remarks>
public interface ISpecificationRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    /// <summary>
    /// Recupera uma página de agregados que satisfazem a especificação, na ordem total informada.
    /// </summary>
    /// <param name="specification">Critério de seleção.</param>
    /// <param name="sort">Ordenação, cuja ordem total é construída pelo Domain.</param>
    /// <param name="pagination">Solicitação de página.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Sucesso com a página, ou falha (por exemplo, <see cref="ErrorType.Validation"/> para deslocamento acima do limite).</returns>
    Task<Result<PagedResult<TAggregate>>> FindPagedAsync(
        ISpecification<TAggregate> specification,
        SortSpecification<TAggregate, TId> sort,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default);
}

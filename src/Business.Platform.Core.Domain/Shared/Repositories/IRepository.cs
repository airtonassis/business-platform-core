namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Contrato de persistência de raízes de agregado, independente de infraestrutura.
/// </summary>
/// <typeparam name="TAggregate">Tipo da raiz de agregado.</typeparam>
/// <typeparam name="TId">Tipo do identificador da raiz de agregado.</typeparam>
/// <remarks>
/// Falhas esperadas são comunicadas por <see cref="Result"/>; cancelamento por <see cref="OperationCanceledException"/>.
/// Mecanismo de persistência, transações e commit pertencem às implementações. A política funcional de exclusão
/// (física ou lógica) pertence ao domínio/módulo; a implementação apenas a executa tecnicamente.
/// </remarks>
public interface IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    /// <summary>
    /// Recupera um agregado pelo identificador.
    /// </summary>
    /// <param name="id">Identificador do agregado.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Sucesso com o agregado, ou falha (por exemplo, <see cref="ErrorType.NotFound"/>).</returns>
    Task<Result<TAggregate>> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Solicita a inclusão de um agregado novo.
    /// </summary>
    /// <param name="aggregate">Agregado a incluir.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Sucesso ou falha da operação.</returns>
    Task<Result> AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Solicita a atualização de um agregado existente.
    /// </summary>
    /// <param name="aggregate">Agregado a atualizar.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Sucesso ou falha da operação.</returns>
    Task<Result> UpdateAsync(TAggregate aggregate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Solicita a remoção de um agregado.
    /// </summary>
    /// <param name="aggregate">Agregado a remover.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Sucesso ou falha da operação.</returns>
    Task<Result> RemoveAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
}

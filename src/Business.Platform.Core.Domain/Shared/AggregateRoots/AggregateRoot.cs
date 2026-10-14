namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Classe base de raízes de agregado. Herda identidade, igualdade e hash de <see cref="Entity{TId}"/>.
/// </summary>
/// <typeparam name="TId">Tipo do identificador da raiz do agregado.</typeparam>
/// <remarks>
/// Não adiciona membros nem comportamento. Regras de agregados concretos, como invariantes,
/// encapsulamento de entidades filhas e multitenancy, pertencem às subclasses.
/// </remarks>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    /// <summary>
    /// Inicializa a raiz do agregado com seu identificador.
    /// </summary>
    /// <param name="id">Identificador da raiz do agregado.</param>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> é <see langword="null"/>.</exception>
    protected AggregateRoot(TId id) : base(id)
    {
    }
}

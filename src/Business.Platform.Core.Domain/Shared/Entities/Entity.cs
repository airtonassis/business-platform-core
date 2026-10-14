namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Classe base de entidades de domínio, definida por sua identidade e não por seus atributos.
/// </summary>
/// <typeparam name="TId">
/// Tipo do identificador. Deve preservar semântica estável de igualdade e hash durante a vida da entidade.
/// </typeparam>
/// <remarks>
/// Duas entidades são iguais somente se possuem o mesmo tipo concreto de runtime e identificadores iguais
/// segundo <see cref="EqualityComparer{T}.Default"/>. Atributos e relacionamentos não participam da igualdade.
/// A validade semântica do identificador (por exemplo, <see cref="Guid.Empty"/>, zero ou string vazia)
/// pertence ao tipo de identidade ou ao domínio. A propriedade <see cref="Id"/> não pode ser substituída,
/// mas a imutabilidade profunda de identificadores de referência não é garantida.
/// </remarks>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    /// <summary>
    /// Inicializa a entidade com seu identificador.
    /// </summary>
    /// <param name="id">Identificador da entidade.</param>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> é <see langword="null"/>.</exception>
    protected Entity(TId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        Id = id;
    }

    /// <summary>Identificador da entidade, definido na construção e sem possibilidade de substituição.</summary>
    public TId Id { get; }

    /// <summary>Indica se duas entidades são iguais (mesmo tipo concreto e mesmo identificador).</summary>
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Indica se duas entidades são diferentes.</summary>
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);

    /// <inheritdoc />
    public bool Equals(Entity<TId>? other) =>
        other is not null
        && GetType() == other.GetType()
        && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    /// <inheritdoc />
    public sealed override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    /// <inheritdoc />
    public sealed override int GetHashCode() =>
        HashCode.Combine(GetType(), EqualityComparer<TId>.Default.GetHashCode(Id));
}

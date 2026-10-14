namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Classe base de objetos de valor, definidos por seus componentes de igualdade e não por identidade.
/// </summary>
/// <remarks>
/// Dois objetos de valor são iguais somente se possuem o mesmo tipo concreto de runtime e se os componentes
/// produzidos por <see cref="GetEqualityComponents"/> são iguais, um a um e na mesma ordem, segundo
/// <see cref="EqualityComparer{T}.Default"/> aplicado a <see cref="object"/>. Componentes <see langword="null"/>
/// são aceitos. A Foundation não trata automaticamente um <see cref="System.Collections.IEnumerable"/> como
/// coleção estrutural: a representação estrutural (ordem, normalização, delimitadores) pertence ao objeto de valor
/// concreto. A Foundation também não garante imutabilidade profunda nem estabilidade do hash entre processos,
/// e o hash não é armazenado em cache: componentes mutáveis alterados após o uso em coleções hash quebram o contrato.
/// </remarks>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>Indica se dois objetos de valor são iguais (mesmo tipo concreto e mesmos componentes).</summary>
    public static bool operator ==(ValueObject? left, ValueObject? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Indica se dois objetos de valor são diferentes.</summary>
    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);

    /// <inheritdoc />
    public bool Equals(ValueObject? other)
    {
        if (other is null || GetType() != other.GetType())
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        using var left = GetEqualityComponents().GetEnumerator();
        using var right = other.GetEqualityComponents().GetEnumerator();

        while (true)
        {
            var hasLeft = left.MoveNext();

            if (hasLeft != right.MoveNext())
            {
                return false;
            }

            if (!hasLeft)
            {
                return true;
            }

            if (!EqualityComparer<object?>.Default.Equals(left.Current, right.Current))
            {
                return false;
            }
        }
    }

    /// <inheritdoc />
    public sealed override bool Equals(object? obj) => Equals(obj as ValueObject);

    /// <inheritdoc />
    public sealed override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(GetType());

        foreach (var component in GetEqualityComponents())
        {
            hash.Add(component);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Produz, em ordem determinística, os componentes que definem a igualdade do objeto de valor.
    /// </summary>
    /// <returns>Sequência de componentes, que podem ser <see langword="null"/>.</returns>
    protected abstract IEnumerable<object?> GetEqualityComponents();
}

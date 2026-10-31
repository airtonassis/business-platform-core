using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Ordenação imutável de um agregado, com <see cref="TotalOrder"/> sempre total pelo identificador.
/// </summary>
/// <typeparam name="TAggregate">Tipo da raiz de agregado.</typeparam>
/// <typeparam name="TId">Tipo do identificador da raiz de agregado.</typeparam>
/// <remarks>
/// Somente o Domain constrói a ordem total: <see cref="Create"/> é o único ponto de construção, o construtor é
/// privado e Application/Infrastructure apenas recebem o resultado. Não exige <c>IComparable&lt;TId&gt;</c>: a
/// comparação do identificador é do armazenamento, sobre a representação persistida. Quando o identificador não
/// é informado, é anexado em ordem ascendente; quando é informado, deve ser a última chave solicitada.
/// A igualdade é estrutural sobre <see cref="TotalOrder"/>.
/// </remarks>
public sealed class SortSpecification<TAggregate, TId> : IEquatable<SortSpecification<TAggregate, TId>>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    /// <summary>Quantidade máxima de chaves solicitadas, contando o identificador explícito.</summary>
    public const int MaxRequestedKeys = 4;

    /// <summary>Quantidade máxima de chaves da ordem total.</summary>
    public const int MaxTotalOrderKeys = MaxRequestedKeys + 1;

    private const string IdentifierName = nameof(Entity<TId>.Id);

    private SortSpecification(SortKey<TAggregate>[] requested, SortKey<TAggregate>[] total)
    {
        RequestedKeys = new ReadOnlyCollection<SortKey<TAggregate>>(requested);
        TotalOrder = new ReadOnlyCollection<SortKey<TAggregate>>(total);
    }

    /// <summary>Chaves solicitadas, na ordem recebida.</summary>
    public IReadOnlyList<SortKey<TAggregate>> RequestedKeys { get; }

    /// <summary>Ordem total: chaves solicitadas seguidas, se necessário, do identificador ascendente.</summary>
    public IReadOnlyList<SortKey<TAggregate>> TotalOrder { get; }

    /// <summary>
    /// Cria a ordenação validando as chaves e construindo a ordem total. Retorna somente o primeiro erro, na ordem:
    /// vazia, excesso de chaves, chave nula, chave duplicada, identificador fora da última posição.
    /// </summary>
    /// <param name="keys">Chaves solicitadas (de uma a quatro).</param>
    /// <returns>Sucesso com a ordenação ou falha de validação com código de <see cref="SortingErrorCodes"/>.</returns>
    public static Result<SortSpecification<TAggregate, TId>> Create(IEnumerable<SortKey<TAggregate>?>? keys)
    {
        var requested = new List<SortKey<TAggregate>?>();

        if (keys is not null)
        {
            foreach (var key in keys)
            {
                requested.Add(key);

                if (requested.Count > MaxRequestedKeys)
                {
                    break;
                }
            }
        }

        if (requested.Count == 0)
        {
            return Failure(SortingErrorCodes.KeysEmpty, "Ao menos uma chave de ordenação deve ser informada.");
        }

        if (requested.Count > MaxRequestedKeys)
        {
            return Failure(SortingErrorCodes.KeysExceedMaximum, "O máximo de chaves de ordenação solicitadas é 4.");
        }

        if (requested.Contains(null))
        {
            return Failure(SortingErrorCodes.KeyNull, "As chaves de ordenação não podem ser nulas.");
        }

        var validated = new SortKey<TAggregate>[requested.Count];
        var members = new HashSet<(Type, string)>();

        for (var index = 0; index < validated.Length; index++)
        {
            var key = requested[index]!;

            if (!members.Add((key.DeclaringType, key.MemberName)))
            {
                return Failure(SortingErrorCodes.KeyDuplicated, "A mesma propriedade não pode ser informada mais de uma vez.");
            }

            validated[index] = key;
        }

        var identifierIndex = Array.FindIndex(validated, IsIdentifier);

        if (identifierIndex >= 0 && identifierIndex != validated.Length - 1)
        {
            return Failure(SortingErrorCodes.IdentifierNotLast, "O identificador, quando informado, deve ser a última chave.");
        }

        var total = identifierIndex >= 0 ? validated : [.. validated, CreateIdentifierKey()];

        return new SortSpecification<TAggregate, TId>(validated, total);
    }

    /// <summary>Indica se duas ordenações são iguais por valor.</summary>
    public static bool operator ==(SortSpecification<TAggregate, TId>? left, SortSpecification<TAggregate, TId>? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Indica se duas ordenações são diferentes por valor.</summary>
    public static bool operator !=(SortSpecification<TAggregate, TId>? left, SortSpecification<TAggregate, TId>? right) =>
        !(left == right);

    /// <inheritdoc />
    public bool Equals(SortSpecification<TAggregate, TId>? other) =>
        other is not null && TotalOrderEquals(other);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SortSpecification<TAggregate, TId> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (var key in TotalOrder)
        {
            hash.Add(key);
        }

        return hash.ToHashCode();
    }

    private bool TotalOrderEquals(SortSpecification<TAggregate, TId> other)
    {
        if (TotalOrder.Count != other.TotalOrder.Count)
        {
            return false;
        }

        for (var index = 0; index < TotalOrder.Count; index++)
        {
            if (!TotalOrder[index].Equals(other.TotalOrder[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsIdentifier(SortKey<TAggregate> key) =>
        key.DeclaringType == typeof(Entity<TId>)
        && string.Equals(key.MemberName, IdentifierName, StringComparison.Ordinal);

    private static SortKey<TAggregate> CreateIdentifierKey()
    {
        var property = typeof(Entity<TId>).GetProperty(IdentifierName)!;
        var parameter = Expression.Parameter(typeof(TAggregate), "a");
        var selector = Expression.Lambda<Func<TAggregate, TId>>(Expression.Property(parameter, property), parameter);

        return SortKey<TAggregate>.FromValidated(selector, property, SortDirection.Ascending);
    }

    private static Error Failure(string code, string description) => new(code, description, ErrorType.Validation);
}

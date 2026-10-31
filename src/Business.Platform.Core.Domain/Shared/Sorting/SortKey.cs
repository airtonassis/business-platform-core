using System.Linq.Expressions;
using System.Reflection;

namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Chave de ordenação imutável de um agregado: seletor de propriedade, direção e nome estável do membro.
/// </summary>
/// <typeparam name="TAggregate">Tipo do agregado ordenado.</typeparam>
/// <remarks>
/// O seletor deve ser acesso direto a uma propriedade legível e de instância do próprio agregado
/// (<c>a =&gt; a.Propriedade</c>): conversões, chamadas de método, acessos encadeados, campos, closures e
/// constantes são rejeitados. A igualdade é estrutural (membro declarante, nome do membro e direção).
/// Não conhece provedor nem esquema persistido; a tradução pertence à Infrastructure.
/// </remarks>
public sealed class SortKey<TAggregate> : IEquatable<SortKey<TAggregate>>
{
    private SortKey(LambdaExpression selector, PropertyInfo member, SortDirection direction)
    {
        Selector = selector;
        MemberName = member.Name;
        KeyType = member.PropertyType;
        DeclaringType = member.DeclaringType!;
        Direction = direction;
    }

    /// <summary>Seletor da propriedade, no formato <c>a =&gt; a.Propriedade</c>.</summary>
    public LambdaExpression Selector { get; }

    /// <summary>Nome estável da propriedade selecionada.</summary>
    public string MemberName { get; }

    /// <summary>Tipo estático da propriedade selecionada.</summary>
    public Type KeyType { get; }

    /// <summary>Direção de ordenação.</summary>
    public SortDirection Direction { get; }

    internal Type DeclaringType { get; }

    /// <summary>
    /// Cria uma chave de ordenação validada.
    /// </summary>
    /// <typeparam name="TKey">Tipo da propriedade selecionada.</typeparam>
    /// <param name="selector">Acesso direto a uma propriedade do agregado.</param>
    /// <param name="direction">Direção de ordenação.</param>
    /// <returns>Sucesso com a chave, ou falha de validação (<c>Sorting.Selector.Invalid</c> ou <c>Sorting.Direction.Invalid</c>).</returns>
    public static Result<SortKey<TAggregate>> Create<TKey>(
        Expression<Func<TAggregate, TKey>>? selector,
        SortDirection direction = SortDirection.Ascending)
    {
        if (selector is null || !TryGetMember(selector, out var member))
        {
            return new Error(
                SortingErrorCodes.SelectorInvalid,
                "O seletor deve ser acesso direto a uma propriedade legível do agregado.",
                ErrorType.Validation);
        }

        if (!Enum.IsDefined(direction))
        {
            return new Error(
                SortingErrorCodes.DirectionInvalid,
                "A direção de ordenação informada não é válida.",
                ErrorType.Validation);
        }

        return new SortKey<TAggregate>(selector, member, direction);
    }

    internal static SortKey<TAggregate> FromValidated(LambdaExpression selector, PropertyInfo member, SortDirection direction) =>
        new(selector, member, direction);

    /// <summary>Indica se duas chaves são iguais por valor.</summary>
    public static bool operator ==(SortKey<TAggregate>? left, SortKey<TAggregate>? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Indica se duas chaves são diferentes por valor.</summary>
    public static bool operator !=(SortKey<TAggregate>? left, SortKey<TAggregate>? right) => !(left == right);

    /// <inheritdoc />
    public bool Equals(SortKey<TAggregate>? other) =>
        other is not null
        && DeclaringType == other.DeclaringType
        && string.Equals(MemberName, other.MemberName, StringComparison.Ordinal)
        && Direction == other.Direction;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SortKey<TAggregate> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(DeclaringType, StringComparer.Ordinal.GetHashCode(MemberName), Direction);

    private static bool TryGetMember(LambdaExpression selector, out PropertyInfo member)
    {
        member = null!;

        if (selector.Parameters.Count != 1
            || selector.Body is not MemberExpression { Member: PropertyInfo property } access
            || access.Expression != selector.Parameters[0]
            || property.GetMethod is null
            || property.GetMethod.IsStatic
            || property.GetIndexParameters().Length != 0)
        {
            return false;
        }

        member = property;

        return true;
    }
}

/// <summary>
/// Códigos estáveis dos erros de validação de ordenação.
/// </summary>
public static class SortingErrorCodes
{
    /// <summary>Seletor ausente ou que não é acesso direto a uma propriedade legível do agregado.</summary>
    public const string SelectorInvalid = "Sorting.Selector.Invalid";

    /// <summary>Direção de ordenação não definida em <see cref="SortDirection"/>.</summary>
    public const string DirectionInvalid = "Sorting.Direction.Invalid";

    /// <summary>Sequência de chaves ausente ou vazia.</summary>
    public const string KeysEmpty = "Sorting.Keys.Empty";

    /// <summary>Mais de quatro chaves solicitadas (contando o identificador explícito).</summary>
    public const string KeysExceedMaximum = "Sorting.Keys.ExceedsMaximum";

    /// <summary>Chave nula na sequência.</summary>
    public const string KeyNull = "Sorting.Key.Null";

    /// <summary>Mesma propriedade informada mais de uma vez, mesmo com direções diferentes.</summary>
    public const string KeyDuplicated = "Sorting.Key.Duplicated";

    /// <summary>Identificador explícito que não é a última chave solicitada.</summary>
    public const string IdentifierNotLast = "Sorting.Identifier.NotLast";
}

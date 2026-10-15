using System.Linq.Expressions;

namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Implementação padrão de <see cref="ISpecification{T}"/> sobre uma expressão fornecida.
/// </summary>
/// <typeparam name="T">Tipo avaliado pela especificação.</typeparam>
/// <remarks>
/// A expressão é mantida por referência: a classe não copia nem protege a árvore, e variáveis capturadas
/// (closures) continuam sendo lidas no momento da avaliação. Não há garantia de imutabilidade profunda.
/// </remarks>
public sealed class Specification<T> : ISpecification<T>
{
    /// <summary>
    /// Cria uma especificação a partir de uma expressão.
    /// </summary>
    /// <param name="criteria">Expressão do critério.</param>
    /// <exception cref="ArgumentNullException"><paramref name="criteria"/> é <see langword="null"/>.</exception>
    public Specification(Expression<Func<T, bool>> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        Criteria = criteria;
    }

    /// <inheritdoc />
    public Expression<Func<T, bool>> Criteria { get; }
}

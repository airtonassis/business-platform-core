using System.Linq.Expressions;

namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Contrato de uma regra de seleção expressa como árvore de expressão, independente de infraestrutura.
/// </summary>
/// <typeparam name="T">Tipo avaliado pela especificação.</typeparam>
/// <remarks>
/// A composição (<see cref="SpecificationExtensions"/>) opera sobre <see cref="Criteria"/> sem compilá-la nem invocá-la.
/// A tradução da expressão por um provedor específico (por exemplo, um ORM) não é garantida por este contrato.
/// </remarks>
public interface ISpecification<T>
{
    /// <summary>
    /// Expressão que define o critério. Implementações não devem retornar <see langword="null"/>.
    /// </summary>
    Expression<Func<T, bool>> Criteria { get; }
}

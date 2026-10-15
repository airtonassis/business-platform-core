using System.Linq.Expressions;

namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Operadores de composição de <see cref="ISpecification{T}"/>.
/// </summary>
/// <remarks>
/// Argumentos nulos e <see cref="ISpecification{T}.Criteria"/> nulo são erros de programação e lançam exceção;
/// não são convertidos em <c>Result</c>. A composição não compila nem invoca as expressões: apenas as combina
/// em uma nova árvore, sem <see cref="InvocationExpression"/>.
/// </remarks>
public static class SpecificationExtensions
{
    /// <summary>
    /// Combina duas especificações com conjunção lógica (<see cref="Expression.AndAlso(Expression, Expression)"/>).
    /// </summary>
    /// <typeparam name="T">Tipo avaliado.</typeparam>
    /// <param name="left">Primeiro operando.</param>
    /// <param name="right">Segundo operando.</param>
    /// <returns>Nova especificação satisfeita quando ambos os operandos são satisfeitos.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="left"/> ou <paramref name="right"/> é nulo.</exception>
    /// <exception cref="ArgumentException">O <see cref="ISpecification{T}.Criteria"/> de um operando é nulo.</exception>
    public static ISpecification<T> And<T>(this ISpecification<T> left, ISpecification<T> right) =>
        Combine(left, right, Expression.AndAlso);

    /// <summary>
    /// Combina duas especificações com disjunção lógica (<see cref="Expression.OrElse(Expression, Expression)"/>).
    /// </summary>
    /// <typeparam name="T">Tipo avaliado.</typeparam>
    /// <param name="left">Primeiro operando.</param>
    /// <param name="right">Segundo operando.</param>
    /// <returns>Nova especificação satisfeita quando ao menos um operando é satisfeito.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="left"/> ou <paramref name="right"/> é nulo.</exception>
    /// <exception cref="ArgumentException">O <see cref="ISpecification{T}.Criteria"/> de um operando é nulo.</exception>
    public static ISpecification<T> Or<T>(this ISpecification<T> left, ISpecification<T> right) =>
        Combine(left, right, Expression.OrElse);

    /// <summary>
    /// Nega uma especificação (<see cref="Expression.Not(Expression)"/>).
    /// </summary>
    /// <typeparam name="T">Tipo avaliado.</typeparam>
    /// <param name="specification">Especificação a negar.</param>
    /// <returns>Nova especificação satisfeita quando o operando não é satisfeito.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="specification"/> é nulo.</exception>
    /// <exception cref="ArgumentException">O <see cref="ISpecification{T}.Criteria"/> do operando é nulo.</exception>
    public static ISpecification<T> Not<T>(this ISpecification<T> specification)
    {
        var criteria = GetCriteria(specification, nameof(specification));

        return new Specification<T>(Expression.Lambda<Func<T, bool>>(Expression.Not(criteria.Body), criteria.Parameters));
    }

    private static Specification<T> Combine<T>(
        ISpecification<T> left,
        ISpecification<T> right,
        Func<Expression, Expression, BinaryExpression> operation)
    {
        var leftCriteria = GetCriteria(left, nameof(left));
        var rightCriteria = GetCriteria(right, nameof(right));

        // Parâmetro novo: nenhuma árvore existente o referencia, portanto não há captura indevida.
        var parameter = Expression.Parameter(typeof(T), leftCriteria.Parameters[0].Name);
        var leftBody = Replace(leftCriteria, parameter);
        var rightBody = Replace(rightCriteria, parameter);

        return new Specification<T>(Expression.Lambda<Func<T, bool>>(operation(leftBody, rightBody), parameter));
    }

    private static Expression<Func<T, bool>> GetCriteria<T>(ISpecification<T> specification, string argumentName)
    {
        ArgumentNullException.ThrowIfNull(specification, argumentName);

        return specification.Criteria
            ?? throw new ArgumentException("O critério da especificação não pode ser nulo.", argumentName);
    }

    private static Expression Replace<T>(Expression<Func<T, bool>> criteria, ParameterExpression target) =>
        new ParameterReplacer(criteria.Parameters[0], target).Visit(criteria.Body);

    // Substitui somente o parâmetro de origem, por identidade de referência, respeitando escopos aninhados:
    // se uma lambda, bloco ou catch interno declara o mesmo ParameterExpression, ele o sombreia e não é alterado.
    private sealed class ParameterReplacer(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            ReferenceEquals(node, source) ? target : node;

        protected override Expression VisitLambda<TDelegate>(Expression<TDelegate> node) =>
            node.Parameters.Contains(source) ? node : base.VisitLambda(node);

        protected override Expression VisitBlock(BlockExpression node) =>
            node.Variables.Contains(source) ? node : base.VisitBlock(node);

        protected override CatchBlock VisitCatchBlock(CatchBlock node) =>
            ReferenceEquals(node.Variable, source) ? node : base.VisitCatchBlock(node);
    }
}

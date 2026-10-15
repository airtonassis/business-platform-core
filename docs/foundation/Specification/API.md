# API — Specification Foundation

Namespace: `Business.Platform.Core.Domain.Shared`

## ISpecification\<T\>

```csharp
public interface ISpecification<T>
{
    Expression<Func<T, bool>> Criteria { get; }
}
```

Implementações não devem retornar `null` em `Criteria` (a composição lança `ArgumentException` se retornarem).

## Specification\<T\>

```csharp
public sealed class Specification<T> : ISpecification<T>
{
    public Specification(Expression<Func<T, bool>> criteria);

    public Expression<Func<T, bool>> Criteria { get; }
}
```

| Membro | Comportamento |
|---|---|
| Construtor | `ArgumentNullException` (`criteria`) se nulo |
| `Criteria` | Mesma instância fornecida; sem setter |

## SpecificationExtensions

```csharp
public static class SpecificationExtensions
{
    public static ISpecification<T> And<T>(this ISpecification<T> left, ISpecification<T> right);
    public static ISpecification<T> Or<T>(this ISpecification<T> left, ISpecification<T> right);
    public static ISpecification<T> Not<T>(this ISpecification<T> specification);
}
```

| Método | Expressão | Exceções |
|---|---|---|
| `And` | `Expression.AndAlso` | `ArgumentNullException` (`left`/`right`); `ArgumentException` se `Criteria` nulo |
| `Or` | `Expression.OrElse` | idem |
| `Not` | `Expression.Not` | `ArgumentNullException` (`specification`); `ArgumentException` se `Criteria` nulo |

Cada chamada devolve uma nova `Specification<T>`; os operandos não são alterados. A composição não usa `Expression.Invoke`, não compila e não referencia `IQueryable`.

## Specifications

```csharp
public static class Specifications
{
    public static ISpecification<T> True<T>();   // x => true
    public static ISpecification<T> False<T>();  // x => false
}
```

## Garantias e não-garantias

- Garante: curto-circuito de `AndAlso`/`OrElse`; reescrita de parâmetros por identidade de referência; sombreamento de escopos aninhados respeitado.
- Não garante: imutabilidade profunda; tradução por qualquer ORM; desempenho; simplificação das expressões.
- Closures são lidas na avaliação.

## Dependência

`System.Linq.Expressions` (ADR-0003). `IRepository<TAggregate, TId>` 1.0 permanece inalterado.

# Componente: Specification

`ISpecification<T>` é um contrato fundamental da Shared Foundation que representa uma regra de seleção como uma árvore de expressão (`Expression<Func<T, bool>>`), independente de infraestrutura. A feature entrega também a implementação padrão `Specification<T>`, os operadores de composição `And`, `Or` e `Not` e as identidades lógicas `True` e `False`.

## Componentes

| Tipo | Arquivo | Papel |
|---|---|---|
| `ISpecification<T>` | `Shared/Specifications/ISpecification.cs` | Contrato com a propriedade `Criteria` |
| `Specification<T>` | `Shared/Specifications/Specification.cs` | Implementação selada sobre uma expressão |
| `SpecificationExtensions` | `Shared/Specifications/SpecificationExtensions.cs` | `And`, `Or`, `Not` |
| `Specifications` | `Shared/Specifications/Specifications.cs` | `True<T>()`, `False<T>()` |

Namespace: `Business.Platform.Core.Domain.Shared`.

## Operadores

- **And:** conjunção com `Expression.AndAlso`; o operando da direita só é avaliado se o da esquerda for verdadeiro.
- **Or:** disjunção com `Expression.OrElse`; o operando da direita só é avaliado se o da esquerda for falso.
- **Not:** negação com `Expression.Not`.
- **True\<T\>():** representa `x => true` (elemento neutro de `And`).
- **False\<T\>():** representa `x => false` (elemento neutro de `Or`).

- `And` e `Or` criam um novo `ParameterExpression`; os parâmetros de ambos os operandos são reescritos para esse novo parâmetro, por identidade de referência.
- `Not` reutiliza os parâmetros existentes do operando e aplica `Expression.Not` ao corpo.
- A composição não utiliza `Expression.Invoke`, `Compile()` nem `IQueryable`. A composição não introduz novos nós `InvocationExpression`; nós dessa natureza já existentes nas expressões de entrada podem ser preservados.

## Exceções de programação

Argumentos nulos e `Criteria` nulo são erros de programação e lançam exceção; não são convertidos em `Result`.

| Situação | Exceção | `ParamName` |
|---|---|---|
| `criteria` nulo no construtor | `ArgumentNullException` | `criteria` |
| operando nulo em `And`/`Or` | `ArgumentNullException` | `left` / `right` |
| operando nulo em `Not` | `ArgumentNullException` | `specification` |
| `Criteria` nulo em implementação personalizada | `ArgumentException` | `left` / `right` / `specification` |

O operando `left` é validado antes de `right`.

## Limitações

- **Closures:** variáveis capturadas pela expressão são lidas no momento da avaliação, não da composição.
- **Sem imutabilidade profunda:** `Specification<T>` guarda a mesma instância da expressão fornecida; a árvore de expressão é imutável, mas objetos capturados pela expressão não são protegidos.
- **ORMs:** não há garantia de que qualquer ORM ou provedor traduza `Criteria` ou expressões compostas (ver ADR-0003).
- **Desempenho:** nenhuma promessa de desempenho é feita.

## Fora do escopo

- Integração com `IRepository` (o contrato Repository 1.0 permanece inalterado).
- Paginação, ordenação, projeção e `Include`.
- Tradução ou execução de consultas, `IQueryable`, ORMs.
- Cache ou compilação de expressões.
- Simplificação algébrica das expressões compostas.
- Especificações nomeadas de negócio.

## Decisão arquitetural

`ADR-0003 — Expression Trees in Domain` (`docs/architecture/decisions/ADR-0003-expression-trees-in-domain.md`) autoriza a dependência adicional `System.Linq.Expressions` no assembly Domain, preservando a allowlist explícita (ADR-0002).

---

**Versão:** 1.0 (Implementado — FEATURE-0008)  
**Status:** Architecture Gate APPROVED — implementado; aguardando auditoria independente  
**Namespace:** `Business.Platform.Core.Domain.Shared`

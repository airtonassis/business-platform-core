# Implementação — Specification Foundation

## Arquivos de produção

`src/Business.Platform.Core.Domain/Shared/Specifications/`

1. `ISpecification.cs`
2. `Specification.cs`
3. `SpecificationExtensions.cs`
4. `Specifications.cs`

## Decisões técnicas

### D01 — Contrato com expressão

`ISpecification<T>` possui apenas `Criteria` (`Expression<Func<T, bool>>`). Sem métodos default; composição em métodos de extensão.

### D02 — `Specification<T>`

Classe pública e selada; o construtor chama `ArgumentNullException.ThrowIfNull(criteria)` e guarda a referência. A propriedade é somente leitura (sem setter).

### D03 — Composição

`And`/`Or` delegam a um método privado `Combine` que:

1. valida `left` e `right` (nessa ordem) com `GetCriteria`;
2. cria um **novo** `ParameterExpression` (nome do parâmetro de `left`);
3. reescreve o corpo de cada operando para o novo parâmetro;
4. aplica `Expression.AndAlso` ou `Expression.OrElse`;
5. devolve `new Specification<T>(Expression.Lambda<Func<T, bool>>(...))`.

`Not` valida o operando e aplica `Expression.Not` sobre o corpo, reutilizando os parâmetros do operando (não há segunda árvore a unificar).

Usar um parâmetro novo garante que nenhuma árvore existente o referencie, o que elimina captura indevida ao reescrever.

### D04 — Validação (Ajuste A)

`GetCriteria`: `ArgumentNullException.ThrowIfNull(specification, argumentName)`; se `Criteria` for nulo, lança `ArgumentException` com o nome do argumento. Falhas são erros de programação e não viram `Result`.

### D05 — ExpressionVisitor seguro (Ajuste B)

`ParameterReplacer` (classe privada aninhada em `SpecificationExtensions`):

- `VisitParameter`: substitui apenas quando `ReferenceEquals(node, source)`;
- `VisitLambda`, `VisitBlock`, `VisitCatchBlock`: se o escopo declara o próprio parâmetro de origem, o nó é devolvido sem alteração (sombreamento); caso contrário o visitor desce normalmente.

Parâmetros de outras lambdas (por exemplo, `v` em `x.Values.Any(v => ...)`) mantêm suas instâncias. A composição não introduz novos nós `InvocationExpression`; nós dessa natureza já existentes nas expressões de entrada podem ser preservados.

### D06 — Identidades

`True<T>()` => `new Specification<T>(_ => true)`; `False<T>()` => `new Specification<T>(_ => false)`. Cada chamada cria uma instância nova.

### D07 — Dependências

Nova referência de assembly: `System.Linq.Expressions` (ADR-0003). Allowlist em `ErrorArchitectureTests`: `System.Runtime`, `System.Collections`, `System.Linq.Expressions`, todos com public key token `b03f5f7f11d50a3a`. O projeto Domain continua sem referências de pacote, projeto ou framework.

## Arquivos de teste e documentação

- `tests/UnitTests/Domain/Shared/Specifications/SpecificationTests.cs`
- `tests/ArchitectureTests/Domain/Shared/Specifications/SpecificationArchitectureTests.cs`
- `tests/ArchitectureTests/Domain/Shared/Errors/ErrorArchitectureTests.cs` (somente a allowlist)
- `docs/architecture/decisions/ADR-0003-expression-trees-in-domain.md`
- `docs/foundation/Specification/*.md`

## Limitações conhecidas

- Closures são lidas no momento da avaliação.
- Sem imutabilidade profunda.
- Sem garantia de tradução por ORMs.
- Sem promessa de desempenho; árvores compostas não são simplificadas.
- Repository 1.0 inalterado; sem integração.

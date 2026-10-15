# Regras de Negócio — Specification Foundation

## Invariantes

| # | Regra |
|---|---|
| S1 | `ISpecification<T>` expõe uma única propriedade somente leitura, `Criteria`, do tipo `Expression<Func<T, bool>>`. |
| S2 | A interface não possui métodos default nem outros membros. |
| S3 | `Specification<T>` é pública e selada; o construtor público recebe `criteria`. |
| S4 | `new Specification<T>(null)` lança `ArgumentNullException` (`ParamName` = `criteria`). |
| S5 | `Criteria` devolve a mesma instância da expressão fornecida. |
| S6 | `And` usa `Expression.AndAlso`; `Or` usa `Expression.OrElse`; `Not` usa `Expression.Not`. |
| S7 | A composição produz `Expression<Func<T, bool>>` e não usa `Expression.Invoke`, `Compile()`, `Func<T, bool>` como contrato nem `IQueryable`. |
| S8 | A composição não altera as especificações operandos e devolve uma nova especificação. |
| S9 | Operando nulo lança `ArgumentNullException` identificando `left`, `right` ou `specification`. |
| S10 | `Criteria` nulo em implementação personalizada lança `ArgumentException` (não `ArgumentNullException`) identificando o argumento. |
| S11 | `And`/`Or` validam `left` antes de `right`. |
| S12 | A reescrita de parâmetros substitui somente o `ParameterExpression` de origem, por identidade de referência. |
| S13 | Parâmetros de outras lambdas são preservados; lambda, bloco ou `catch` que declare o mesmo parâmetro de origem o sombreia e permanece intacto. |
| S14 | `True<T>()` representa `x => true`; `False<T>()` representa `x => false`; nenhuma identidade é `null`. |

## Equivalências lógicas (verificadas por comportamento)

- `a And True` ≡ `a`; `a Or False` ≡ `a`.
- `a And False` ≡ `False`; `a Or True` ≡ `True`.
- `Not(Not(a))` ≡ `a`.
- Leis de De Morgan.

Equivalência é lógica, não estrutural: as árvores resultantes não são simplificadas.

## Erros de programação versus falhas esperadas

Argumentos nulos e `Criteria` nulo são erros de programação: lançam exceção e **não** são convertidos em `Result`. A feature não define falhas esperadas.

## Curto-circuito

`AndAlso` e `OrElse` preservam o curto-circuito: com `And`, o operando da direita não é avaliado quando o da esquerda é falso; com `Or`, não é avaliado quando o da esquerda é verdadeiro. A composição em si não avalia nenhum operando.

## Closures

Variáveis capturadas por uma expressão são lidas no momento da avaliação (após `Compile()` ou na tradução), não no momento da composição. O contrato não garante imutabilidade profunda.

## Tradução por provedores

Não há garantia de que um ORM ou outro provedor traduza qualquer expressão. A feature não promete desempenho.

## Fora do escopo

Integração com `IRepository`, paginação, ordenação, projeção, execução de consultas, cache/compilação, simplificação algébrica.

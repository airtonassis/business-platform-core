# Testes — Pagination

## Matriz

| Arquivo | Cobertura |
|---|---|
| `tests/UnitTests/Domain/Shared/Pagination/PaginationRequestTests.cs` | limites de página/tamanho (0, negativo, `int.MinValue`, 101, `int.MaxValue`), 1 e 100, `Offset` sem overflow, `Default`, igualdade, hash, operadores, null, precedência, códigos exatos e `ErrorType.Validation`, descrições sem valores recebidos |
| `tests/UnitTests/Domain/Shared/Pagination/PagedResultTests.cs` | `TotalCount` 0, página única/intermediária/última/além do total (com e sem itens), `long.MaxValue`, `HasPreviousPage`/`HasNextPage`/`TotalPages`, validações e precedência, `Count` negativo/acima de `PageSize`, enumerador com mais/menos/infinito, elemento nulo (inclusive na posição `Count + 1`), propagação de exceção do enumerador, `Count` lido uma vez, cópia defensiva, ordem, mutação via `IList<T>`, coleção vazia |
| `tests/ArchitectureTests/Domain/Shared/Pagination/PaginationArchitectureTests.cs` | namespace, `sealed`, construtores privados, assinaturas exatas, sem setters, dependências proibidas, sem `System.ObjectModel`, sem novas interfaces, allowlist preservada, `IRepository` e `ISpecification` inalterados |

## Resultados (Release)

| Projeto | Antes | Depois | Falhas | Ignorados |
|---|---|---|---|---|
| UnitTests | 322 | 402 | 0 | 0 |
| ArchitectureTests | 162 | 178 | 0 | 0 |

Build: 0 avisos, 0 erros. `dotnet format --verify-no-changes`: sem alterações.

## Mutation testing

Comando (workaround de MSBuild somente na linha de comando; `stryker-config.json` inalterado, `break = 90`):

```powershell
dotnet tool run dotnet-stryker --msbuild-path "C:\Program Files\dotnet\sdk\10.0.401\MSBuild.exe" --mutate "**/Shared/Pagination/*.cs"
```

Escopo: `PaginationRequest.cs` e `PagedResult.cs`.

| Métrica | Valor |
|---|---|
| Mutantes gerados (projeto) | 250 |
| Testados | 75 |
| Killed | 70 |
| Timeout | 1 |
| Survived | 4 |
| No coverage | 0 |
| Compilation error | 5 (3 em Pagination, 2 em `Error.cs`) |
| Ignored/skipped | 170 (16 "block already covered" + 154 fora do filtro `--mutate`) |
| **Score** | **94,67 %** ((70+1)/75), limite 90 % |

O score refere-se somente aos mutantes testados nos arquivos de Pagination (75), não é prova de cobertura universal.

Sobreviventes:

- `PagedResult.cs` L23: `totalCount == 0` → `false` — mutante **equivalente** (a fórmula já resulta em 0 para `totalCount = 0`).
- `PagedResult.cs` L75, L102, L111: mensagens de exceção substituídas por `""` — o contrato testado é o tipo de exceção e `ParamName`; as mensagens não são contrato.

O timeout ocorre no mutante em `PaginationRequest.cs` L79, no operador `!=` (`!(left == right)` → `left != right`): o operador passa a chamar a si mesmo, gerando recursão infinita, e é contado como detectado.

## Remediação pós-auditoria (AUD-01 a AUD-04)

- Testes adicionais em `PagedResultTests`: `Items.Count > TotalCount`, itens com `TotalCount = 0` (páginas 1 e posterior) e página além do total com itens não vazios — impedem validação cruzada entre `Items.Count` e `TotalCount`; o teste de exceção do enumerador também verifica a identidade da instância original.
- `PaginationArchitectureTests`: verificação de interfaces independente de nome (grafo de tipos do componente, membros de interfaces do Domain e baseline não vazio).

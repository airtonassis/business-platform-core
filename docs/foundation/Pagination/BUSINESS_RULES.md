# Regras de negócio — Pagination

## PaginationRequest

| Regra | Descrição |
|---|---|
| PAG-01 | `PageNumber >= 1` (página inicial = 1). |
| PAG-02 | `PageSize >= 1`. |
| PAG-03 | `PageSize <= 100` (`MaxPageSize`). |
| PAG-04 | Tamanho padrão = 20 (`DefaultPageSize`). Sem clamp automático. |
| PAG-05 | `Offset = (long)(PageNumber - 1) * PageSize`, sem overflow. |
| PAG-06 | Igualdade por valor de `PageNumber` e `PageSize`; `Default` equivale a `Create(1, 20)`. |

### Erros (`ErrorType.Validation`, descrições fixas, sem os valores recebidos)

Precedência: apenas o primeiro erro é retornado.

1. `Pagination.PageNumber.LessThanOne` — `pageNumber < 1`.
2. `Pagination.PageSize.LessThanOne` — `pageSize < 1`.
3. `Pagination.PageSize.ExceedsMaximum` — `pageSize > 100`.

## PagedResult&lt;T&gt;

- `PageNumber` e `PageSize` vêm do request; `TotalCount` é `long` não negativo.
- `TotalPages = 0` se `TotalCount == 0`; senão `TotalCount / PageSize + (TotalCount % PageSize == 0 ? 0 : 1)`.
- `HasPreviousPage = PageNumber > 1`; `HasNextPage = PageNumber < TotalPages`.
- Página além do total é permitida, inclusive com `Items` não vazio.
- Não se exige consistência entre `Items.Count` e `TotalCount` (podem vir de consultas em momentos diferentes).
- `Items` preserva a ordem recebida e rejeita mutação da coleção; os objetos `T` não são protegidos.

### Validação de `Create` (ordem)

1. `items == null` → `ArgumentNullException("items")`.
2. `request == null` → `ArgumentNullException("request")`.
3. `totalCount < 0` → `ArgumentOutOfRangeException("totalCount")`.
4. `items.Count < 0` → `ArgumentException("items")`.
5. `items.Count > request.PageSize` → `ArgumentException("items")`.
6. Enumeração produz mais itens que `Count` → `ArgumentException("items")`.
7. Elemento `null` → `ArgumentException("items")`.
8. Enumeração produz menos itens que `Count` → `ArgumentException("items")`.

Exceções do próprio enumerador são propagadas sem conversão.

## Concorrência

Não há snapshot atômico: se a coleção de origem for alterada concorrentemente durante a cópia, o resultado é indefinido (podendo falhar por inconsistência de `Count`). Offset pagination pode apresentar duplicações ou omissões com mudanças concorrentes. `TotalCount` é best-effort.

## Ordenação

Nenhum dos tipos aplica ordenação. É responsabilidade da futura camada de consulta garantir ordenação determinística com desempate único (ex.: chave primária como último critério).

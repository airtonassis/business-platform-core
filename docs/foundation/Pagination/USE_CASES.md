# Casos de uso — Pagination

## Válidos

```csharp
PaginationRequest.Create(1);              // página 1, tamanho 20, Offset 0
PaginationRequest.Create(3, 50);          // Offset 100
PaginationRequest.Create(1, 100);         // tamanho máximo
PaginationRequest.Default;                // equivalente a Create(1, 20)
```

Página com itens:

```csharp
var page = PagedResult<string>.Create(["a", "b"], PaginationRequest.Create(2, 2).Value, 5);
// TotalPages = 3, HasPreviousPage = true, HasNextPage = true
```

## Inválidos

| Chamada | Resultado |
|---|---|
| `Create(0)` / `Create(-1)` | falha `Pagination.PageNumber.LessThanOne` |
| `Create(1, 0)` | falha `Pagination.PageSize.LessThanOne` |
| `Create(1, 101)` | falha `Pagination.PageSize.ExceedsMaximum` |
| `Create(0, 0)` | falha `Pagination.PageNumber.LessThanOne` (precedência) |
| `PagedResult.Create(null, ...)` | `ArgumentNullException("items")` |
| `PagedResult.Create(items, req, -1)` | `ArgumentOutOfRangeException("totalCount")` |
| `Count` maior que `PageSize` | `ArgumentException("items")` |
| Elemento `null` | `ArgumentException("items")` |

## Vazios

`PagedResult<T>.Create([], request, 0)` com `request.PageNumber = 1`: `TotalCount = 0`, `TotalPages = 0`, `HasPreviousPage = false`, `HasNextPage = false`.

Com `PageNumber > 1` e os mesmos dados vazios: `TotalPages = 0`, `HasPreviousPage = true` (regra `PageNumber > 1`) e `HasNextPage = false`.

Com `totalCount > 0` e itens vazios (ex.: página além do fim) a criação também é permitida. A Foundation não valida `Items.Count` contra `TotalCount` (inclusive `Items.Count > TotalCount` ou itens com `TotalCount = 0`), desde que `Items.Count <= PageSize`.

## Além do total

`Create([], PaginationRequest.Create(10, 10).Value, 25)` → `TotalPages = 3`, `HasPreviousPage = true`, `HasNextPage = false`. Também é permitido ter itens (ex.: dados alterados entre as consultas).

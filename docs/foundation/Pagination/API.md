# API — Pagination

Namespace: `Business.Platform.Core.Domain.Shared`.

## PaginationRequest

```csharp
public sealed class PaginationRequest : IEquatable<PaginationRequest>
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static PaginationRequest Default { get; }

    public int PageNumber { get; }
    public int PageSize { get; }
    public long Offset { get; }

    public static Result<PaginationRequest> Create(int pageNumber, int pageSize = DefaultPageSize);

    public bool Equals(PaginationRequest? other);
    public override bool Equals(object? obj);
    public override int GetHashCode();
    public static bool operator ==(PaginationRequest? left, PaginationRequest? right);
    public static bool operator !=(PaginationRequest? left, PaginationRequest? right);
}
```

### Erros de `Create`

| Código | Condição | Tipo |
|---|---|---|
| `Pagination.PageNumber.LessThanOne` | `pageNumber < 1` | `Validation` |
| `Pagination.PageSize.LessThanOne` | `pageSize < 1` | `Validation` |
| `Pagination.PageSize.ExceedsMaximum` | `pageSize > 100` | `Validation` |

Apenas o primeiro erro é retornado, na ordem da tabela. As descrições são fixas, em português, e não incluem os valores recebidos.

## PagedResult&lt;T&gt;

```csharp
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public long TotalCount { get; }
    public long TotalPages { get; }
    public bool HasPreviousPage { get; }
    public bool HasNextPage { get; }

    public static PagedResult<T> Create(
        IReadOnlyCollection<T> items,
        PaginationRequest request,
        long totalCount);
}
```

### Exceções de `Create` (nesta ordem)

| Condição | Exceção |
|---|---|
| `items` nulo | `ArgumentNullException("items")` |
| `request` nulo | `ArgumentNullException("request")` |
| `totalCount < 0` | `ArgumentOutOfRangeException("totalCount")` |
| `items.Count < 0` | `ArgumentException("items")` |
| `items.Count > request.PageSize` | `ArgumentException("items")` |
| enumeração com mais elementos que `Count` | `ArgumentException("items")` |
| elemento `null` | `ArgumentException("items")` |
| enumeração com menos elementos que `Count` | `ArgumentException("items")` |

Exceções lançadas pelo enumerador são propagadas sem conversão.

## Exemplo

```csharp
var request = PaginationRequest.Create(2, 10);
if (request.IsFailure)
{
    return request.Error; // ex.: Pagination.PageNumber.LessThanOne
}

var page = PagedResult<string>.Create(items, request.Value, totalCount);
```

## Observações

- Nenhum tipo executa consultas ou aplica ordenação; a camada de consulta deve garantir ordenação determinística com desempate único.
- Offset pagination pode duplicar/omitir itens sob mudanças concorrentes; `TotalCount` é best-effort.
- `Items` é uma coleção não modificável; os objetos `T` podem ser mutáveis.

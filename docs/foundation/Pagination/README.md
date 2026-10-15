# Pagination Foundation (FEATURE-0009)

## Objetivo

Fornecer, no Domain, dois tipos independentes de HTTP, ORM, Infrastructure e bibliotecas externas:

- `PaginationRequest` — solicitação de página validada (numeração baseada em 1).
- `PagedResult<T>` — página já obtida, com metadados de navegação.

Ambos residem em `Business.Platform.Core.Domain.Shared` (pasta `Shared/Pagination/`).

## Escopo

- Validação de `pageNumber` e `pageSize` via `Result<PaginationRequest>`.
- Cálculo seguro de `Offset` e `TotalPages` (sem overflow).
- Cópia defensiva e coleção não modificável dos itens.

Fora do escopo: execução de consultas, ordenação, cursor/keyset pagination, serialização, contratos HTTP.

## Visão geral

```csharp
var request = PaginationRequest.Create(pageNumber: 2, pageSize: 20);
if (request.IsFailure) return request.Error;

// A camada de consulta (futura) usa request.Value.Offset e request.Value.PageSize.
var page = PagedResult<Customer>.Create(items, request.Value, totalCount);

page.TotalPages;       // long
page.HasNextPage;      // PageNumber < TotalPages
page.HasPreviousPage;  // PageNumber > 1
```

## Limitações

- Nenhum dos tipos executa consultas nem aplica ordenação.
- A futura camada de consulta deve garantir ordenação determinística com desempate único.
- Paginação por offset pode duplicar ou omitir itens quando os dados mudam concorrentemente.
- `TotalCount` é best-effort; não há consistência transacional com `Items`.
- `Items` é uma coleção não modificável, mas os objetos `T` podem ser mutáveis (sem imutabilidade profunda).
- Não há clamp automático: valores inválidos resultam em falha.

Veja também: [BUSINESS_RULES.md](BUSINESS_RULES.md), [USE_CASES.md](USE_CASES.md), [IMPLEMENTATION.md](IMPLEMENTATION.md), [TESTS.md](TESTS.md), [CHECKLIST.md](CHECKLIST.md), [API.md](API.md).

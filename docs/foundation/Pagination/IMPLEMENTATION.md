# Implementação — Pagination

## Arquivos

- `src/Business.Platform.Core.Domain/Shared/Pagination/PaginationRequest.cs`
- `src/Business.Platform.Core.Domain/Shared/Pagination/PagedResult.cs`

## PaginationRequest

- Classe `sealed`, construtor privado, propriedades somente leitura, `IEquatable<PaginationRequest>`.
- `Create` retorna `Result<PaginationRequest>` usando `Error` com `ErrorType.Validation` e a conversão implícita `Error` → `Result<T>` existente; nenhuma abstração nova.
- `Offset` calculado no construtor com `(long)(pageNumber - 1) * pageSize`.
- `GetHashCode` = `HashCode.Combine(PageNumber, PageSize)`; `==`/`!=` tratam referências nulas.

## PagedResult&lt;T&gt;

- Classe `sealed`, construtor privado, sem `IEquatable` e sem interfaces públicas.
- `TotalPages` usa `totalCount / pageSize + (totalCount % pageSize == 0 ? 0 : 1)`; a forma `(totalCount + pageSize - 1) / pageSize` é evitada por poder estourar `long`.

### Imutabilidade da coleção (PAG-07)

`Count` é lido uma única vez; os itens são copiados para um `T[]` privado, encapsulado em `ReadOnlyCollection<T>` e exposto apenas como `IReadOnlyList<T>`. Não há acessor público para o array nem uso de LINQ. Pela interface `IList<T>`, `set` do indexador, `Add`, `Insert`, `Remove`, `RemoveAt` e `Clear` lançam `NotSupportedException`.

A imutabilidade é da **coleção**, não dos objetos `T`. Também não há snapshot atômico caso a origem seja alterada concorrentemente.

### Validação de enumeração (PAG-08)

O excedente é detectado **antes** de `buffer[index]`, evitando `IndexOutOfRangeException`; um enumerador infinito é interrompido no primeiro elemento além de `Count`. Elementos `null` e contagens inconsistentes resultam em `ArgumentException("items")`; exceções do enumerador propagam sem conversão. Nenhum resultado parcial é criado.

## Dependências

Assemblies referenciados pelo Domain após a compilação (verificado): `System.Runtime`, `System.Collections`, `System.Linq.Expressions` — allowlist inalterada. `ReadOnlyCollection<T>` resolve em `System.Runtime`; `System.ObjectModel` e `System.Linq` não são referenciados. Nenhum ADR adicional foi necessário.

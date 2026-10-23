# ADR-0004 — Contrato Repository 2.x: `FindPagedAsync`, paginação, contagem best-effort e `MaxOffset`

## Status

**Accepted**

Data: 2026-10-22

Origem: Repository 2.x — decisões D-01, D-02, D-03, D-04, D-05, D-07, D-10 e D-11 do complemento de reconciliação.

---

## Contexto

`IRepository<TAggregate, TId>` 1.0 (`GetByIdAsync`, `AddAsync`, `UpdateAsync`, `RemoveAsync`) não oferece consulta por critério. As Foundations `Specification` (ADR-0003) e `Pagination` (`PaginationRequest`, `PagedResult<T>`) estão CLOSED, mas ainda não há contrato que as una em uma consulta paginada.

`PaginationRequest.Offset` é `long` (`(PageNumber - 1) * PageSize`) e `PagedResult<T>.TotalCount` é best-effort. Consultas por offset profundo degradam linearmente e um `FindAsync` sem limite permitiria materializar conjuntos arbitrariamente grandes.

---

## Alternativas consideradas

- **`FindAsync(ISpecification)` retornando `IReadOnlyList<T>` sem limite:** risco de materialização ilimitada. Rejeitada.
- **Alterar `IRepository` 1.0:** quebra implementações e viola o estado CLOSED. Rejeitada.
- **Retornar `IQueryable<T>`:** vaza o provedor para fora da Infrastructure (ver ADR-0003). Rejeitada.
- **Contagem exata e transacionalmente consistente:** exige custo e isolamento que o contrato não pode impor. Rejeitada.
- **Paginação por cursor (keyset) como contrato único:** fora do escopo desta etapa; não impedida para evolução futura.

---

## Decisão

1. **Evolução aditiva.** O contrato 2.x é um **novo contrato**, que **não herda** nem altera `IRepository<TAggregate, TId>` 1.0. Todas as Foundations CLOSED permanecem inalteradas.
2. **Contrato fixado.** A assinatura abaixo é normativa:

```csharp
public interface ISpecificationRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    Task<Result<PagedResult<TAggregate>>> FindPagedAsync(
        ISpecification<TAggregate> specification,
        SortSpecification<TAggregate, TId> sort,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default);
}
```

   Uma implementação concreta pode implementar `IRepository` 1.0 e `ISpecificationRepository` separadamente; nenhuma interface herda a outra.
3. **Sem consulta ilimitada.** O novo contrato **não** define `FindAsync` (ou equivalente) sem paginação. Toda consulta por critério é limitada por `PageSize` (≤ `PaginationRequest.MaxPageSize`).
4. **Ordenação obrigatória e total.** `FindPagedAsync` exige `SortSpecification` com `TotalOrder` (ADR-0005); não há ordenação implícita nem padrão do provedor.
5. **Contagem best-effort.** `TotalCount` é obtido separadamente dos itens, em momento possivelmente distinto, sem garantia de consistência entre ambos, conforme `PagedResult<T>`. Páginas além do total são permitidas e retornam lista vazia. A contagem não bloqueia nem invalida a página: o contrato não exige snapshot, transação nem isolamento.
6. **`MaxOffset` obrigatório.** O contrato impõe um limite máximo de `PaginationRequest.Offset`.
   - O **valor operacional** não é fixado por este ADR; será definido posteriormente por decisão do PO/arquitetura e injetado como configuração da Infrastructure/Application.
   - Enquanto o valor não existir, nenhuma implementação do contrato é autorizada.
   - `Offset > MaxOffset` retorna `Result.Failure` com `ErrorType.Validation` e código `Pagination.OffsetExceedsLimit`, **antes** de qualquer acesso ao armazenamento.
   - A comparação é feita em `long`, sem overflow.
7. **Responsabilidades por camada.**
   - **Domain:** define contrato, `ISpecification`, `PaginationRequest`, `PagedResult` e `SortSpecification`; não conhece provedor.
   - **Infrastructure:** traduz especificação e ordenação para o provedor, executa a consulta e aplica `MaxOffset` (ADR-0006 para falhas de tradução).
8. **Falhas.** Falhas esperadas por `Result`; cancelamento por `OperationCanceledException`; defeitos e falhas não classificadas propagam como exceção (ADR-0006).

---

## Justificativa

- Contrato aditivo preserva compatibilidade e o estado CLOSED das Foundations.
- Limitar página e offset torna o custo da consulta previsível e elimina o risco de materialização ilimitada.
- Best-effort é a semântica já formalizada em `PagedResult<T>`; exigir mais forçaria transações ou bloqueios fora do Domain.

---

## Consequências

### Positivas

- Consulta por critério segura e limitada, reutilizando Specification e Pagination existentes.
- `IRepository` 1.0 permanece intacto.

### Negativas / Custos

- Offset profundo é recusado; navegação além de `MaxOffset` exigirá outra estratégia (por exemplo, keyset) em ADR futuro.
- Consumidores não podem recuperar "todos" os registros por este contrato.
- `TotalCount` pode divergir dos itens retornados.

### Escopo da autorização

Este ADR **não autoriza implementação**. Nenhum código de Domain, Infrastructure, teste ou configuração é criado ou alterado por ele.

---

## Relação com outros ADRs

- ADR-0003: `ISpecification<T>` como `Expression<Func<T, bool>>`.
- ADR-0005: ordenação (`SortSpecification`, `TotalOrder`).
- ADR-0006: política de tradução e classificação de falhas.

---

## Rastreabilidade de decisões

| Decisão | Aplicação neste ADR |
|---|---|
| D-05 | Aplicável; formalizada neste ADR |
| D-07 | Aplicável; formalizada neste ADR |
| D-11 | Aplicável; formalizada neste ADR |
| D-06 | Obrigação futura de Infrastructure; não implementada nem detalhada por este ADR |
| D-08, D-09 | Decisões externas a este ADR; não geram cláusula aqui |

---

## Rastreabilidade

- `src/Business.Platform.Core.Domain/Shared/Repositories/IRepository.cs` (1.0, inalterado).
- `src/Business.Platform.Core.Domain/Shared/Pagination/` (`PaginationRequest`, `PagedResult<T>`).
- `src/Business.Platform.Core.Domain/Shared/Specifications/`.
- `docs/foundation/Repository/`, `docs/foundation/Pagination/`.

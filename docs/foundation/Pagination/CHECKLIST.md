# Checklist — Pagination Foundation (FEATURE-0009)

## Baseline

- [x] Branch `main`, HEAD `fcfd02d`, árvore limpa, nada staged, nenhum dos 12 arquivos pré-existente

## Contrato

- [x] `PaginationRequest` sealed, construtor privado, `Default`, `Create`, `Offset` em `long`, igualdade por valor e operadores
- [x] Códigos `Pagination.PageNumber.LessThanOne`, `Pagination.PageSize.LessThanOne`, `Pagination.PageSize.ExceedsMaximum` (`Validation`), precedência respeitada
- [x] `PagedResult<T>` sealed, construtor privado, sem `IEquatable`, sem interface pública
- [x] `TotalPages` sem overflow; navegação conforme regras; página além do total permitida
- [x] PAG-07: `T[]` privado + `ReadOnlyCollection<T>` exposto como `IReadOnlyList<T>`; sem LINQ
- [x] PAG-08: validação e ordem determinísticas; `Count` lido uma vez; excedente detectado antes do acesso ao buffer; exceções do enumerador propagadas

## Arquitetura

- [x] Allowlist inalterada: `System.Runtime`, `System.Collections`, `System.Linq.Expressions`
- [x] Sem `System.ObjectModel`, `System.Linq`, `System.Text.Json`, ORM, HTTP ou Infrastructure
- [x] `IRepository`, `ISpecification`, `Specification<T>`, ADRs e demais Foundations inalterados

## Qualidade

- [x] Testes: 402 unitários + 178 arquiteturais, 0 falhas, 0 ignorados
- [x] Build Release: 0 avisos, 0 erros; `dotnet format --verify-no-changes` limpo
- [x] Mutation: 75 testados, 70 killed, 1 timeout, 4 survived (1 equivalente, 3 mensagens); score 94,67 % (≥ 90 %)

## Documentação

- [x] Sete documentos em `docs/foundation/Pagination/`; limitações (sem consulta, sem ordenação, offset concorrente, `TotalCount` best-effort, imutabilidade só da coleção) documentadas

## Pendente

- [ ] Auditoria independente (Codex)
- [ ] Commit/publicação (não autorizados nesta feature)

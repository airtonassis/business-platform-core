# Implementação — Auditing

## Arquivos

| Arquivo | Conteúdo |
|---|---|
| `src/Business.Platform.Core.Domain/Shared/Auditing/AuditActorKind.cs` | enum `User = 1`, `System = 2` |
| `src/Business.Platform.Core.Domain/Shared/Auditing/AuditActor.cs` | classe `sealed`, construtor privado, `Create`, igualdade e operadores |
| `src/Business.Platform.Core.Domain/Shared/Auditing/AuditMetadata.cs` | classe `sealed`, construtor privado, `Create`, `WithUpdate`, igualdade estrutural |

## Decisões

- Não herdam `ValueObject`; implementam `IEquatable<T>` explicitamente, como `PaginationRequest`.
- Nenhuma interface pública nova no Domain (sem `IAuditable`).
- Erros construídos com `Error(code, description, ErrorType.Validation)` e retornados via conversão implícita para `Result<T>`.
- `AuditActor.Create` valida na ordem declarada e interrompe no primeiro erro; a verificação de whitespace usa apenas o primeiro e o último caractere (O(1)); controle é verificado por varredura única.
- `AuditMetadata` compara instantes com `==` de `DateTimeOffset` (instante absoluto); como o offset é sempre zero, não há ambiguidade.
- `WithUpdate` constrói nova instância reutilizando `CreatedBy`/`CreatedAt`.
- Somente `System.Runtime` é necessário; a allowlist do Domain (`System.Runtime`, `System.Collections`, `System.Linq.Expressions`) permanece inalterada.

## Limitações

Sem histórico persistido, sem controle de concorrência, sem leitura de relógio, sem resolução de identidade e sem geração de DomainEvents.

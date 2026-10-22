# Auditing Foundation (FEATURE-0010)

## Objetivo

Fornecer, no Domain, tipos imutáveis para registrar **quem** e **quando** criou e atualizou um registro, sem depender de HTTP, ORM, Infrastructure ou bibliotecas externas:

- `AuditActorKind` — natureza do ator (`User = 1`, `System = 2`).
- `AuditActor` — ator identificado por tipo e identificador, com igualdade por valor.
- `AuditMetadata` — dados de criação e da última atualização, com igualdade estrutural.

Todos residem em `Business.Platform.Core.Domain.Shared` (pasta `Shared/Auditing/`).

## Escopo

- Validação do ator e dos instantes via `Result<T>` (`ErrorType.Validation`).
- Instantes obrigatoriamente em UTC (offset zero).
- Invariantes temporais: atualização nunca anterior à criação nem à atualização anterior.
- `WithUpdate` devolve nova instância; a original nunca é alterada.

Fora do escopo: acesso ao relógio, resolução de identidade/autenticação, `IAuditable`, DomainEvents, histórico, soft delete, controle de concorrência, persistência.

## Visão geral

```csharp
var actor = AuditActor.Create(AuditActorKind.User, "user-42");
if (actor.IsFailure) return actor.Error;

var meta = AuditMetadata.Create(actor.Value, createdAtUtc);
if (meta.IsFailure) return meta.Error;

var updated = meta.Value.WithUpdate(actor.Value, updatedAtUtc);
```

## Limitações

- O tipo não lê o relógio: o chamador informa os instantes. Integração futura com `TimeProvider` ocorrerá fora do Domain (ou em feature própria); nada disso está implementado aqui.
- Não há histórico: somente criação e a **última** atualização são mantidas.
- Não há controle de concorrência: duas atualizações concorrentes com instantes válidos são ambas aceitas; a última a persistir prevalece.
- A relação entre `AuditActor` e o sistema de identidade não é verificada.

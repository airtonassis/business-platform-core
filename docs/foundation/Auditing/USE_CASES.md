# Casos de uso — Auditing

## UC-01 — Registrar criação por usuário

```csharp
var actor = AuditActor.Create(AuditActorKind.User, "user-42").Value;
var meta = AuditMetadata.Create(actor, createdAtUtc);
```

## UC-02 — Registrar criação por processo de sistema

```csharp
var actor = AuditActor.Create(AuditActorKind.System, "nightly-import").Value;
var meta = AuditMetadata.Create(actor, createdAtUtc);
```

## UC-03 — Registrar atualização

```csharp
var updated = meta.Value.WithUpdate(actor, updatedAtUtc);
// updated.Value.CreatedBy/CreatedAt preservados; meta.Value inalterado.
```

## UC-04 — Atualizações sucessivas

Cada `WithUpdate` parte da instância anterior; `UpdatedAt` nunca regride. Instantes iguais e o mesmo ator são aceitos.

## UC-05 — Rejeição de dados inválidos

| Entrada | Resultado |
|---|---|
| `AuditActorKind` fora do enum | `Audit.Actor.KindUndefined` |
| Identificador vazio/whitespace | `Audit.Actor.IdRequired` |
| `default` como instante | `Audit.*.NotSet` |
| Offset `+01:00` | `Audit.*.NonUtcOffset` |
| Atualização anterior à criação | `Audit.UpdatedAt.BeforeCreation` |
| Atualização anterior à anterior | `Audit.UpdatedAt.BeforeLastUpdate` |

## UC-06 — Uso futuro com TimeProvider (não implementado)

A camada de aplicação poderá obter o instante via `TimeProvider.GetUtcNow()` e passá-lo a `Create`/`WithUpdate`. O Domain não depende de `TimeProvider`.

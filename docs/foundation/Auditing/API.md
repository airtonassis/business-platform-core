# API — Auditing

Namespace: `Business.Platform.Core.Domain.Shared`.

## AuditActorKind

```csharp
public enum AuditActorKind
{
    User = 1,
    System = 2
}
```

## AuditActor

```csharp
public sealed class AuditActor : IEquatable<AuditActor>
{
    public const int MaxIdLength = 256;

    public AuditActorKind Kind { get; }
    public string Id { get; }

    public static Result<AuditActor> Create(AuditActorKind kind, string? id);

    public bool Equals(AuditActor? other);
    public override bool Equals(object? obj);
    public override int GetHashCode();
    public static bool operator ==(AuditActor? left, AuditActor? right);
    public static bool operator !=(AuditActor? left, AuditActor? right);
}
```

### Erros de `Create` (`ErrorType.Validation`)

| Ordem | Código |
|---|---|
| 1 | `Audit.Actor.KindUndefined` |
| 2 | `Audit.Actor.IdRequired` |
| 3 | `Audit.Actor.IdSurroundingWhitespace` |
| 4 | `Audit.Actor.IdTooLong` |
| 5 | `Audit.Actor.IdInvalidCharacter` |

## AuditMetadata

```csharp
public sealed class AuditMetadata : IEquatable<AuditMetadata>
{
    public AuditActor CreatedBy { get; }
    public DateTimeOffset CreatedAt { get; }
    public AuditActor? UpdatedBy { get; }
    public DateTimeOffset? UpdatedAt { get; }

    public static Result<AuditMetadata> Create(AuditActor createdBy, DateTimeOffset createdAt);
    public Result<AuditMetadata> WithUpdate(AuditActor updatedBy, DateTimeOffset updatedAt);

    public bool Equals(AuditMetadata? other);
    public override bool Equals(object? obj);
    public override int GetHashCode();
    public static bool operator ==(AuditMetadata? left, AuditMetadata? right);
    public static bool operator !=(AuditMetadata? left, AuditMetadata? right);
}
```

Ator nulo em `Create`/`WithUpdate` lança `ArgumentNullException`.

### Erros de `Create` (`ErrorType.Validation`)

| Ordem | Código |
|---|---|
| 1 | `Audit.CreatedAt.NotSet` |
| 2 | `Audit.CreatedAt.NonUtcOffset` |

### Erros de `WithUpdate` (`ErrorType.Validation`)

| Ordem | Código |
|---|---|
| 1 | `Audit.UpdatedAt.NotSet` |
| 2 | `Audit.UpdatedAt.NonUtcOffset` |
| 3 | `Audit.UpdatedAt.BeforeCreation` |
| 4 | `Audit.UpdatedAt.BeforeLastUpdate` |

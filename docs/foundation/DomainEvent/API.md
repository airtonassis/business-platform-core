# API Pública — DomainEvent

**Namespace:** `Business.Platform.Core.Domain.Shared`  
**Assembly:** `Business.Platform.Core.Domain`  
**Arquivo:** `src/Business.Platform.Core.Domain/Shared/DomainEvents/IDomainEvent.cs`  
**Visibilidade:** `public interface`

---

## Contrato Público

```csharp
namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Contrato mínimo de um evento de domínio: registro de um fato ocorrido, com identidade e instante de ocorrência.
/// </summary>
/// <remarks>
/// A interface expõe apenas leitura e não cria nem consulta o relógio: a origem de <see cref="EventId"/> e
/// <see cref="OccurredOnUtc"/> é responsabilidade do código que constrói o evento. Não valida o offset UTC,
/// não garante imutabilidade profunda e não garante, por si só, deduplicação, idempotência ou processamento
/// exatamente uma vez. Eventos concretos podem incluir identificadores de agregados e outros dados de domínio.
/// </remarks>
public interface IDomainEvent
{
    /// <summary>Identificador do evento. A unicidade depende de quem constrói o evento.</summary>
    Guid EventId { get; }

    /// <summary>Instante de ocorrência do fato. Por convenção, com offset UTC; a interface não o valida.</summary>
    DateTimeOffset OccurredOnUtc { get; }
}
```

| Membro | Tipo | Acesso |
|---|---|---|
| `EventId` | `Guid` | somente getter |
| `OccurredOnUtc` | `DateTimeOffset` | somente getter |

Não há métodos, eventos, interfaces-base, genéricos nem atributos.

---

## O que a interface garante (verificado por testes)

- É uma interface pública, não genérica, no namespace e assembly acima.
- Declara exatamente duas propriedades, com os tipos `Guid` e `DateTimeOffset`.
- Não declara setters de nenhuma visibilidade.
- Depende apenas de `System`; não depende de `Entity`, `AggregateRoot`, `Error`, `Result`, infraestrutura, mensageria ou serialização.

## O que a interface não garante

- Offset UTC em tempo de execução: é convenção, não validada.
- Imutabilidade profunda: implementações concretas decidem a própria mutabilidade.
- Unicidade de `EventId`, deduplicação, idempotência ou processamento exatamente uma vez: dependem de mecanismos posteriores.
- Origem do tempo: a interface não cria nem consulta o relógio.

---

## Exemplos de Eventos Concretos

Exemplos ilustrativos; tipos como `CustomerCreated` pertencem ao domínio de quem os define.

```csharp
public sealed record CustomerCreated(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid CustomerId,
    string Email) : IDomainEvent;

public sealed record InvoiceIssued(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    string InvoiceNumber) : IDomainEvent;
```

Eventos podem usar identificadores de agregado de tipos distintos (`Guid`, `int`, `string`, tipos customizados) como dados próprios, sem relação com `AggregateRoot<TId>`.

Construção, com a origem do tempo no código chamador:

```csharp
var domainEvent = new CustomerCreated(Guid.NewGuid(), DateTimeOffset.UtcNow, customerId, "alice@example.com");
```

---

## Compatibilidade

- `Entity<TId>`, `AggregateRoot<TId>`, `Error` e `Result` não foram modificados.
- `AggregateRoot<TId>` não possui membros de eventos.
- `Result<IDomainEvent>` é utilizável como qualquer tipo de referência.

## Fora do escopo

Classe-base de evento, registro/leitura/limpeza de eventos pendentes, handlers, dispatcher, Integration Events, Outbox, mensageria, persistência, serialização e transações.

---

## Nota de Versão

**Versão:** 1.0  
**Status:** Implementado — aguardando auditoria independente do Codex  
**Compatibilidade:** .NET 10

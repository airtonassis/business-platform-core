# Implementação — DomainEvent

**Status:** Implementado — aguardando auditoria independente do Codex.  
**Escopo:** apenas a interface `IDomainEvent`.

---

## Arquivo

`src/Business.Platform.Core.Domain/Shared/DomainEvents/IDomainEvent.cs`

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

## Componentes existentes

`Entity<TId>`, `AggregateRoot<TId>`, `Error` e `Result` permanecem inalterados. `AggregateRoot<TId>` não possui `Events`, `RaiseEvent`, `ClearRaisedEvents` nem armazenamento de eventos.

## Não implementado (fora do escopo)

Classe-base de evento, registro/leitura/limpeza de eventos pendentes, handlers, dispatcher, Integration Events, Outbox, mensageria, persistência, serialização e transações.

## Decisões de implementação

- Namespace `Business.Platform.Core.Domain.Shared`, como os demais componentes da Foundation; o diretório `DomainEvents` apenas organiza o arquivo.
- Sem atributos nem validação: a interface não possui lógica executável.
- Eventos concretos podem ser `record`, classe ou struct, com `init` ou construtor; a interface não impõe a forma.

## Testes

Ver `TESTS.md` (14 unitários, 19 execuções arquiteturais) e `CHECKLIST.md` (resultados de build, testes, formatação e mutation N/A).

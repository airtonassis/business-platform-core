# Regras de Negócio e Invariantes — DomainEvent

Este documento define as regras obrigatórias e invariantes arquiteturais do componente `IDomainEvent`.

---

## Design Decisions Aprovadas (D01–D08)

### D01 — IDomainEvent: Contrato com EventId e OccurredOnUtc

**Decisão:** `IDomainEvent` é interface com duas propriedades obrigatórias.

```csharp
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredOnUtc { get; }
}
```

**Justificativa:**
- Identidade e temporalidade são essenciais para eventos
- Consistência entre todas as implementações
- Flexibilidade: eventos concretos adicionam semântica própria

**Nota:** `AggregateRootId` não está no contrato universal. Eventos concretos incluem identificadores de agregado quando necessário.

---

### D02 — EventId: Obrigatório, Guid

**Decisão:** `EventId` é obrigatório, do tipo `Guid`.

```csharp
Guid EventId { get; }
```

**Justificativa:**
- Cada ocorrência de evento pode ser referenciada por um identificador
- `Guid` é o tipo padrão .NET para identificadores; `Guid.NewGuid()` torna colisões improváveis, mas a interface não impõe nem verifica unicidade
- Rastreamento e logging facilitados

**Importante:** EventId não garante automaticamente deduplicação, idempotência ou processamento exatamente uma vez. Essas propriedades dependem de mecanismos de infraestrutura posteriores.

---

### D03 — OccurredOnUtc: Obrigatório, DateTimeOffset, UTC

**Decisão:** `OccurredOnUtc` é obrigatório, tipo `DateTimeOffset`, sempre UTC.

```csharp
DateTimeOffset OccurredOnUtc { get; }  // offset = 0 (UTC)
```

**Justificativa:**
- `DateTimeOffset` preserva informação de fuso horário
- UTC elimina ambiguidades de tempo
- O instante de ocorrência é informação essencial (não é único: eventos distintos podem compartilhar o mesmo instante)

**Convenção:** o offset esperado é UTC (`TimeSpan.Zero`). A interface não valida nem garante o offset em tempo de execução; essa responsabilidade é do código que constrói o evento.

**Nota:** IDomainEvent não cria nem consulta o relógio. A origem do tempo é responsabilidade do código que constrói o evento.

---

### D04 — Imutabilidade: Registros Estáveis de Fatos

**Decisão:** Eventos são tratados como registros estáveis de fatos. A interface expõe apenas getters; `init` não é exigido universalmente.

```csharp
// Interface — apenas leitura
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredOnUtc { get; }
}

// Implementação — sem setters públicos
public sealed class CustomerCreated : IDomainEvent
{
    public Guid EventId { get; init; }
    public DateTimeOffset OccurredOnUtc { get; init; }
    public Guid CustomerId { get; init; }
    public string Email { get; init; }
}
```

**Justificativa:**
- Eventos registram fatos que ocorreram — fatos não mudam
- Sem imutabilidade, histórico não é confiável

**Nota:** a interface garante apenas getters; imutabilidade profunda não é garantida. `Guid` e `DateTimeOffset` são tipos de valor, portanto as duas propriedades do contrato não são referências mutáveis. O risco de mutabilidade está nas implementações concretas (setters expostos) e nos dados adicionais que elas expõem, como coleções ou objetos mutáveis; a responsabilidade é de cada evento concreto.

---

### D05 — AggregateRoot<TId>: Preservado Integralmente

**Decisão:** `AggregateRoot<TId>` não recebe modificações.

**Justificativa:**
- Ciclo de vida de eventos é responsabilidade de infraestrutura, não foundation
- Gerenciamento de eventos pendentes, coleta, limpeza e publicação pertencem a features futuras
- Simplificar domain events a um contrato puro maximiza flexibilidade

**Implicação:**
- Agregados concretos podem implementar coleta de eventos (fora do escopo desta feature)
- IDomainEvent e AggregateRoot<TId> são completamente independentes

---

### D06 — Ciclo de Vida: Fora do Escopo

**Decisão:** Não implementar nem especificar na foundation:
- Registro/coleta de eventos pendentes
- Leitura de eventos de agregados
- Limpeza de eventos
- Publicação ou confirmação

**Justificativa:**
- Essas responsabilidades dependem de padrões de infraestrutura (Outbox, handlers, etc.)
- Deixar espaço para decisões futuras sem comprometer o contrato

**Implicação:** Esta feature é apenas o contrato de `IDomainEvent`. Como agregados gerenciam eventos é responsabilidade de features futuras ou de implementações concretas.

---

### D07 — Independência: Domínio vs Infraestrutura

**Decisão:** `IDomainEvent` é completamente independente de:
- Integration Events
- Mensageria (MediatR, NServiceBus, RabbitMQ)
- Handlers ou dispatchers
- Entity Framework Core
- Outbox pattern
- Serialização (JSON, XML, Protobuf)
- Transações
- Logging ou observabilidade

**Justificativa:**
- Domain Events são conceito de domínio, não de infraestrutura
- Transformação para Integration Events é responsabilidade de adapter
- Publicação é responsabilidade de camada de infraestrutura

**Verificação:** Testes de arquitetura (NetArchTest) validam ausência de dependências proibidas.

---

### D08 — Testes: Contrato Aprovado

**Decisão:** Matriz de testes focada no contrato `IDomainEvent`:
- Interface pública e namespace
- Propriedades `EventId` e `OccurredOnUtc`
- Tipos das propriedades
- Ausência de setters na interface
- Implementações concretas de exemplo
- Ausência de dependências indevidas

**Nota:** Não exigir mutation score de 90% quando não houver mutantes elegíveis. Registrar N/A quando apropriado.

---

## RN-DOMEVENT-01 — Contrato de IDomainEvent

`IDomainEvent` é uma interface pública com exatamente duas propriedades:

```csharp
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredOnUtc { get; }
}
```

Implementações concretas devem implementar ambas as propriedades.

---

## RN-DOMEVENT-02 — Invariante: EventId

- `EventId` é tipo `Guid`
- `EventId` é obrigatório (sem nullable)
- `EventId` deve identificar a ocorrência; a unicidade não é verificada pela interface e depende de quem constrói o evento
- Geração: responsabilidade do código que cria o evento (tipicamente `Guid.NewGuid()`)

**Nota:** Guid.Empty é valor válido tecnicamente (não é null), mas semanticamente pode ser inválido. Validação pertence ao domínio.

---

## RN-DOMEVENT-03 — Invariante: OccurredOnUtc

- `OccurredOnUtc` é tipo `DateTimeOffset`
- `OccurredOnUtc` é obrigatório (sem nullable)
- Offset deve estar em UTC (`TimeSpan.Zero`)
- Representa o momento exato de ocorrência do evento

**Como garantir UTC:**
```csharp
// Correto
var @event = new CustomerCreated
{
    OccurredOnUtc = DateTimeOffset.UtcNow,  // Automatically UTC
    ...
};

// Correto
var @event = new CustomerCreated
{
    OccurredOnUtc = new DateTimeOffset(2026, 10, 13, 14, 30, 0, TimeSpan.Zero),
    ...
};

// Evitar
var @event = new CustomerCreated
{
    OccurredOnUtc = DateTimeOffset.Now,  // Local time — verificar offset
    ...
};
```

**Nota:** IDomainEvent não valida offset. Responsabilidade de agregados/domain services.

---

## RN-DOMEVENT-04 — Invariante: Imutabilidade

- Propriedades devem ser `{ get; init; }` ou `{ get; private set; }` (setter apenas no construtor)
- Sem setters públicos ou protegidos
- Interface expõe apenas getters

**Verificação:** teste arquitetural valida que a interface não declara setters. Implementações concretas não são restringidas pela interface, e a imutabilidade profunda não é garantida.

---

## RN-DOMEVENT-05 — Invariante: Independência de AggregateRoot

- `IDomainEvent` não depende de `AggregateRoot<TId>`
- `AggregateRoot<TId>` não possui membros para gerenciar eventos
- Agregados concretos podem implementar coleta de eventos (fora do escopo)

**Implicação:** Eventos e agregados são conceitos ortogonais nesta feature.

---

## RN-DOMEVENT-06 — Invariante: Sem Infraestrutura

- Sem dependências em EF Core, MediatR, logging, serialização
- Sem atributos de serialização (JsonPropertyName, XmlAttribute, etc.)
- Sem interfaces de handlers ou publicadores
- Sem chamadas a relógio ou IO

**Verificação:** Testes de arquitetura (NetArchTest) validam.

---

## RN-DOMEVENT-07 — Namespace e Localização

`IDomainEvent` reside em `Business.Platform.Core.Domain.Shared`:

```
src/Business.Platform.Core.Domain/Shared/DomainEvents/IDomainEvent.cs
```

Implementações concretas residem em seus bounded contexts:

```
src/Business.Platform.Core.Domain/Customers/Events/CustomerCreated.cs
src/Business.Platform.Core.Domain/Orders/Events/OrderCreated.cs
```

---

## RN-DOMEVENT-08 — Validação de Construção

Criação de eventos inválidos:

- `EventId == Guid.Empty` → **Aceito** (não é null; validação semântica do domínio)
- `EventId == null` → **Impossível** (Guid não é nullable)
- `OccurredOnUtc` com offset não-UTC → **Aceito pela interface** (responsabilidade de agregado validar)
- `OccurredOnUtc` com valor inválido → **Impossível** (DateTimeOffset valida internamente)

---

## RN-DOMEVENT-09 — Compatibilidade

`IDomainEvent` é compatível com:
- Entity<TId> (nenhuma interação)
- AggregateRoot<TId> (nenhuma interação)
- Error (ortogonal)
- Result<T> (Result<IDomainEvent> compila e funciona)
- Testes (xUnit, Shouldly, NetArchTest)

Nenhum componente existente é modificado.

---

## RN-DOMEVENT-10 — Fora do Escopo

Esta feature **não inclui**:
- Classe-base de evento ou qualquer implementação além do contrato `IDomainEvent`
- Ciclo de vida (coleta, limpeza, confirmação)
- Handlers ou dispatchers
- Publicação ou bus
- Outbox pattern
- Integração com AggregateRoot
- Serialização
- Rastreamento de versões

Essas responsabilidades pertencem a features futuras ou infraestrutura.

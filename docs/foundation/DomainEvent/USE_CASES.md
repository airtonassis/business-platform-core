# Exemplos de Uso — DomainEvent

Cenários representativos de utilização de `IDomainEvent`. Tipos como `CustomerCreated` e `CustomerErrors` são ilustrativos e pertencem ao domínio de quem os define. Os exemplos assumem nullable habilitado e warnings como erros.

---

## 1. Definição de Evento Concreto

Um `record` com construtor primário compila com nullable habilitado, pois todas as propriedades são inicializadas pelo construtor.

```csharp
public sealed record CustomerCreated(
    Guid EventId,
    DateTimeOffset OccurredOnUtc,
    Guid CustomerId,
    string Email,
    string Name) : IDomainEvent;
```

Alternativa com classe e propriedades obrigatórias:

```csharp
public sealed class CustomerCreatedEvent : IDomainEvent
{
    public required Guid EventId { get; init; }
    public required DateTimeOffset OccurredOnUtc { get; init; }
    public required Guid CustomerId { get; init; }
    public required string Email { get; init; }
}
```

A interface não impõe `record`, `init` ou `required`; a forma é decisão do evento concreto.

---

## 2. Criação de Evento

A origem do tempo e do identificador é do código que constrói o evento.

```csharp
var domainEvent = new CustomerCreated(
    Guid.NewGuid(),
    DateTimeOffset.UtcNow,
    customerId,
    "alice@example.com",
    "Alice");
```

---

## 3. Evento Retornado por uma Operação de Domínio

`AggregateRoot<TId>` não possui mecanismo de eventos. Enquanto não houver decisão arquitetural sobre eventos pendentes, o evento é um valor construído e devolvido por quem o produz.

```csharp
public static Result<CustomerCreated> RegisterCustomer(Guid customerId, string email, string name)
{
    if (string.IsNullOrWhiteSpace(email))
        return CustomerErrors.EmailRequired;

    return Result.Success(new CustomerCreated(
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        customerId,
        email,
        name));
}
```

## 3.1 Identificadores de Agregado de Tipos Distintos

```csharp
public sealed record AccountOpened(Guid EventId, DateTimeOffset OccurredOnUtc, int AccountNumber) : IDomainEvent;
public sealed record InvoiceIssued(Guid EventId, DateTimeOffset OccurredOnUtc, string InvoiceNumber) : IDomainEvent;
```

---

## 4. Testes

```csharp
[Fact]
public void CustomerCreated_ShouldPreserveIdentityAndTime()
{
    var id = Guid.NewGuid();
    var occurredOn = new DateTimeOffset(2026, 10, 13, 14, 30, 0, TimeSpan.Zero);

    IDomainEvent domainEvent = new CustomerCreated(id, occurredOn, Guid.NewGuid(), "alice@example.com", "Alice");

    domainEvent.EventId.ShouldBe(id);
    domainEvent.OccurredOnUtc.ShouldBe(occurredOn);
    domainEvent.OccurredOnUtc.Offset.ShouldBe(TimeSpan.Zero);
}

[Fact]
public void Event_CanBeUsedAsResultValue()
{
    IDomainEvent domainEvent = new CustomerCreated(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), "alice@example.com", "Alice");

    var result = Result.Success(domainEvent);

    result.IsSuccess.ShouldBeTrue();
}
```

---

## 5. Pontos de Atenção

### Mutar evento por meio da interface

```csharp
domainEvent.EventId = Guid.NewGuid();  // Erro de compilação: a interface só expõe getters
```

A interface não impede que uma implementação concreta exponha setters ou dados mutáveis; evitá-los é decisão do evento concreto.

### OccurredOnUtc fora de UTC

```csharp
OccurredOnUtc = DateTimeOffset.Now,     // offset local; a interface não valida
OccurredOnUtc = DateTimeOffset.UtcNow,  // offset UTC (preferido)
```

### EventId padrão

Um `Guid.Empty` não é rejeitado pela interface. Gerar o identificador (por exemplo, `Guid.NewGuid()`) é responsabilidade de quem constrói o evento.

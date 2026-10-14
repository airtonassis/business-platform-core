# Especificação da API Pública — AggregateRoot

## Resumo

`AggregateRoot<TId>` é uma classe abstrata que herda integralmente de `Entity<TId>` e adiciona a semântica de raiz de agregado.

**Namespace:** `Business.Platform.Core.Domain.Shared`  
**Assembly:** `Business.Platform.Core.Domain`  
**Arquivo:** `src/Business.Platform.Core.Domain/Shared/AggregateRoots/AggregateRoot.cs`  
**Visibilidade:** `public abstract`  

---

## Contrato Público Completo

```csharp
namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Classe base de raízes de agregado. Herda identidade, igualdade e hash de <see cref="Entity{TId}"/>.
/// </summary>
/// <typeparam name="TId">Tipo do identificador da raiz do agregado.</typeparam>
/// <remarks>
/// Não adiciona membros nem comportamento. Regras de agregados concretos, como invariantes,
/// encapsulamento de entidades filhas e multitenancy, pertencem às subclasses.
/// </remarks>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    /// <summary>
    /// Inicializa a raiz do agregado com seu identificador.
    /// </summary>
    /// <param name="id">Identificador da raiz do agregado.</param>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> é <see langword="null"/>.</exception>
    protected AggregateRoot(TId id) : base(id)
    {
    }

    // Herda de Entity<TId>:
    // public TId Id { get; }
    // public bool Equals(Entity<TId>? other)
    // public sealed override bool Equals(object? obj)
    // public sealed override int GetHashCode()
    // public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    // public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
}
```

---

## Membros Herdados de Entity<TId>

### Propriedade

| Membro | Assinatura | Descrição |
|---|---|---|
| `Id` | `public TId Id { get; }` | Identificador do agregado, definido na construção e sem setter |

### Métodos

| Membro | Assinatura | Descrição |
|---|---|---|
| `Equals` (tipado) | `public bool Equals(Entity<TId>? other)` | Compara agregados pelo tipo concreto e `Id` |
| `Equals` (object) | `public sealed override bool Equals(object? obj)` | Sealed override; delega à versão tipada |
| `GetHashCode` | `public sealed override int GetHashCode()` | Sealed override; usa tipo e `Id` |

### Operadores (Herdados)

| Membro | Assinatura | Descrição |
|---|---|---|
| `operator ==` | `public static bool operator ==(Entity<TId>? left, Entity<TId>? right)` | Igualdade herdada de `Entity<TId>`; trata nulidade corretamente |
| `operator !=` | `public static bool operator !=(Entity<TId>? left, Entity<TId>? right)` | Desigualdade herdada; inverso de `==` |

Nota:

- `Equals(object?)` e `GetHashCode()` são `sealed override` em `Entity<TId>`: o compilador impede que subclasses os sobrescrevam.
- C# permite declarar novos operadores `==` e `!=` em subclasses (operadores são estáticos e não virtuais: um operador novo em subclasse seria escolhido pelo compilador conforme os tipos estáticos dos operandos, produzindo comparações inconsistentes). A arquitetura da FEATURE-0004 proíbe essa redefinição; o teste arquitetural verifica que `AggregateRoot<TId>` não os declara e as regras de uso devem ser observadas pelos agregados concretos.
- A igualdade permanece baseada em tipo concreto e `Id`.

---

## Invariantes Garantidos

1. **Identidade não-nula:** `Id` é validado no construtor; nunca pode ser null
2. **Id sem setter:** `Id` não pode ser substituído após a construção. Isso não torna automaticamente imutáveis o objeto identificador (se for um tipo de referência mutável) nem o estado das subclasses
3. **Igualdade por tipo e identidade:** dois agregados são iguais se e somente se são do mesmo tipo concreto e possuem o mesmo `Id`
4. **Hash consistente:** se `a == b`, então `a.GetHashCode() == b.GetHashCode()`
5. **Referências nulas tratadas:** `null == null` é `true`; `aggregate == null` é `false`

---

## Padrões de Uso Esperados

### 1. Definição de Agregado Concreto

```csharp
public sealed class Order : AggregateRoot<Guid>
{
    private readonly List<OrderLine> _lines = new();

    public string CustomerEmail { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    private Order(Guid id, string customerEmail)
        : base(id)
    {
        CustomerEmail = customerEmail;
    }

    public static Result<Order> Create(Guid id, string customerEmail)
    {
        if (string.IsNullOrWhiteSpace(customerEmail))
            return Result.Failure<Order>(OrderErrors.InvalidEmail);

        return Result.Success(new Order(id, customerEmail));
    }

    public void AddLine(OrderLine line)
    {
        if (_lines.Any(l => l.Id == line.Id))
            throw new ArgumentException("Line already exists");

        _lines.Add(line);
    }
}
```

### 2. Agregado com Identificador Value Object

```csharp
public sealed class CustomerId : IEquatable<CustomerId>
{
    public Guid Value { get; }

    public CustomerId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("CustomerId cannot be empty");

        Value = value;
    }

    public override bool Equals(object? obj) => Equals(obj as CustomerId);
    public bool Equals(CustomerId? other) => other is not null && Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(CustomerId? left, CustomerId? right) =>
        left?.Equals(right) ?? right is null;
    public static bool operator !=(CustomerId? left, CustomerId? right) => !(left == right);
}

public sealed class Customer : AggregateRoot<CustomerId>
{
    public string Name { get; private set; }

    private Customer(CustomerId id, string name)
        : base(id)
    {
        Name = name;
    }

    public static Customer Create(Guid idValue, string name)
    {
        return new Customer(new CustomerId(idValue), name);
    }
}
```

A igualdade de `Customer` usa `EqualityComparer<CustomerId>.Default`; `CustomerId` deve manter igualdade e hash estáveis (D05 de Entity).

### 3. Agregado com Entidades Filhas

```csharp
public sealed class OrderLine : Entity<Guid>
{
    public Guid OrderId { get; }
    public int Quantity { get; }

    public OrderLine(Guid id, Guid orderId, int quantity)
        : base(id)
    {
        OrderId = orderId;
        Quantity = quantity;
    }
}

public sealed class Order : AggregateRoot<Guid>
{
    private readonly List<OrderLine> _lines = new();

    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    private Order(Guid id)
        : base(id)
    {
    }

    public void AddLine(OrderLine line)
    {
        if (line.OrderId != Id)
            throw new ArgumentException("Line must belong to this order");

        _lines.Add(line);
    }
}
```

### 4. Comparação de Agregados

```csharp
var order1 = Order.Create(orderId, "customer@example.com").Value;
var order2 = Order.Create(orderId, "customer@example.com").Value;

// Igualdade é por tipo e ID, não por estado interno
bool areEqual = order1 == order2;  // true (mesmo tipo, mesmo ID)

// Uso em coleções
var orders = new HashSet<Order> { order1, order2 };
Console.WriteLine(orders.Count);  // 1 (deduplicação)
```

### 5. Agregados em Domain Service

```csharp
public class PlaceOrderService
{
    public Result Execute(string customerEmail, List<OrderLine> lines)
    {
        var orderResult = Order.Create(Guid.NewGuid(), customerEmail);

        if (orderResult.IsFailure)
            return Result.Failure(orderResult.Error);

        var order = orderResult.Value;

        foreach (var line in lines)
        {
            order.AddLine(line);
        }

        // Repository ou persistência seria chamado aqui (fora deste serviço)
        return Result.Success();
    }
}
```

---

## Restrições de Implementação

### Não Permitido

- ❌ Instanciar `AggregateRoot<TId>` diretamente (é `abstract`)
- ❌ Mutar `Id` após construção (sem setter; herdado de `Entity<TId>`)
- ❌ Herdar diretamente de `AggregateRoot<TId>` sem tipo específico (recomendação: sempre forneça `TId` concreto)
- ❌ Utilizar `AggregateRoot<TId?>` (nulidade proibida por restrição genérica)
- ❌ Sobrescrever `Equals` ou `GetHashCode` em subclasses (são `sealed override` de `Entity<TId>`)
- ❌ Redefinir `operator ==` ou `operator !=` em subclasses (herdados)
- ❌ Adicionar `Events` ou `Reconstitute` a esta abstração
- ❌ Impor padrão de `TenantId` (agregados concretos o implementam se necessário)
- ❌ Depender de infraestrutura (ORM, repository, serialização, etc.)

### Permitido

- ✅ Herdar de `AggregateRoot<TId>` com qualquer `TId : notnull`
- ✅ Definir propriedades imutáveis em subclasses
- ✅ Implementar validação de domínio em subclasses (via exceção ou `Result<T>`)
- ✅ Encapsular entidades filhas (via `IReadOnlyList`, métodos, ou nenhuma exposição)
- ✅ Adicionar `TenantId` ou outras propriedades específicas do domínio concreto
- ✅ Utilizar `AggregateRoot<TId>` como parâmetro de coleções (`List<Order>`)
- ✅ Comparar agregados com `==`, `!=`, `Equals` (igualdade herdada)
- ✅ Utilizar em `HashSet<AggregateRoot<TId>>`, `Dictionary<AggregateRoot<TId>, TValue>`
- ✅ Retornar agregados de factory methods que retornam `Result<T>` (recomendado para falhas esperadas)

---

## Notas de Versão

**Versão:** 1.0  
**Status:** Implementation Complete — Auditoria Codex Pendente  
**Namespace:** `Business.Platform.Core.Domain.Shared`  
**Arquivo:** `src/Business.Platform.Core.Domain/Shared/AggregateRoots/AggregateRoot.cs`  
**Depend:** `Entity<TId>` (obrigatório)  
**Compatibilidade:** .NET 10

---

## Compatibilidade com Entity<TId>

Um agregado é uma entidade especial. Agregados podem:

- Ser comparados com `Entity<TId>` do mesmo `TId` (tipos concretos diferentes nunca são iguais)
- Ser armazenados em `List<Entity<TId>>`
- Usar métodos de `Entity<TId>`

Exemplo:

```csharp
AggregateRoot<Guid> order = Order.Create(Guid.NewGuid(), "customer@example.com").Value;
Entity<Guid> entity = order;  // Upcasting funciona

// SimpleEntity é ilustrativo: uma subclasse concreta de Entity<Guid>
bool equal = order == new SimpleEntity(order.Id);  // false (tipos concretos diferentes)
```


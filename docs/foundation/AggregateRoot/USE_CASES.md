# Exemplos de Uso — AggregateRoot

Este documento apresenta cenários representativos de utilização do componente `AggregateRoot<TId>` na construção de agregados de domínio.

Os exemplos demonstram padrões e não substituem as regras definidas em `BUSINESS_RULES.md`.

Todos os exemplos utilizam a `AggregateRoot<TId>` implementada em `src/Business.Platform.Core.Domain/Shared/AggregateRoots/AggregateRoot.cs` (namespace `Business.Platform.Core.Domain.Shared`). O contrato completo está em `API.md`.

---

## 1. Agregado Simples — Order (com Guid)

Um agregado que contém linhas de pedido.

```csharp
public sealed class OrderLine : Entity<Guid>
{
    public Guid OrderId { get; }
    public int Quantity { get; }
    public decimal UnitPrice { get; }

    public OrderLine(Guid id, Guid orderId, int quantity, decimal unitPrice)
        : base(id)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive");

        OrderId = orderId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}

public sealed class Order : AggregateRoot<Guid>
{
    private readonly List<OrderLine> _lines = new();

    public string CustomerEmail { get; private set; }
    public decimal TotalAmount { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    private Order(Guid id, string customerEmail)
        : base(id)
    {
        CustomerEmail = customerEmail;
        TotalAmount = 0m;
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
            throw new ArgumentException("Line already exists in order");

        _lines.Add(line);
        TotalAmount += line.Quantity * line.UnitPrice;
    }

    public void RemoveLine(Guid lineId)
    {
        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line == null)
            throw new ArgumentException("Line not found");

        _lines.Remove(line);
        TotalAmount -= line.Quantity * line.UnitPrice;
    }
}

// Uso
var orderId = Guid.NewGuid();
var result = Order.Create(orderId, "customer@example.com");

if (result.IsSuccess)
{
    var order = result.Value;
    var lineId = Guid.NewGuid();
    var line = new OrderLine(lineId, orderId, 5, 29.99m);
    order.AddLine(line);
    
    Console.WriteLine($"Order {order.Id} has {order.Lines.Count} lines");
}
```

---

## 2. Agregado com Value Objects

Um agregado que usa value objects para representar conceitos.

```csharp
public sealed class Address : IEquatable<Address>
{
    public string Street { get; }
    public string City { get; }
    public string PostalCode { get; }

    public Address(string street, string city, string postalCode)
    {
        Street = street ?? throw new ArgumentNullException(nameof(street));
        City = city ?? throw new ArgumentNullException(nameof(city));
        PostalCode = postalCode ?? throw new ArgumentNullException(nameof(postalCode));
    }

    public override bool Equals(object? obj) => Equals(obj as Address);
    public bool Equals(Address? other) =>
        other is not null && Street == other.Street && City == other.City && PostalCode == other.PostalCode;
    public override int GetHashCode() => HashCode.Combine(Street, City, PostalCode);
}

public sealed class Customer : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public string Email { get; private set; }
    public Address Address { get; private set; }

    private Customer(Guid id, string name, string email, Address address)
        : base(id)
    {
        Name = name;
        Email = email;
        Address = address;
    }

    public static Result<Customer> Create(Guid id, string name, string email, Address address)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Customer>(CustomerErrors.NameRequired);

        return Result.Success(new Customer(id, name, email, address));
    }

    public void UpdateAddress(Address newAddress)
    {
        if (newAddress == null)
            throw new ArgumentNullException(nameof(newAddress));

        Address = newAddress;
    }
}

// Uso
var customerId = Guid.NewGuid();
var address = new Address("123 Main St", "Springfield", "12345");
var result = Customer.Create(customerId, "John Doe", "john@example.com", address);
```

---

## 3. Agregado Aninhado — Team com Members

Um agregado com múltiplas entidades filhas que compartilham lógica.

```csharp
public sealed class TeamMember : Entity<Guid>
{
    public Guid TeamId { get; }
    public string Role { get; }

    public TeamMember(Guid id, Guid teamId, string role)
        : base(id)
    {
        TeamId = teamId;
        Role = role ?? throw new ArgumentNullException(nameof(role));
    }
}

public sealed class Team : AggregateRoot<Guid>
{
    private readonly List<TeamMember> _members = new();
    
    public string Name { get; private set; }
    public IReadOnlyList<TeamMember> Members => _members.AsReadOnly();

    private Team(Guid id, string name)
        : base(id)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public static Team Create(Guid id, string name)
    {
        return new Team(id, name);
    }

    public void AddMember(TeamMember member)
    {
        if (_members.Any(m => m.Id == member.Id))
            throw new ArgumentException("Member already in team");

        if (member.TeamId != Id)
            throw new ArgumentException("Member must belong to this team");

        _members.Add(member);
    }
}

// Uso
var teamId = Guid.NewGuid();
var team = Team.Create(teamId, "Engineering");

var memberId = Guid.NewGuid();
var member = new TeamMember(memberId, teamId, "Lead");
team.AddMember(member);
```

---

## 4. Agregado com Identificador Customizado

Um agregado que usa um Value Object como `TId`.

```csharp
public sealed class ProjectId : IEquatable<ProjectId>
{
    public Guid Value { get; }

    public ProjectId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("ProjectId cannot be empty");

        Value = value;
    }

    public override bool Equals(object? obj) => Equals(obj as ProjectId);
    public bool Equals(ProjectId? other) => other is not null && Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(ProjectId? left, ProjectId? right) =>
        left?.Equals(right) ?? right is null;
    public static bool operator !=(ProjectId? left, ProjectId? right) => !(left == right);
}

public sealed class Project : AggregateRoot<ProjectId>
{
    public string Name { get; private set; }

    private Project(ProjectId id, string name)
        : base(id)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public static Project Create(Guid idValue, string name)
    {
        var id = new ProjectId(idValue);
        return new Project(id, name);
    }
}

// Uso
var projectId = new ProjectId(Guid.NewGuid());
var project = Project.Create(projectId.Value, "Platform Migration");
```

---

## 5. Comparação de Agregados

Demonstra igualdade de agregados baseada em identidade.

```csharp
var orderId = Guid.NewGuid();

var order1 = Order.Create(orderId, "customer@example.com").Value;
order1.AddLine(new OrderLine(Guid.NewGuid(), orderId, 5, 29.99m));

var order2 = Order.Create(orderId, "customer@example.com").Value;
// order2 está vazio (sem linhas)

// Igualdade é baseada em ID e tipo concreto, não em estado interno
bool areEqual = order1 == order2;  // true — mesmo tipo e mesmo Id

// Uso em coleções
var orders = new HashSet<Order> { order1, order2 };
Console.WriteLine(orders.Count);  // 1 — agregados iguais são removidos por deduplicação
```

---

## 6. Domain Service Usando Agregados

Um serviço de domínio que orquestra múltiplos agregados.

```csharp
public class AssignMemberToTeamService
{
    public Result Execute(Team team, TeamMember member)
    {
        // Validar invariantes do domínio
        if (team.Members.Any(m => m.Id == member.Id))
            return Result.Failure(TeamErrors.MemberAlreadyAssigned);

        // Modificar o agregado atomicamente
        team.AddMember(member);

        return Result.Success();
    }
}

// Uso
var service = new AssignMemberToTeamService();
var result = service.Execute(team, member);
```

---

## 7. Uso Incorreto (Antipadrões)

### Tentar modificar entidade filha de fora (se exposta via IReadOnlyList)

```csharp
var order = Order.Create(orderId, "customer@example.com").Value;
var line = new OrderLine(Guid.NewGuid(), orderId, 5, 29.99m);
order.AddLine(line);

// Se Lines retorna IReadOnlyList:
// ERRADO — não fazer isso
// order.Lines[0].Quantity = 10;  // Erro: propriedade somente leitura

// CORRETO — remover e adicionar via métodos do agregado
order.RemoveLine(line.Id);
var newLine = new OrderLine(line.Id, orderId, 10, line.UnitPrice);
order.AddLine(newLine);
```

**Nota:** o encapsulamento de `_lines` é decisão do agregado concreto. Se `Lines` não for exposto, não há via externa de modificação.

### Confundir agregados em coleções

```csharp
// Dois agregados diferentes com IDs diferentes
var order1 = Order.Create(Guid.NewGuid(), "customer1@example.com").Value;
var order2 = Order.Create(Guid.NewGuid(), "customer2@example.com").Value;

// HashSet<Order> vai manter ambos — IDs são diferentes
var orders = new HashSet<Order> { order1, order2 };
Console.WriteLine(orders.Count);  // 2 — ambos estão presentes
```

### Tentar mutar a identidade do agregado

```csharp
// ERRADO — AggregateRoot<TId> não permite mudar de agregado
// order.Id = newOrderId;  // Erro: Id não tem setter

// Se um agregado se torna inválido ou precisa ser reciclado,
// a nova instância é criada e persistida separadamente.
// Recarregamento de um agregado salvo pertence a infraestrutura (Repository).
```


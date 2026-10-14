# Exemplos de Uso — Entity

Este documento apresenta cenários representativos de utilização do componente `Entity<TId>` na Shared Foundation e nas camadas de domínio.

Os exemplos demonstram intenção de uso e não substituem as regras definidas em `BUSINESS_RULES.md`.

Todos os exemplos utilizam a `Entity<TId>` implementada em `src/Business.Platform.Core.Domain/Shared/Entities/Entity.cs` (namespace `Business.Platform.Core.Domain.Shared`). A classe-base não é redefinida aqui; o contrato completo está em `API.md`. Tipos como `UserErrors` e `Employee` são ilustrativos e pertencem ao domínio de quem os utiliza.

---

## 1. Entidade com Identificador Guid

Padrão comum para entidades em agregados modernos.

```csharp
public sealed class User : Entity<Guid>
{
    public string Email { get; private set; }
    public string Name { get; private set; }

    public User(Guid id, string email, string name)
        : base(id)
    {
        Email = email ?? throw new ArgumentNullException(nameof(email));
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}

// Uso
var userId = Guid.NewGuid();
var user = new User(userId, "alice@example.com", "Alice");
var userId2 = userId;
var user2 = new User(userId2, "bob@example.com", "Bob");

// Igualdade
bool areEqual = user == user2;  // true — mesmo tipo, mesmo Id
```

---

## 2. Entidade com Identificador Inteiro

Padrão comum em sistemas legados ou com chaves numéricas.

```csharp
public sealed class Product : Entity<int>
{
    public string Name { get; private set; }
    public decimal Price { get; private set; }

    public Product(int id, string name, decimal price)
        : base(id)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Price = price >= 0 ? price : throw new ArgumentException("Price must be non-negative");
    }
}

// Uso
var product1 = new Product(1, "Widget", 9.99m);
var product2 = new Product(1, "Gadget", 15.50m);

// Igualdade
bool areEqual = product1 == product2;  // true — mesmo tipo, mesmo Id
// Nota: Name e Price não afetam igualdade
```

---

## 3. Identificador Value Object Customizado

Padrão DDD para identidades tipadas.

```csharp
public sealed class UserId : IEquatable<UserId>
{
    public Guid Value { get; }

    public UserId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty");

        Value = value;
    }

    public override bool Equals(object? obj) => Equals(obj as UserId);
    public bool Equals(UserId? other) => other is not null && Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(UserId? left, UserId? right) => left?.Equals(right) ?? right is null;
    public static bool operator !=(UserId? left, UserId? right) => !(left == right);
}

public sealed class User : Entity<UserId>
{
    public string Email { get; private set; }

    public User(UserId id, string email)
        : base(id)
    {
        Email = email ?? throw new ArgumentNullException(nameof(email));
    }
}

// Uso
var userId = new UserId(Guid.NewGuid());
var user = new User(userId, "alice@example.com");
var userId2 = new UserId(userId.Value);
var user2 = new User(userId2, "bob@example.com");

// Igualdade
bool areEqual = user == user2;  // true — mesmo UserId
```

---

## 4. Herança Hierárquica

Duas entidades concretas distintas que compartilham o mesmo tipo de identificador. A igualdade, o hash e os operadores são herdados de `Entity<TId>` e não podem ser sobrescritos (`Equals(object?)` e `GetHashCode()` são `sealed override`).

```csharp
public sealed class Admin : Entity<Guid>
{
    public string Role { get; private set; }

    public Admin(Guid id, string role)
        : base(id)
    {
        Role = role ?? throw new ArgumentNullException(nameof(role));
    }
}

public sealed class User : Entity<Guid>
{
    public string Email { get; private set; }

    public User(Guid id, string email)
        : base(id)
    {
        Email = email ?? throw new ArgumentNullException(nameof(email));
    }
}

// Uso
var id = Guid.NewGuid();
var user = new User(id, "alice@example.com");
var admin = new Admin(id, "SuperAdmin");

// Igualdade (o operador == compara Entity<Guid>; tipos concretos diferentes nunca são iguais)
bool areEqual = user == admin;  // false — tipos concretos diferentes
```

---

## 5. Relacionamentos em Entidades Concretas

Padrão correto: relacionamentos na entidade concreta, não na base.

```csharp
public sealed class Organization : Entity<Guid>
{
    public string Name { get; private set; }

    // Relacionamentos definidos aqui, não na Entity<TId> base
    private readonly List<Guid> _memberIds = new();
    public IReadOnlyList<Guid> MemberIds => _memberIds.AsReadOnly();

    public Organization(Guid id, string name)
        : base(id)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public void AddMember(Guid memberId)
    {
        if (!_memberIds.Contains(memberId))
            _memberIds.Add(memberId);
    }
}

// Uso
var orgId = Guid.NewGuid();
var org1 = new Organization(orgId, "Acme Corp");
org1.AddMember(Guid.NewGuid());

var org2 = new Organization(orgId, "Acme Corp");
// org2 tem MemberIds vazio

// Igualdade
bool areEqual = org1 == org2;  // true — mesmo tipo, mesmo Id
// Nota: MemberIds não afetam igualdade
```

---

## 6. Validação de Identidade no Domínio

Padrão: Entity<TId> valida nulidade, domínio valida semântica.

```csharp
public sealed class Project : Entity<Guid>
{
    public string Name { get; private set; }

    public Project(Guid id, string name)
        : base(id)  // Valida que id != null
    {
        // Entity<Guid> aceita Guid.Empty (é não-nulo)
        // Validação de domínio pertence à subclasse:
        if (id == Guid.Empty)
            throw new ArgumentException("Project ID cannot be empty", nameof(id));

        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}

// Uso
try
{
    var project = new Project(Guid.Empty, "My Project");  // Lança ArgumentException (validação de domínio)
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Erro de domínio: {ex.Message}");
}

var validId = Guid.NewGuid();
var project = new Project(validId, "My Project");  // OK
```

---

## 7. Incompatibilidade de Tipos

Demonstra que tipos concretos diferentes não são nunca iguais.

```csharp
public sealed class Account : Entity<string>
{
    public Account(string iban)
        : base(iban ?? throw new ArgumentNullException(nameof(iban)))
    {
        if (string.IsNullOrWhiteSpace(iban))
            throw new ArgumentException("IBAN cannot be empty");
    }
}

public sealed class Wallet : Entity<string>
{
    public Wallet(string address)
        : base(address ?? throw new ArgumentNullException(nameof(address)))
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Address cannot be empty");
    }
}

// Uso
var account = new Account("DE89370400440532013000");
var wallet = new Wallet("DE89370400440532013000");

// Igualdade
bool areEqual = account == wallet;  // false — tipos concretos diferentes
```

---

## 8. Composição em Domain Services

Padrão de uso com múltiplas entidades.

```csharp
public class CreateUserService
{
    public Result<User> Execute(Guid userId, string email)
    {
        // Validação
        if (string.IsNullOrWhiteSpace(email))
            return UserErrors.EmailRequired;

        // Criação
        var user = new User(userId, email);

        // Retorno de Result<Entity>
        return Result.Success(user);
    }
}

// Uso
var service = new CreateUserService();
var result = service.Execute(Guid.NewGuid(), "alice@example.com");

if (result.IsSuccess)
{
    var user = result.Value;  // User (Value só é acessado após verificar IsSuccess)
    Console.WriteLine($"User created: {user.Id}");
}
```

---

## 9. Collections de Entidades

Padrão de retorno em casos de uso.

```csharp
public Result<IEnumerable<User>> GetUsersByDomain(string domain)
{
    var users = new List<User>
    {
        new(Guid.NewGuid(), "alice@domain.com"),
        new(Guid.NewGuid(), "bob@domain.com")
    };

    return Result.Success<IEnumerable<User>>(users);
}

// Uso
var result = GetUsersByDomain("example.com");
if (result.IsSuccess)
{
    foreach (var user in result.Value)
    {
        Console.WriteLine($"User: {user.Id}");
    }
}
```

---

## 10. Uso Incorreto (Antipadrões)

### Tentar mudar Id

```csharp
var user = new User(Guid.NewGuid(), "alice@example.com");

// Isto NÃO é possível — Id não tem setter público ou protegido
// user.Id = Guid.NewGuid();  // Erro de compilação

// Contrato de Entity<TId> impede modificação pública de identidade
```

### Definir relacionamentos na base

`Entity<TId>` não deve receber `TenantId`, `OrganizationId`, `ParentId`, `OwnerId` nem coleções de navegação. Eles pertencem às entidades concretas:

```csharp
public sealed class Department : Entity<Guid>
{
    public Guid TenantId { get; private set; }
    private readonly List<Employee> _employees = new();
    public IReadOnlyList<Employee> Employees => _employees.AsReadOnly();

    public Department(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }
}
```

### Usar default(TId) sem validação

```csharp
// Entity<TId> aceita default(TId) se for não-nulo
// Para Guid, default é Guid.Empty (não-nulo)
var user = new User(Guid.Empty, "alice@example.com");  // user.Id == Guid.Empty

// Solução: validação em subclasse se semanticamente inválido
public sealed class User : Entity<Guid>
{
    public string Email { get; private set; }

    public User(Guid id, string email)
        : base(id)  // Valida que id != null
    {
        // Validação semântica: Guid.Empty pode ser inválido no domínio
        if (id == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty", nameof(id));

        Email = email ?? throw new ArgumentNullException(nameof(email));
    }
}
```

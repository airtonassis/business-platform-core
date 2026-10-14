# API Pública — Entity

Especificação completa da API pública do componente `Entity<TId>`.

---

## Namespace

```csharp
Business.Platform.Core.Domain.Shared
```

---

## Classe Abstrata: Entity<TId>

### Genérico

```csharp
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
```

A restrição `where TId : notnull` oferece verificações de nulidade em compilação, garantindo que apenas tipos não-nuláveis possam ser utilizados como identificadores. Validação em runtime permanece obrigatória no construtor.

---

### Propriedades

#### Id

```csharp
/// <summary>
/// Identificador único e imutável da entidade.
/// Uma entidade é definida primariamente por sua identidade.
/// </summary>
public TId Id { get; }
```

- **Tipo:** `TId` (genérico, não-nulo)
- **Acesso:** Somente leitura (getter público, sem setter)
- **Garantia:** Nunca é `null` após construção
- **Imutabilidade:** Não pode ser alterado após instanciação
- **Validação:** Construtor garante que `Id != null`

---

### Construtores

#### Constructor (Protegido)

```csharp
/// <summary>
/// Inicializa uma entidade com um identificador.
/// </summary>
/// <param name="id">Identificador único (não pode ser null).</param>
/// <exception cref="ArgumentNullException">Se id é null.</exception>
/// <remarks>
/// Protegido: somente subclasses podem chamar.
/// Validações de domínio específicas devem ser implementadas na subclasse.
/// Exemplo: se Guid.Empty é inválido no seu domínio, valide em User.
/// </remarks>
protected Entity(TId id)
```

- **Visibilidade:** `protected`
- **Parâmetros:** `TId id` — identificador da entidade
- **Exceções:**
  - `ArgumentNullException` se `id == null`
- **Comportamento:** Inicializa a propriedade `Id`
- **Uso:** Chamado por subclasses via `base(id)`

**Exemplo de Uso:**

```csharp
public sealed class User : Entity<Guid>
{
    public string Email { get; }

    private User(Guid id, string email)
        : base(id)  // Chama construtor protegido
    {
        Email = email ?? throw new ArgumentNullException(nameof(email));
    }

    public static User Create(Guid id, string email)
    {
        return new User(id, email);
    }
}

public sealed class Admin : Entity<Guid>
{
    public string Role { get; }

    private Admin(Guid id, string role)
        : base(id)
    {
        Role = role ?? throw new ArgumentNullException(nameof(role));
    }

    public static Admin Create(Guid id, string role)
    {
        return new Admin(id, role);
    }
}

// Uso (os exemplos desta página usam User.Create/Admin.Create, que retornam a própria entidade)
var user = User.Create(Guid.NewGuid(), "alice@example.com");
```

---

### Métodos de Igualdade

#### Equals(object?)

```csharp
/// <summary>
/// Determina se esta entidade é igual a outro objeto.
/// Duas entidades são iguais se e somente se:
/// 1. São do mesmo tipo concreto;
/// 2. Possuem o mesmo Id.
/// </summary>
/// <param name="obj">Objeto a comparar (pode ser null).</param>
/// <returns>true se as entidades são iguais; false caso contrário.</returns>
/// <remarks>
/// Implementação necessária para respeitar contrato de Object.Equals.
/// Nenhum outro atributo ou relacionamento participa da igualdade.
/// Comparação de tipos concretos garante que User(id: 1) ≠ Admin(id: 1).
/// Método é sealed override — não pode ser sobrescrito em subclasses.
/// </remarks>
public sealed override bool Equals(object? obj)
```

- **Parâmetros:** `object? obj` — objeto a comparar (pode ser nulo)
- **Retorno:** `bool` — true se iguais, false caso contrário
- **Comportamento:**
  1. Se `obj` não é do tipo `Entity<TId>`, retorna `false`
  2. Se tipos concretos diferem (`GetType() != other.GetType()`), retorna `false`
  3. Compara `Id` usando `EqualityComparer<TId>.Default.Equals(Id, other.Id)`
  4. Se `obj` é null, retorna `false`
- **Exemplos:**

```csharp
var user1 = User.Create(Guid.NewGuid(), "alice@example.com");
var user2 = User.Create(user1.Id, "bob@example.com");
bool equal = user1.Equals(user2);  // true (mesmo tipo, mesmo Id)

var admin = Admin.Create(user1.Id, "SuperAdmin");
bool equal = user1.Equals(admin);  // false (tipos diferentes)

bool equal = user1.Equals(null);  // false
```

---

#### Equals(Entity<TId>?) — IEquatable<Entity<TId>>

```csharp
/// <summary>
/// Sobrecarga tipada de Equals para comparação sem boxing.
/// Implementação de IEquatable&lt;Entity&lt;TId&gt;&gt;.
/// </summary>
/// <param name="other">Outra entidade (pode ser null).</param>
/// <returns>true se as entidades são iguais; false caso contrário.</returns>
/// <remarks>
/// Preferir esta sobrecarga a Equals(object?) quando o tipo é conhecido,
/// para evitar boxing e melhorar performance.
/// Método não é virtual — implementação é fixa.
/// </remarks>
public bool Equals(Entity<TId>? other)
```

- **Parâmetros:** `Entity<TId>? other` — outra entidade (pode ser null)
- **Retorno:** `bool`
- **Comportamento:**
  1. Se `other` é null, retorna `false`
  2. Se tipos concretos diferem, retorna `false`
  3. Compara `EqualityComparer<TId>.Default.Equals(Id, other.Id)`
- **Exemplos:**

```csharp
Entity<Guid>? entity1 = User.Create(id, "alice@example.com");
Entity<Guid>? entity2 = User.Create(id, "bob@example.com");
bool equal = entity1.Equals(entity2);  // true

Entity<Guid>? nullEntity = null;
bool equal = entity1.Equals(nullEntity);  // false
```

---

#### GetHashCode()

```csharp
/// <summary>
/// Retorna código hash da entidade.
/// Baseado em tipo concreto e identificador.
/// </summary>
/// <returns>Código hash (int).</returns>
/// <remarks>
/// Consistente com Equals: se Equals(a, b) é true, GetHashCode(a) == GetHashCode(b).
/// Necessário para uso em HashSet, Dictionary, etc.
/// Usa EqualityComparer&lt;TId&gt;.Default.GetHashCode(Id) para o hash do identificador.
/// Método é sealed override — não pode ser sobrescrito em subclasses.
/// </remarks>
public sealed override int GetHashCode()
```

- **Retorno:** `int` — código hash
- **Garantia:** Consistência com `Equals`
  - Se `entity1 == entity2`, então `entity1.GetHashCode() == entity2.GetHashCode()`
- **Comportamento:** Combina hash do tipo concreto e do `Id`
- **Exemplos:**

```csharp
var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
var user1 = User.Create(id, "alice@example.com");
var user2 = User.Create(id, "bob@example.com");

int hash1 = user1.GetHashCode();
int hash2 = user2.GetHashCode();
bool sameHash = hash1 == hash2;  // true

// Uso em HashSet
var users = new HashSet<Entity<Guid>> { user1, user2 };
int count = users.Count;  // 1 (deduplicação funciona)
```

---

### Operadores

#### operator ==(Entity<TId>?, Entity<TId>?)

```csharp
/// <summary>
/// Operador de igualdade.
/// </summary>
/// <param name="left">Entidade à esquerda (pode ser null).</param>
/// <param name="right">Entidade à direita (pode ser null).</param>
/// <returns>true se as entidades são iguais; false caso contrário.</returns>
/// <remarks>
/// Trata nulidade corretamente: null == null → true, null == entity → false.
/// Quando left não é null, delega à igualdade tipada left.Equals(right):
/// mesmo tipo concreto e EqualityComparer&lt;TId&gt;.Default.Equals(Id, other.Id).
/// </remarks>
public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
```

- **Parâmetros:**
  - `left` — entidade à esquerda (referência, pode ser null)
  - `right` — entidade à direita (referência, pode ser null)
- **Retorno:** `bool`
- **Comportamento:**
  1. Se `left` é null, retorna `right is null`
  2. Caso contrário, retorna `left.Equals(right)` (igualdade tipada, `Equals(Entity<TId>?)`)
  3. Não utiliza `==` internamente nem `object.Equals(left, right)`, portanto não há recursão
- **Exemplos:**

```csharp
var user1 = User.Create(id1, "alice@example.com");
var user2 = User.Create(id1, "bob@example.com");
bool equal = user1 == user2;  // true

bool equal = user1 == null;  // false
bool equal = null == user1;  // false
bool equal = (User?)null == (User?)null;  // true
```

---

#### operator !=(Entity<TId>?, Entity<TId>?)

```csharp
/// <summary>
/// Operador de desigualdade.
/// </summary>
/// <param name="left">Entidade à esquerda (pode ser null).</param>
/// <param name="right">Entidade à direita (pode ser null).</param>
/// <returns>true se as entidades não são iguais; false caso contrário.</returns>
/// <remarks>
/// Inverso lógico de operator == (retorna !(left == right)); mesma verificação de nulidade e igualdade tipada.
/// </remarks>
public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
```

- **Parâmetros:** Mesmos que `operator ==`
- **Retorno:** `bool`
- **Comportamento:** `!(left == right)`
- **Exemplos:**

```csharp
var user1 = User.Create(id1, "alice@example.com");
var user2 = User.Create(id2, "bob@example.com");
bool notEqual = user1 != user2;  // true

bool notEqual = user1 != null;  // true
bool notEqual = null != user1;  // true
bool notEqual = (User?)null != (User?)null;  // false
```

---

## Padrões de Uso Esperados

### 1. Definição de Entidade Concreta com Factory que Retorna Result

A validação semântica (por exemplo, `Guid.Empty`) pertence ao domínio, não à `Entity<TId>`. Tipos como `CustomerErrors` são ilustrativos.

```csharp
public sealed class Customer : Entity<Guid>
{
    public string Email { get; }
    public string Name { get; }

    private Customer(Guid id, string email, string name)
        : base(id)
    {
        Email = email;
        Name = name;
    }

    public static Result<Customer> Create(Guid id, string email, string name)
    {
        if (id == Guid.Empty)
            return CustomerErrors.InvalidId;

        if (string.IsNullOrWhiteSpace(email))
            return CustomerErrors.EmailRequired;

        return Result.Success(new Customer(id, email, name));
    }
}

// Uso: a entidade só é obtida após verificar o sucesso
var result = Customer.Create(Guid.NewGuid(), "alice@example.com", "Alice");

if (result.IsSuccess)
{
    Customer customer = result.Value;
    Console.WriteLine(customer.Id);
}
else
{
    Console.WriteLine(result.Error.Code);
}
```

### 2. Comparação de Identidades

```csharp
var currentUser = User.Create(userId, "alice@example.com");
var loadedUser = User.Create(userId, "alice.updated@example.com");

if (currentUser == loadedUser)
{
    Console.WriteLine("Mesma entidade");
}
```

### 3. Uso em Collections

```csharp
var users = new HashSet<Entity<Guid>>
{
    User.Create(userId1, "alice@example.com"),
    User.Create(userId1, "bob@example.com")  // Mesmo Id, removido por deduplicação
};

Console.WriteLine(users.Count);  // 1

var userDict = new Dictionary<Entity<Guid>, string>();
userDict[currentUser] = "Administrador";
// Busca posterior com entidade equivalente funciona
```

### 4. Validação de Domain Service

```csharp
public class AssignUserToTeamService
{
    public Result Execute(Team team, User user)
    {
        // Verificar que o usuário já não pertence ao time
        foreach (var memberId in team.MemberIds)
        {
            var member = LoadUserById(memberId);
            if (member == user)  // Utiliza operador ==
                return Result.Failure(TeamErrors.UserAlreadyMember);
        }

        team.AddMember(user.Id);
        return Result.Success();
    }
}
```

---

## Restrições de Implementação

### Não Permitido

- ❌ Criar instâncias diretamente de `Entity<TId>` (é `abstract`)
- ❌ Mutar `Id` após construção (sem setter)
- ❌ Utilizar `Entity<TId?>` (a restrição `notnull` sinaliza nulidade em compilação; a validação em runtime ocorre no construtor)
- ❌ Sobrescrever `Equals(object?)` ou `GetHashCode()` em subclasses (são `sealed override`; não compila)
- ❌ Adicionar relacionamentos (`TenantId`, `ParentId`, navegações) à `Entity<TId>`

### Permitido

- ✅ Herdar de `Entity<TId>` com qualquer `TId : notnull`
- ✅ Definir propriedades imutáveis adicionais em subclasses
- ✅ Implementar validação de domínio em subclasses
- ✅ Utilizar `Entity<TId>` como parâmetro de coleções (`List<Entity<Guid>>`)
- ✅ Comparar entidades com `==`, `!=`, `Equals`
- ✅ Utilizar em `HashSet<Entity<TId>>`, `Dictionary<Entity<TId>, TValue>`

---

## Nota de Versão

**Versão:** 1.0  
**Status:** Implementation Complete — Architecture/QA Gate Pending  
**Namespace:** `Business.Platform.Core.Domain.Shared`  
**Arquivo:** `src/Business.Platform.Core.Domain/Shared/Entities/Entity.cs`

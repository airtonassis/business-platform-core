# Detalhes de Implementação — Entity

## Status

**Implementation Complete — Architecture/QA Gate Pending**

- **Implementation:** ✅ `src/Business.Platform.Core.Domain/Shared/Entities/Entity.cs`
- **Unit Tests:** ✅ `tests/UnitTests/Domain/Shared/Entities/EntityTests.cs` — contagem e evidências em `CHECKLIST.md`
- **Architecture Tests:** ✅ `tests/ArchitectureTests/Domain/Shared/Entities/EntityArchitectureTests.cs` — contagem e evidências em `CHECKLIST.md`
- **Build / Format / Mutation Score:** resultados reportados em `CHECKLIST.md` (seção Execução Registrada)
- **Auditoria Codex / Architecture-QA Gate / Product Owner:** ⏳ PENDENTES

---

## Estrutura da Classe Abstrata

A classe `Entity<TId>` é uma classe abstrata que fornece a base para todas as entidades de domínio. O arquivo-fonte é a referência definitiva; o contrato público está especificado em `API.md`.

### Implementação Efetiva

```csharp
namespace Business.Platform.Core.Domain.Shared;

public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    protected Entity(TId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        Id = id;
    }

    public TId Id { get; }

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);

    public bool Equals(Entity<TId>? other) =>
        other is not null
        && GetType() == other.GetType()
        && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public sealed override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    public sealed override int GetHashCode() =>
        HashCode.Combine(GetType(), EqualityComparer<TId>.Default.GetHashCode(Id));
}
```

Os comentários XML do arquivo-fonte foram omitidos acima por brevidade.

---

## Invariantes de Implementação

### I-ENT-01: Identidade Não-Nula

O construtor protegido valida que `id` não é nulo (`ArgumentNullException.ThrowIfNull(id)`, `ParamName == "id"`) antes de atribuir `Id`. A restrição `where TId : notnull` oferece verificação em compilação; a validação em runtime permanece obrigatória.

**Verificação:** `EntityTests.Constructor_WithNullId_ShouldThrowArgumentNullException`.

---

### I-ENT-02: Propriedade Id Sem Setter

`Id` é declarada como `public TId Id { get; }`: getter público e nenhum setter de qualquer visibilidade.

**Verificação:** `EntityArchitectureTests.Id_ShouldHavePublicGetterAndNoSetterOfAnyVisibility` (`GetSetMethod(nonPublic: true) == null`).

---

### I-ENT-03: Igualdade por Tipo e Id

`Equals(Entity<TId>?)` retorna `true` somente se:

1. `other` não é nulo;
2. `GetType() == other.GetType()` (tipo concreto de runtime idêntico);
3. `EqualityComparer<TId>.Default.Equals(Id, other.Id)`.

`Equals(object?)` delega a `Equals(obj as Entity<TId>)`; objetos que não são `Entity<TId>` resultam em `false`.

**Verificação:** testes unitários de mesma instância, mesmo tipo e mesmo `Id`, `Id` diferente, tipos concretos diferentes, `null`, objeto não-entidade e `Id` de tipo diferente.

---

### I-ENT-04: Consistência de Equals e GetHashCode

Se `a.Equals(b)` é `true`, então `a.GetHashCode() == b.GetHashCode()`. O hash combina o tipo concreto e o hash do identificador (`EqualityComparer<TId>.Default.GetHashCode(Id)`). Colisões entre entidades diferentes são permitidas; apenas a consistência com a igualdade é exigida.

**Verificação:** `EntityTests.GetHashCode_*`, `HashSet_*`, `Dictionary_*`, `Distinct_*`.

---

### I-ENT-05: Operadores == e != Consistentes

`==` retorna `right is null` quando `left` é nulo e `left.Equals(right)` nos demais casos. `!=` retorna `!(left == right)`. Não há recursão nem uso de `object.Equals(left, right)`.

**Verificação:** `EntityTests.OperatorEquality_*`, `OperatorInequality_*` e `Equality_ShouldBeConsistentAcrossEqualsOperatorsAndTypedEquals`.

---

### I-ENT-06: Classe Abstrata Não-Selada com Métodos de Igualdade Lacrados

`Entity<TId>` é `public abstract` e não é `sealed`. `Equals(object?)` e `GetHashCode()` são `sealed override`, portanto subclasses não podem alterar a semântica de igualdade nem de hash. `Equals(Entity<TId>?)` é a implementação implícita de `IEquatable<Entity<TId>>`: o compilador a emite como `virtual final`, sem possibilidade de sobrescrita.

**Verificação:** `EntityArchitectureTests.Entity_ShouldBeAbstractAndNotSealed`, `Equals_Object_ShouldBeSealedOverride`, `GetHashCode_ShouldBeSealedOverride`, `Equals_Typed_ShouldNotBeVirtual`, `Entity_ShouldImplementIEquatable`.

---

### I-ENT-07: Sem Dependências de Infraestrutura

Nenhuma dependência de ORM, banco de dados, logging, cache, mensageria, ASP.NET Core, serialização, nem de `Error` ou `Result`. A única dependência de assembly além de `System.Runtime` é `System.Collections` (`EqualityComparer<TId>`), incluída na política de assemblies permitidos pela decisão ADR-0002 (identificador histórico: ADR-ENT-006).

**Verificação:** `EntityArchitectureTests.Entity_ShouldNotDependOnForbiddenLayersOrFrameworks`, `Entity_ShouldNotDependOnErrorOrResult` e o teste preexistente `ErrorArchitectureTests.DomainAssembly_ShouldReferenceOnlyAllowedAssemblies`.

---

## Contrato de Herança

Espera-se que subclasses concretas:

1. Herdem de `Entity<TId>` com um `TId` específico (herança simples);
2. Sejam `sealed`, a menos que a hierarquia seja intencionalmente aberta;
3. Chamem `base(id)` no construtor para inicializar a identidade;
4. Controlem a instanciação (construtor privado com factory, ou construtor público quando adequado);
5. Validem a semântica da identidade segundo o domínio (por exemplo, rejeitar `Guid.Empty`);
6. Não tentem sobrescrever `Equals(object?)` nem `GetHashCode()` (são `sealed override`).

```csharp
public sealed class User : Entity<Guid>
{
    public string Email { get; }

    private User(Guid id, string email)
        : base(id)
    {
        Email = email;
    }

    // Factory que retorna Result; a entidade só é obtida após verificar o sucesso
    public static Result<User> Create(Guid id, string email)
    {
        if (id == Guid.Empty)
            return UserErrors.InvalidId;

        if (string.IsNullOrWhiteSpace(email))
            return UserErrors.EmailRequired;

        return Result.Success(new User(id, email));
    }
}
```

`UserErrors` é ilustrativo e pertence ao domínio do consumidor.

---

## Decisões Arquiteturais Documentadas

### D01: Identidade Genérica

`TId : notnull` permite flexibilidade de tipos de identificador com verificações de nulidade em compilação.

**Alternativa Considerada:** um `EntityId` global que envolve qualquer tipo.  
**Decisão:** tipos específicos por bounded context, conforme DDD. Sem `EntityId` global.

### D02: Sem Relacionamentos Obrigatórios

`Entity<TId>` não define `TenantId`, `OrganizationId`, navegações, etc.

**Razão:** maximizar reutilização; bounded contexts distintos têm estruturas de relacionamento distintas.  
**Trade-off:** subclasses declaram os relacionamentos de que precisam.

### D03: Igualdade por Tipo Concreto

Dois tipos concretos diferentes (`User` e `Admin`) com o mesmo `Id` não são iguais.

**Razão:** semântica de domínio.  
**Trade-off:** a igualdade não pode ser customizada por subclasses (`sealed override`).

### D04: Sem Validação de default(TId)

`Entity<TId>` não rejeita `Guid.Empty`, `0`, `""`.

**Razão:** a validade semântica pertence ao tipo de identificador ou ao domínio.  
**Trade-off:** subclasses devem validar explicitamente se esses valores forem inválidos.

### D05: Estabilidade do Identificador

A propriedade `Id` não pode ser substituída após a construção. A Foundation não garante imutabilidade profunda de identificadores de referência e não faz cópias defensivas.

**Razão:** permitir `TId` de referência sem impor cópias genéricas.  
**Trade-off:** tipos personalizados usados como `TId` devem manter estáveis a igualdade e o hash durante a vida da entidade.

### ADR-0002: `System.Collections` na política de assemblies

Identificador histórico: ADR-ENT-006. Formalizada em `docs/architecture/decisions/ADR-0002-entity-system-collections.md`.

O contrato aprovado exige `EqualityComparer<TId>.Default`, cujo tipo pertence a `System.Collections`. A lista `AllowedReferencedAssemblies` de `ErrorArchitectureTests` passou a aceitar `System.Runtime` e `System.Collections`, preservando a verificação do public key token da Microsoft. Nenhum outro assembly é autorizado por essa decisão. Ver também `Error/IMPLEMENTATION.md` e `CHECKLIST.md`.

---

## Ambiguidades e Esclarecimentos

### A1: Herança Múltipla de Entidades

C# não admite herança múltipla de classes. Uma entidade concreta herda de uma única classe; para combinar comportamentos, use composição.

### A2: Tipos Concretos Diferentes com Mesmo TId

`user == admin` (ambos `Entity<Guid>` com o mesmo `Id`) é `false`, pois os tipos concretos de runtime diferem. Subclasses não podem alterar esse comportamento porque `Equals(object?)` e `GetHashCode()` são `sealed override`.

Uma subclasse de uma entidade concreta (por exemplo, `SuperUser : User`) também não é igual a uma instância de `User` com o mesmo `Id`, pois `GetType()` difere.

### A3: Identificadores Personalizados

A igualdade e o hash do `Id` seguem `EqualityComparer<TId>.Default`. Structs (inclusive `record struct`) possuem igualdade por valor padrão, enquanto classes sem `Equals`/`GetHashCode` apropriados usam igualdade por referência. Tipos personalizados devem definir ou possuir semântica apropriada de igualdade e hash, estável durante a vida da entidade (D05).

---

## Estrutura de Testes

Ver `TESTS.md` para a estratégia, a matriz de requisitos e a rastreabilidade entre cenários e testes executáveis. Contagens e resultados de execução estão em `CHECKLIST.md`.

---

## Fora do Escopo

- Persistência ou mapeamento ORM
- Auditoria, timestamps, soft delete
- Eventos de domínio
- AggregateRoot
- Tenant/Organization
- Serialização
- Qualquer dependência de infraestrutura

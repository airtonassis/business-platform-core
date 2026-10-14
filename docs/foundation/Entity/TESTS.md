# Estratégia de Testes e Cobertura — Entity

## Status da Implementação dos Testes

- **Testes unitários de Entity implementados:** 58 (`tests/UnitTests/Domain/Shared/Entities/EntityTests.cs`) — 58 aprovados.
- **Testes arquiteturais de Entity implementados:** 17 (`tests/ArchitectureTests/Domain/Shared/Entities/EntityArchitectureTests.cs`) — 17 aprovados.
- **Totais da suíte (com Error e Result):** 171 testes unitários e 54 testes arquiteturais, todos aprovados.
- **Matriz abaixo:** preservada como **matriz de requisitos** (68 cenários). A quantidade de testes executáveis difere da quantidade de cenários porque um teste pode cobrir mais de um cenário e há testes adicionais fora da matriz. A cobertura efetiva está na seção **Rastreabilidade**.
- Evidências de execução (mutation, build, formatação): ver `CHECKLIST.md`.

## Matriz Completa de Cenários de Testes Unitários

Cada teste deve ser robusto, documentado e medir comportamento específico de invariantes.

---

## Seção A: Construção (Constructor)

### A1: Constructor_With_Valid_Guid_Id_Should_Set_Id

- Construtor `protected` com `Guid id` válido (não-nulo)
- Resultado: `entity.Id` equals `id`
- Implementação: Subclasse concreta para testar (ex: `TestEntity<Guid>`)

### A2: Constructor_With_Valid_Int_Id_Should_Set_Id

- Construtor com `int id` válido
- Resultado: `entity.Id` equals `id`

### A3: Constructor_With_Valid_String_Id_Should_Set_Id

- Construtor com `string id` válido (não-vazio)
- Resultado: `entity.Id` equals `id`

### A4: Constructor_With_Null_Id_Should_Throw_ArgumentNullException

- Construtor com `id = null`
- Resultado: Lança `ArgumentNullException`
- Mensagem contém "null" ou "ID" para clareza

### A5: Constructor_With_Custom_ValueObject_Id_Should_Set_Id

- Construtor com `TId` como Value Object (ex: `UserId`)
- Resultado: `entity.Id` equals `id`

---

## Seção B: Propriedade Id (Imutabilidade)

### B1: Id_Property_Is_ReadOnly

- Verifica que `Id` não possui setter público
- Teste de reflexão: `PropertyInfo.CanWrite == false` ou setter é inacessível
- Resultado: Nenhuma maneira pública de mutar `Id`

### B2: Id_Property_Has_No_Setter

- Verifica que propriedade `Id` não possui setter em qualquer nível de visibilidade
- Teste de reflexão: `PropertyInfo.GetSetMethod(nonPublic: true) == null`
- Resultado: Propriedade é somente leitura (getter apenas, nenhum setter)

---

## Seção C: Igualdade com Equals(object?)

### C1: Equals_Same_Instance_Should_Return_True

- `entity.Equals(entity)`
- Resultado: `true`

### C2: Equals_Different_Instance_Same_Type_Same_Id_Should_Return_True

- `entity1.Equals(entity2)` onde ambos são `User(id: 123)`
- Resultado: `true`

### C3: Equals_Different_Instance_Same_Type_Different_Id_Should_Return_False

- `entity1.Equals(entity2)` onde `User(id: 123)` vs `User(id: 456)`
- Resultado: `false`

### C4: Equals_Different_Types_Same_Id_Should_Return_False

- `user.Equals(admin)` onde ambos têm `id = 123`
- Resultado: `false` (tipos concretos diferentes)

### C5: Equals_With_Null_Should_Return_False

- `entity.Equals(null)`
- Resultado: `false`

### C6: Equals_With_Non_Entity_Object_Should_Return_False

- `entity.Equals("not an entity")`
- Resultado: `false`

### C7: Equals_With_Entity_Different_Generic_Type_Should_Return_False

- `Entity<Guid>`.Equals(`Entity<int>`)
- Resultado: `false` (tipos genéricos diferentes)

---

## Seção D: Igualdade Tipada (Equals(Entity<TId>?))

### D1: Equals_Typed_Same_Instance_Should_Return_True

- `entity.Equals((Entity<Guid>)entity)`
- Resultado: `true`

### D2: Equals_Typed_Different_Instance_Same_Type_Same_Id_Should_Return_True

- `user1.Equals((Entity<Guid>)user2)` com mesmo `Id`
- Resultado: `true`

### D3: Equals_Typed_Null_Should_Return_False

- `entity.Equals((Entity<TId>?)null)`
- Resultado: `false`

### D4: Equals_Typed_Different_Concrete_Type_Should_Return_False

- `user1.Equals((Entity<Guid>)admin)` com mesmo `Id`
- Resultado: `false`

---

## Seção E: GetHashCode

### E1: GetHashCode_Should_Be_Consistent

- Chamar `entity.GetHashCode()` múltiplas vezes
- Resultado: Sempre retorna o mesmo valor

### E2: GetHashCode_Same_Type_Same_Id_Should_Return_Same_Hash

- `user1.GetHashCode() == user2.GetHashCode()` onde ambos têm mesmo tipo e `Id`
- Resultado: `true`

### E3: GetHashCode_Different_Id_Entries_In_HashSet

- Adicionar entidades com `Id` diferentes a `HashSet<Entity<Guid>>`
- Resultado: Ambas permanecem no set (hashes podem diferir ou colidir, não é validado)
- Nota: Consistência com `Equals` é obrigatória; diferentes hashes são melhor prática mas não requeridos

### E4: GetHashCode_Consistency_After_Multiple_Calls

- Chamar `GetHashCode()` múltiplas vezes na mesma entidade
- Resultado: Sempre retorna mesmo valor (não muda durante vida da entidade)

### E5: GetHashCode_Should_Work_In_HashSet

- Adicionar duas entidades com mesmo `Id` a `HashSet<Entity<Guid>>`
- Resultado: `HashSet.Count == 1` (deduplicação funciona)

### E6: GetHashCode_Should_Work_In_Dictionary

- Usar entidades como chaves de `Dictionary<Entity<Guid>, string>`
- Resultado: Busca por entidade equivalente retorna valor correto

---

## Seção F: Operador == (Igualdade)

### F1: Operator_Equal_Same_Instance_Should_Return_True

- `entity == entity`
- Resultado: `true`

### F2: Operator_Equal_Different_Instance_Same_Type_Same_Id_Should_Return_True

- `user1 == user2` com mesmo `Id`
- Resultado: `true`

### F3: Operator_Equal_Different_Id_Should_Return_False

- `user1 == user2` com `Id` diferentes
- Resultado: `false`

### F4: Operator_Equal_Different_Types_Should_Return_False

- `user == admin` com mesmo `Id`
- Resultado: `false`

### F5: Operator_Equal_Null_Left_Should_Return_False

- `(Entity<Guid>?)null == entity`
- Resultado: `false`

### F6: Operator_Equal_Null_Right_Should_Return_False

- `entity == (Entity<Guid>?)null`
- Resultado: `false`

### F7: Operator_Equal_Both_Null_Should_Return_True

- `(Entity<Guid>?)null == (Entity<Guid>?)null`
- Resultado: `true`

---

## Seção G: Operador != (Desigualdade)

### G1: Operator_NotEqual_Same_Instance_Should_Return_False

- `entity != entity`
- Resultado: `false`

### G2: Operator_NotEqual_Different_Id_Should_Return_True

- `user1 != user2` com `Id` diferentes
- Resultado: `true`

### G3: Operator_NotEqual_Different_Types_Should_Return_True

- `user != admin` com mesmo `Id`
- Resultado: `true`

### G4: Operator_NotEqual_Null_Left_Should_Return_True

- `(Entity<Guid>?)null != entity`
- Resultado: `true`

### G5: Operator_NotEqual_Both_Null_Should_Return_False

- `(Entity<Guid>?)null != (Entity<Guid>?)null`
- Resultado: `false`

---

## Seção H: Reflexão e Contrato de Tipo

### H1: Class_Should_Be_Abstract

- Verifica que a classe é `abstract`
- Não pode ser instanciada diretamente (`new Entity<Guid>()` não compila)
- Teste: NetArchTest ou reflexão valida `TypeInfo.IsAbstract == true`

### H2: Class_Should_Not_Be_Sealed

- Verifica que a classe NÃO é `sealed` (permite herança)
- Teste de reflexão: `TypeInfo.IsSealed == false`

### H3: Constructor_Should_Be_Protected

- Verifica que o construtor é `protected` e não `public`
- Teste de reflexão: Construtor possui `MethodAttributes.Family` ou equivalente

### H4: Class_Should_Implement_IEquatable

- Verifica que a classe implementa `IEquatable<Entity<TId>>`
- Teste de reflexão: `typeof(Entity<Guid>).GetInterfaces().Contains(typeof(IEquatable<Entity<Guid>>))`

### H5: Id_Property_Should_Exist_And_Be_ReadOnly

- Verifica que existe propriedade `Id` com getter público
- Verifica que `Id` não possui setter público ou protegido
- Teste de reflexão: `PropertyInfo.CanRead == true`, `PropertyInfo.CanWrite == false`

### H6: Equals_Should_Be_Sealed_Override

- Verifica que `Equals(object?)` é `sealed override`
- Teste de reflexão: Método está marcado com `MethodAttributes.Final` e `MethodAttributes.Virtual`
- Não pode ser sobrescrito em subclasses

### H7: GetHashCode_Should_Be_Sealed_Override

- Verifica que `GetHashCode()` é `sealed override`
- Teste de reflexão: Método está marcado com `MethodAttributes.Final` e `MethodAttributes.Virtual`
- Não pode ser sobrescrito em subclasses

### H8: Equals_Typed_Should_Not_Be_Virtual

- Verifica que `Equals(Entity<TId>?)` não pode ser sobrescrito
- Por ser a implementação implícita de `IEquatable<Entity<TId>>`, o compilador a emite como `virtual final` (`newslot`): a reflexão pode mostrar `IsVirtual == true`, mas `IsFinal == true` e o método não admite sobrescrita
- Teste de reflexão: `!IsVirtual || IsFinal` (em C#, o método não é declarado `virtual` nem `override`)
- Implementação é fixa

### H9: Operators_Equality_And_Inequality_Should_Be_Defined

- Verifica que operadores `==` e `!=` estão definidos
- Teste de reflexão: Métodos `op_Equality` e `op_Inequality` existem

---

## Seção I: Propriedades de Igualdade (Reflexividade, Simetria, Transitividade)

### I1: Equals_Should_Be_Reflexive

- `entity.Equals(entity)` é sempre `true`
- Reflexividade: um objeto é igual a si mesmo

### I2: Equals_Should_Be_Symmetric

- Se `entity1.Equals(entity2)` é `true`, então `entity2.Equals(entity1)` é `true`
- Não há ordenação — igualdade é bidirecional

### I3: Equals_Should_Be_Transitive

- Se `entity1.Equals(entity2)` e `entity2.Equals(entity3)`, então `entity1.Equals(entity3)`
- Composição de igualdades preserva resultado

### I4: Equals_Consistency_With_Operator

- `entity1.Equals(entity2)` deve retornar mesmo resultado que `entity1 == entity2`
- `Equals` e operador `==` são consistentes

---

## Seção J: Cenários de Domain-Driven Design

### J1: Entity_Should_Work_With_Guid_Id

- Cria `TestEntity<Guid>` com `Guid.NewGuid()`
- Resultado: Propriedades funcionam, igualdade funciona

### J2: Entity_Should_Work_With_Int_Id

- Cria `TestEntity<int>` com `id = 1`
- Resultado: Propriedades funcionam, igualdade funciona

### J3: Entity_Should_Work_With_Long_Id

- Cria `TestEntity<long>` com `id = 123L`
- Resultado: Propriedades funcionam

### J4: Entity_Should_Work_With_String_Id

- Cria `TestEntity<string>` com `id = "UUID-1234"`
- Resultado: Propriedades funcionam

### J5: Entity_Should_Work_With_Custom_ValueObject_Id

- Define `UserId : IEquatable<UserId>` com semântica de igualdade apropriada
- Cria `TestEntity<UserId>` com `UserId`
- Resultado: Igualdade respeita igualdade do `UserId`

### J6: Entity_Equality_Should_Ignore_Other_Attributes

- Cria dois `TestEntity<Guid>` com mesmo `Id` mas atributos diferentes
- Resultado: Ainda são iguais (atributos não participam da igualdade)

### J7: Multiple_Concrete_Types_Should_Not_Be_Equal_Even_With_Same_Id

- Define `UserEntity : Entity<Guid>` e `AdminEntity : Entity<Guid>`
- Cria instâncias com mesmo `Id`
- Resultado: `user != admin` (tipos concretos diferentes)

---

## Seção K: Edge Cases e Comportamentos Especiais

### K1: Entity_Should_Support_Null_Safe_Navigation

- Compara `entity1 == null`, `null == entity1`, `null == null`
- Resultado: Semanticamente correto (false, false, true)

### K2: Entity_Should_Be_Usable_In_Linq

- Utiliza entidades em `.Where()`, `.Select()`, `.Distinct()`
- Resultado: `Distinct()` remove duplicatas corretamente (mesma identidade)

### K3: Entity_Should_Be_Usable_In_HashSet

- Adiciona múltiplas instâncias com mesmo `Id` a `HashSet<Entity<Guid>>`
- Resultado: Deduplicação funciona (mesma identidade conta como uma única entrada)

### K4: Entity_Should_Be_Usable_In_Dictionary

- Usa entidades como chaves em `Dictionary<Entity<Guid>, string>`
- Busca posterior com instância equivalente retorna valor correto

### K5: Entity_Should_Handle_Zero_Int_Id

- Cria `TestEntity<int>` com `id = 0`
- Resultado: Válido para Entity<int>; validação semântica pertence à subclasse

### K6: Entity_Should_Handle_Empty_String_Id

- Cria `TestEntity<string>` com `id = ""`
- Resultado: Válido para Entity<string> (não é null); validação semântica pertence à subclasse

### K7: Entity_Should_Handle_Guid_Empty_Id

- Cria `TestEntity<Guid>` com `id = Guid.Empty`
- Resultado: Válido para Entity<Guid>; validação semântica pertence à subclasse

---

## Seção L: Testes de Arquitetura

### L1: Entity_Should_Not_Reference_Logging

- Verifica que nenhuma importação de logging existem
- Resultado: Nenhuma dependência de logging
- Ferramenta: NetArchTest ou Shouldly

### L2: Entity_Should_Not_Reference_EntityFramework

- Verifica que não há importação de `Microsoft.EntityFrameworkCore`
- Resultado: Sem dependência de ORM
- Ferramenta: NetArchTest

### L3: Entity_Should_Not_Reference_AspNetCore

- Verifica que não há importação de `Microsoft.AspNetCore`
- Resultado: Sem dependência de apresentação
- Ferramenta: NetArchTest

### L4: Entity_Should_Only_Use_System_Namespaces

- Verifica que apenas namespaces de `System` e do projeto estão em uso
- Resultado: Independência de infraestrutura
- Ferramenta: NetArchTest ou reflexão

### L5: Entity_Should_Reside_In_Correct_Namespace

- Verifica que `Entity<TId>` está em `Business.Platform.Core.Domain.Shared`
- Resultado: Namespace correto
- Ferramenta: Reflexão

---

## Matriz de Cobertura

| Categoria | Quantidade | Padrão |
|-----------|-----------|--------|
| Construção (A) | 5 | Validação de construtor |
| Propriedade (B) | 2 | Imutabilidade |
| Equals(object?) (C) | 7 | Igualdade polimórfica |
| Equals(Entity<TId>?) (D) | 4 | Igualdade tipada |
| GetHashCode (E) | 6 | Consistência de hash |
| Operador == (F) | 7 | Igualdade com operador |
| Operador != (G) | 5 | Desigualdade com operador |
| Reflexão (H) | 9 | Contrato de tipo (IEquatable, sealed override) |
| Propriedades de Igualdade (I) | 4 | Reflexividade, simetria, transitividade |
| DDD Scenarios (J) | 7 | Padrões de domínio |
| Edge Cases (K) | 7 | Comportamentos especiais (incluindo collections) |
| Arquitetura (L) | 5 | Independência de infraestrutura |
| **Total Esperado** | **68** | |

---

## Rastreabilidade: Cenários Documentados → Testes Executáveis

Legenda: **U** = `EntityTests` (unitário); **A** = `EntityArchitectureTests` (arquitetura). Status: **Coberto**, **Parcial** ou **Não coberto**.

| Cenário | Teste executável | Status |
|---|---|---|
| A1 | U `Constructor_WithGuid_ShouldSetId` | Coberto |
| A2 | U `Constructor_WithInt_ShouldSetId` | Coberto |
| A3 | U `Constructor_WithString_ShouldSetId` | Coberto |
| A4 | U `Constructor_WithNullId_ShouldThrowArgumentNullException` (verifica `ParamName == "id"`; não verifica o texto da mensagem) | Coberto |
| A5 | U `Constructor_WithCustomIdentifier_ShouldSetId` (usa `record struct`, não classe `UserId`) | Coberto |
| B1, B2, H5 | A `Id_ShouldHavePublicGetterAndNoSetterOfAnyVisibility` (`GetGetMethod(false)` não nulo, `GetSetMethod(true)` nulo, `CanWrite == false`) | Coberto |
| C1 | U `Equals_SameInstance_ShouldBeTrue` | Coberto |
| C2 | U `Equals_SameTypeSameId_ShouldBeTrue` | Coberto |
| C3 | U `Equals_SameTypeDifferentId_ShouldBeFalse` | Coberto |
| C4, J7 | U `Equals_DifferentConcreteTypesSameId_ShouldBeFalse`, `Equals_DerivedRuntimeTypeSameId_ShouldBeFalse`, `HashSet_ShouldKeepDifferentConcreteTypesWithSameId` | Coberto |
| C5 | U `Equals_NullObject_ShouldBeFalse` | Coberto |
| C6 | U `Equals_NonEntityObject_ShouldBeFalse` | Coberto |
| C7 | U `Equals_EntityWithDifferentIdType_ShouldBeFalse` (`Entity<int>` vs `Entity<long>`) | Coberto |
| D1 | U `EqualsTyped_SameInstance_ShouldBeTrue` | Coberto |
| D2 | U `EqualsTyped_SameTypeSameId_ShouldBeTrue` | Coberto |
| D3 | U `EqualsTyped_Null_ShouldBeFalse` | Coberto |
| D4 | U `EqualsTyped_DifferentConcreteTypeSameId_ShouldBeFalse` | Coberto |
| E1, E4 | U `GetHashCode_ShouldBeStableAcrossCalls` (um único teste cobre os dois cenários) | Coberto |
| E2 | U `GetHashCode_EqualEntities_ShouldHaveSameHash`, `GetHashCode_ShouldCombineConcreteTypeAndIdentifierHash` | Coberto |
| E3 | U `HashSet_ShouldKeepEntitiesWithDifferentIds` (não exige hashes diferentes) | Coberto |
| E5, K3 | U `HashSet_ShouldDeduplicateEqualEntities` | Coberto |
| E6, K4 | U `Dictionary_ShouldFindValueByEquivalentEntity` | Coberto |
| F1–F7 | U `OperatorEquality_SameInstance`, `_SameTypeSameId`, `_DifferentId`, `_DifferentConcreteTypes`, `_LeftNull`, `_RightNull`, `_BothNull` (sufixos de `..._Should...`) | Coberto |
| G1–G5 | U `OperatorInequality_SameInstance`, `_DifferentId`, `_DifferentConcreteTypes`, `_LeftNull`, `_BothNull` (além de `_SameTypeSameId` e `_RightNull`, fora da matriz) | Coberto |
| H1, H2 | A `Entity_ShouldBeAbstractAndNotSealed` | Coberto |
| H3 | A `Entity_ShouldExposeOnlyProtectedConstructor` | Coberto |
| H4 | A `Entity_ShouldImplementIEquatable` | Coberto |
| H6 | A `Equals_Object_ShouldBeSealedOverride` | Coberto |
| H7 | A `GetHashCode_ShouldBeSealedOverride` | Coberto |
| H8 | A `Equals_Typed_ShouldNotBeVirtual` (`!IsVirtual \|\| IsFinal`) | Coberto |
| H9 | A `Entity_ShouldDeclareEqualityOperators` | Coberto |
| I1 | U `Equality_ShouldBeReflexive` | Coberto |
| I2 | U `Equality_ShouldBeSymmetric` | Coberto |
| I3 | U `Equality_ShouldBeTransitive` | Coberto |
| I4 | U `Equality_ShouldBeConsistentAcrossEqualsOperatorsAndTypedEquals` | Coberto |
| J1 | U `Constructor_WithGuid_ShouldSetId` e testes de igualdade com `UserEntity` (`Entity<Guid>`) | Coberto |
| J2 | U `Constructor_WithInt_ShouldSetId`, `Equality_WithIntIds_ShouldCompareByValue` (igualdade, desigualdade, `!=` e hash com `int`) | Coberto |
| J3 | U `Constructor_WithLong_ShouldSetId`, `Equality_WithLongIds_ShouldCompareByValue` | Coberto |
| J4 | U `Constructor_WithString_ShouldSetId`, `Equality_WithStringIds_ShouldCompareByValueNotReference` (instâncias de string distintas, comparação ordinal sensível a maiúsculas/minúsculas) | Coberto |
| J5 | U `Equality_WithCustomIdentifierEqualByValue_ShouldBeTrue`, `..._DifferentValue_ShouldBeFalse`, `Equality_WithReferenceIdentifierEqualByValue_ShouldBeTrue` (usa `record struct` e `record class`, não `IEquatable<UserId>` explícito) | Coberto |
| J6 | U `Equality_ShouldIgnoreOtherAttributes` | Coberto |
| K1 | U `OperatorEquality_LeftNull`, `_RightNull`, `_BothNull` | Coberto |
| K2 | U `Distinct_ShouldRemoveEqualEntities`, `Linq_ShouldRespectEntityEquality` (`Where`, `Select`, `Distinct`, `Contains`, `Count`) | Coberto |
| K5 | U `Constructor_WithZero_ShouldBeAccepted`, `Equality_WithDefaultValuesAsIds_ShouldCompareByValue` | Coberto |
| K6 | U `Constructor_WithEmptyString_ShouldBeAccepted`, `Equality_WithDefaultValuesAsIds_ShouldCompareByValue` | Coberto |
| K7 | U `Constructor_WithGuidEmpty_ShouldBeAccepted`, `Equality_WithDefaultValuesAsIds_ShouldCompareByValue` | Coberto |
| L1, L2, L3 | A `Entity_ShouldNotDependOnForbiddenLayersOrFrameworks` (lista inclui `Microsoft.Extensions.Logging`, `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore`) | Coberto |
| L4 | A `Entity_ShouldOnlyDependOnSystemNamespaces` (NetArchTest `OnlyHaveDependenciesOn("System", "Business.Platform.Core.Domain.Shared")`; verificado que a regra falha quando `System` é removido da lista). Complementar: `ErrorArchitectureTests.DomainAssembly_ShouldReferenceOnlyAllowedAssemblies` (preexistente, nível de assembly) | Coberto |
| L5 | A `Entity_ShouldResideInSharedNamespace`, `Entity_ShouldResideInDomainAssembly` | Coberto |

**Cenários não cobertos:** nenhum. As lacunas J2, J3, J4, K2 e L4, identificadas na auditoria, foram fechadas com os testes indicados acima (5 unitários e 1 arquitetural adicionados).
**Observação:** o texto da mensagem da exceção em A4 ("contém 'null' ou 'ID'") não é verificado; o teste valida o tipo da exceção e `ParamName == "id"`.

### Testes executáveis adicionais (fora da matriz)

| Teste | Objetivo |
|---|---|
| U `Equals_DerivedRuntimeTypeSameId_ShouldBeFalse` | Subclasse de tipo concreto com mesmo `Id` não é igual |
| U `Id_ShouldReturnSameReferenceProvided` | `Id` de referência é preservado sem cópia defensiva (D05) |
| U `Equality_WithDefaultValuesAsIds_ShouldCompareByValue` | Valores padrão (`Guid.Empty`, 0, `""`) são aceitos e comparados por valor (D04) |
| U `GetHashCode_ShouldCombineConcreteTypeAndIdentifierHash` | Hash combina tipo concreto e hash do `Id` (sem exigir hashes distintos) |
| U `OperatorInequality_SameTypeSameId_ShouldBeFalse`, `OperatorInequality_RightNull_ShouldBeTrue` | Casos de `!=` além dos cenários G1–G5 |
| A `ComponentFilter_ShouldSelectOnlyEntity` | Garante que as regras de dependência não passam de forma vazia |
| A `Entity_ShouldNotDependOnErrorOrResult` (2 casos) | Entity não depende de `Error` nem de `Result` |
| A `Entity_ShouldRequireNotNullIdentifier` | Verificação indireta de `where TId : notnull` pelos atributos `Nullable`/`NullableContext` (a garantia real é em tempo de compilação) |
| A `Entity_ShouldNotExposeRelationshipsOrExtraPublicMembers` | Única propriedade é `Id`; nenhum campo além do backing field (D02) |

---

## Meta de Mutation Testing

- **Target:** ≥90% mutation score
- **Escopo:** Todas as linhas de código da classe `Entity<TId>`
- **Exceções permitidas:** Nenhuma (documentar em comments se houver)
- **Resultado reportado (Stryker.NET 4.8.1, 2026-10-13):** 100% em `Entity.cs`; 97,59% global. Detalhes e artefato em `CHECKLIST.md`.

---

## Notas sobre Implementação de Testes

### Test Fixtures

```csharp
public class TestEntity<TId> : Entity<TId> where TId : notnull
{
    public TestEntity(TId id) : base(id) { }
}

public class TestEntityString : Entity<string>
{
    public TestEntityString(string id) : base(id) { }
}

public sealed class UserEntity : Entity<Guid>
{
    private UserEntity(Guid id) : base(id) { }

    public static UserEntity Create(Guid id) => new(id);
}

public sealed class AdminEntity : Entity<Guid>
{
    private AdminEntity(Guid id) : base(id) { }

    public static AdminEntity Create(Guid id) => new(id);
}
```

### Ferramentas Adotadas

- **Framework de Testes:** xUnit
- **Assertions:** Shouldly
- **Testes de Arquitetura:** NetArchTest
- **Mutation Testing:** Stryker.NET

Todas já são parte do repositório de projetos de teste.

### Organização de Testes

Organizar testes por seção (A-L) em classes ou grupos lógicos para clareza e manutenibilidade.

### Assertions

Utilizar Shouldly para afirmações claras:

```csharp
entity1.ShouldBe(entity2);
entity1.Equals(entity2).ShouldBeTrue();
user.GetHashCode().ShouldBe(anotherUser.GetHashCode());
```

Não asserir valores específicos de hash (zero é um hash válido) nem exigir hashes distintos para entidades diferentes: colisões são permitidas.

### Naming Convention

Seguir padrão: `MethodName_Scenario_ExpectedResult`

Exemplo: `Equals_Different_Types_Same_Id_Should_Return_False`

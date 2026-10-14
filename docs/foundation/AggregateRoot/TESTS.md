# Testes — AggregateRoot

Este documento registra a estratégia, os testes efetivamente implementados e a rastreabilidade dos requisitos de `AggregateRoot<TId>`.

Comportamentos de igualdade, hash, operadores e validação de `id` nulo pertencem a `Entity<TId>` e são cobertos em exaustão por `EntityTests` (58 testes). Os testes de AggregateRoot verificam apenas que a herança os preserva, sem duplicar essa suíte.

Arquivos:

- `tests/UnitTests/Domain/Shared/AggregateRoots/AggregateRootTests.cs`
- `tests/ArchitectureTests/Domain/Shared/AggregateRoots/AggregateRootArchitectureTests.cs`

---

## Testes Unitários (18)

Utilizam agregados concretos privados de teste: `TestAggregate<TId>`, `OrderAggregate` (com atributo `Name`), `InvoiceAggregate` e `PlainEntity` (`Entity<Guid>` simples).

| Teste | Verifica |
|---|---|
| `Constructor_WithGuid_ShouldSetId` | Construção com `Guid` |
| `Constructor_WithInt_ShouldSetId` | Construção com `int` |
| `Constructor_WithLong_ShouldSetId` | Construção com `long` |
| `Constructor_WithString_ShouldSetId` | Construção com `string` |
| `Constructor_WithCustomIdentifier_ShouldSetId` | Construção com `record struct` |
| `Constructor_WithNullId_ShouldPropagateArgumentNullExceptionFromEntity` | `ArgumentNullException` com `ParamName == "id"` propagada de `Entity` |
| `Constructor_WithDefaultIdentifiers_ShouldBeAcceptedWithoutExtraValidation` | `Guid.Empty`, `0`, `""` aceitos (sem validação adicional) |
| `Aggregate_ShouldBeAssignableToEntityAndEquatable` | Herança de `Entity<Guid>`, `IEquatable<Entity<Guid>>` e `AggregateRoot<Guid>` |
| `Equality_SameTypeSameId_ShouldBeEqualIgnoringState` | `Equals`, `==`, `!=` e hash; estado (`Name`) não participa |
| `Equality_SameTypeDifferentId_ShouldNotBeEqual` | Desigualdade por `Id` |
| `Equality_DifferentConcreteTypesWithSameId_ShouldNotBeEqual` | Tipos concretos diferentes nunca são iguais |
| `Equality_AggregateAndPlainEntityWithSameId_ShouldNotBeEqual` | Agregado e entidade simples com o mesmo `Id`, nos dois sentidos |
| `Equality_NullHandling_ShouldFollowEntityContract` | `null` nos operadores |
| `Equality_WithDifferentGenericIdentifiers_ShouldCompareByValue` | `int`, `long`, `string` e `record struct` |
| `HashSet_ShouldDeduplicateAggregatesByTypeAndId` | Deduplicação em `HashSet` |
| `HashSet_WithDifferentConcreteTypesAndSameId_ShouldKeepBoth` | Tipos diferentes coexistem |
| `Dictionary_ShouldFindAggregateByEquivalentInstance` | Busca por instância equivalente |
| `Linq_ShouldRespectAggregateEquality` | `Distinct` e `Contains` |

---

## Testes Arquiteturais (22 execuções)

A contagem inclui as execuções de `Theory`. Os testes refletem as limitações da reflexão: a verificação da ausência de membros usa `DeclaredOnly` sobre o tipo fechado `AggregateRoot<Guid>`; a restrição `notnull` é verificada pelos atributos `Nullable`/`NullableContext`, pois não é uma restrição de runtime.

| Teste | Verifica |
|---|---|
| `AggregateRoot_ShouldResideInDomainAssembly` | Assembly `Business.Platform.Core.Domain` |
| `AggregateRoot_ShouldResideInSharedNamespace` | Namespace `Business.Platform.Core.Domain.Shared` |
| `ComponentFilter_ShouldSelectOnlyAggregateRoot` | O filtro das regras NetArchTest seleciona exatamente `AggregateRoot<>` (evita aprovação vazia) |
| `AggregateRoot_ShouldBePublicAbstractAndNotSealed` | Pública, abstrata, não selada |
| `AggregateRoot_ShouldInheritDirectlyFromEntityWithSameIdentifier` | Base direta `Entity<TId>` com o mesmo `TId` |
| `AggregateRoot_ShouldRequireNotNullIdentifier` | `notnull` via anotações de nulidade |
| `AggregateRoot_ShouldExposeOnlyProtectedConstructorWithIdentifier` | Nenhum construtor público; um construtor protegido `(TId)` |
| `AggregateRoot_ShouldDeclareOnlyTheConstructor` | Único membro declarado é o construtor |
| `AggregateRoot_ShouldNotDeclareProperties` | Sem propriedades declaradas |
| `AggregateRoot_ShouldNotDeclareFields` | Sem campos declarados |
| `AggregateRoot_ShouldNotDeclareMethodsOrEvents` | Sem métodos, eventos nem tipos aninhados declarados |
| `AggregateRoot_ShouldNotImplementAdditionalInterfaces` | Interfaces idênticas às de `Entity<Guid>` |
| `AggregateRoot_ShouldNotRedefineEquals` (3 execuções) | Sem `Equals` declarado para `object`, `Entity<Guid>` e `AggregateRoot<Guid>` |
| `AggregateRoot_ShouldNotRedefineHashOrOperators` | Sem `GetHashCode`, `op_Equality`, `op_Inequality` declarados |
| `AggregateRoot_ShouldResolveEqualityMembersFromEntity` | `Equals`, `GetHashCode` e `Id` resolvidos em `Entity<Guid>` |
| `AggregateRoot_ShouldNotDependOnForbiddenLayersOrFrameworks` | Sem dependência de camadas superiores, ORM, ASP.NET, logging, HTTP, JSON, `System.Data` |
| `AggregateRoot_ShouldOnlyDependOnSystemAndSharedNamespace` | Dependências apenas em `System` e no namespace `Shared` |
| `AggregateRoot_ShouldNotDependOnErrorOrResult` (2 execuções) | Sem dependência de `Error` nem `Result` |
| `AggregateRoot_ShouldNotDeclareEventPersistenceAuditOrTenantMembers` | Nenhum membro declarado com nomes ligados a evento, persistência, auditoria ou tenant |

---

## Rastreabilidade

| Requisito | Cobertura |
|---|---|
| RN-ARG-01 Herança direta de `Entity<TId>` | Arq. `ShouldInheritDirectlyFromEntityWithSameIdentifier`; Unit `Aggregate_ShouldBeAssignableToEntityAndEquatable` |
| RN-ARG-02 Construtor protegido mínimo | Arq. `ShouldExposeOnlyProtectedConstructorWithIdentifier`, `ShouldDeclareOnlyTheConstructor` |
| RN-ARG-03 Sem redefinição de igualdade, hash e operadores | Arq. `ShouldNotRedefineEquals`, `ShouldNotRedefineHashOrOperators`, `ShouldResolveEqualityMembersFromEntity` |
| RN-ARG-04 Nenhum membro adicional | Arq. `ShouldDeclareOnlyTheConstructor`, `ShouldNotDeclareProperties`, `ShouldNotDeclareFields`, `ShouldNotDeclareMethodsOrEvents`, `ShouldNotImplementAdditionalInterfaces` |
| RN-ARG-07 / RN-ARG-08 Sem eventos, persistência, auditoria, multitenancy | Arq. `ShouldNotDeclareEventPersistenceAuditOrTenantMembers` e ausência de membros (verificação estrutural, não semântica) |
| RN-ARG-09 Independência de infraestrutura | Arq. `ShouldNotDependOnForbiddenLayersOrFrameworks`, `ShouldOnlyDependOnSystemAndSharedNamespace`, `ShouldNotDependOnErrorOrResult` |
| RN-ARG-11 Igualdade independente de estado | Unit `Equality_SameTypeSameId_ShouldBeEqualIgnoringState` |
| RN-ARG-13 Sem selagem obrigatória | Arq. `ShouldBePublicAbstractAndNotSealed` |
| RN-ARG-15 Sem genéricos adicionais | Arq. `ShouldExposeOnlyProtectedConstructorWithIdentifier` (tipo fechado com um único `TId`) e `ShouldRequireNotNullIdentifier` |
| `id` nulo | Unit `Constructor_WithNullId_ShouldPropagateArgumentNullExceptionFromEntity` (comportamento de `Entity`) |
| Igualdade, desigualdade por tipo e por `Id` | Unit `Equality_*` |
| Coleções | Unit `HashSet_*`, `Dictionary_*`, `Linq_*` |

## Limitações

- Regras de agregados concretos (ciclo de vida, `Result`, encapsulamento de filhas, `TenantId`, referências por identificador) são diretrizes, não regras da classe-base, e não são testadas aqui.
- A ausência de eventos, persistência e multitenancy é verificada estruturalmente (sem membros declarados, sem dependências e sem nomes sugestivos); não há prova semântica além disso.
- Mutation testing: `AggregateRoot.cs` não gera mutantes elegíveis (ver `CHECKLIST.md`); a proteção do comportamento vem dos testes acima e dos mutantes de `Entity.cs`.

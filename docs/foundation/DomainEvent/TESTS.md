# Testes — DomainEvent

Este documento registra a estratégia, os testes efetivamente implementados e a rastreabilidade do contrato `IDomainEvent`.

A interface não possui lógica executável. Os testes verificam o contrato (membros, tipos, ausência de setters, dependências) e o uso do contrato por implementações concretas definidas **exclusivamente nos testes**. Não há validações de `EventId` ou `OccurredOnUtc` a testar, pois a interface não as realiza.

Arquivos:

- `tests/UnitTests/Domain/Shared/DomainEvents/DomainEventTests.cs`
- `tests/ArchitectureTests/Domain/Shared/DomainEvents/DomainEventArchitectureTests.cs`

---

## Testes Unitários (14)

Utilizam implementações privadas de teste: `SimpleEvent`, `OrderPlacedEvent` (dados adicionais), `AggregateEvent<TId>` (identificador de agregado de tipo variável) e `PlainAggregate` (`AggregateRoot<Guid>` sem eventos).

| Teste | Verifica |
|---|---|
| `ConcreteEvent_ShouldImplementInterface` | Implementação concreta é atribuível a `IDomainEvent` |
| `EventId_ShouldBePreserved` | `EventId` informado é devolvido |
| `OccurredOnUtc_ShouldBePreserved` | `OccurredOnUtc` informado é devolvido |
| `OccurredOnUtc_WithUtcOffset_ShouldKeepZeroOffset` | Valor com offset UTC mantém offset zero |
| `EventId_ShouldAcceptAnyGuidWithoutInterfaceValidation` | `Guid.Empty` é aceito (a interface não valida) |
| `OccurredOnUtc_ShouldNotBeValidatedByInterface_WhenOffsetIsNotUtc` | Offset não UTC é preservado (a interface não valida nem normaliza) |
| `Properties_ShouldUseGuidAndDateTimeOffset` | Tipos de `EventId` e `OccurredOnUtc` |
| `ConcreteEvent_ShouldCarryAdditionalDomainData` | Evento concreto com dados adicionais |
| `ConcreteEvents_ShouldSupportAggregateIdentifiersOfDistinctTypes` | `Guid`, `int`, `string` e identificador customizado |
| `Event_ShouldBeIndependentFromAggregateRoot` | Evento não é `AggregateRoot` nem `Entity` |
| `AggregateRoot_ShouldNotBeADomainEvent_AndExistsWithoutEvents` | Agregado funciona sem relação com eventos |
| `EventsInDifferentInstances_ShouldKeepTheirOwnValues` | Instâncias independentes |
| `Events_ShouldBeUsableAsHeterogeneousCollection` | `List<IDomainEvent>` com tipos distintos |
| `Event_ShouldBeUsableAsResultValue` | `Result<IDomainEvent>` |

---

## Testes Arquiteturais (19 execuções)

São 15 métodos de teste: 14 `Fact` e 1 `Theory` com 4 `InlineData`, totalizando 19 execuções. Os atributos gerados pelo compilador (namespace `System.Runtime.CompilerServices`, como `Nullable`) são desconsiderados nas verificações de atributos explícitos, evitando falsos positivos.

| Teste | Verifica |
|---|---|
| `DomainEvent_ShouldResideInDomainAssembly` | Assembly `Business.Platform.Core.Domain` |
| `DomainEvent_ShouldResideInSharedNamespace` | Namespace `Business.Platform.Core.Domain.Shared` |
| `ComponentFilter_ShouldSelectOnlyIDomainEvent` | O filtro das regras NetArchTest seleciona exatamente `IDomainEvent` (evita aprovação vazia) |
| `DomainEvent_ShouldBePublicInterface` | Interface pública, não genérica |
| `DomainEvent_ShouldDeclareExactlyEventIdAndOccurredOnUtc` | Exatamente duas propriedades |
| `DomainEvent_ShouldUseGuidAndDateTimeOffset` | Tipos das propriedades |
| `DomainEvent_ShouldExposeOnlyGetters` | Sem setters de qualquer visibilidade |
| `DomainEvent_ShouldNotDeclareAdditionalMembers` | Somente os dois getters; sem eventos, campos ou tipos aninhados |
| `DomainEvent_ShouldNotInheritFromOtherInterfaces` | Sem interfaces-base |
| `DomainEvent_ShouldNotHaveExplicitCustomAttributes` | `IDomainEvent` sem atributos personalizados explícitos |
| `EventId_ShouldNotHaveExplicitCustomAttributes` | `EventId` sem atributos personalizados explícitos |
| `OccurredOnUtc_ShouldNotHaveExplicitCustomAttributes` | `OccurredOnUtc` sem atributos personalizados explícitos |
| `DomainEvent_ShouldNotDependOnForbiddenLayersOrFrameworks` | Sem camadas superiores, ASP.NET, EF Core, `Microsoft.Extensions`, MediatR, HTTP, JSON, `System.Data`, `System.Transactions` |
| `DomainEvent_ShouldOnlyDependOnSystemNamespace` | Dependências apenas em `System` |
| `DomainEvent_ShouldNotDependOnOtherFoundationComponents` (4 execuções) | Sem dependência de `Entity`, `AggregateRoot`, `Error`, `Result` |
| `AggregateRoot_ShouldNotReferenceOrImplementDomainEvents` | `AggregateRoot` não implementa nem referencia `IDomainEvent`; a estrutura completa de `AggregateRoot` segue coberta por `AggregateRootArchitectureTests` |

---

## Rastreabilidade

| Requisito | Cobertura |
|---|---|
| RN-DOMEVENT-01 Contrato com duas propriedades | Arq. `ShouldDeclareExactlyEventIdAndOccurredOnUtc`, `ShouldNotDeclareAdditionalMembers`, `ShouldNotInheritFromOtherInterfaces` |
| RN-DOMEVENT-02 `EventId` `Guid` | Arq. `ShouldUseGuidAndDateTimeOffset`; Unit `Properties_ShouldUseGuidAndDateTimeOffset`, `EventId_ShouldBePreserved` |
| RN-DOMEVENT-03 `OccurredOnUtc` `DateTimeOffset` | Arq. `ShouldUseGuidAndDateTimeOffset`; Unit `OccurredOnUtc_ShouldBePreserved`, `..._WithUtcOffset_...` |
| RN-DOMEVENT-04 Somente getters | Arq. `ShouldExposeOnlyGetters` |
| RN-DOMEVENT-05 Independência de `AggregateRoot` | Unit `Event_ShouldBeIndependentFromAggregateRoot`, `AggregateRoot_ShouldNotBeADomainEvent_...`; Arq. `AggregateRoot_ShouldNotReferenceOrImplementDomainEvents` |
| RN-DOMEVENT-06 Sem infraestrutura | Arq. `ShouldNotDependOnForbiddenLayersOrFrameworks`, `ShouldOnlyDependOnSystemNamespace` |
| RN-DOMEVENT-07 Namespace e localização | Arq. `ShouldResideInSharedNamespace`, `ShouldResideInDomainAssembly` |
| RN-DOMEVENT-08 Valores não validados pela interface | Unit `EventId_ShouldAcceptAnyGuidWithoutInterfaceValidation`, `OccurredOnUtc_ShouldNotBeValidatedByInterface_...` |
| RN-DOMEVENT-09 Compatibilidade | Unit `Event_ShouldBeUsableAsResultValue`; Arq. `ShouldNotDependOnOtherFoundationComponents` |

---

## Resultados Executados (2026-10-13, após correções da auditoria)

Configuração: `Release`, .NET 10, `dotnet test -c Release --no-build --no-restore` após `dotnet build -c Release -warnaserror` (0 avisos, 0 erros).

| Projeto | Total | Aprovados | Falhas |
|---|---|---|---|
| UnitTests | 203 | 203 | 0 |
| ArchitectureTests | 95 | 95 | 0 |

Desses, 14 unitários e 19 execuções arquiteturais são de `IDomainEvent`; os demais pertencem às features anteriores (sem regressão).

## Mutation Testing

- Ferramenta: Stryker.NET 4.8.1 (`dotnet stryker --msbuild-path ...`, conforme registrado nas features anteriores).
- `IDomainEvent.cs`: **zero mutantes elegíveis; score N/A**, pois a interface não contém lógica executável. Não há score a declarar para este arquivo.
- Score global do projeto na execução (anterior às correções, que alteraram apenas testes arquiteturais e documentação): 97,59% (107 mutantes: 79 mortos, 2 timeout, 2 sobreviventes, 2 erros de compilação, 22 ignorados), todos em arquivos preexistentes; nenhum valor atribuível a `IDomainEvent`. O Stryker não foi reexecutado após as correções.
- Relatório: `StrykerOutput/2026-10-13.22-29-32/reports/mutation-report.json` (diretório não versionado).

## Limitações

- A interface não garante, e os testes não afirmam, offset UTC em tempo de execução, imutabilidade profunda, unicidade de `EventId`, deduplicação ou idempotência.
- Os testes unitários exercem implementações definidas nos próprios testes; protegem o contrato, não regras de eventos concretos de domínio.
- A ausência de atributos é verificada apenas para o tipo e suas duas propriedades; atributos do compilador são desconsiderados.
- A ausência de relação com `AggregateRoot` é verificada estruturalmente (tipos e dependências), não semanticamente.
- A verificação de ausência de setters considera apenas a interface; implementações concretas decidem a própria mutabilidade.

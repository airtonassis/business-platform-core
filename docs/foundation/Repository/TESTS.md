# Estratégia de Testes — Repository Foundation

## Escopo de Testes

### Fase 1: Testes Arquiteturais (implementados)

**Objetivo:** Garantir que o contrato `IRepository<TAggregate, TId>` permanece independente de infraestrutura e com assinaturas exatas.

**Arquivo:** `tests/ArchitectureTests/Domain/Shared/Repositories/RepositoryArchitectureTests.cs`

**Testes (22 casos executados: 17 métodos, sendo 2 `Theory` com 3 e 4 casos):**

| Teste | Verificação |
|-------|------------|
| `Repository_ShouldResideInDomainAssemblyAndSharedNamespace` | Assembly Domain; namespace `Business.Platform.Core.Domain.Shared` |
| `ComponentFilter_ShouldSelectOnlyRepository` | Filtro de nome seleciona somente `IRepository<,>` |
| `Repository_ShouldBePublicInterface` | Interface pública |
| `Repository_ShouldHaveAggregateAndIdParameters` | Parâmetros `TAggregate`, `TId` |
| `Repository_ShouldConstrainAggregateToAggregateRootOfSameId` | `TAggregate : AggregateRoot<TId>`, sem outras restrições |
| `Repository_IdentifierShouldHaveNoClrConstraintsBeyondNotNullAnnotation` | `TId` sem `GenericParameterAttributes` e sem constraints de tipo (ver limite abaixo) |
| `Repository_ShouldRequireNotNullIdentifier` | `TId : notnull` (anotação `[Nullable(1)]`/`[NullableContext(1)]`, sem `[Nullable(2)]`) |
| `Repository_ShouldDeclareExactlyFourMethods` | Apenas `AddAsync`, `GetByIdAsync`, `RemoveAsync`, `UpdateAsync` |
| `Repository_MethodsShouldBePublicInstanceAbstractWithoutDefaultImplementation` | Métodos públicos, de instância, abstratos e virtuais; sem corpo (sem implementação default) |
| `Repository_ShouldNotDeclareNonPublicOrStaticMethods` | Sem métodos privados/protegidos/internos/estáticos extras nem construtores |
| `Repository_ShouldNotDeclarePropertiesEventsFieldsNestedTypesOrBaseInterfaces` | Ausência de membros extras |
| `GetByIdAsync_ShouldReturnTaskOfResultOfAggregate` | `Task<Result<TAggregate>>`; parâmetros `(TId id, CancellationToken cancellationToken)` |
| `WriteMethods_ShouldReturnTaskOfResultAndAcceptAggregate` (Theory ×3) | `Task<Result>`; parâmetros `(TAggregate aggregate, CancellationToken cancellationToken)` |
| `AllMethods_ShouldHaveOptionalCancellationTokenWithDefault` (Theory ×4) | `CancellationToken` opcional; valor default efetivo equivale a `default(CancellationToken)` e não é cancelável |
| `Repository_ShouldNotDependOnForbiddenLayersOrFrameworks` | Sem Application, Infrastructure, Api, Contracts, ASP.NET Core, EF Core, Dapper, System.Data etc. |
| `Repository_ShouldOnlyDependOnSystemAndSharedNamespace` | Somente `System` e `Domain.Shared` |
| `Repository_ShouldHaveNoImplementationInDomainAssembly` | Nenhuma classe implementa `IRepository<,>` no Domain |

**Framework:** xUnit + NetArchTest + Shouldly

**Resultado:** 22/22 aprovados (suíte arquitetural total: 139; suíte unitária: 264; total geral: 403).

**Limites de reflexão (documentados):**
- `notnull` do C# **não** é a constraint CLR `struct`/`class`/`new()` e não gera `GenericParameterAttributes`; só é refletido como anotação de nulabilidade em metadados. Os testes verificam `GenericParameterAttributes.None`, ausência de constraints de tipo e a anotação, sem exigir atributos inexistentes.
- `= default` em struct pode ser refletido com `DefaultValue == null`; o teste normaliza para `default(CancellationToken)`.
- Reflexão não prova semântica de persistência; isso pertence às implementações.

---

### Fase 2: Testes Unitários — não aplicável

Não foram criados testes unitários de comportamento: a interface não tem implementação, e mocks/fakes só verificariam o próprio teste. O contrato é validado por reflexão na Fase 1.

---

### Fase 3: Testes de Integração (Infrastructure — FEATURE-futura)

**Objetivo:** Validar implementações concretas (EF Core, Dapper, etc.).

**Estrutura futura:**

```
tests/IntegrationTests/Infrastructure/Persistence/
├── EntityFramework/
│   └── EfRepositoryIntegrationTests.cs
├── Dapper/
│   └── DapperRepositoryIntegrationTests.cs
└── InMemory/
    └── InMemoryRepositoryIntegrationTests.cs
```

**Testes (exemplo EF Core) — pseudocódigo ilustrativo de feature futura; `Order`, `EfOrderRepository` e `DatabaseFixture` não existem:**

```csharp
[Collection("Database")]
public sealed class EfRepositoryIntegrationTests
{
    private readonly DatabaseFixture _fixture;

    public EfRepositoryIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingOrder_ReturnsOrder()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new Order(orderId);
        await _fixture.DbContext.Orders.AddAsync(order);
        await _fixture.DbContext.SaveChangesAsync();

        var repository = new EfOrderRepository(_fixture.DbContext);

        // Act
        var result = await repository.GetByIdAsync(orderId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(orderId);
    }

    [Fact]
    public async Task AddAsync_WithNewOrder_PersistsOrder()
    {
        // Arrange
        var order = new Order(Guid.NewGuid());
        var repository = new EfOrderRepository(_fixture.DbContext);

        // Act
        var result = await repository.AddAsync(order);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _fixture.DbContext.Orders.Local.ShouldContain(order);
    }
}
```

**Nota:** Estes testes são FEATURE-futura (após implementação em Infrastructure)

---

## Estratégia de Validação: Mutation Testing

### Objetivo

Para interfaces sem lógica executável, mutation testing é **N/A** (não aplicável).

**Justificativa:**
- `IRepository<TAggregate, TId>` é contrato (interface pura)
- Não há corpo de método para mutar
- Testes arquiteturais e de contrato são verificações suficientes

### Escopo de Testes Efetivos

Priorizar:
1. **Testes Arquiteturais** (NetArchTest): Validam restrições genéricas, dependências, namespace
2. **Testes de Contrato** (assinaturas por reflexão): Validam retornos, parâmetros, abstração dos métodos e `CancellationToken`
3. **Mutation Testing:** Aplicável apenas a **implementações concretas** em Infrastructure

### Configuração Futura (para implementações)

Quando EfRepository, DapperRepository, etc. forem implementadas em Infrastructure:

```json
{
  "stryker-config.json": {
    "coverage-analysis": "perTestInIsolation",  // ADR-0001
    "baseline": "main"
  }
}
```

Naquele ponto, implementações terão lógica (conversão de BD, mapeamento, etc.) suscetível a mutation testing.

---

## Cobertura de Código

### Expectativa para Fase 1 (Contrato Domain.Shared)

```
Testes Arquiteturais:    ✅ 22 testes (todos aprovados)
Testes Unitários:        N/A (interface sem implementação)
Mutation Testing:        N/A (interface sem lógica)
Code Coverage:           N/A (interface sem corpo)
```

**Justificativa:** Interface é contrato; validação por testes arquiteturais e de contrato é suficiente.

### Ferramentas

- **xUnit:** Framework de teste
- **Shouldly:** Asserções fluentes
- **NetArchTest:** Testes arquiteturais
- **Stryker.NET:** Mutation testing (conforme ADR-0001)
- **ReportGenerator:** Cobertura consolidada

---

## Execução de Testes

### Localmente

```bash
# Testes arquiteturais
dotnet test tests/ArchitectureTests/Business.Platform.Core.ArchitectureTests.csproj -c Release

# Testes unitários
dotnet test tests/UnitTests/Business.Platform.Core.UnitTests.csproj -c Release

# Mutation testing (com workaround MSBuild no Windows)
dotnet stryker --msbuild-path "<SDK MSBuild path>"
```

### No CI/CD

```yaml
# .github/workflows/tests.yml (exemplo)
- name: Run Architecture Tests
  run: dotnet test tests/ArchitectureTests/ -c Release

- name: Run Unit Tests
  run: dotnet test tests/UnitTests/ -c Release --collect:"XPlat Code Coverage"

- name: Run Mutation Testing
  run: dotnet stryker
```

---

## Testes Não-Implementados Nesta Fase

### (Porque pertencem a Infrastructure — FEATURE-futura)

- ❌ Teste com banco de dados real
- ❌ Teste de isolamento transacional
- ❌ Teste de concorrência (Read-Write)
- ❌ Teste de performance (throughput, latência)
- ❌ Teste de rollback em caso de erro
- ❌ Teste de cascata de foreign keys

**Razão:** Estes testes requerem implementações concretas (EF Core Repository, etc.) que não existem ainda.

---

## Checklist de Testes para Aprovação

### Fase 1 — Contrato (ESTA FEATURE)

- [x] ArchitectureTests compilam e passam (22/22)
- [x] Sem testes unitários de comportamento (interface pura)
- [x] Testes verificam restrições genéricas, namespace, dependências, assinaturas e ausência de implementação
- [x] Sem warnings ao compilar
- [x] Sem violações de testes arquiteturais pré-existentes
- [x] API idêntica em todos os 7 documentos

### Fase 2 — Implementação EF Core (FEATURE-futura)

- [ ] EfRepositoryIntegrationTests compilam e passam
- [ ] Teste de persistência (INSERT, UPDATE)
- [ ] Teste de remoção
- [ ] Teste de erro (agregado não encontrado, constraint violation)
- [ ] Mutation testing com score ≥ 80% (aplicável a implementação concreta)

---

## Documentação de Testes

### Test Naming Convention

Padrão do repositório: `Componente_Should...` (ex.: `Repository_ShouldBePublicInterface`).

### Test Organization

Os testes vivem em `tests/ArchitectureTests/Domain/Shared/Repositories/`.

---

## Pontos para Features Futuras

1. **Mutation Testing:** aplicável a implementações concretas; meta a definir quando existirem.
2. **Specification:** testes de query serão adicionados com a feature correspondente.
3. **Benchmarks e resiliência (retry/timeout):** pertencem às implementações em Infrastructure.

---

**Versão:** 1.0 (Implementado — FEATURE-0007)  
**Status:** Architecture Gate APPROVED — implementado; aguardando auditoria independente

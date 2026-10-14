# Plano de Implementação — Repository Foundation

## Fase 1: Contrato (Domain.Shared)

### Artefatos Criados

```
src/Business.Platform.Core.Domain/Shared/Repositories/
├── IRepository.cs                      # Interface genérica
```

### IRepository.cs — Contrato Mínimo

**Namespace:** `Business.Platform.Core.Domain.Shared`

> O bloco abaixo é um **resumo ilustrativo** com comentários ampliados. A fonte da verdade é `src/Business.Platform.Core.Domain/Shared/Repositories/IRepository.cs`; assinaturas e restrições são idênticas.

```csharp
/// <summary>
/// Contrato para persistência de um agregado tipado.
/// Apenas tipos que herdam de AggregateRoot<TId> podem ser persistidos.
/// </summary>
/// <typeparam name="TAggregate">Tipo do agregado (obrigatoriamente AggregateRoot<TId>).</typeparam>
/// <typeparam name="TId">Tipo do identificador do agregado (não-nulo).</typeparam>
public interface IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    /// <summary>
    /// Recupera um agregado pelo seu identificador único.
    /// </summary>
    /// <param name="id">Identificador do agregado.</param>
    /// <param name="cancellationToken">Token para cancelamento de operação.</param>
    /// <returns>
    /// Task<Result<TAggregate>>:
    /// - Se encontrado: Result.Success(aggregate)
    /// - Se não encontrado: Result.Failure(error com ErrorType.NotFound)
    /// - Se erro operacional: Result.Failure(error de persistência)
    /// </returns>
    Task<Result<TAggregate>> GetByIdAsync(
        TId id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Solicita inclusão de agregado novo.
    /// </summary>
    /// <param name="aggregate">Agregado novo, validado, ainda não persistido.</param>
    /// <param name="cancellationToken">Token para cancelamento de operação.</param>
    /// <returns>
    /// Task<Result>:
    /// - Se sucesso: Result.Success()
    /// - Se falha (ex: ID duplicado): Result.Failure(error)
    /// - Se erro operacional: Result.Failure(error)
    /// </returns>
    /// <remarks>
    /// AddAsync semântica: "Incluir novo agregado" (não é upsert).
    /// Se agregado com mesmo ID existe, retorna erro (constraint violation).
    /// Não valida regras de negócio; assume agregado é válido.
    /// Persistência (commit, transação) é responsabilidade da implementação.
    /// </remarks>
    Task<Result> AddAsync(
        TAggregate aggregate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Solicita atualização de agregado existente.
    /// </summary>
    /// <param name="aggregate">Agregado existente, modificado em memória.</param>
    /// <param name="cancellationToken">Token para cancelamento de operação.</param>
    /// <returns>
    /// Task<Result>:
    /// - Se sucesso: Result.Success()
    /// - Se falha (ex: agregado não existe): Result.Failure(error)
    /// - Se erro operacional: Result.Failure(error)
    /// </returns>
    /// <remarks>
    /// UpdateAsync semântica: "Atualizar agregado existente" (não é inclusão).
    /// Se agregado com esse ID não existe, retorna erro.
    /// Não valida regras de negócio; assume agregado é válido.
    /// Persistência (commit, transação) é responsabilidade da implementação.
    /// </remarks>
    Task<Result> UpdateAsync(
        TAggregate aggregate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Solicita remoção de agregado.
    /// </summary>
    /// <param name="aggregate">Agregado a remover.</param>
    /// <param name="cancellationToken">Token para cancelamento de operação.</param>
    /// <returns>
    /// Task<Result>:
    /// - Se sucesso: Result.Success()
    /// - Se falha (ex: constraint de integridade referencial): Result.Failure(error)
    /// - Se erro operacional: Result.Failure(error)
    /// </returns>
    /// <remarks>
    /// RemoveAsync semântica: "Remover agregado".
    /// Política de exclusão (física vs. lógica/soft delete) = definida funcionalmente pelo domínio/módulo;
    /// a implementação apenas a executa tecnicamente.
    /// Persistência (commit, transação) é responsabilidade da implementação.
    /// </remarks>
    Task<Result> RemoveAsync(
        TAggregate aggregate,
        CancellationToken cancellationToken = default);
}
```

### Decisões de Implementação

#### D-IMPL-01: Onde Vive IRepository?

**Decisão:** `Business.Platform.Core.Domain/Shared/Repositories/IRepository.cs`

**Alternativa rejeitada:** `Business.Platform.Core.Domain.Abstractions` — não existe diretório, Domain.Shared é local correto

#### D-IMPL-02: Naming Convention

**Decisão:** `IRepository<TAggregate, TId>` genérico, sem suffix

**Alternativa rejeitada:**
- `IBaseRepository` — genérico demais
- `IRepository<TAggregate>` com inference — perde clareza de TId

#### D-IMPL-03: Sem Implementação Concreta em Domain

**Decisão:** Interface apenas em Domain.Shared; implementações concretas residem em Infrastructure

**Estrutura futura:**

```
src/Business.Platform.Core.Domain/
├── Shared/
│   ├── Repositories/
│   │   └── IRepository.cs            # Interface (contrato)

src/Business.Platform.Core.Infrastructure/
├── Persistence/
│   ├── EntityFramework/
│   │   └── EfRepository.cs           # Implementação EF Core (FEATURE-futura)
│   ├── Dapper/
│   │   └── DapperRepository.cs       # Implementação Dapper (FEATURE-futura)
│   └── InMemory/
│       └── InMemoryRepository.cs     # Teste/Demo (FEATURE-futura)
```

#### D-IMPL-04: Sem Commit, Rollback, Atomicidade Multi-Agregado

**Decisão:** IRepository não oferece:
- `CommitAsync()`, `SaveAsync()`, `SaveChangesAsync()`
- `BeginTransactionAsync()`, `RollbackAsync()`
- Unit of Work
- Garantia de atomicidade entre múltiplos agregados
- Garantia de rollback em erro

**Justificativa:**
- Responsabilidade clara: Repository = CRUD de um agregado (não coordenação multi-agregado)
- Agnóstico: contrato não prescreve modelo transacional
- Flexibilidade: implementações podem oferecer diferentes garantias (commit imediato, deferred, eventual consistency)
- Simplicidade: sem promessas implícitas de atomicidade

**Consequências para Caller:**
- `AddAsync` retorna sucesso, mas agregado pode não estar durável persistido ainda
- Se múltiplas operações falham, state pode ser parcialmente persistido (sem rollback automático)
- Application Service responsável por coordenar strategy de recovery:
  - Retry com backoff
  - Compensating transaction (desfazer AddAsync com RemoveAsync)
  - Saga pattern (coordenação externa)
  - Outro padrão apropriado ao caso de uso

**Implementação Documenta:**
- Quando agregado é durável persistido (após retorno? deferred? eventual?)
- Que mecanismo transacional usa (if any)
- Quais garantias oferece (ACID? BASE? Eventual consistency?)

#### D-IMPL-05: Contrato com Task

**Decisão:** Retornos usam `Task<Result<T>>` / `Task<Result>` (padrão .NET). `ValueTask` não faz parte do contrato.

**Justificativa:**
- `Task` é o padrão compatível com todas as implementações e simplifica o contrato
- `ValueTask` é otimização de alocação; pode ser usada em handlers e internamente em implementações sem alterar o contrato

**Nota:** a escolha é de contrato, não uma proibição de `ValueTask` em outras camadas.

#### D-IMPL-06: CancellationToken = default

**Decisão:** Parâmetro `cancellationToken` com valor padrão

**Benefício:**
- Permite omissão simples: `await repo.GetByIdAsync(id)`
- Habilitada para casos avançados: `await repo.GetByIdAsync(id, cts.Token)`

---

## Fase 2: Testes Arquiteturais (implementados)

Arquivo: `tests/ArchitectureTests/Domain/Shared/Repositories/RepositoryArchitectureTests.cs` (22 casos de teste, ver TESTS.md).

Convenções seguidas: mesmo padrão dos testes arquiteturais de `AggregateRoot` (reflexão + NetArchTest + Shouldly).

### Regras Verificadas

- ✅ IRepository reside no assembly Domain e no namespace `Business.Platform.Core.Domain.Shared`
- ✅ Filtro `^IRepository(`2)?$` seleciona exatamente `IRepository<,>` (regras de dependência não passam de forma vazia)
- ✅ IRepository é uma interface pública com dois parâmetros genéricos (`TAggregate`, `TId`)
- ✅ `TAggregate : AggregateRoot<TId>` e `TId : notnull`
  - Limite de reflexão: `notnull` do C# **não** produz `GenericParameterAttributes` (não é a constraint CLR `struct`/`class`/`new()`); o teste verifica que `TId` tem `GenericParameterAttributes.None`, nenhuma constraint de tipo e a anotação de nulabilidade `[Nullable(1)]`/`[NullableContext(1)]` sem `[Nullable(2)]`. A semântica completa de nulabilidade só é imposta pelo compilador.
- ✅ Exatamente quatro métodos (`GetByIdAsync`, `AddAsync`, `UpdateAsync`, `RemoveAsync`); sem propriedades, eventos, campos, tipos aninhados ou interfaces base
- ✅ `GetByIdAsync` retorna `Task<Result<TAggregate>>`; `AddAsync`, `UpdateAsync` e `RemoveAsync` retornam `Task<Result>`
- ✅ Métodos públicos, de instância e abstratos, sem implementação default, sem métodos estáticos e sem métodos não públicos
- ✅ `CancellationToken` opcional (`= default`) em todos os métodos, com valor default efetivo equivalente a `default(CancellationToken)` (não cancelável)
- ✅ Sem dependência de Application, Infrastructure, Api, Contracts, ASP.NET Core, EF Core, Dapper, System.Data etc.
- ✅ Depende apenas de `System` e `Business.Platform.Core.Domain.Shared`
- ✅ Nenhuma implementação concreta no assembly Domain

---

## Fase 3: Testes Unitários (não aplicável nesta feature)

Não há testes unitários de comportamento: a interface não possui implementação, e testes com mocks ou fakes verificariam apenas o próprio código do teste. O contrato é validado integralmente pelos testes arquiteturais (assinaturas por reflexão). Testes de comportamento pertencem às implementações concretas (FEATURE-futura).

---

## Fase 4: Integração com Container DI

### Estrutura Futura em Infrastructure

> Pseudocódigo ilustrativo de uma feature futura; nada disto existe nesta feature (`Order`, `Customer`, `EfOrderRepository`, `AddDomain` etc. são hipotéticos).

```csharp
// src/Business.Platform.Core.Infrastructure/DependencyInjection.cs (exemplo)
public static class DependencyInjection
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // Implementações EF Core (FEATURE-futura)
        services.AddScoped<IRepository<Order, Guid>, EfOrderRepository>();
        services.AddScoped<IRepository<Customer, Guid>, EfCustomerRepository>();
        // ... mais agregados

        return services;
    }
}

// Uso em Application
services.AddDomain()           // Registra Domain (só interfaces)
        .AddRepositories()      // Registra implementações
        .AddApplicationServices();
```

---

## Questões de Implementação (resolvidas)

1. **UC04 — Update:** `UpdateAsync` faz parte do contrato; `SaveAsync` não existe.

2. **Transações Multi-Agregado:** o contrato não coordena nem garante atomicidade multi-agregado. A estratégia (transação compensatória, Saga, Unit of Work futuro, etc.) é definida pela aplicação ou por feature futura; falha parcial não é revertida pelo contrato.

3. **Política de exclusão:** definida funcionalmente pelo domínio/módulo e executada tecnicamente pela implementação do repositório do módulo; o contrato não a codifica nem permite escolha arbitrária entre exclusão física e lógica.

4. **Error Codes:** `ErrorType.NotFound` para ausência; `Error.Code` é definido pela implementação/módulo.

---

**Versão:** 1.0 (Implementado — FEATURE-0007)  
**Status:** Architecture Gate APPROVED — implementado; aguardando auditoria independente

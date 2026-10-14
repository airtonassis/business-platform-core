# Regras de Negócio — Repository Foundation

> **Nota sobre os exemplos C#:** são **pseudocódigo ilustrativo simplificado**; tipos como `Order`, `Account` e `CustomerId` e métodos de negócio não existem no repositório. Apenas `IRepository<TAggregate, TId>`, `Result` e `Error` são contratos reais.

## Invariantes do Contrato

### I1 — Apenas AggregateRoots

**Regra:** O contrato `IRepository<TAggregate, TId>` aceita apenas tipos que herdam de `AggregateRoot<TId>`.

**Mecanismo:** Restrição genérica `where TAggregate : AggregateRoot<TId>` impõe isso em compilação.

**Exceções:** Nenhuma. Violação é erro de compilação.

**Justificativa:**
- DDD: apenas raízes de agregado são unidades persistentes
- Entidades internas (OrderLine, Address components) não atravessam limites de agregado
- Repositories especializados para entidades internas violariam o padrão

**Exemplos:**

```csharp
// ✅ Válido: Order herda de AggregateRoot<Guid>
public sealed class Order : AggregateRoot<Guid> { }
var repo = serviceProvider.GetRequiredService<IRepository<Order, Guid>>();

// ❌ Inválido: OrderLine herda de Entity<Guid>, não AggregateRoot<Guid>
public sealed class OrderLine : Entity<Guid> { }
// var repo = serviceProvider.GetRequiredService<IRepository<OrderLine, Guid>>(); // Erro de compilação

// ❌ Inválido: Address é ValueObject, não AggregateRoot
public sealed class Address : ValueObject { }
// var repo = serviceProvider.GetRequiredService<IRepository<Address, ???>>(); // Erro de compilação
```

---

### I2 — ID Não-Nulo

**Regra:** Todo tipo `TId` deve ser não-nulo (`where TId : notnull`).

**Mecanismo:** Restrição genérica e validação em runtime (construtor do agregado).

**Exceções:** Nenhuma. `TId?` é rejeitada em compilação.

**Justificativa:**
- Segurança de tipos: identidades nulas são inválidas por definição
- Consistência com `Entity<TId>` e `AggregateRoot<TId>`
- Evita `GetByIdAsync(null)` ou armazenamento de agregados sem ID

**Exemplos:**

```csharp
// ✅ Válido: Guid é não-nulo
public sealed class Order : AggregateRoot<Guid> { }

// ✅ Válido: CustomerId é Value Object não-nulo
public sealed class CustomerId : ValueObject, IEquatable<CustomerId>
{
    private readonly Guid _value;
    // IEquatable implementado, igualdade estável
}
public sealed class Customer : AggregateRoot<CustomerId> { }

// ❌ Inválido: string? é anulável
// var repo = serviceProvider.GetRequiredService<IRepository<Order, string?>>(); // Erro de compilação
```

---

### I3 — Operações Retornam Result

**Regra:** Todas as operações retornam `Result<T>` ou `Result` para comunicar sucesso/falha esperados.

**Mecanismo:**

- `GetByIdAsync` → `Task<Result<TAggregate>>`
- `AddAsync` → `Task<Result>`
- `UpdateAsync` → `Task<Result>`
- `RemoveAsync` → `Task<Result>`

**Exceções:** Nenhuma. Violação de contrato.

**Justificativa:**
- Alinhado com Error/Result Foundation
- Separa falhas esperadas (agregado não encontrado) de exceções (erro de conexão)
- Força caller a tratar falhas explicitamente

**Consequências:**

- Caller **deve** verificar `result.IsSuccess` antes de acessar valor
- O contrato declara retorno via `Result`; o sucesso carrega um agregado (nunca `null` como valor de sucesso)
- Falhas operacionais (timeout, erro de banco) são convertidas a `Result.Failure(...)`

**Exemplos:**

```csharp
// Correto: Verificar resultado
var result = await repository.GetByIdAsync(orderId, ct);
if (result.IsSuccess)
{
    var order = result.Value;
    // Processar order
}
else
{
    // Tratar erro result.Error
}

// Incorreto: Acesso sem validação
// var order = result.Value; // InvalidOperationException se IsFailure
```

---

### I4 — Semântica de ADD vs UPDATE vs REMOVE

**Regra:**

- `AddAsync(new_aggregate)` — inclusão de novo agregado (erro se existe)
- `UpdateAsync(existing_aggregate)` — atualização de agregado existente (erro se não existe)
- `RemoveAsync(aggregate)` — remoção de agregado

**Mecanismo:**
- Semântica é clara no nome da operação
- AddAsync ≠ UpdateAsync (não é upsert)
- Violação de semântica resulta em erro (constraint de unicidade, aggregado não encontrado, etc.)

**Justificativa:**
- Elimina ambiguidade: caller sabe exatamente o que espera
- Proteção contra bugs: tentar atualizar agregado inexistente é detectado (não silenciado como insert)
- Consistência com DDD: cada operação tem responsabilidade clara

### I5 — Sem Commit, Rollback, Atomicidade Multi-Agregado

**Regra:** O contrato **não oferece**:
- `CommitAsync()`, `SaveAsync()`, `SaveChangesAsync()`
- `BeginTransaction()`, `RollbackAsync()`
- Garantia de commit automático
- Garantia de atomicidade, nem de uma única operação nem entre múltiplos agregados
- Garantia de rollback em erro

Qualquer dessas garantias, se existir, é oferecida e documentada pela implementação concreta e pelo mecanismo transacional que ela usa.

**Mecanismo:**
- Cada operação (`AddAsync`, `UpdateAsync`, `RemoveAsync`) retorna `Result` (sucesso/falha)
- **Quem chama** decide como coordenar persistência
- Implementação concreta documenta suas garantias

**Application Service pode orquestrar:**
```csharp
// Chamar múltiplos repositórios
var r1 = await repo1.AddAsync(agg1, ct);
var r2 = await repo2.UpdateAsync(agg2, ct);
// Se r2 falha, r1 não é necessariamente revertido (o contrato não garante rollback nem compensação)
```

**O contrato não garante (a implementação pode oferecer, e deve documentar):**
- Atomicidade (ambas operações commitam ou nenhuma)
- Durabilidade (commit pode ser deferred, eventual)
- Isolamento (leitura durante escrita pode ver estado inconsistente)

**Justificativa:**
- Simplicidade: contrato agnóstico a modelo transacional
- Flexibilidade: implementações variam (BD relacional, eventual consistency, cache)
- Responsabilidade: Application Service coordena strategy apropriada (retry, compensating transaction, saga, etc.)

**Padrão para múltiplos agregados:**

```csharp
public sealed class TransferMoneyService
{
    private readonly IRepository<Account, Guid> _accountRepository;

    public async Task<Result> ExecuteAsync(Guid source, Guid dest, decimal amount, CancellationToken ct)
    {
        var sourceResult = await _accountRepository.GetByIdAsync(source, ct);
        if (sourceResult.IsFailure) return sourceResult;

        var destResult = await _accountRepository.GetByIdAsync(dest, ct);
        if (destResult.IsFailure) return destResult;

        // Lógica de domínio
        var transferResult = sourceResult.Value.TransferTo(destResult.Value, amount);
        if (transferResult.IsFailure) return transferResult;

        // Persistir ambos — responsabilidade da aplicação coordenar
        var saveSourceResult = await _accountRepository.UpdateAsync(sourceResult.Value, ct);
        if (saveSourceResult.IsFailure) return saveSourceResult;

        var saveDestResult = await _accountRepository.UpdateAsync(destResult.Value, ct);
        // Se falha aqui, a falha de dest é reportada; o estado persistido de dest depende da implementação
        // e a operação em source (que reportou sucesso) não é necessariamente revertida.
        // Estratégia de recovery (retry, compensating transaction, etc.) é responsabilidade da aplicação.
        if (saveDestResult.IsFailure) return saveDestResult;

        return Result.Success();
    }
}

// Nota: Sem Unit of Work no contrato v1.0.
// Coordenação multi-agregado é responsabilidade de Application Service ou pattern futuro (ex: Saga).
```

---

### I6 — Recuperação por ID Único

**Regra:** `GetByIdAsync(TId)` recupera no máximo um agregado (pelo ID único).

**Mecanismo:**
- ID é chave primária ou identificador único do agregado
- Retorna `Result<TAggregate>` (nenhum ou exatamente um)

**Exceções:**
- Múltiplos agregados com o mesmo ID: bug da implementação (viola invariante de banco)
- ID não encontrado: `Result.Failure(...)`, não exceção

**Justificativa:**
- Semântica clara: ID identifica unicamente o agregado
- Sem ambiguidade: resultado é determinístico

**Exemplos:**

```csharp
var result = await orderRepository.GetByIdAsync(orderId, ct);
// result.Value contém exatamente um Order ou nenhum (IsFailure)
```

---

### I7 — Sem Busca Complexa no Contrato 1.0

**Regra:** O contrato mínimo não inclui:
- ❌ `FindAsync(predicate)`
- ❌ `FindAllAsync()`
- ❌ `CountAsync()`
- ❌ Queries paramétricos genéricos

**Mecanismo:** Delegados a Specification Foundation (FEATURE-0008) ou Application Services.

**Exceções:** Implementações concretas podem oferecer métodos adicionais; o contrato base permanece minimalista.

**Justificativa:**
- Separação de responsabilidades: Repository = Get/Add/Remove; Specification = Query
- Escalabilidade: Specification Foundation pode oferecer otimizações (índices, eager-loading) sem alterar IRepository
- Simplicidade: contrato enxuto é mais fácil de implementar em múltiplos ORMs

**Padrão para queries futuras:**

```csharp
// Versão 1.0 — minimalist
var repo = serviceProvider.GetRequiredService<IRepository<Order, Guid>>();
var result = await repo.GetByIdAsync(orderId, ct);

// Versão 2.0 — com Specification (planejado)
var specRepo = serviceProvider.GetRequiredService<ISpecificationRepository<Order, Guid>>();
var ordersByCustomer = await specRepo.FindAsync(new OrdersByCustomerSpec(customerId), ct);
```

---

### I8 — Sem Eventos no Contrato 1.0

**Regra:** `IRepository<TAggregate, TId>` não coleta, retorna ou persiste eventos de domínio.

**Mecanismo:** Versão 1.0 é responsável apenas por CRUD do agregado.

**Exceções:** Nenhuma nesta versão. Eventos serão adicionados em FEATURE-0005+ (DomainEvent Foundation expansion).

**Justificativa:**
- DDD: eventos são responsabilidade do agregado, não do repositório
- Simplicidade: separação de concerns no contrato
- Evolução: adição de eventos não quebra implementações existentes (se feita como extensão)

**Padrão para eventos (planejado):**

```csharp
// Versão 1.0 — sem eventos
var result = await orderRepository.AddAsync(order, ct);

// Versão 2.0+ — com evento publishing (planejado)
var result = await orderRepository.AddAsync(order, ct);
if (result.IsSuccess)
{
    var eventPublisher = serviceProvider.GetRequiredService<IEventPublisher>();
    foreach (var evt in order.UncomittedEvents)
        await eventPublisher.PublishAsync(evt, ct);
}
```

---

### I9 — Deleção: Política Funcional do Domínio, Execução Técnica da Implementação

**Regra:** `RemoveAsync(aggregate)` solicita remoção. Há duas responsabilidades distintas:

- **Política funcional** (o agregado deve ser excluído fisicamente ou logicamente/soft delete, retenção, auditoria): definida pelo **domínio ou módulo**.
- **Execução técnica** (DELETE, flag de exclusão, cascata, filtros de consulta): feita pela **implementação**, que **executa a política definida** e não escolhe livremente entre exclusão física e lógica.

**Mecanismo:**
- O módulo declara a política do seu agregado
- A implementação do repositório do módulo a aplica e a documenta
- O contrato `IRepository` não impõe nem codifica a escolha

**Exceções:** Nenhuma no contrato. Se o módulo não definiu política, a implementação não deve assumir uma por conta própria.

**Justificativa:**
- Ownership: a decisão funcional sobre deletar dado é propriedade do domínio, não da infraestrutura
- Auditoria: soft delete pode ser requisito de compliance (domínio decide)
- Contrato não se envolve com semântica de deleção

---

### I10 — Sem Validação de Agregado no Repositório

**Regra:** `AddAsync`, `UpdateAsync`, `RemoveAsync` assumem que o agregado é válido. Não validam regras de negócio.

**Mecanismo:**
- Agregado deve ser validado antes de passar a `AddAsync` (ex: factory com `Result<TAggregate>`)
- Repositório apenas persiste

**Exceções:** Validações de infraestrutura (ex: "ID já existe") são capturadas e retornadas como `Result.Failure`.

**Justificativa:**
- DDD: validação de regras pertence ao agregado ou domain service
- Responsabilidade: repositório é serviço técnico, não valida lógica de negócio
- Reutilização: evita duplicação de validação

**Exemplo:**

```csharp
// Domain Service valida
public sealed class CreateOrderService
{
    private readonly IRepository<Order, Guid> _repository;

    public async ValueTask<Result> ExecuteAsync(CreateOrderCommand cmd, CancellationToken ct)
    {
        // Factory valida
        var orderResult = Order.Create(Guid.NewGuid(), cmd.CustomerId, cmd.Items);
        if (orderResult.IsFailure)
            return orderResult; // Falha de validação

        var order = orderResult.Value;

        // Repositório apenas persiste
        return await _repository.AddAsync(order, ct);
    }
}
```

---

### I11 — Concorrência: Dependente da Implementação

**Regra:** Segurança de concorrência (thread-safety, isolamento, identidade de instâncias) depende das implementações concretas e de suas condições de uso. O contrato não oferece nenhuma dessas garantias.

**Mecanismo:**
- Implementação concreta define seu modelo de concorrência
- Caller responsável por entender garantias da implementação que está usando

**Cenários comuns (não garantidos):**
- Múltiplos `GetByIdAsync` simultâneos: depende da implementação (ex.: um `DbContext` EF Core não é thread-safe)
- `AddAsync` + `AddAsync` com mesmo ID: o resultado depende da implementação (um banco relacional costuma rejeitar por constraint, mas não é garantido pelo contrato)
- Modificação durante `GetByIdAsync`: estado depende de isolamento transacional (ORM-dependente)

**Justificativa:**
- Contrato é agnóstico: não prescreve modelo de concorrência
- Implementações variam: BD in-memory, BD transacional, cache distribuído
- Responsabilidade: caller escolhe implementação apropriada para seu caso de uso

---

## Invariantes de Implementação

### IV1 — Identidade de Instâncias: Definida pela Implementação

**Regra:** O contrato **não** garante que chamadas repetidas a `GetByIdAsync` com o mesmo ID retornem instâncias distintas ou a mesma instância.

**Mecanismo:** Uma implementação com *identity map* ou cache (ex.: EF Core com tracking) pode devolver a mesma instância; outra pode materializar uma nova a cada chamada. Cada implementação documenta seu comportamento (ver I11).

**Consequência:** O caller não deve presumir isolamento entre instâncias nem compartilhamento de estado.

---

### IV2 — Sem Cache Implícito

**Expectativa:** O contrato não exige nem proíbe cache; se a implementação cachear, deve documentar essa característica.

**Mecanismo:** Implementação concreta decide.

---

## Semântica de Erros

### E1 — Agregado Não Encontrado (NotFound)

**ErrorType:** `ErrorType.NotFound`

**Error.Code:** Definido pela implementação (ex: `"Order.NotFound"`, `"Aggregates.NotFound"`)

**Contexto:** Agregado com ID solicitado não existe

**Retorno:** `Result<TAggregate>.Failure(error)` onde `error.Type == ErrorType.NotFound`

**Ação do Caller:** Validar ID, comunicar ausência (a camada de API decide o status de transporte), ou retry em contexto apropriado

---

### E2 — Duplicidade/Conflito (Conflict ou Failure)

**ErrorType:** `ErrorType.Conflict` (se operação violou invariante) ou `ErrorType.Failure` (genérico)

**Error.Code:** Definido pela implementação, independente de mecanismo (ex: não presuma "UniqueConstraintViolation" — BD pode usar trigger, app-level validation, etc.)

**Contexto:**
- `AddAsync` com agregado que já existe (identidade duplicada)
- `UpdateAsync` com conflito de versão concorrente
- Qualquer violação de invariante de dados

**Ação do Caller:** Retry em contexto apropriado, ou comunicar conflito ao usuário

---

### E3 — Falha Operacional (Failure)

**ErrorType:** `ErrorType.Failure`

**Error.Code:** Definido pela implementação (ex: `"Persistence.Timeout"`, `"Database.Unavailable"`)

**Contexto:** Erro técnico não recuperável (timeout, conexão recusada, erro de sintaxe SQL, etc.)

**Retorno:** `Result.Failure(error)`

**Ação do Caller:** Retry com backoff, fallback, ou escalar operacionalmente

---

### E4 — Operação Cancelada

**Mecanismo:** `CancellationToken` sinaliza; implementação lança `OperationCanceledException`

**Exceção:** `OperationCanceledException` (padrão .NET)

**Contexto:** Shutdown gracioso, timeout, cancelamento explícito

**Ação do Caller:** Capturar e tratar como interrupção técnica (não erro de domínio)

---

## Contrato de Versão

**Versão:** 1.0 (Implementado — FEATURE-0007)

**Próximas Versions:**

- **1.1:** Possíveis métodos de extensão de conveniência (não planejados nem aprovados)
- **2.0:** `ISpecificationRepository<TAggregate, TId>` para busca complexa (FEATURE-0008)
- **2.1:** Integração com Domain Events (`GetUncomittedEventsAsync` — FEATURE-0005+)
- **3.0:** Batch operations, se necessário

Alterações quebra-compatibilidade (ex: remover método) requerem novo ADR e major version bump.

---

**Versão:** 1.0 (Implementado — FEATURE-0007)  
**Status:** Architecture Gate APPROVED — implementado; aguardando auditoria independente

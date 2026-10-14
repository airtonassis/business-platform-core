# Componente: Repository

O `IRepository<TAggregate, TId>` é um contrato fundamental da Shared Foundation responsável por abstrair a persistência de agregados de domínio, preservando independência de infraestrutura e alinhamento com Domain-Driven Design (DDD).

## Propósito Arquitetural

Em Domain-Driven Design, um agregado é uma unidade coesa de entidades que:

1. É tratado como uma unidade de consistência nos limites de persistência
2. Obedece a invariantes internos imutáveis
3. É recuperado e armazenado através de sua raiz (`AggregateRoot<TId>`)

O padrão Repository oferece:

- **Abstração:** uma interface que oculta detalhes de persistência (EF Core, Dapper, SQL bruto, etc.)
- **Independência:** o domínio não conhece o ORM ou banco de dados
- **Testabilidade:** mocks e in-memory podem ser substituídos sem alterar Application Services
- **Consistência:** um único ponto de acesso para persistência de cada agregado

O componente `IRepository<TAggregate, TId>` fornece:

- Um contrato genérico para recuperação por ID
- Um contrato genérico para adição, atualização e remoção
- Retornos typed como `Result<TAggregate>` (alinhado com Error/Result Foundation)
- Signatures assíncronas (`Task`) com suporte a `CancellationToken`
- Proteção arquitetural contra repositórios para entidades internas ou value objects

## Responsabilidades

O `IRepository<TAggregate, TId>` deve:

- definir um contrato que qualquer persistência de agregado pode implementar;
- aceitar apenas raízes de agregado (`where TAggregate : AggregateRoot<TId>`);
- rejeitar em compilação repositórios para entidades internas ou value objects;
- fornecer operação de recuperação por chave primária (ID do agregado);
- fornecer operação de adição de novo agregado;
- fornecer operação de atualização de agregado existente;
- fornecer operação de remoção de agregado;
- retornar `Result<TAggregate>` ou `Result` para comunicar sucesso/falha esperados;
- suportar operações assíncronas com `Task` e `CancellationToken`;
- não impor eventos, especificações, paginação, transações explícitas ou busca complexa;
- ser independente de qualquer ORM ou framework de persistência específico;
- não prescrever garantias de concorrência, isolamento ou identidade de instâncias (dependem da implementação).

## Estrutura Conceitual

Um repositório é composto de operações:

```
IRepository<Order, Guid>
├── GetByIdAsync(orderId) → Result<Order>     (Recuperação)
├── AddAsync(order) → Result                   (Adição)
├── UpdateAsync(order) → Result                (Atualização)
└── RemoveAsync(order) → Result                (Remoção)
```

Cada operação:

- **Retorna `Result<T>` ou `Result`:** comunica sucesso ou falha esperada (ex: "agregado não encontrado" = falha legítima)
- **É assíncrona:** preparada para I/O remoto (banco de dados, cache, etc.)
- **Respeita `CancellationToken`:** habilitada para shutdown gracioso, timeouts, etc.
- **Persistência delegada:** commit, transação, atomicidade e durabilidade são definidos e documentados pela implementação; o contrato não os garante

## Tipo Genérico: TAggregate e TId

O contrato é genérico sobre dois tipos:

```csharp
public interface IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    // operações
}
```

### TAggregate

- Deve herdar de `AggregateRoot<TId>` (obrigatório pela restrição genérica)
- Nunca pode ser uma `Entity<TId>` que não seja raiz
- Nunca pode ser um `ValueObject`
- Nunca pode ser um tipo arbitrário

Exemplos válidos (ilustrativos; `Order`, `Customer` e `Invoice` são hipotéticos):

```csharp
public sealed class Order : AggregateRoot<Guid> { }
public sealed class Customer : AggregateRoot<CustomerId> { }
public sealed class Invoice : AggregateRoot<string> { }
```

Exemplos **inválidos** (pseudocódigo conceitual; `OrderLineRepository` etc. não existem — o ponto é que o fechamento genérico não compila):

```csharp
// ❌ OrderLine é entidade filha, não raiz
var repo = new OrderLineRepository();  // IRepository<OrderLine, Guid> — erro de compilação

// ❌ Address é value object
var repo = new AddressRepository();  // IRepository<Address, ???> — erro de compilação

// ❌ Tipo arbitrário
var repo = new UserRepository<string>();  // IRepository<string, Guid> — erro de compilação
```

### TId

- Deve ser não-nulo (`where TId : notnull`)
- Pode ser primitivo (`Guid`, `int`, `long`, `string`)
- Pode ser Value Object (desde que implemente igualdade corretamente)
- Sua validade semântica (ex: `Guid.Empty` é válido?) pertence ao agregado concreto

## Operações Mínimas

### GetByIdAsync

```csharp
Task<Result<TAggregate>> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
```

- **Retorna:** `Result<TAggregate>` com o agregado se encontrado, `Result.Failure(...)` se não
- **Semântica:** Recuperação por ID único. Agregado não encontrado = erro `ErrorType.NotFound` (falha esperada)
- **Cancellation:** Respeitável para shutdown ou timeout
- **Nota:** Cache, identidade de instâncias e isolamento dependem da implementação

### AddAsync

```csharp
Task<Result> AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
```

- **Semântica:** Inclusão de **agregado novo**. Não é upsert.
- **Parâmetro:** Agregado construído e validado (via factory com `Result<TAggregate>`)
- **Retorna:** `Result` indicando sucesso ou falha
- **Falha esperada:** Agregado com mesmo ID já existe (constraint de unicidade)
- **Nota:** Persistência (commit, durabilidade) = responsabilidade da implementação

### UpdateAsync

```csharp
Task<Result> UpdateAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
```

- **Semântica:** Atualização de **agregado existente**. Não é inclusão.
- **Parâmetro:** Agregado recuperado via `GetByIdAsync` e modificado
- **Retorna:** `Result` indicando sucesso ou falha
- **Falha esperada:** Agregado com esse ID não existe
- **Nota:** Persistência (commit, durabilidade) = responsabilidade da implementação

### RemoveAsync

```csharp
Task<Result> RemoveAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
```

- **Semântica:** Solicitação de remoção de agregado
- **Parâmetro:** Agregado já recuperado
- **Retorna:** `Result` indicando sucesso ou falha
- **Falha esperada:** Agregado não existe; constraint de integridade referencial
- **Nota:** A política de exclusão (física vs. lógica/soft delete) é definida funcionalmente pelo domínio/módulo; a implementação a executa tecnicamente, sem escolhê-la arbitrariamente

## Fora do Escopo Atual

A versão 1.0 do `IRepository<TAggregate, TId>` **não fornece**:

- ❌ Commit, SaveChanges explícitos
- ❌ Unit of Work (coordenação multi-agregado)
- ❌ Garantia de atomicidade entre múltiplos agregados
- ❌ Garantia de persistência durável imediata (commit automático)
- ❌ Transações explícitas (`BeginTransactionAsync`, `CommitAsync`)
- ❌ Busca complexa (`FindAsync(predicate)`, `GetAll()`)
- ❌ Especificações ou Paginação (FEATURE-0008, FEATURE-0009)
- ❌ Contagem agregada (`CountAsync()`)
- ❌ Operações bulk (`SaveBulkAsync`, `DeleteBulkAsync`)
- ❌ Eventos de domínio (`GetUncomittedEvents`)
- ❌ Auditoria (timestamps, user tracking)
- ❌ Versionamento otimista/pessimista
- ❌ Política de exclusão concreta (definida pelo domínio/módulo; executada pela implementação)

**Responsabilidades de quem chama:**
- Coordenação de múltiplos agregados (Application Service)
- Transações locais e persistência (implementação concreta do repositório)
- Strategy de recovery em caso de falha parcial
- Integração com Unit of Work ou padrões distribuídos (FEATURE-futura)

## Namespace

```csharp
Business.Platform.Core.Domain.Shared
```

Estrutura de diretórios:

```
src/Business.Platform.Core.Domain/
├── Shared/
│   ├── Repositories/
│   │   └── IRepository.cs
│   ├── Errors/
│   ├── Results/
│   └── ...
```

## Relação com Outras Foundations

### Error e Result

- `GetByIdAsync` retorna `Result<TAggregate>` — sucesso com valor ou falha com erro
- `AddAsync`, `UpdateAsync` e `RemoveAsync` retornam `Result` — sucesso ou falha esperada
- Agregados não encontrados retornam `Result.Failure` com um `Error` de `ErrorType.NotFound` (`Error.Code` definido pela implementação)
- Integração com `Error` e `Result` Foundations é **obrigatória**

### Entity, AggregateRoot, ValueObject

- `IRepository<TAggregate, TId>` aceita apenas `TAggregate : AggregateRoot<TId>`
- Garante que agregados reutilizem a identidade e igualdade de `Entity<TId>`
- Recusa repositórios para `Entity` não-raiz e `ValueObject`

### IDomainEvent (futuro)

- Versão 1.0 não captura eventos
- Versão 2.0+ pode incluir `GetUncomittedEventsAsync` (planejado para FEATURE-0005+)

## Padrão de Uso

> **Nota:** os exemplos abaixo são **pseudocódigo ilustrativo simplificado**: `Account`, `Order`, comandos e métodos de negócio não existem no repositório. Apenas `IRepository`, `Result` e `Error` são contratos reais.

### Exemplo 1: Application Service Que Recupera e Modifica Dois Agregados

```csharp
public sealed class TransferMoneyService
{
    private readonly IRepository<Account, Guid> _accountRepository;

    public TransferMoneyService(IRepository<Account, Guid> accountRepository)
    {
        _accountRepository = accountRepository;
    }

    public async ValueTask<Result> ExecuteAsync(Guid sourceAccountId, Guid destAccountId, decimal amount, CancellationToken ct)
    {
        // Recuperar agregados
        var sourceResult = await _accountRepository.GetByIdAsync(sourceAccountId, ct);
        if (sourceResult.IsFailure)
            return sourceResult; // Conta de origem não encontrada

        var destResult = await _accountRepository.GetByIdAsync(destAccountId, ct);
        if (destResult.IsFailure)
            return destResult; // Conta de destino não encontrada

        var source = sourceResult.Value;
        var destination = destResult.Value;

        // Modificar agregados (lógica de domínio)
        var transferResult = source.TransferTo(destination, amount);
        if (transferResult.IsFailure)
            return transferResult; // Transferência falhou (ex: saldo insuficiente)

        // Persistir: cada Result deve ser verificado individualmente
        var saveSource = await _accountRepository.UpdateAsync(source, ct);
        if (saveSource.IsFailure)
            return saveSource; // Falha reportada; o estado persistido da origem depende da implementação. O destino não foi tentado por este fluxo

        var saveDestination = await _accountRepository.UpdateAsync(destination, ct);
        if (saveDestination.IsFailure)
        {
            // ATENÇÃO: a primeira operação reportou sucesso e não há garantia de que foi revertida. O estado do destino é desconhecido/depende da implementação. O contrato não garante reversão.
            // Recuperação/compensação (retry, transação compensatória, Saga, etc.) depende de
            // uma estratégia definida pela aplicação ou por uma implementação transacional futura.
            return saveDestination;
        }

        return Result.Success();
    }
}
```

### Exemplo 2: Application Service Que Cria um Novo Agregado

```csharp
public sealed class CreateOrderHandler
{
    private readonly IRepository<Order, Guid> _orderRepository;

    public CreateOrderHandler(IRepository<Order, Guid> orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async ValueTask<Result<Guid>> HandleAsync(CreateOrderCommand command, CancellationToken ct)
    {
        // Factory valida e retorna Result
        var orderResult = Order.Create(
            Guid.NewGuid(),
            command.CustomerId,
            command.Items
        );

        if (orderResult.IsFailure)
            return orderResult.Error;

        var order = orderResult.Value;

        // Persistir novo agregado
        var saveResult = await _orderRepository.AddAsync(order, ct);
        if (saveResult.IsFailure)
            return saveResult.Error;

        return Result.Success(order.Id);
    }
}
```

---

**Versão:** 1.0 (Implementado — FEATURE-0007)  
**Status:** Architecture Gate APPROVED — implementado; aguardando auditoria independente
**Namespace:** `Business.Platform.Core.Domain.Shared`

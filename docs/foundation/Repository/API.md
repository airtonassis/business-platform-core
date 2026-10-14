# API Pública — Repository Foundation

Especificação completa da API pública do componente `IRepository<TAggregate, TId>`.

> **Nota sobre os exemplos C#:** os trechos de uso e os padrões são **pseudocódigo ilustrativo simplificado** (tipos como `Order`, `OrderDto`, `OrderErrors`, `IEventPublisher` e métodos de negócio não existem no repositório; fragmentos fora de método não compilam isoladamente). Apenas `IRepository<TAggregate, TId>`, `Result`, `Error` e `ErrorType` são contratos reais.

---

## Namespace

```csharp
Business.Platform.Core.Domain.Shared
```

---

## Interface: IRepository<TAggregate, TId>

### Genéricos

```csharp
public interface IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : notnull
{
    // Operações
}
```

### Restrições Genéricas

#### TAggregate

- **Restrição:** `where TAggregate : AggregateRoot<TId>`
- **Significado:** Apenas raízes de agregado podem ter repositórios
- **Validação:** Em compilação; violação é erro de compilação
- **Exemplos válidos:**
  ```csharp
  public sealed class Order : AggregateRoot<Guid> { }
  public sealed class Customer : AggregateRoot<CustomerId> { }
  ```
- **Exemplos inválidos:**
  ```csharp
  public sealed class OrderLine : Entity<Guid> { }  // ❌ Entity, não AggregateRoot
  public sealed class Address : ValueObject { }    // ❌ ValueObject
  var repo = serviceProvider.GetRequiredService<IRepository<OrderLine, Guid>>(); // Erro: não compila
  ```

#### TId

- **Restrição:** `where TId : notnull`
- **Significado:** Tipos não-nuláveis apenas
- **Validação:** Em compilação (aviso/erro de nulabilidade conforme o contexto nullable) e em runtime (o construtor de `Entity` rejeita `null` com `ArgumentNullException`)
- **Exemplos válidos:**
  - `Guid` (tipo de valor não anulável)
  - `int`, `long`, `string`
  - `public sealed class CustomerId : ValueObject { }` (Value Object não-nulo)
- **Exemplos inválidos:**
  - `Guid?` — restrição `notnull` rejeita em compilação
  - `int?` — rejeita em compilação

---

## Métodos de Operação

### GetByIdAsync

```csharp
/// <summary>
/// Recupera um agregado pelo seu identificador único.
/// </summary>
/// <param name="id">Identificador único do agregado (não pode ser null).</param>
/// <param name="cancellationToken">
/// Token para sinalização de cancelamento de operação (shutdown, timeout).
/// Padrão: CancellationToken.None (operação pode não ser cancelável em algumas implementações).
/// </param>
/// <returns>
/// Task que ao completar retorna Result&lt;TAggregate&gt;:
///
/// - Se agregado encontrado: Result.Success(agregado)
///   - Value: instância do agregado, conforme carregada pela implementação
///   - IsSuccess: true
///   - Error: Error.None
///
/// - Se agregado não encontrado: Result.Failure(error)
///   - Error.Type: ErrorType.NotFound
///   - Error.Code: definido pela implementação (ex: "Order.NotFound")
///   - IsFailure: true
///   - Value: indisponível (acesso lança InvalidOperationException)
///
/// - Se erro operacional (timeout, falha de conexão, etc.):
///   Result.Failure(error)
///   - Error.Type: ErrorType.Failure ou específico
///   - Error.Code: definido pela implementação (ex: "Persistence.Error")
///   - IsFailure: true
/// </returns>
/// <exception cref="ArgumentNullException">Se id é null (somente se TId permite null; notnull rejeita em compilação).</exception>
/// <exception cref="OperationCanceledException">Se cancellationToken sinaliza cancelamento.</exception>
/// <remarks>
/// GetByIdAsync NÃO lança exceção se agregado não é encontrado.
/// Usa padrão Result para comunicar falha esperada.
///
/// ErrorType.NotFound distingue "agregado não existe" de erro operacional.
///
/// O contrato não garante identidade de instâncias (nova instância ou
/// instância compartilhada), cache, determinismo, thread-safety nem
/// isolamento transacional. Essas propriedades dependem da implementação
/// concreta e de suas condições de uso (ver BUSINESS_RULES I11).
/// </remarks>
public Task<Result<TAggregate>> GetByIdAsync(
    TId id,
    CancellationToken cancellationToken = default);
```

#### Exemplos de Uso

```csharp
// Caso de sucesso
var orderId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
var result = await orderRepository.GetByIdAsync(orderId);

if (result.IsSuccess)
{
    var order = result.Value;
    Console.WriteLine($"Order found: {order.Id}");
}
else
{
    Console.WriteLine($"Error: {result.Error.Code}");
}

// Com cancellationToken
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var result = await orderRepository.GetByIdAsync(orderId, cts.Token);

// Pattern matching (C# 8+)
var message = await orderRepository.GetByIdAsync(orderId) switch
{
    { IsSuccess: true, Value: var order } => $"Order {order.Id} found",
    { IsFailure: true, Error.Code: "Aggregates.NotFound" } => "Order not found",
    { IsFailure: true, Error.Code: var code } => $"Error: {code}"
};
```

#### Semântica de Erro (Independente do Provedor)

O `Error.Type` (ErrorType) categoriza falha semanticamente. Os códigos abaixo são **exemplos ilustrativos**; `Error.Code` é definido pela implementação/módulo. `ErrorType` não conhece protocolos de transporte (ex.: HTTP); o mapeamento para status HTTP pertence à camada de API.

| ErrorType | Contexto | Exemplos de Error.Code | Ação |
|-----------|----------|----------------------|------|
| `NotFound` | Agregado não existe | `"Order.NotFound"`, `"Aggregates.NotFound"` | Validar ID; comunicar ausência |
| `Conflict` | Agregado duplicado ou conflito de versão | `"Order.AlreadyExists"`, `"Conflict"` | Retry ou resolver conflito |
| `Failure` | Erro operacional (timeout, BD indisponível) | `"Database.Timeout"`, `"Unavailable"` | Retry com backoff; escalar |
| `Unauthorized` | Identidade não autenticada (autenticação) | `"Access.Unauthenticated"` | Exigir autenticação |
| `Forbidden` | Identidade conhecida sem permissão (autorização) | `"Access.Denied"` | Negar acesso |
| `Validation` | Entrada ou regra de validação não atendida | `"Order.Invalid"` | Corrigir a entrada |

**Nota:** Error.Code é definido pela implementação e pode mapear diferentes mecanismos (constraints BD, triggers, validações app-level) para a mesma categoria semântica. Caller não presume detalhes de infraestrutura.

---

### AddAsync

```csharp
/// <summary>
/// Solicita inclusão de agregado novo (não é upsert).
/// </summary>
/// <param name="aggregate">
/// Agregado novo, construído e validado (ex: via factory Result&lt;TAggregate&gt;).
/// Não deve existir no repositório.
/// </param>
/// <param name="cancellationToken">Token de cancelamento.</param>
/// <returns>
/// Task que ao completar retorna Result:
///
/// - Se sucesso: Result.Success()
/// - Se agregado com mesmo ID existe: Result.Failure(erro constraint)
/// - Se erro operacional: Result.Failure(erro técnico)
/// </returns>
/// <exception cref="ArgumentNullException">Se aggregate é null.</exception>
/// <exception cref="OperationCanceledException">Se cancellationToken sinaliza cancelamento.</exception>
/// <remarks>
/// AddAsync semântica: "Incluir novo agregado".
/// Se agregado com esse ID existe, retorna erro (violação de constraint).
/// Não valida regras de negócio; assume agregado é válido.
///
/// Persistência (commit, transação, durabilidade) = responsabilidade da implementação.
/// O contrato não garante commit automático nem atomicidade; a implementação define e
/// documenta quando o agregado se torna durável.
/// </remarks>
public Task<Result> AddAsync(
    TAggregate aggregate,
    CancellationToken cancellationToken = default);
```

#### Exemplos de Uso

```csharp
// Construir agregado validado
var orderResult = Order.Create(
    Guid.NewGuid(),
    customerId,
    items,
    shippingAddress
);

if (orderResult.IsFailure)
    return orderResult.Error; // Validação de domínio falhou

var order = orderResult.Value;

// Persistir
var addResult = await orderRepository.AddAsync(order);

if (addResult.IsFailure)
    return addResult.Error; // Persistência falhou

return Result.Success(order.Id);

// Com cancellation
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var result = await orderRepository.AddAsync(order, cts.Token);

// Tratamento de erro específico
if (addResult.IsFailure)
{
    if (addResult.Error.Code == "Persistence.UniqueConstraintViolation")
    {
        // Agregado com este ID já existe
        return Result.Failure(OrderErrors.AlreadyExists);
    }

    // Outro erro de persistência
    return addResult.Error;
}
```

#### Semântica de Erro (códigos ilustrativos, definidos pela implementação)

| Error.Code | Significado | Ação |
|------------|------------|------|
| `"Persistence.UniqueConstraintViolation"` | ID já existe | Validar unicidade de ID antes de criar |
| `"Persistence.ForeignKeyViolation"` | Referência inválida | Validar que refs. existem antes de Add |
| `"Persistence.Error"` | Erro genérico | Retry; escalar |
| `"Persistence.Timeout"` | Timeout | Retry com backoff |

---

### UpdateAsync

```csharp
/// <summary>
/// Solicita atualização de agregado existente (não é inclusão).
/// </summary>
/// <param name="aggregate">
/// Agregado existente, recuperado via GetByIdAsync e modificado em memória.
/// </param>
/// <param name="cancellationToken">Token de cancelamento.</param>
/// <returns>
/// Task que ao completar retorna Result:
///
/// - Se sucesso: Result.Success()
/// - Se agregado não existe: Result.Failure(erro NotFound)
/// - Se erro operacional: Result.Failure(erro técnico)
/// </returns>
/// <exception cref="ArgumentNullException">Se aggregate é null.</exception>
/// <exception cref="OperationCanceledException">Se cancellationToken sinaliza cancelamento.</exception>
/// <remarks>
/// UpdateAsync semântica: "Atualizar agregado existente".
/// Se agregado com esse ID não existe, retorna erro (ErrorType.NotFound).
/// Não valida regras de negócio; assume agregado é válido.
///
/// Persistência (commit, transação, durabilidade) = responsabilidade da implementação.
/// O contrato não garante commit automático nem atomicidade; a implementação define e
/// documenta quando a alteração se torna durável.
///
/// Nota: Modificação em memória sem UpdateAsync não persiste.
/// </remarks>
public Task<Result> UpdateAsync(
    TAggregate aggregate,
    CancellationToken cancellationToken = default);
```

---

### RemoveAsync

```csharp
/// <summary>
/// Solicita a remoção de um agregado do repositório.
/// </summary>
/// <param name="aggregate">
/// Agregado a remover. Deve:
/// - Ter sido previamente recuperado via GetByIdAsync
/// - Ser válido (ID não muda)
///
/// Removê-lo não é validado; implementação tenta e retorna erro se não encontrado.
/// </param>
/// <param name="cancellationToken">Token de cancelamento.</param>
/// <returns>
/// Task que ao completar retorna Result:
///
/// - Se removido com sucesso: Result.Success()
///   - IsSuccess: true
///   - Error: Error.None
///
/// - Se agregado não encontrado: Result.Failure(error)
///   - Error.Type: ErrorType.NotFound
///   - IsFailure: true
///
/// - Se falha de constraint (ex: referência ativa): Result.Failure(error)
///   - Error.Code definido pela implementação (ex: "Persistence.ForeignKeyViolation")
///
/// - Se erro operacional: Result.Failure(error)
///   - Error.Code definido pela implementação (ex: "Persistence.Error")
/// </returns>
/// <exception cref="ArgumentNullException">Se aggregate é null.</exception>
/// <exception cref="OperationCanceledException">Se cancellationToken sinaliza cancelamento.</exception>
/// <remarks>
/// RemoveAsync semântica: remover agregado.
///
/// Persistência (deleção, cascata, constraints) é responsabilidade da implementação.
///
/// Após a remoção ser efetivada, a recuperação via GetByIdAsync tende a falhar com
/// NotFound; o momento e a visibilidade dependem da implementação e do mecanismo transacional.
///
/// Política de exclusão (física vs. lógica/soft delete) é definida funcionalmente pelo
/// domínio/módulo; a implementação apenas a executa tecnicamente (ver BUSINESS_RULES I9).
/// </remarks>
public Task<Result> RemoveAsync(
    TAggregate aggregate,
    CancellationToken cancellationToken = default);
```

#### Exemplos de Uso

```csharp
// Recuperar
var result = await orderRepository.GetByIdAsync(orderId);
if (result.IsFailure)
    return result; // Não encontrado

var order = result.Value;

// Validar que pode remover (lógica de domínio)
if (!order.CanBeCanceled)
    return Result.Failure(OrderErrors.CannotCancel);

// Remover
var removeResult = await orderRepository.RemoveAsync(order);
if (removeResult.IsFailure)
    return removeResult.Error;

return Result.Success();

// Com cancellation
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var result = await orderRepository.RemoveAsync(order, cts.Token);
```

#### Semântica de Erro

| Error.Code | Significado | Ação |
|------------|------------|------|
| `"Aggregates.NotFound"` | Agregado não existe (já removido?) | O caller decide se trata como idempotente |
| `"Persistence.ForeignKeyViolation"` | Referência ativa impede deleção | Validar que agregado não é referenciado |
| `"Persistence.Error"` | Erro genérico | Retry; escalar |

---

## Padrões de Uso

### Padrão 1: Recuperação Segura com Match

```csharp
var result = await repository.GetByIdAsync(id);

var message = result.Match(
    onSuccess: aggregate => $"Found: {aggregate.Id}",
    onFailure: error => $"Error: {error.Code}"
);

Console.WriteLine(message);
```

### Padrão 2: CQRS — Aplicação Service Orquestra

> Ilustrativo: `IEventPublisher` e `UncomittedEvents` não existem; o contrato `IRepository` não lida com eventos.

```csharp
public sealed class CreateOrderHandler
{
    private readonly IRepository<Order, Guid> _orderRepository;
    private readonly IEventPublisher _eventPublisher;

    public async ValueTask<Result<OrderDto>> HandleAsync(CreateOrderCommand cmd, CancellationToken ct)
    {
        // 1. Construir agregado (validado)
        var orderResult = Order.Create(Guid.NewGuid(), cmd.CustomerId, cmd.Items);
        if (orderResult.IsFailure)
            return orderResult.Error;

        var order = orderResult.Value;

        // 2. Persistir
        var addResult = await _orderRepository.AddAsync(order, ct);
        if (addResult.IsFailure)
            return addResult.Error;

        // 3. Publicar eventos (Application layer responsibility)
        foreach (var evt in order.UncomittedEvents)
            await _eventPublisher.PublishAsync(evt, ct);

        // 4. Retornar DTO
        return Result.Success(new OrderDto { Id = order.Id, Total = order.Total });
    }
}
```

### Padrão 3: Modificação e Atualização

```csharp
public sealed class UpdateOrderShippingHandler
{
    private readonly IRepository<Order, Guid> _orderRepository;

    public async ValueTask<Result> HandleAsync(UpdateOrderShippingCommand cmd, CancellationToken ct)
    {
        // 1. Recuperar
        var result = await _orderRepository.GetByIdAsync(cmd.OrderId, ct);
        if (result.IsFailure)
            return result;

        var order = result.Value;

        // 2. Modificar em memória
        var updateResult = order.UpdateShippingAddress(cmd.NewAddress);
        if (updateResult.IsFailure)
            return updateResult;

        // 3. Persistir a alteração (UpdateAsync; AddAsync não é upsert)
        return await _orderRepository.UpdateAsync(order, ct);
    }
}
```

**Nota:** A modificação em memória só é persistida por `UpdateAsync`; commit e transação são responsabilidade da implementação.

### Padrão 4: Remoção com Regra de Negócio

```csharp
public sealed class CancelOrderHandler
{
    private readonly IRepository<Order, Guid> _orderRepository;

    public async ValueTask<Result> HandleAsync(CancelOrderCommand cmd, CancellationToken ct)
    {
        // 1. Recuperar
        var result = await _orderRepository.GetByIdAsync(cmd.OrderId, ct);
        if (result.IsFailure)
            return result;

        var order = result.Value;

        // 2. Validar regra de negócio
        var cancelResult = order.Cancel();
        if (cancelResult.IsFailure)
            return cancelResult; // "Cannot cancel already processed order"

        // 3. Remover
        return await _orderRepository.RemoveAsync(order, ct);
    }
}
```

---

## Considerações de Implementação

### Task vs ValueTask

**Especificado:** `Task<Result<T>>`

**Justificativa:**
- Task é o padrão .NET adotado pelo contrato
- ValueTask é otimização de alocação, que pode ser usada em handlers e internamente em implementações
- Contrato permanece simples e compatível

**Compatibilidade:**
- Todos ORMs .NET modernos suportam Task (EF Core, Dapper, etc.)
- Transparente para caller (await funciona igual)
- Implementações podem usar ValueTask internamente sem alterar contrato

### CancellationToken = default

**Especificado:** Parâmetro com valor padrão

**Benefícios:**
- Uso simples: `await repo.GetByIdAsync(id)` sem argumentar token
- Habilitação opcional: `await repo.GetByIdAsync(id, cts.Token)` para advanced cases

**Implementação:**
```csharp
public async Task<Result<Order>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
{
    // implementação respeita cancellationToken
    // se cancellationToken sinaliza, lança OperationCanceledException
}
```

### Concorrência e Identidade de Instâncias

**O contrato não oferece garantias** de thread-safety, isolamento automático, identidade de instâncias ou cache. Tudo isso depende da implementação concreta e de suas condições de uso (BUSINESS_RULES I11).

**Dependem da implementação:**
- Chamadas concorrentes (ex.: um `DbContext` EF Core não é thread-safe)
- Escrita concorrente com ID duplicado (um banco relacional costuma rejeitar, mas o contrato não garante)
- Leitura durante escrita — isolamento transacional define visibilidade
- Nova instância ou instância compartilhada a cada consulta

---

## Versão da API

**Versão:** 1.0 (Implementado — FEATURE-0007)

**Estabilidade:** Contrato aprovado e implementado

**Roadmap:**

- **v1.0:** GetByIdAsync, AddAsync, UpdateAsync, RemoveAsync
- **v2.0:** ISpecificationRepository com FindAsync (Specification Foundation v2.0)
- **v2.1:** Paginação (Pagination Foundation)
- **v2.0+:** Eventos de domínio (quando FEATURE-0005 expande)

---

## Conversões Implícitas de Result<TValue>

`Result<TValue>` (não o `Result` não genérico) já define duas conversões implícitas:

```csharp
public static implicit operator Result<TValue>(TValue value);  // sucesso
public static implicit operator Result<TValue>(Error error);   // falha
```

Portanto, em um método que retorna `Result<T>` (ou `Task<Result<T>>`/`ValueTask<Result<T>>` em `async`), `return value;` e `return error;` são válidos. Em métodos que retornam `Result` não genérico **não** há conversão implícita: use `Result.Success()` e `Result.Failure(error)`.

> Nos exemplos acima, `return addResult.Error;` em um retorno `Result` não genérico é simplificação de pseudocódigo; o equivalente real é `return Result.Failure(addResult.Error);` (ou `return addResult;`).

---

**Status da Especificação:** Architecture Gate APPROVED — implementado (baseline `4562911`)  
**Data:** 2026-10-13  
**Baseline:** commit `4562911`

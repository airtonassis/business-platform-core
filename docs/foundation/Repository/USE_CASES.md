# Casos de Uso — Repository Foundation

> **Nota sobre os exemplos C#:** os trechos são **pseudocódigo ilustrativo simplificado**. Tipos de exemplo (`Order`, `Account`, DTOs, comandos, `Mock<>`) e métodos de negócio não existem no repositório e não são compiláveis isoladamente. Apenas `IRepository<TAggregate, TId>`, `Result` e `Error` são contratos reais.

## UC01 — Recuperar Agregado por ID

**Atores:** Application Service, Domain Service, Query Handler

**Pré-condições:**
- `IRepository<TAggregate, TId>` está registrado no container
- `TId` (ID do agregado) é válido e conhecido

**Fluxo Principal:**

```
1. Caller chama: await repository.GetByIdAsync(id, cancellationToken)
2. Repositório consulta armazenamento (BD, cache, etc.)
3. Se encontrado:
   - Retorna Result<TAggregate>.Success com agregado
4. Se não encontrado:
   - Retorna Result<TAggregate>.Failure com erro de ErrorType.NotFound
5. Se erro operacional (timeout, falha de conexão):
   - Retorna Result<TAggregate>.Failure com erro de persistência
```

**Fluxo de Código:**

```csharp
public sealed class GetOrderByIdHandler
{
    private readonly IRepository<Order, Guid> _orderRepository;

    public GetOrderByIdHandler(IRepository<Order, Guid> orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async ValueTask<Result<OrderDto>> HandleAsync(GetOrderByIdQuery query, CancellationToken ct)
    {
        // UC01: Recuperar agregado
        var result = await _orderRepository.GetByIdAsync(query.OrderId, ct);

        if (result.IsFailure)
            return result.Error; // Order not found

        var order = result.Value;

        // Mapear para DTO
        return Result.Success(new OrderDto
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            Total = order.CalculateTotal()
        });
    }
}
```

**Pós-condições:**
- Agregado recuperado reflete o estado persistido, conforme a implementação
- Mudanças feitas em memória não são persistidas sem `UpdateAsync`

**Variações:**
- **Agregado não encontrado:** Retorna `Failure` com erro específico
- **Timeout:** Retorna `Failure` com erro de persistência
- **Cancelamento:** Lança `OperationCanceledException`

**Questões Abertas:**
- O `Error.Code` de "não encontrado" é definido pela implementação/módulo; o contrato exige apenas `ErrorType.NotFound`.

---

## UC02 — Adicionar Novo Agregado

**Atores:** Application Service, Domain Service

**Pré-condições:**
- Agregado foi construído e validado (ex: via factory com `Result<TAggregate>`)
- ID é válido e único
- `IRepository<TAggregate, TId>` está registrado

**Fluxo Principal:**

```
1. Domain Service ou Application Service constrói agregado (validado)
2. Caller chama: await repository.AddAsync(aggregate, cancellationToken)
3. Repositório solicita a persistência do agregado (durabilidade e atomicidade conforme a implementação)
4. Se sucesso:
   - Retorna Result.Success()
5. Se falha (ex: constraint violation):
   - Retorna Result.Failure com erro específico
6. Se erro operacional:
   - Retorna Result.Failure com erro de persistência
```

**Fluxo de Código:**

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
        // Domain validation: Factory com Result
        var orderResult = Order.Create(
            Guid.NewGuid(),
            command.CustomerId,
            command.Items,
            command.ShippingAddress
        );

        if (orderResult.IsFailure)
            return orderResult.Error; // Validação falhou

        var order = orderResult.Value;

        // UC02: Adicionar agregado
        var addResult = await _orderRepository.AddAsync(order, ct);

        if (addResult.IsFailure)
            return addResult.Error; // Persistência falhou

        return Result.Success(order.Id);
    }
}
```

**Pós-condições:**
- Em caso de sucesso, a implementação aceitou a inclusão do agregado; durabilidade e atomicidade dependem da implementação e do mecanismo transacional usado
- ID permanece estável
- Agregado pode ser recuperado via `GetByIdAsync` conforme as garantias de visibilidade da implementação (ex.: após commit)

**Variações:**
- **Constraint violation:** Retorna `Failure` com erro de unicidade (ex: "Order.DuplicateReference")
- **Timeout:** Retorna `Failure` com erro de persistência
- **Cancelamento:** Lança `OperationCanceledException`

**Questões Abertas:**
- Se `AddAsync` falha, o agregado pode ter sido persistido parcialmente ou totalmente revertido? O contrato não garante reversão; a implementação deve documentar seu comportamento.

---

## UC03 — Remover Agregado Existente

**Atores:** Application Service, Domain Service

**Pré-condições:**
- Agregado foi recuperado via `GetByIdAsync`
- Regras de negócio permitem remoção (ex: Order não foi processado)
- `IRepository<TAggregate, TId>` está registrado

**Fluxo Principal:**

```
1. Caller recupera agregado: await repository.GetByIdAsync(id)
2. Valida regras de remoção (ex: status deve ser "Pending")
3. Se validação OK:
   - Chama: await repository.RemoveAsync(aggregate)
4. Se sucesso:
   - Retorna Result.Success()
5. Se falha (ex: constraint de integridade referencial):
   - Retorna Result.Failure
6. Se erro operacional:
   - Retorna Result.Failure
```

**Fluxo de Código:**

```csharp
public sealed class CancelOrderHandler
{
    private readonly IRepository<Order, Guid> _orderRepository;

    public CancelOrderHandler(IRepository<Order, Guid> orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async ValueTask<Result> HandleAsync(CancelOrderCommand command, CancellationToken ct)
    {
        // UC01: Recuperar agregado
        var result = await _orderRepository.GetByIdAsync(command.OrderId, ct);

        if (result.IsFailure)
            return result; // Order not found

        var order = result.Value;

        // Domain logic: validar cancelamento
        var cancelResult = order.Cancel();
        if (cancelResult.IsFailure)
            return cancelResult; // Order já foi processada, não pode cancelar

        // UC03: Remover agregado
        var removeResult = await _orderRepository.RemoveAsync(order, ct);

        if (removeResult.IsFailure)
            return removeResult; // Persistência falhou

        return Result.Success();
    }
}
```

**Pós-condições:**
- Em caso de sucesso, a implementação aceitou a remoção; atomicidade e momento de efetivação dependem da implementação e do mecanismo transacional
- Após a remoção ser efetivada, a recuperação via `GetByIdAsync` tende a retornar `Failure` (ex.: `NotFound`), conforme as garantias da implementação
- Integridade referencial e cascata são definidas pela implementação/política do módulo

**Variações:**
- **Remoção não permitida:** Retorna `Failure` com erro de validação (ex: "Order.CannotCancel")
- **Constraint de integridade:** Retorna `Failure` (ex: "Persistence.ForeignKeyViolation")
- **Timeout:** Retorna `Failure`
- **Cancelamento:** Lança `OperationCanceledException`

---

## UC04 — Recuperar, Modificar e Atualizar Agregado

**Atores:** Application Service, Domain Service

**Pré-condições:**
- Agregado existe no repositório
- Mudanças são permitidas pelas regras de negócio

**Fluxo Principal:**

```
1. Caller recupera agregado: await repository.GetByIdAsync(id)
2. Valida que agregado existe (IsSuccess)
3. Modifica agregado em memória (via métodos de negócio)
4. Persiste mudanças: await repository.UpdateAsync(agregado)
5. Se sucesso:
   - Retorna Result.Success()
6. Se falha:
   - Retorna Result.Failure (agregado não existe, error técnico, etc.)
```

**Fluxo de Código:**

```csharp
public sealed class UpdateOrderShippingHandler
{
    private readonly IRepository<Order, Guid> _orderRepository;

    public UpdateOrderShippingHandler(IRepository<Order, Guid> orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Result> HandleAsync(UpdateOrderShippingCommand command, CancellationToken ct)
    {
        // UC01: Recuperar agregado existente
        var result = await _orderRepository.GetByIdAsync(command.OrderId, ct);

        if (result.IsFailure)
            return result; // Order not found

        var order = result.Value;

        // Domain logic: Modificar em memória
        var updateResult = order.UpdateShippingAddress(command.NewAddress);
        if (updateResult.IsFailure)
            return updateResult; // Atualização não permitida (ex: Order já foi enviado)

        // UC04: Persistir mudanças via UpdateAsync
        return await _orderRepository.UpdateAsync(order, ct);
    }
}
```

**Pós-condições:**
- Se sucesso: é o resultado reportado pela operação; durabilidade e visibilidade dependem da implementação
- Se falha: Agregado pode estar parcialmente modificado em BD (depende de implementação)

**Nota sobre Persistência e Atomicidade:**
- Sem garantia de commit durável imediato (implementação define quando)
- Sem garantia de rollback em caso de erro subsequente
- Sem garantia de atomicidade com outras operações
- Se UpdateAsync falha, o estado persistido pode ser desconhecido e o estado anterior não é necessariamente restaurado
- Application Service responsável por strategy de recovery (retry, compensating, etc.)

**Variações:**
- **Update não permitido:** Retorna `Failure` com erro de validação
- **Agregado não existe:** Retorna `Failure` com `ErrorType.NotFound`
- **Erro técnico:** Retorna `Failure` com erro operacional

---

## UC05 — Múltiplos Agregados: Escopo e Responsabilidade

**Atores:** Application Service (orquestrador)

**Contexto:**
- Operações que afetam múltiplos agregados (ex: TransferMoney entre contas) são **fora do escopo do repositório**
- Repositório é responsável apenas por operações de um agregado
- Coordenação é responsabilidade da Application Service ou de patterns arquiteturais adicionais

**Pré-condições:**
- Dois ou mais agregados existem
- Operação requer modificações coordenadas
- Ambos repositórios estão registrados

**Fluxo Principal:**

```
1. Application Service recupera múltiplos agregados (1..N chamas GetByIdAsync)
2. Valida pré-condições (ambos existem)
3. Aplica lógica de domínio (ex: debita de uma, credita em outra)
4. Persiste ambos (1..N chamadas `UpdateAsync`, `AddAsync` ou `RemoveAsync`, conforme a intenção de cada agregado)
5. Se qualquer persistência falha:
   - Application Service decide estratégia de recovery
   - Opções: retry, log de falha parcial, compensating action, etc.
```

**Fluxo de Código:**

```csharp
public sealed class TransferMoneyService
{
    private readonly IRepository<Account, Guid> _accountRepository;

    public TransferMoneyService(IRepository<Account, Guid> accountRepository)
    {
        _accountRepository = accountRepository;
    }

    public async Task<Result> ExecuteAsync(Guid sourceId, Guid destId, decimal amount, CancellationToken ct)
    {
        // Recuperar múltiplos agregados
        var sourceResult = await _accountRepository.GetByIdAsync(sourceId, ct);
        if (sourceResult.IsFailure)
            return sourceResult; // Source not found

        var destResult = await _accountRepository.GetByIdAsync(destId, ct);
        if (destResult.IsFailure)
            return destResult; // Destination not found

        var source = sourceResult.Value;
        var destination = destResult.Value;

        // Lógica de domínio: Transferência entre agregados
        var transferResult = source.TransferOut(amount);
        if (transferResult.IsFailure)
            return transferResult; // Source saldo insuficiente

        destination.TransferIn(amount);

        // Persistir mudanças — Application Service coordena
        var saveSourceResult = await _accountRepository.UpdateAsync(source, ct);
        if (saveSourceResult.IsFailure)
            return saveSourceResult; // Falha reportada em source (estado persistido depende da implementação); destination não foi tentado por este fluxo

        var saveDestResult = await _accountRepository.UpdateAsync(destination, ct);
        // Se falha aqui: a falha de Destination é reportada (seu estado persistido depende da implementação);
        // a operação em Source, que reportou sucesso, não é necessariamente revertida
        // Estratégia de recovery é responsabilidade da aplicação:
        //   - Retry do conjunto completo
        //   - Compensating transaction (reverter Source)
        //   - Log para análise manual
        //   - Outro padrão (ex: Saga, eventual consistency)
        if (saveDestResult.IsFailure)
            return saveDestResult;

        return Result.Success();
    }
}
```

**Pós-condições:**
- Se ambas as operações retornam sucesso: é o resultado reportado por cada uma; durabilidade, visibilidade e atomicidade conjunta não são garantidas pelo contrato
- Se falha: estado pode ser inconsistente (um agregado modificado, outro não); Application Service define estratégia

**Nota Importante:**
- IRepository não oferece transações, Unit of Work ou coordenação de múltiplos agregados
- Coordenação é responsabilidade da **Application Service**
- Estratégias de recovery (retry, compensating transaction, Saga pattern, etc.) são externas ao repositório
- Unit of Work ou similar pode ser adicionado em FEATURE-futura como padrão arquitetural separado

---

## UC06 — Testes Unitários com Mock de Repositório

**Atores:** Test Developer, Application/Domain Service Tests

**Pré-condições:**
- Application Service depende de `IRepository<TAggregate, TId>`
- Teste precisa mockear persistência

**Fluxo de Teste:**

```csharp
public sealed class CreateOrderHandlerTests
{
    [Fact]
    public async Task Handle_WithValidCommand_ReturnsOrderId()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var mockRepository = new Mock<IRepository<Order, Guid>>();

        // Mock GetByIdAsync
        mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken ct) =>
                Result.Success<Order>(Order.Create(id, Guid.NewGuid(), []).Value));

        // Mock AddAsync
        mockRepository
            .Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var handler = new CreateOrderHandler(mockRepository.Object);
        var command = new CreateOrderCommand(Guid.NewGuid(), []);

        // Act
        var result = await handler.HandleAsync(command, default);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        mockRepository.Verify(r => r.AddAsync(It.IsAny<Order>(), default), Times.Once);
    }
}
```

**Pós-condições:**
- Application Service foi testada isoladamente de infraestrutura
- A lógica do Application Service foi exercitada contra um dublê do repositório, sem banco de dados real (o comportamento de persistência real não é validado)

**Benefícios:**
- Testes executam rapidamente
- Sem dependência de banco de dados
- Pode simular falhas de persistência facilmente

---

## UC07 — Testes Arquiteturais: Verificar que Repositório não Tem Dependências Proibidas

**Atores:** Architecture Test Developer

**Pré-condições:**
- `IRepository<TAggregate, TId>` foi definida em `Domain.Shared`
- Testes arquiteturais estão configurados

**Fluxo de Teste:**

```csharp
[Fact]
public void IRepository_ShouldNotDependOnInfrastructure()
{
    // NetArchTest: verifica que tipos em Domain.Shared.Repositories
    // não importam de Application, Infrastructure, API, EntityFrameworkCore, etc.
    var result = Types.InAssembly(typeof(IRepository<,>).Assembly)
        .That()
        .ResideInNamespace("Business.Platform.Core.Domain.Shared")
        .ShouldNot()
        .HaveDependencyOnAny([
            "Business.Platform.Core.Application",
            "Business.Platform.Core.Infrastructure",
            "Microsoft.EntityFrameworkCore",
            "System.Data"
        ])
        .GetResult();

    result.IsSuccessful.ShouldBeTrue();
}
```

**Pós-condições:**
- Contrato permanece independente de infraestrutura
- Violações são detectadas pelo teste arquitetural (a regra real está em `RepositoryArchitectureTests`)

---

## Resumo de Casos de Uso

| UC | Nome | Atores | Suportado | Escopo |
|----|------|--------|-----------|--------|
| UC01 | Recuperar por ID | App Service, Query Handler | ✅ GetByIdAsync | Repositório |
| UC02 | Adicionar Novo | App Service, Domain Service | ✅ AddAsync | Repositório |
| UC03 | Remover | App Service, Domain Service | ✅ RemoveAsync | Repositório |
| UC04 | Modificar (Update) | App Service, Domain Service | ✅ UpdateAsync | Repositório |
| UC05 | Múltiplos Agregados | Application Service | ⚠️ Multi-repo calls | Application Service |
| UC06 | Teste Unitário | Test Developer | ✅ Mock IRepository | Test Framework |
| UC07 | Teste Arquitetural | Arch Tester | ✅ NetArchTest | Test Framework |

---

**Versão:** 1.0 (Implementado — FEATURE-0007)  
**Status:** Architecture Gate APPROVED — implementado; aguardando auditoria independente

**Notas Importantes:**
- UC04 (Update) é suportado por `UpdateAsync`; `AddAsync` é exclusivamente inclusão (não é upsert)
- UC05 (Múltiplos Agregados) é responsabilidade da Application Service, não do repositório
- Sem SaveAsync, Unit of Work ou Saga pattern obrigatório neste contrato

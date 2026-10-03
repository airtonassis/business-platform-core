# Exemplos de Uso — Result

Demonstração prática de como utilizar o `Result` em serviços de domínio e casos de uso da aplicação.

---

## 1. Retornando Sucesso e Falha com Validação

```csharp
public class UserErrors
{
    public static readonly Error EmailEmpty = new(
        "User.EmailEmpty",
        "Email não pode estar vazio.",
        ErrorType.Validation);
    
    public static readonly Error EmailDuplicate = new(
        "User.EmailDuplicate",
        "Email já está registrado.",
        ErrorType.Conflict);
}

public class CreateUserService
{
    public Result<User> CreateUser(string name, string email)
    {
        // Validação
        if (string.IsNullOrWhiteSpace(email))
        {
            return UserErrors.EmailEmpty;  // Conversão implícita: Error → Result<User>
        }

        // Criar entidade
        var user = new User(name, email);
        return user;  // Conversão implícita: User → Result<User>
    }
}

// Consumindo em Application Service
var result = userService.CreateUser("João", "joao@email.com");

if (result.IsFailure)
{
    logger.LogWarning("Falha ao criar usuário: {Code} - {Description}", 
        result.Error.Code, 
        result.Error.Description);
    
    return BadRequest(result.Error);
}

return Ok(result.Value);
```

---

## 2. Usando Match para Consumir Result (Padrão Principal)

### Match com sucesso tipado (Result<T>)

```csharp
var result = userService.CreateUser("João", "joao@email.com");

// Match com 2 delegates: onSuccess(value) e onFailure(error)
string message = result.Match(
    onSuccess: user => $"Usuário criado: {user.Name}",
    onFailure: error => $"Erro: {error.Description}"
);

return Ok(new { message });
```

### Match para executar ações (sem retorno tipado)

```csharp
var result = userService.CreateUser("João", "joao@email.com");

// Match com 2 delegates: onSuccess() sem parâmetro e onFailure(error)
result.Match(
    onSuccess: () => 
    {
        // Lógica de sucesso para Result não-genérico
        logger.LogInformation("Operação concluída com sucesso");
        return "OK";
    },
    onFailure: error => 
    {
        logger.LogWarning("Operação falhou: {Code}", error.Code);
        return "ERROR";
    }
);
```

---

## 3. Decisões de Negócio Baseadas em Resultado

```csharp
public class PaymentService
{
    public Result ProcessPayment(decimal amount, User user)
    {
        if (amount <= 0)
        {
            return Result.Failure(
                new Error(
                    "Payment.InvalidAmount",
                    "Valor deve ser maior que zero.",
                    ErrorType.Validation));
        }

        if (user.Balance < amount)
        {
            return Result.Failure(
                new Error(
                    "Payment.InsufficientFunds",
                    "Saldo insuficiente.",
                    ErrorType.Failure));
        }

        // Processar pagamento
        user.Debit(amount);
        return Result.Success();  // Result não-genérico: apenas IsSuccess/IsFailure
    }
}

// Consumidor
var paymentResult = paymentService.ProcessPayment(100, user);

paymentResult.Match(
    onSuccess: () => 
    {
        logger.LogInformation("Pagamento processado");
        return "Pagamento realizado com sucesso";
    },
    onFailure: error => 
    {
        return $"Pagamento falhou: {error.Description}";
    }
);
```

---

## 4. Fluxo de Erros com Conversão Implícita

```csharp
public class OrderService
{
    public Result<Order> CreateOrder(User user, List<Item> items)
    {
        // Validar items
        if (items.Count == 0)
        {
            return OrderErrors.EmptyItems;  // Error → Result<Order> implicitamente
        }

        // Validar user
        if (!user.IsActive)
        {
            return OrderErrors.UserInactive;  // Error → Result<Order> implicitamente
        }

        var order = new Order(user, items);
        return order;  // Order → Result<Order> implicitamente
    }
}

var orderResult = orderService.CreateOrder(user, items);

// Padrão: sempre verificar IsFailure primeiro
if (orderResult.IsFailure)
{
    throw new InvalidOperationException($"Falha: {orderResult.Error.Code}");
}

var order = orderResult.Value;  // Acesso seguro a Value
```

---

## 5. Casos Especiais: Conversões Implícitas e Validações

### Conversão implícita rejeita null

```csharp
// Isto lança ArgumentNullException:
Result<User> result = (User?)null;  // ❌ ArgumentNullException

// Correto:
Result<User> result = Result.Success(user);  // ✓ user não pode ser null
```

### Conversão implícita de Error rejeita Error.None

```csharp
// Isto lança ArgumentException:
Result<User> result = Error.None;  // ❌ ArgumentException

// Correto, com error válido:
Result<User> result = new Error("User.NotFound", "...");  // ✓
```

### Match com delegates null lança exceção

```csharp
var result = Result.Success<int>(42);

// Isto lança ArgumentNullException:
result.Match(
    onSuccess: null,  // ❌ ArgumentNullException
    onFailure: error => "error"
);

// Correto:
result.Match(
    onSuccess: value => value.ToString(),  // ✓
    onFailure: error => error.Code
);
```

---

## 6. Encadeamento de Operações (Padrão Funcional)

```csharp
public class OrderProcessingService
{
    public Result<Order> ProcessOrder(CreateOrderRequest request)
    {
        return ValidateRequest(request)
            .Match(
                onSuccess: validRequest =>
                {
                    var order = new Order(validRequest);
                    return order;  // Conversão implícita: Order → Result<Order>
                },
                onFailure: error => 
                {
                    // Se falhar validação, retorna erro
                    return (Result<Order>)error;  // Conversão explícita para type clarity
                }
            );
    }

    private Result<CreateOrderRequest> ValidateRequest(CreateOrderRequest request)
    {
        if (string.IsNullOrEmpty(request.UserId))
        {
            return OrderErrors.UserIdRequired;  // Error → Result<CreateOrderRequest>
        }

        return request;  // CreateOrderRequest → Result<CreateOrderRequest>
    }
}
```
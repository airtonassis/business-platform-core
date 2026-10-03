# API Pública — Result

Especificação completa da API pública do componente `Result` e `Result<TValue>`.

---

## Namespace

```csharp
Business.Platform.Core.Domain.Shared
```

---

## Classe Base: Result

### Propriedades

```csharp
/// <summary>
/// Indica se a operação resultou em sucesso.
/// </summary>
public bool IsSuccess { get; }

/// <summary>
/// Indica se a operação resultou em falha. Equivalente a !IsSuccess.
/// </summary>
public bool IsFailure { get; }

/// <summary>
/// Erro associado ao resultado.
/// - Em Success: sempre Error.None
/// - Em Failure: sempre um Error válido diferente de Error.None
/// Nunca é null após construção.
/// </summary>
public Error Error { get; }
```

### Método de Factory: Success (Não-Genérico)

```csharp
/// <summary>
/// Cria um Result de sucesso sem valor de retorno.
/// </summary>
/// <returns>Result com IsSuccess=true e Error=Error.None.</returns>
public static Result Success();
```

**Uso:**
```csharp
var result = Result.Success();
// result.IsSuccess == true
// result.Error == Error.None
```

### Método de Factory: Failure (Não-Genérico)

```csharp
/// <summary>
/// Cria um Result de falha.
/// </summary>
/// <param name="error">Erro que ocorreu. Não pode ser null nem Error.None.</param>
/// <returns>Result com IsFailure=true e Error=error.</returns>
/// <exception cref="ArgumentNullException">Se error é null.</exception>
/// <exception cref="ArgumentException">Se error é Error.None.</exception>
public static Result Failure(Error error);
```

**Uso:**
```csharp
var result = Result.Failure(UserErrors.NotFound);
// result.IsFailure == true
// result.Error == UserErrors.NotFound
```

---

## Classe Genérica: Result<TValue> : Result

### Propriedades

```csharp
/// <summary>
/// Valor do resultado quando IsSuccess é true.
/// </summary>
/// <returns>O valor armazenado.</returns>
/// <exception cref="InvalidOperationException">Se IsFailure é true.</exception>
public TValue Value { get; }
```

**Acesso Seguro:**
```csharp
if (result.IsSuccess)
{
    var value = result.Value;  // Seguro
}
// Nunca acessar Value sem verificar IsSuccess
```

### Método de Factory: Success<TValue>

```csharp
/// <summary>
/// Cria um Result de sucesso com valor tipado.
/// </summary>
/// <typeparam name="TValue">Tipo do valor retornado.</typeparam>
/// <param name="value">Valor do resultado. Não pode ser null.</param>
/// <returns>Result&lt;TValue&gt; com IsSuccess=true, Error=Error.None e Value=value.</returns>
/// <exception cref="ArgumentNullException">Se value é null.</exception>
public static Result<TValue> Success<TValue>(TValue value);
```

**Uso:**
```csharp
var user = new User("João", "joao@email.com");
var result = Result.Success(user);
// result.IsSuccess == true
// result.Value == user
// result.Error == Error.None
```

### Método de Factory: Failure<TValue>

```csharp
/// <summary>
/// Cria um Result de falha tipado.
/// </summary>
/// <typeparam name="TValue">Tipo do valor que teria sido retornado.</typeparam>
/// <param name="error">Erro que ocorreu. Não pode ser null nem Error.None.</param>
/// <returns>Result&lt;TValue&gt; com IsFailure=true e Error=error.</returns>
/// <exception cref="ArgumentNullException">Se error é null.</exception>
/// <exception cref="ArgumentException">Se error é Error.None.</exception>
public static Result<TValue> Failure<TValue>(Error error);
```

**Uso:**
```csharp
var result = Result.Failure<User>(UserErrors.NotFound);
// result.IsFailure == true
// result.Error == UserErrors.NotFound
// result.Value → lança InvalidOperationException
```

### Operador de Conversão Implícita: TValue → Result<TValue>

```csharp
/// <summary>
/// Converte implicitamente um valor TValue para Result&lt;TValue&gt; de sucesso.
/// Equivalente a Result.Success&lt;TValue&gt;(value).
/// </summary>
/// <param name="value">Valor a converter. Não pode ser null.</param>
/// <returns>Result&lt;TValue&gt; com IsSuccess=true.</returns>
/// <exception cref="ArgumentNullException">Se value é null.</exception>
public static implicit operator Result<TValue>(TValue value);
```

**Uso:**
```csharp
Result<User> result = user;  // Conversão implícita
// Equivalente a Result.Success(user)
```

### Operador de Conversão Implícita: Error → Result<TValue>

```csharp
/// <summary>
/// Converte implicitamente um Error para Result&lt;TValue&gt; de falha.
/// Equivalente a Result.Failure&lt;TValue&gt;(error).
/// </summary>
/// <param name="error">Erro a converter. Não pode ser null nem Error.None.</param>
/// <returns>Result&lt;TValue&gt; com IsFailure=true.</returns>
/// <exception cref="ArgumentNullException">Se error é null.</exception>
/// <exception cref="ArgumentException">Se error é Error.None.</exception>
public static implicit operator Result<TValue>(Error error);
```

**Uso:**
```csharp
Result<User> result = UserErrors.NotFound;  // Conversão implícita
// Equivalente a Result.Failure<User>(UserErrors.NotFound)
```

### Método: Match (Sobrecarada 1 - Sem Valor)

```csharp
/// <summary>
/// Padrão de consumo: executa onSuccess() ou onFailure(error) conforme o estado.
/// Sobrecarada para Result sem valor tipado.
/// </summary>
/// <typeparam name="TResult">Tipo de retorno do Match.</typeparam>
/// <param name="onSuccess">
/// Delegate executado se IsSuccess.
/// Não recebe parâmetros; retorna TResult.
/// Não pode ser null.
/// </param>
/// <param name="onFailure">
/// Delegate executado se IsFailure.
/// Recebe o Error como parâmetro; retorna TResult.
/// Não pode ser null.
/// </param>
/// <returns>Resultado de onSuccess() ou onFailure(error) conforme o estado.</returns>
/// <exception cref="ArgumentNullException">Se onSuccess ou onFailure é null.</exception>
public TResult Match<TResult>(
    Func<TResult> onSuccess,
    Func<Error, TResult> onFailure);
```

**Uso (Result Não-Genérico):**
```csharp
Result result = Result.Success();
string message = result.Match(
    onSuccess: () => "Operação concluída",
    onFailure: error => $"Erro: {error.Description}"
);
// message == "Operação concluída"
```

### Método: Match (Sobrecarada 2 - Com Valor)

```csharp
/// <summary>
/// Padrão de consumo: executa onSuccess(value) ou onFailure(error) conforme o estado.
/// Sobrecarada para Result com valor tipado.
/// </summary>
/// <typeparam name="TResult">Tipo de retorno do Match.</typeparam>
/// <param name="onSuccess">
/// Delegate executado se IsSuccess.
/// Recebe Value (tipo TValue) como parâmetro; retorna TResult.
/// Não pode ser null.
/// </param>
/// <param name="onFailure">
/// Delegate executado se IsFailure.
/// Recebe o Error como parâmetro; retorna TResult.
/// Não pode ser null.
/// </param>
/// <returns>Resultado de onSuccess(value) ou onFailure(error) conforme o estado.</returns>
/// <exception cref="ArgumentNullException">Se onSuccess ou onFailure é null.</exception>
public TResult Match<TResult>(
    Func<TValue, TResult> onSuccess,
    Func<Error, TResult> onFailure);
```

**Uso (Result<TValue>):**
```csharp
Result<User> result = Result.Success(user);
string message = result.Match(
    onSuccess: user => $"Bem-vindo {user.Name}",
    onFailure: error => $"Erro: {error.Description}"
);
// message == "Bem-vindo João"
```

---

## Construtor Protegido (Uso Interno Apenas)

```csharp
/// <summary>
/// Construtor protegido para inicializar Result com estado imutável.
/// Uso interno: chamado por factories e subclasses.
/// </summary>
/// <param name="isSuccess">Indica se o resultado é sucesso.</param>
/// <param name="error">Erro associado (Error.None se sucesso, error válido se falha).</param>
/// <remarks>
/// Invariantes mantidas internamente:
/// - Se isSuccess=true: error deve ser Error.None
/// - Se isSuccess=false: error deve ser válido e ≠ Error.None
/// - error nunca é null
/// </remarks>
protected Result(bool isSuccess, Error error);

/// <summary>
/// Construtor protegido-interno para inicializar Result&lt;TValue&gt;.
/// Uso: factories Success&lt;TValue&gt; e Failure&lt;TValue&gt;.
/// </summary>
/// <param name="value">Valor (pode ser null internamente em Failure, inacessível via Value).</param>
/// <param name="isSuccess">Indica se o resultado é sucesso.</param>
/// <param name="error">Erro associado.</param>
internal Result(TValue? value, bool isSuccess, Error error);
```

---

## Características de Design

### Sealed

```csharp
public sealed class Result<TValue> : Result
{
    // Não pode ser estendida
}
```

Result<TValue> é **sealed** — não pode ser herdada. Isso garante contrato imutável.

### Imutabilidade

Todas as propriedades são **read-only**. Nenhuma pode mudar após construção:
- `IsSuccess`: computado como `!IsFailure`
- `IsFailure`: read-only
- `Error`: read-only
- `Value`: read-only (somente em Result<TValue>)

### Nullable Reference Types

Com `Nullable = enable`:
- `Error` é sempre não-null (referência não-nulável: `Error`)
- `Value` em Result<TValue> é `TValue`, nunca `TValue?`
- Parâmetros em factories e operadores são estritamente tipados

---

## Diagrama de Estados

```
┌─────────────────────────────────────────────┐
│           Result / Result<TValue>           │
└─────────────────────────────────────────────┘
              │
              ├─ Success()
              │  ├─ IsSuccess = true
              │  ├─ IsFailure = false
              │  ├─ Error = Error.None
              │  └─ Value = <value> [Result<T> only]
              │
              └─ Failure(Error)
                 ├─ IsSuccess = false
                 ├─ IsFailure = true
                 ├─ Error = <validError>
                 └─ Value = ❌ InvalidOperationException
```

---

## Resumo de Exceções

| Cenário | Exceção | Mensagem |
|---------|---------|----------|
| `Result.Success<T>(null)` | `ArgumentNullException` | Valor não pode ser null |
| `Result.Failure(null)` | `ArgumentNullException` | Erro não pode ser null |
| `Result.Failure(Error.None)` | `ArgumentException` | Error.None não é válido para Failure |
| `Result<T>.Value` quando `IsFailure` | `InvalidOperationException` | O valor de um Result em falha não pode ser acessado |
| `result.Match(null, ...)` | `ArgumentNullException` | onSuccess não pode ser null |
| `result.Match(..., null)` | `ArgumentNullException` | onFailure não pode ser null |
| Conversão implícita: `Result<T> = (T)null` | `ArgumentNullException` | Valor não pode ser null |
| Conversão implícita: `Result<T> = (Error)null` | `ArgumentNullException` | Erro não pode ser null |
| Conversão implícita: `Result<T> = Error.None` | `ArgumentException` | Error.None não é válido |

---

## Fora do Escopo da V1

Os seguintes métodos e padrões **não existem** nesta versão:

- `Map<TResult>(Func<TValue, TResult> onSuccess)`
- `Bind<TResult>(Func<TValue, Result<TResult>> onSuccess)`
- `MapError(Func<Error, Error> onError)`
- `Ensure(Func<TValue, bool> predicate, Error error)`
- `IsSuccess`, `IsFailure` com padrões async
- `Match` com `Action` (void) — apenas `Func<T>` (com retorno)
- Igualdade por valor customizada (`IEquatable<Result>`, `==`, `!=`)
- `ToString()` customizado
- `Unit` type
- `Either<L, R>` equivalente

Essas capacidades podem ser adicionadas em versões futuras mediante necessidade documentada.

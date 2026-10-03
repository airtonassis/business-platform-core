# Detalhes de Implementação — Result

## Status

**Implementation Complete — Architecture/QA Gate Pending**

- **Implementation:** ✅ Complete
- **Unit Tests:** ✅ 60 testes (113 total com Error) — evidência: `tests/UnitTests/TestResults/feature-0002-unit.trx`
- **Architecture Tests:** ✅ 21 testes (37 total com Error) — evidência: `tests/ArchitectureTests/TestResults/feature-0002-architecture.trx`
- **Build:** ✅ 0 warnings, 0 errors (Release)
- **Format:** ✅ `dotnet format --verify-no-changes` exit code 0 — evidência: `feature-0002-format.log`
- **Mutation Score (execução atual confirmada):** ✅ 96.30% (Result.cs) / 97.22% (Global) — relatório: `StrykerOutput/2026-10-02.23-05-57/reports/mutation-report.json`

---

## Estrutura da Classe Base

A classe `Result` é implementada utilizando genéricos para suporte a retornos tipados e um tipo estático não genérico para operações sem retorno de valor.

### Contrato Público Completo

#### Classe Base: Result

```csharp
public class Result
{
    /// <summary>
    /// Indica se a operação resultou em sucesso.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Indica se a operação resultou em falha.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Erro associado ao resultado. Em Success, é sempre Error.None.
    /// Em Failure, é um erro válido diferente de Error.None.
    /// Nunca é null após construção.
    /// </summary>
    public Error Error { get; }

    /// <summary>
    /// Construtor protegido para inicializar Result com estado imutável.
    /// </summary>
    /// <param name="isSuccess">Indica se o resultado é sucesso.</param>
    /// <param name="error">Erro associado (Error.None se sucesso, error válido se falha).</param>
    protected Result(bool isSuccess, Error error);

    /// <summary>
    /// Factory para criar um Result de sucesso (sem valor de retorno).
    /// </summary>
    /// <returns>Result com IsSuccess=true e Error=Error.None.</returns>
    public static Result Success();

    /// <summary>
    /// Factory para criar um Result de falha.
    /// </summary>
    /// <param name="error">Erro que ocorreu (não pode ser null nem Error.None).</param>
    /// <returns>Result com IsFailure=true e Error=error.</returns>
    /// <exception cref="ArgumentNullException">Se error é null.</exception>
    /// <exception cref="ArgumentException">Se error é Error.None.</exception>
    public static Result Failure(Error error);

    /// <summary>
    /// Factory para criar um Result de sucesso com valor tipado.
    /// </summary>
    /// <typeparam name="TValue">Tipo do valor retornado.</typeparam>
    /// <param name="value">Valor do resultado (não pode ser null).</param>
    /// <returns>Result&lt;TValue&gt; com IsSuccess=true, Error=Error.None e Value=value.</returns>
    /// <exception cref="ArgumentNullException">Se value é null.</exception>
    public static Result<TValue> Success<TValue>(TValue value);

    /// <summary>
    /// Factory para criar um Result de falha tipado.
    /// </summary>
    /// <typeparam name="TValue">Tipo do valor que teria sido retornado.</typeparam>
    /// <param name="error">Erro que ocorreu (não pode ser null nem Error.None).</param>
    /// <returns>Result&lt;TValue&gt; com IsFailure=true e Error=error.</returns>
    /// <exception cref="ArgumentNullException">Se error é null.</exception>
    /// <exception cref="ArgumentException">Se error é Error.None.</exception>
    public static Result<TValue> Failure<TValue>(Error error);
}
```

#### Classe Genérica: Result&lt;TValue&gt;

```csharp
public sealed class Result<TValue> : Result
{
    /// <summary>
    /// Valor do resultado quando IsSuccess é true.
    /// </summary>
    /// <exception cref="InvalidOperationException">Se IsFailure é true.</exception>
    public TValue Value { get; }

    /// <summary>
    /// Construtor interno para inicializar Result&lt;TValue&gt;.
    /// Chamado pelas factories Success&lt;TValue&gt; e Failure&lt;TValue&gt;.
    /// </summary>
    /// <param name="value">Valor (pode ser null internamente durante Failure, mas Value property rejeita acesso).</param>
    /// <param name="isSuccess">Estado de sucesso/falha.</param>
    /// <param name="error">Erro associado.</param>
    internal Result(TValue? value, bool isSuccess, Error error);

    /// <summary>
    /// Converte implicitamente um valor TValue para Result&lt;TValue&gt; de sucesso.
    /// Equivalente a Result.Success&lt;TValue&gt;(value).
    /// </summary>
    /// <param name="value">Valor a converter (não pode ser null).</param>
    /// <exception cref="ArgumentNullException">Se value é null.</exception>
    public static implicit operator Result<TValue>(TValue value);

    /// <summary>
    /// Converte implicitamente um Error para Result&lt;TValue&gt; de falha.
    /// Equivalente a Result.Failure&lt;TValue&gt;(error).
    /// </summary>
    /// <param name="error">Erro a converter (não pode ser null nem Error.None).</param>
    /// <exception cref="ArgumentNullException">Se error é null.</exception>
    /// <exception cref="ArgumentException">Se error é Error.None.</exception>
    public static implicit operator Result<TValue>(Error error);

    /// <summary>
    /// Padrão de consumo: executa onSuccess ou onFailure conforme o estado.
    /// Sobrecarada 1: sem valor de sucesso (para Result não genérico).
    /// </summary>
    /// <typeparam name="TResult">Tipo de retorno do Match.</typeparam>
    /// <param name="onSuccess">Delegate executado se IsSuccess; não pode ser null.</param>
    /// <param name="onFailure">Delegate executado se IsFailure, recebendo o Error; não pode ser null.</param>
    /// <returns>Resultado de onSuccess ou onFailure conforme o estado.</returns>
    /// <exception cref="ArgumentNullException">Se onSuccess ou onFailure é null.</exception>
    public TResult Match<TResult>(
        Func<TResult> onSuccess,
        Func<Error, TResult> onFailure);

    /// <summary>
    /// Padrão de consumo: executa onSuccess ou onFailure conforme o estado.
    /// Sobrecarada 2: com valor de sucesso tipado.
    /// </summary>
    /// <typeparam name="TResult">Tipo de retorno do Match.</typeparam>
    /// <param name="onSuccess">Delegate executado se IsSuccess, recebendo Value; não pode ser null.</param>
    /// <param name="onFailure">Delegate executado se IsFailure, recebendo o Error; não pode ser null.</param>
    /// <returns>Resultado de onSuccess ou onFailure conforme o estado.</returns>
    /// <exception cref="ArgumentNullException">Se onSuccess ou onFailure é null.</exception>
    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<Error, TResult> onFailure);
}
```

---

## Validação e Invariantes

### Construtor Público de Error

Além da validação de Error (já documentada), Result valida:

- `Result.Success<TValue>(value)`: se `value == null` → `ArgumentNullException`
- `Result.Failure(error)`: se `error == null` → `ArgumentNullException`
- `Result.Failure(error)`: se `error == Error.None` → `ArgumentException`
- `Result.Success<TValue>(null)` (conversão implícita): mesmo comportamento que factory

### Match: Validação de Delegates

- `onSuccess` não pode ser `null` → `ArgumentNullException`
- `onFailure` não pode ser `null` → `ArgumentNullException`

### Invariantes Imutáveis

Após construção:
- `IsSuccess` e `IsFailure` nunca mudam
- `Error` nunca muda e nunca é `null`
- Se `IsSuccess`: `Error == Error.None` (garantido internamente)
- Se `IsFailure`: `Error != Error.None` e `Error != null` (garantido internamente)

## Escopo da V1

Incluso:
- `Result` e `Result<TValue>` com factories e conversões implícitas
- Validação de null e Error.None
- `Match` com 2 sobrecargas síncronas
- Imutabilidade e sealed

**Fora do escopo (futuro):**
- Map, Bind, MapError, Ensure
- Operações assíncronas
- Unit, Either, Try-pattern
- Igualdade customizada (==, !=, IEquatable)
- Integração HTTP/ProblemDetails
- Logging, persistência
- Extensões de funções compostas


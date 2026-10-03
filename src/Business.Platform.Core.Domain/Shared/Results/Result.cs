namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Representa o resultado de uma operação de domínio que pode resultar em sucesso ou falha.
/// Um Result pode estar em um dos dois estados: sucesso ou falha, nunca ambos.
/// </summary>
public class Result
{
    /// <summary>
    /// Construtor protegido para inicializar Result com estado imutável.
    /// </summary>
    /// <param name="isSuccess">Indica se o resultado é sucesso.</param>
    /// <param name="error">Erro associado (Error.None se sucesso, error válido se falha).</param>
    /// <remarks>
    /// Invariantes mantidas internamente:
    /// - Se isSuccess=true: error deve ser Error.None
    /// - Se isSuccess=false: error deve ser válido e ≠ Error.None
    /// - error nunca é null
    /// </remarks>
    protected Result(bool isSuccess, Error error)
    {
        if (error is null)
        {
            throw new ArgumentNullException(nameof(error), "Erro não pode ser null.");
        }

        // Validar invariantes
        if (isSuccess && error != Error.None)
        {
            throw new ArgumentException("Success deve estar associado a Error.None.", nameof(error));
        }

        if (!isSuccess && error == Error.None)
        {
            throw new ArgumentException("Failure não pode estar associado a Error.None.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// Indica se a operação resultou em sucesso.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Indica se a operação resultou em falha. Equivalente a !IsSuccess.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Erro associado ao resultado.
    /// - Em Success: sempre Error.None
    /// - Em Failure: sempre um Error válido diferente de Error.None
    /// Nunca é null após construção.
    /// </summary>
    public Error Error { get; }

    /// <summary>
    /// Cria um Result de sucesso sem valor de retorno.
    /// </summary>
    /// <returns>Result com IsSuccess=true e Error=Error.None.</returns>
    public static Result Success() => new(true, Error.None);

    /// <summary>
    /// Cria um Result de falha.
    /// </summary>
    /// <param name="error">Erro que ocorreu (não pode ser null nem Error.None).</param>
    /// <returns>Result com IsFailure=true e Error=error.</returns>
    /// <exception cref="ArgumentNullException">Se error é null.</exception>
    /// <exception cref="ArgumentException">Se error é Error.None.</exception>
    public static Result Failure(Error error)
    {
        if (error is null)
        {
            throw new ArgumentNullException(nameof(error), "Erro não pode ser null.");
        }

        if (error == Error.None)
        {
            throw new ArgumentException("Error.None não é válido para Failure.", nameof(error));
        }

        return new(false, error);
    }

    /// <summary>
    /// Cria um Result de sucesso com valor tipado.
    /// </summary>
    /// <typeparam name="TValue">Tipo do valor retornado.</typeparam>
    /// <param name="value">Valor do resultado (não pode ser null).</param>
    /// <returns>Result&lt;TValue&gt; com IsSuccess=true, Error=Error.None e Value=value.</returns>
    /// <exception cref="ArgumentNullException">Se value é null.</exception>
    public static Result<TValue> Success<TValue>(TValue value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value), "Valor não pode ser null.");
        }

        return new(value, true, Error.None);
    }

    /// <summary>
    /// Cria um Result de falha tipado.
    /// </summary>
    /// <typeparam name="TValue">Tipo do valor que teria sido retornado.</typeparam>
    /// <param name="error">Erro que ocorreu (não pode ser null nem Error.None).</param>
    /// <returns>Result&lt;TValue&gt; com IsFailure=true e Error=error.</returns>
    /// <exception cref="ArgumentNullException">Se error é null.</exception>
    /// <exception cref="ArgumentException">Se error é Error.None.</exception>
    public static Result<TValue> Failure<TValue>(Error error)
    {
        if (error is null)
        {
            throw new ArgumentNullException(nameof(error), "Erro não pode ser null.");
        }

        if (error == Error.None)
        {
            throw new ArgumentException("Error.None não é válido para Failure.", nameof(error));
        }

        return new(default, false, error);
    }

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
    /// Delegate executado se IsFailure, recebendo o Error; retorna TResult.
    /// Não pode ser null.
    /// </param>
    /// <returns>Resultado de onSuccess() ou onFailure(error) conforme o estado.</returns>
    /// <exception cref="ArgumentNullException">Se onSuccess ou onFailure é null.</exception>
    public TResult Match<TResult>(
        Func<TResult> onSuccess,
        Func<Error, TResult> onFailure)
    {
        if (onSuccess is null)
        {
            throw new ArgumentNullException(nameof(onSuccess), "onSuccess não pode ser null.");
        }

        if (onFailure is null)
        {
            throw new ArgumentNullException(nameof(onFailure), "onFailure não pode ser null.");
        }

        return IsSuccess
            ? onSuccess()
            : onFailure(Error);
    }
}

/// <summary>
/// Representa o resultado de uma operação de domínio que retorna um valor tipado.
/// Estende Result com propriedade Value e operadores de conversão implícita.
/// </summary>
/// <typeparam name="TValue">Tipo do valor retornado em caso de sucesso.</typeparam>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    /// <summary>
    /// Construtor interno para inicializar Result&lt;TValue&gt;.
    /// Chamado pelas factories Success&lt;TValue&gt; e Failure&lt;TValue&gt;.
    /// </summary>
    /// <param name="value">Valor (pode ser null internamente em Failure, mas Value property rejeita acesso).</param>
    /// <param name="isSuccess">Estado de sucesso/falha.</param>
    /// <param name="error">Erro associado.</param>
    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// Valor do resultado quando IsSuccess é true.
    /// </summary>
    /// <returns>O valor armazenado.</returns>
    /// <exception cref="InvalidOperationException">Se IsFailure é true.</exception>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("O valor de um Result em falha não pode ser acessado.");

    /// <summary>
    /// Converte implicitamente um valor TValue para Result&lt;TValue&gt; de sucesso.
    /// Equivalente a Result.Success&lt;TValue&gt;(value).
    /// </summary>
    /// <param name="value">Valor a converter (não pode ser null).</param>
    /// <returns>Result&lt;TValue&gt; com IsSuccess=true.</returns>
    /// <exception cref="ArgumentNullException">Se value é null.</exception>
    public static implicit operator Result<TValue>(TValue value) => Success(value);

    /// <summary>
    /// Converte implicitamente um Error para Result&lt;TValue&gt; de falha.
    /// Equivalente a Result.Failure&lt;TValue&gt;(error).
    /// </summary>
    /// <param name="error">Erro a converter (não pode ser null nem Error.None).</param>
    /// <returns>Result&lt;TValue&gt; com IsFailure=true.</returns>
    /// <exception cref="ArgumentNullException">Se error é null.</exception>
    /// <exception cref="ArgumentException">Se error é Error.None.</exception>
    public static implicit operator Result<TValue>(Error error) => Failure<TValue>(error);

    /// <summary>
    /// Padrão de consumo: executa onSuccess(value) ou onFailure(error) conforme o estado.
    /// Sobrecarada para Result com valor tipado.
    /// </summary>
    /// <typeparam name="TResult">Tipo de retorno do Match.</typeparam>
    /// <param name="onSuccess">
    /// Delegate executado se IsSuccess, recebendo Value; retorna TResult.
    /// Não pode ser null.
    /// </param>
    /// <param name="onFailure">
    /// Delegate executado se IsFailure, recebendo o Error; retorna TResult.
    /// Não pode ser null.
    /// </param>
    /// <returns>Resultado de onSuccess(value) ou onFailure(error) conforme o estado.</returns>
    /// <exception cref="ArgumentNullException">Se onSuccess ou onFailure é null.</exception>
    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<Error, TResult> onFailure)
    {
        if (onSuccess is null)
        {
            throw new ArgumentNullException(nameof(onSuccess), "onSuccess não pode ser null.");
        }

        if (onFailure is null)
        {
            throw new ArgumentNullException(nameof(onFailure), "onFailure não pode ser null.");
        }

        return IsSuccess
            ? onSuccess(Value)
            : onFailure(Error);
    }
}

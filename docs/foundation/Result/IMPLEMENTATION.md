# Detalhes de Implementação — Result

## Estrutura da Classe Base

A classe `Result` é implementada utilizando genéricos para suporte a retornos tipados e um tipo estático não genérico para retornos void/operação sem retorno.

### Assinatura Pública Principal

```csharp
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    protected Result(bool isSuccess, Error error);

    public static Result Success();
    public static Result Failure(Error error);
    public static Result<TValue> Success<TValue>(TValue value);
    public static Result<TValue> Failure<TValue>(Error error);
}

public class Result<TValue> : Result
{
    private readonly TValue? _value;

    public TValue Value => IsSuccess 
        ? _value! 
        : throw new InvalidOperationException("O valor de um Result em falha não pode ser acessado.");

    protected internal Result(TValue? value, bool isSuccess, Error error);

    public static implicit operator Result<TValue>(TValue? value);
    public static implicit operator Result<TValue>(Error error);
}

## Histórico de Implementação
[Data Atual]: Estrutura base de Result e Result<T> definida.

Support a conversões implícitas adicionado.

Métodos de extensão e Match mapeados.
### Estado do Error

Todo Result deve possuir uma representação válida de Error.

- Result em Success deve utilizar `Error.None`.
- Result em Failure deve possuir um Error diferente de `Error.None`.
- `Error` não deve ser nulo.


# Exemplos de Uso — Error

Este documento apresenta cenários representativos de utilização
do componente `Error` na Shared Foundation e nas camadas de domínio
e aplicação.

Os exemplos demonstram intenção de uso e não substituem as regras
definidas em `BUSINESS_RULES.md`.

---

## 1. Erro de Validação

Utilizado quando uma entrada ou regra de validação não é atendida.

```csharp
var error = new Error(
    "User.EmailInvalid",
    "O endereço de e-mail informado é inválido.",
    ErrorType.Validation);
```

## 2. Recurso Não Encontrado

Utilizado quando um recurso esperado não existe.

```csharp
var error = new Error(
    "User.NotFound",
    "O usuário informado não foi encontrado.",
    ErrorType.NotFound);
```

## 3. Conflito

Utilizado quando a operação entra em conflito com o estado atual do domínio ou da aplicação.

```csharp
var error = new Error(
    "User.EmailAlreadyExists",
    "Já existe um usuário utilizando o e-mail informado.",
    ErrorType.Conflict);
```

## 4. Não Autenticado

Utilizado quando uma operação exige identidade autenticada e essa condição não foi atendida.

```csharp
var error = new Error(
    "Authentication.Required",
    "A operação requer autenticação.",
    ErrorType.Unauthorized);
```

## 5. Não Autorizado

Utilizado quando uma identidade conhecida não possui permissão para executar determinada operação.

```csharp
var error = new Error(
    "Authorization.Forbidden",
    "O usuário não possui permissão para executar esta operação.",
    ErrorType.Forbidden);
```

## 6. Falha Genérica

Quando nenhuma classificação mais específica representar adequadamente
a falha, deve ser utilizado `ErrorType.Failure`.

```csharp
var error = new Error(
    "Operation.Failed",
    "Não foi possível concluir a operação.",
    ErrorType.Failure);
```

## 7. Ausência de Erro

`Error.None` representa explicitamente a ausência de uma falha.

```csharp
Error error = Error.None;
```

Seu principal consumidor será posteriormente o componente `Result`.

Conceitualmente:

```text
Result.Success
    └── Error.None

Result.Failure
    └── Error válido
```

`Error.None` nunca deve ser utilizado para representar uma falha.

## 8. Catálogo de Erros do Domínio

Erros recorrentes devem poder ser centralizados próximo ao domínio que os possui.

Exemplo conceitual:

```csharp
public static class UserErrors
{
    public static readonly Error NotFound = new(
        "User.NotFound",
        "O usuário informado não foi encontrado.",
        ErrorType.NotFound);

    public static readonly Error EmailAlreadyExists = new(
        "User.EmailAlreadyExists",
        "Já existe um usuário utilizando o e-mail informado.",
        ErrorType.Conflict);
}
```

Isso permite reutilizar códigos estáveis sem transformar a Shared Foundation em um catálogo central de erros de todos os módulos.

A propriedade do erro continua pertencendo ao domínio ou módulo responsável pela regra.

## 9. Uso Incorreto

O componente não deve ser utilizado para transportar detalhes técnicos de exceções ou informações sensíveis.

Exemplo que **NÃO** deve ser utilizado:

```csharp
new Error(
    "Database.Error",
    exception.ToString(),
    ErrorType.Failure);
```

## 10. Integração futura com Result

Após a implementação do componente `Result`, erros poderão ser
transportados explicitamente pelos casos de uso.

Exemplo conceitual:

```csharp
if (user is null)
{
    return UserErrors.NotFound;
}
```

A conversão de `Error` para `Result<T>` pertence ao componente
`Result` e não ao componente `Error`.

### Uma decisão importante que esse documento estabelece

Observe a diferença entre:

```text
Authentication.Required → Unauthorized
Authorization.Forbidden → Forbidden
```

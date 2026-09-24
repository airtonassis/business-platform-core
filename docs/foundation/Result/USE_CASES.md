# Exemplos de Uso — Result

Demonstração prática de como utilizar o `Result` em serviços de domínio e casos de uso da aplicação.

---

## 1. Retornando Sucesso e Falha Simples

```csharp
public Result<User> CreateUser(string name, string email)
{
    if (string.IsNullOrWhiteSpace(email))
    {
        return UserErrors.EmailEmpty; // Conversão implícita de Error para Result.Failure
    }

    var user = new User(name, email);
    return user; // Conversão implícita de User para Result.Success
}

var result = userService.CreateUser("João", "joao@email.com");

## Consumindo o Result na Camada de Aplicação

if (result.IsFailure)
{
    logger.LogWarning("Falha ao criar usuário: {Code} - {Description}", 
        result.Error.Code, 
        result.Error.Description);
        
    return BadRequest(result.Error);
}

return Ok(result.Value);
## Encadeamento Funcional (Match / Map)
return result.Match(
    onSuccess: value => HandleOk(value),
    onFailure: error => HandleError(error)
);
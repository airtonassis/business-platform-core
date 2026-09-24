# Detalhes de Implementação — Error

Este documento define o contrato técnico de implementação do componente `Error`.

A implementação deve respeitar integralmente as regras definidas em `BUSINESS_RULES.md`.

---

## Status

**Specification / Design**

O componente ainda não está implementado.

Este documento define o contrato esperado para a implementação.

---

## Localização

O componente pertence ao projeto:

`Business.Platform.Core.Domain`

Namespace:

`Business.Platform.Core.Domain.Shared`

Estrutura esperada:

```text
src/
└── Business.Platform.Core.Domain/
    └── Shared/
        └── Errors/
            ├── Error.cs
            └── ErrorType.cs
```

O componente não deve possuir dependências de Application, Infrastructure ou API.

---

## ErrorType

A classificação dos erros será representada por um `enum`.

```csharp
public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5
}
```

Os valores numéricos devem permanecer explícitos para evitar alterações acidentais no contrato.

Novos tipos somente devem ser adicionados quando existir necessidade arquitetural ou de negócio comprovada.

---

## Error

`Error` deve possuir semântica de igualdade por valor.

A implementação inicial utilizará um `sealed record`.

Contrato:

```csharp
public sealed record Error
{
    public string Code { get; }

    public string Description { get; }

    public ErrorType Type { get; }

    public static Error None { get; }

    public Error(
        string code,
        string description,
        ErrorType type = ErrorType.Failure);
}
```

---

## Error.None

`Error.None` representa exclusivamente ausência de erro.

Sua instância canônica será:

```csharp
public static Error None { get; } =
    new(string.Empty, string.Empty, ErrorType.Failure);
```

`Error.None` constitui a única exceção às regras que exigem `Code`
e `Description` preenchidos.

Nenhum outro `Error` poderá ser construído com `Code` ou
`Description` vazios.

A implementação interna deve permitir a construção controlada de
`Error.None` sem permitir que consumidores criem erros inválidos.

---

## Validação do Construtor

O construtor público deve rejeitar:

- `code == null`;
- `code` vazio;
- `code` contendo somente whitespace;
- `description == null`;
- `description` vazia;
- `description` contendo somente whitespace;
- valor de `ErrorType` não definido.

Comportamento esperado:

```text
code null
→ ArgumentNullException

description null
→ ArgumentNullException

code vazio/whitespace
→ ArgumentException

description vazia/whitespace
→ ArgumentException

ErrorType inválido
→ ArgumentOutOfRangeException
```

Essas exceções representam violações do contrato de programação,
não falhas esperadas de negócio.

---

## Construção de Error.None

Como o construtor público rejeita valores vazios, `Error.None`
deverá utilizar um mecanismo interno controlado.

Exemplo conceitual:

```csharp
private Error()
{
    Code = string.Empty;
    Description = string.Empty;
    Type = ErrorType.Failure;
}

public static Error None { get; } = new();
```

O construtor privado existe exclusivamente para a criação de
`Error.None`.

Não deve ser exposto publicamente.

---

## Imutabilidade

Após a construção, nenhuma propriedade do `Error` poderá ser alterada.

Não utilizar:

```csharp
public string Code { get; set; }
```

As propriedades devem ser somente leitura.

---

## Igualdade

Como `Error` será implementado como `sealed record`, a igualdade
deve considerar:

- Code;
- Description;
- Type.

Exemplo:

```csharp
var first = new Error(
    "User.NotFound",
    "Usuário não encontrado.",
    ErrorType.NotFound);

var second = new Error(
    "User.NotFound",
    "Usuário não encontrado.",
    ErrorType.NotFound);

first == second // true
```

---

## Dependências

O componente poderá depender somente de funcionalidades da BCL
(Base Class Library) do .NET.

Não adicionar dependências para:

- ASP.NET Core;
- Entity Framework Core;
- bibliotecas de validação;
- logging;
- mensageria;
- serialização;
- bibliotecas funcionais.

---

## Integração com Result

`Error` não deve depender de `Result`.

A direção da dependência é:

```text
Error
  ↑
Result
```

Ou seja:

`Result` conhece `Error`.

`Error` não conhece `Result`.

Isso permite implementar e validar `Error` antes de `Result`.

---

## Fora do Escopo da Primeira Versão

Não implementar nesta feature:

- factory methods como `Error.Validation(...)`;
- coleções de erros;
- `ValidationError`;
- metadata;
- exception wrapping;
- HTTP status codes;
- ProblemDetails;
- localização;
- serialização customizada;
- logging;
- Map;
- conversões implícitas.

Essas capacidades somente devem ser adicionadas mediante necessidade
documentada.

---

## Histórico

### FEATURE-0001

Status atual:

`Specification / Design`

A implementação será realizada somente após aprovação da especificação.
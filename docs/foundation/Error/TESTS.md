# Estratégia de Testes e Cobertura — Error

Este documento define os testes obrigatórios para o componente `Error`.

Os testes devem validar os contratos definidos em `BUSINESS_RULES.md`
e `IMPLEMENTATION.md`.

---

## Status

**Implementado e executado — 2026-09-24**

Nenhum teste deve ser marcado como concluído antes da implementação
e execução bem-sucedida da suíte.

Localização da suíte:

- `tests/UnitTests/Domain/Shared/Errors/ErrorTests.cs`
- `tests/UnitTests/Domain/Shared/Errors/ErrorTypeTests.cs`
- `tests/ArchitectureTests/Domain/Shared/Errors/ErrorArchitectureTests.cs`

Cada teste referencia o identificador `ERR-TST-XXX` correspondente
em comentário.

---

## 1. Criação de Error

### ERR-TST-001 — Deve criar Error válido

**Teste:**

`Constructor_WithValidArguments_ShouldCreateError`

Validar:

- `Code`;
- `Description`;
- `Type`.

---

### ERR-TST-002 — Failure como tipo padrão

**Teste:**

`Constructor_WithoutExplicitType_ShouldUseFailure`

Ao construir:

```csharp
var error = new Error(
    "Operation.Failed",
    "Não foi possível concluir a operação.");
```

`Type` deve ser:

```csharp
ErrorType.Failure
```

---

## 2. Validação de Code

### ERR-TST-003 — Code nulo

`Constructor_WithNullCode_ShouldThrowArgumentNullException`

---

### ERR-TST-004 — Code vazio

`Constructor_WithEmptyCode_ShouldThrowArgumentException`

---

### ERR-TST-005 — Code contendo somente espaços

`Constructor_WithWhitespaceCode_ShouldThrowArgumentException`

---

## 3. Validação de Description

### ERR-TST-006 — Description nula

`Constructor_WithNullDescription_ShouldThrowArgumentNullException`

---

### ERR-TST-007 — Description vazia

`Constructor_WithEmptyDescription_ShouldThrowArgumentException`

---

### ERR-TST-008 — Description contendo somente espaços

`Constructor_WithWhitespaceDescription_ShouldThrowArgumentException`

---

## 4. Validação de ErrorType

### ERR-TST-009 — ErrorType inválido

`Constructor_WithUndefinedErrorType_ShouldThrowArgumentOutOfRangeException`

Exemplo:

```csharp
var invalidType = (ErrorType)999;
```

A construção deve falhar.

---

## 5. Error.None

### ERR-TST-010 — None deve existir

`None_ShouldReturnCanonicalError`

Validar que `Error.None` existe e pode ser acessado.

---

### ERR-TST-011 — None deve possuir Code vazio

`None_ShouldHaveEmptyCode`

---

### ERR-TST-012 — None deve possuir Description vazia

`None_ShouldHaveEmptyDescription`

---

### ERR-TST-013 — None deve possuir Failure como Type

`None_ShouldHaveFailureType`

---

### ERR-TST-014 — None deve ser uma instância canônica

`None_ShouldReturnSameInstance`

Chamadas sucessivas a:

```csharp
Error.None
```

devem retornar a mesma instância.

---

## 6. Igualdade por valor

### ERR-TST-015 — Errors equivalentes

`Errors_WithSameValues_ShouldBeEqual`

Dois `Error` com os mesmos:

- Code;
- Description;
- Type;

devem ser equivalentes.

---

### ERR-TST-016 — Code diferente

`Errors_WithDifferentCode_ShouldNotBeEqual`

---

### ERR-TST-017 — Description diferente

`Errors_WithDifferentDescription_ShouldNotBeEqual`

---

### ERR-TST-018 — Type diferente

`Errors_WithDifferentType_ShouldNotBeEqual`

---

## 7. Imutabilidade

### ERR-TST-019 — Error deve ser imutável

`Error_ShouldExposeReadOnlyProperties`

Validar arquiteturalmente que as propriedades públicas do componente
não possuem setters públicos mutáveis.

---

## 8. ErrorType

### ERR-TST-020 — Valores oficiais do enum

`ErrorType_ShouldContainExpectedValues`

A primeira versão deve conter:

```text
Failure
Validation
NotFound
Conflict
Unauthorized
Forbidden
```

O teste deve detectar alterações não intencionais nesse contrato.

---

### ERR-TST-021 — Valores numéricos estáveis

`ErrorType_ShouldHaveExpectedNumericValues`

Validar:

```text
Failure      = 0
Validation   = 1
NotFound     = 2
Conflict     = 3
Unauthorized = 4
Forbidden    = 5
```

---

## 9. Testes de Arquitetura

Os testes de arquitetura devem verificar que `Error` e `ErrorType`:

- pertencem ao projeto `Business.Platform.Core.Domain`;
- não dependem de Application;
- não dependem de Infrastructure;
- não dependem de API;
- não introduzem dependências externas não autorizadas.

Quando aplicável, utilizar a biblioteca de testes de arquitetura
adotada pelo projeto.

---

## 10. Mutation Testing

O componente deve ser submetido a mutation testing.

Meta específica da Shared Foundation:

**Mutation Score >= 90%**

Mutantes sobreviventes devem ser analisados.

A porcentagem não substitui a análise qualitativa dos testes.

---

## 11. Cobertura

A suíte deve cobrir:

- construção válida;
- valores padrão;
- validações de argumentos;
- exceções previstas;
- `Error.None`;
- igualdade;
- imutabilidade;
- `ErrorType`;
- regras arquiteturais.

O objetivo é cobrir todos os comportamentos e invariantes públicos
definidos para o componente.

---

## 12. Status de Execução

| Categoria | Status | Resultado |
|---|---|---|
| Unit Tests | ✔ Aprovado | 40/40 aprovados (ERR-TST-001 a 018, 020, 021) |
| Architecture Tests | ✔ Aprovado | 11/11 aprovados (ERR-TST-019 e regras da seção 9) |
| Mutation Tests | ✔ Aprovado | 100,00% — 7 mortos, 0 sobreviventes, 0 timeouts |

Os status somente poderão ser alterados para `✔ Aprovado` após
execução efetiva e registro do resultado.

### Registro de execução — 2026-09-24

Comandos:

```text
dotnet build Business.Platform.Core.sln -c Release   → 0 warnings, 0 errors
dotnet test  Business.Platform.Core.sln -c Release   → 51/51 aprovados
dotnet format Business.Platform.Core.sln --verify-no-changes → sem alterações
dotnet stryker                                        → 100,00% (break: 90%)
```

Cenários adicionais além da matriz obrigatória:

- todos os valores definidos de `ErrorType` são aceitos pelo construtor;
- valores inválidos `-1`, `6` e `999` de `ErrorType` são rejeitados, com
  verificação de `ParamName`, `ActualValue` e mensagem;
- whitespace variado (`" "`, `"   "`, `"\t"`, `"\r\n"`) para `Code` e `Description`;
- `ParamName` verificado em todas as exceções de argumento;
- igualdade verificada por `Equals`, `==`, `!=` e `GetHashCode`;
- um erro válido nunca é igual a `Error.None`;
- `Error` é `sealed record` e expõe um único construtor público (o construtor de
  `Error.None` não é público);
- o assembly de Domain referencia somente a BCL;
- `Error` não depende de `Result`;
- guarda contra regra vazia: o filtro NetArchTest seleciona exatamente
  `Error` e `ErrorType`.

Mutation testing: 10 mutantes gerados. 3 foram ignorados pelo filtro
*block already covered* (remoção de bloco cujas instruções já são mutadas
individualmente). Os 7 testados foram mortos. O modo `perTestInIsolation` é
necessário. Ver a observação em `IMPLEMENTATION.md` → Histórico.
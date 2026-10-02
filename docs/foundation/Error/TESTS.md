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

## 6.1 Igualdade explícita — Correção Pós-Auditoria (H01)

`Error` é `sealed class Error : IEquatable<Error>`. Os testes abaixo
comprovam o contrato de igualdade explícita.

| ID | Teste | Valida |
|---|---|---|
| ERR-TST-022 | `EqualsTyped_WithSameValues_ShouldReturnTrue` | `Equals(Error?)` simétrico |
| ERR-TST-023 | `EqualsTyped_WithNull_ShouldReturnFalse` | `Equals(Error?)` com `null` |
| ERR-TST-024 | `Equals_WithSameInstance_ShouldReturnTrue` | reflexividade, inclusive `Error.None` |
| ERR-TST-025 | `EqualsObject_WithEquivalentError_ShouldReturnTrue` | `Equals(object?)` |
| ERR-TST-026 | `EqualsObject_WithNull_ShouldReturnFalse` | `Equals(object?)` com `null` |
| ERR-TST-027 | `EqualsObject_WithDifferentType_ShouldReturnFalse` | `Equals(object?)` com outro tipo |
| ERR-TST-028 | `Errors_WithCodeDifferingOnlyByCase_ShouldNotBeEqual` | comparação ordinal de `Code` |
| ERR-TST-029 | `Errors_WithDescriptionDifferingOnlyByCase_ShouldNotBeEqual` | comparação ordinal de `Description` |
| ERR-TST-030 | `EqualityOperators_WithNullOperands_ShouldFollowValueSemantics` | `==`/`!=` com `null` |
| ERR-TST-031 | `GetHashCode_ShouldBeStableForSameInstance` | hash determinístico |
| ERR-TST-032 | `GetHashCode_ShouldBeEqualForEquivalentErrorsOfEachType` | hash consistente com `Equals` para todos os `ErrorType` |
| ERR-TST-033 | `EquivalentErrors_ShouldBeTreatedAsSameKeyInHashSet` | uso como chave em `HashSet<Error>` |
| ERR-TST-034 | `None_ShouldBeTheOnlyEmptyError` | o construtor público não recria `Error.None` |

Testes de arquitetura do novo contrato:

| ID | Teste | Valida |
|---|---|---|
| ERR-TST-035 | `Error_ShouldBeSealedClassAndNotRecord` | classe selada; sem `<Clone>$` nem `EqualityContract` |
| ERR-TST-036 | `Error_ShouldDeclareExplicitValueEquality` | `IEquatable<Error>`; `Equals`, `GetHashCode`, `==` e `!=` declarados em `Error` |
| ERR-TST-037 | `Error_ShouldNotExposeCloningMechanism` | exatamente 2 construtores (público validador + privado de `None`); sem construtor de cópia, sem `ICloneable`, sem métodos públicos que retornem `Error` |

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

Limitação conhecida (R01, dívida técnica não bloqueadora): a inspeção de
referências lê o XML do `.csproj` do Domain e dos `Directory.*.props`, mas
não reproduz completamente a avaliação do MSBuild para referências
introduzidas por imports/targets. Ver `IMPLEMENTATION.md` → Riscos e dívida
técnica.

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
| Unit Tests | ✔ Aprovado | 53/53 aprovados (ERR-TST-001 a 018, 020 a 034) |
| Architecture Tests | ✔ Aprovado | 16/16 aprovados (ERR-TST-019, 035 a 037 e regras da seção 9) |
| Mutation Tests | ✔ Aprovado | 100,00% dos mutantes avaliados — 17 mortos, 1 timeout, 0 sobreviventes (18 avaliados de 23 gerados) |

Os status somente poderão ser alterados para `✔ Aprovado` após
execução efetiva e registro do resultado.

## 13. Histórico de Execução

Os registros estão em ordem cronológica. Cada resultado vale para o estado
do código e da stack **em que foi obtido**.

| Estado | Data | Stack | `Error` | Unit | Arquitetura | Mutação (`perTestInIsolation`) |
|---|---|---|---|---|---|---|
| 1 — Implementação original | 2026-09-24 | .NET 8, xUnit (asserções nativas) | `sealed record` | 40/40 | 11/11 | 100% — 7/7 avaliados (10 gerados) |
| 2 — Remediação .NET 10 | 2026-09-24 | .NET 10, xUnit + Shouldly | `sealed record` (sem alteração) | 40/40 | 11/11 | 100% — 7/7 avaliados (10 gerados) |
| 3 — Correção Pós-Auditoria | 2026-10-02 | .NET 10, xUnit + Shouldly | `sealed class : IEquatable<Error>` | 53/53 | 16/16 | 100% — 18/18 avaliados (23 gerados) |

### Estado 1 — Implementação original em .NET 8 (2026-09-24)

Ambiente: .NET SDK 8.0.423, `net8.0`, xUnit 2.9.3 com **asserções nativas**,
NetArchTest.Rules 1.3.2, Stryker.NET 4.8.1. Estado do código: `Error` como
**`sealed record`**.

```text
dotnet build Business.Platform.Core.sln -c Release   → 0 warnings, 0 errors
dotnet test  Business.Platform.Core.sln -c Release   → 51/51 aprovados (40 unit + 11 arquitetura)
dotnet format Business.Platform.Core.sln --verify-no-changes → sem alterações
dotnet stryker (modo padrão, perTest)                 → 71,43% — 5 mortos, 2 sobreviventes
dotnet stryker (perTestInIsolation)                   → 100,00% — 7 mortos, 0 sobreviventes
```

Na primeira execução, a suíte de arquitetura tinha 10 testes. O 11º, que
garante que o filtro NetArchTest não passa de forma vazia, foi adicionado
antes do mutation testing.

Mutation testing: 10 mutantes gerados. 3 foram ignorados pelo filtro
*block already covered*. No modo padrão, os 2 mutantes do construtor
privado de `Error.None` sobreviveram (71,43%, abaixo do `break` de 90%). Com
`perTestInIsolation`, os 7 avaliados foram mortos. O score histórico de 100%
refere-se a esses 7 mutantes efetivamente avaliados.

Cenários adicionais além da matriz obrigatória, neste estado:

- todos os valores definidos de `ErrorType` são aceitos pelo construtor;
- valores inválidos `-1`, `6` e `999` de `ErrorType` são rejeitados, com
  verificação de `ParamName`, `ActualValue` e mensagem;
- whitespace variado (`" "`, `"   "`, `"	"`, `"
"`) para `Code` e `Description`;
- `ParamName` verificado em todas as exceções de argumento;
- igualdade verificada por `Equals`, `==`, `!=` e `GetHashCode`, gerados pelo record;
- um erro válido nunca é igual a `Error.None`;
- `Error` é `sealed record` (verificado pela presença de `<Clone>$`) e expõe
  um único construtor público (o construtor de `Error.None` não é público);
- o assembly de Domain referencia somente a BCL. Neste estado, a regra
  aceitava qualquer assembly com nome iniciado por `System`; foi substituída
  no Estado 3 (M01);
- `Error` não depende de `Result`;
- guarda contra regra vazia: o filtro NetArchTest seleciona exatamente
  `Error` e `ErrorType`.

### Estado 2 — Architecture Remediation: migração para .NET 10 (2026-09-24)

Ambiente: .NET SDK 10.0.401, `net10.0`, runtime 10.0.0, xUnit 2.9.3,
**Shouldly 4.3.0**, NetArchTest.Rules 1.3.2, Stryker.NET 4.8.1. Estado do
código: `Error` **continua `sealed record`**, sem alteração de código de
produção. Somente a plataforma e as asserções dos testes mudaram.

```text
dotnet --version                                      → 10.0.401
dotnet restore Business.Platform.Core.sln             → sucesso
dotnet build Business.Platform.Core.sln -c Release    → 0 warnings, 0 errors
dotnet test  tests/UnitTests (Release)                → 40/40 aprovados
dotnet test  tests/ArchitectureTests (Release)        → 11/11 aprovados
dotnet format Business.Platform.Core.sln --verify-no-changes → sem alterações
dotnet stryker --msbuild-path "<sdk>\MSBuild.dll"     → 100,00% (perTestInIsolation) — 7 mortos, 0 sobreviventes
dotnet list package --vulnerable                      → nenhum pacote vulnerável
```

As asserções foram migradas de xUnit nativo para Shouldly. Cada
`Should.Throw<T>` é seguido de `ShouldBeOfType<T>()` para manter a exigência
de tipo exato do `Assert.Throws<T>` original (ver `IMPLEMENTATION.md`).
Os cenários testados são os mesmos do Estado 1.

Mutation testing: os mesmos 10 mutantes do Estado 1 (3 ignorados, 7
avaliados e mortos). O modo padrão (`perTest`) não foi executado neste
estado. Sem o `--msbuild-path`, o Stryker não executou neste ambiente (ver
`IMPLEMENTATION.md`).

### Estado 3 — Correção Pós-Auditoria (2026-10-02) — estado atual

Estado do código: `Error` como `sealed class Error : IEquatable<Error>`
(H01). Verificação de dependências com lista explícita de assemblies (M01).
Testes ERR-TST-022 a 037 adicionados.

Ambiente: .NET SDK 10.0.401, `net10.0`, xUnit 2.9.3, Shouldly 4.3.0,
NetArchTest.Rules 1.3.2, Stryker.NET 4.8.1 (stack do ADR-0001). A execução
partiu de um estado limpo, sem `bin/`, `obj/` nem `StrykerOutput/`.

```text
dotnet --version                                      → 10.0.401
dotnet restore Business.Platform.Core.sln             → sucesso
dotnet build Business.Platform.Core.sln -c Release    → 0 warnings, 0 errors
dotnet test  tests/UnitTests (Release)                → 53/53 aprovados
dotnet test  tests/ArchitectureTests (Release)        → 16/16 aprovados
dotnet format Business.Platform.Core.sln --verify-no-changes → sem alterações
dotnet stryker --msbuild-path "<sdk>\MSBuild.dll"     → 100,00% (break: 90%)
```

Contagem de mutantes (relatório JSON):

| Status | Quantidade | Entra no score? |
|---|---|---|
| Killed | 17 | sim |
| Timeout | 1 | sim, como detectado (mutação `==` → `!=` em `operator !=` causa recursão infinita) |
| Survived | 0 | sim |
| Ignored | 3 | não (filtro *block already covered*) |
| CompileError | 2 | não (mutações de `Equals(object?)` que não compilam) |
| **Total gerado** | **23** | 18 avaliados |

O score de 100% refere-se aos 18 mutantes efetivamente avaliados.
`GetHashCode()` não gerou mutantes, pois `HashCode.Combine` não oferece
operadores mutáveis. Sua correção é coberta pelos testes ERR-TST-015 e
ERR-TST-031 a 033, não pelo mutation testing.

Evidência complementar no modo padrão (`perTest`), executado só para
documentar o M02: 15 mortos, 1 timeout, 2 sobreviventes (construtor privado
de `Error.None`), score de 88,89%. Ver `IMPLEMENTATION.md` → Mutation testing.

# Detalhes de Implementação — Error

Este documento define o contrato técnico de implementação do componente `Error`.

A implementação deve respeitar integralmente as regras definidas em `BUSINESS_RULES.md`.

---

## Status

**Implemented — Em Revisão**

O componente está implementado conforme este contrato e aguarda revisão,
auditoria independente e aprovação arquitetural (ver `CHECKLIST.md`).

Arquivos:

- `src/Business.Platform.Core.Domain/Shared/Errors/Error.cs`
- `src/Business.Platform.Core.Domain/Shared/Errors/ErrorType.cs`

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

`Implemented — Em Revisão`

Implementação realizada em 2026-09-24:

- `ErrorType` implementado com valores numéricos explícitos (0–5).
- `Error` implementado como `sealed record` com propriedades somente leitura
  (`get` sem `set`/`init`, o que também impede alteração via expressão `with`).
- Construtor público valida `code` e `description` com
  `ArgumentException.ThrowIfNullOrWhiteSpace` (`ArgumentNullException` para
  `null`, `ArgumentException` para vazio/whitespace) e `type` com
  `Enum.IsDefined` (`ArgumentOutOfRangeException`).
- `Error.None` criado por construtor privado sem parâmetros, exposto como
  instância canônica única.
- API pública documentada com XML documentation comments.

A infraestrutura de build e qualidade foi criada nesta feature, porque os
arquivos correspondentes estavam vazios.

#### Architecture Remediation — .NET 10 (2026-09-24)

A implementação inicial usava .NET 8 e asserções nativas do xUnit, e essa
escolha não estava aprovada. A decisão arquitetural oficial é **.NET 10**,
com a stack de testes **xUnit + Shouldly + NetArchTest + Stryker.NET**.
A remediação migrou a solução sem alterar o código de `Error`/`ErrorType`
nem o comportamento especificado dos testes.

Stack efetivamente utilizada:

| Item | Versão / Configuração |
|---|---|
| SDK | .NET SDK 10.0.401 (`global.json`, `rollForward: latestPatch`) |
| Target | `net10.0` (definido em `Directory.Build.props` para todos os projetos) |
| Configuração global | `Directory.Build.props`: `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `GenerateDocumentationFile` |
| Pacotes | Central Package Management (`Directory.Packages.props`) |
| Test SDK / Runner | Microsoft.NET.Test.Sdk 17.14.1, xunit.runner.visualstudio 3.1.4, coverlet.collector 6.0.4 |
| Testes | xUnit 2.9.3 |
| Asserções | Shouldly 4.3.0 |
| Arquitetura | NetArchTest.Rules 1.3.2 |
| Mutação | Stryker.NET 4.8.1 (ferramenta local em `.config/dotnet-tools.json`; configuração em `stryker-config.json`; `break` = 90%) |

Migração das asserções para Shouldly: `Should.Throw<T>` aceita exceções
**derivadas** de `T`, por exemplo um `ArgumentNullException` passaria como
`ArgumentException`. `Assert.Throws<T>` do xUnit exige o tipo **exato**.
Para preservar a semântica original, cada `Should.Throw<T>` é seguido de
`exception.ShouldBeOfType<T>()`, que verifica o tipo exato.

#### Justificativa técnica — `coverage-analysis: perTestInIsolation`

`Error.None` é uma propriedade estática com inicializador
(`public static Error None { get; } = new();`). Ela é avaliada **uma única
vez por processo**, na inicialização do tipo, e só então invoca o construtor
privado.

O Stryker.NET compila todos os mutantes num único assembly e ativa cada um
em tempo de execução, trocando um identificador de mutante ativo. No modo
padrão de análise de cobertura, o processo de teste é reutilizado entre
mutantes. Quando um mutante do construtor privado é ativado (por exemplo,
`string.Empty` → `"Stryker was here!"` em `Code` ou `Description`), a
instância de `Error.None` já foi criada sem mutação e não é recriada. Os
testes `None_ShouldHaveEmptyCode` e `None_ShouldHaveEmptyDescription`
observam o valor original e o mutante é reportado como **Survived**, embora
os testes sejam capazes de detectá-lo.

Evidência:

| Modo | Mortos | Sobreviventes | Score |
|---|---|---|---|
| Padrão (`perTest`) | 5 | 2 (`Error.cs` linhas do construtor privado) | 71,43% |
| `perTestInIsolation` | 7 | 0 | 100,00% |

O modo `perTestInIsolation` executa os testes de forma isolada, o que
permite que a inicialização estática ocorra com o mutante já ativo.
Portanto, a configuração corrige um falso positivo da ferramenta e não
enfraquece a medição. O custo é um tempo de execução ligeiramente maior,
desprezível para o tamanho atual da Foundation.

Esta configuração deve ser mantida enquanto componentes da Foundation
expuserem estado estático inicializado (como `Error.None`).

#### Execução do Stryker com Visual Studio 2022 instalado

Em máquinas Windows com Visual Studio 2022 instalado, o Stryker.NET resolve
o `MSBuild.exe` do Visual Studio (MSBuild 17.14) para analisar a solution.
O .NET SDK 10.0.401 exige **MSBuild 18.0.0 ou superior**. Sem ajuste, a
análise falha: no Stryker 4.8.1 com "No project references found" e no
5.0.0 com "Failed to analyze project builds".

Nesse ambiente, execute informando o MSBuild do próprio SDK:

```text
dotnet stryker --msbuild-path "C:\Program Files\dotnet\sdk\10.0.401\MSBuild.dll"
```

O caminho depende da máquina e por isso não foi fixado em
`stryker-config.json`.
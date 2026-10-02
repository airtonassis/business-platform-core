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

`Error` é uma **classe selada e imutável** com igualdade por valor
**explícita**. Não é um `record`: records geram um mecanismo de cópia
(`with`) que permitiria clonar `Error.None` e obter outro `Error` vazio sem
passar pela validação do construtor público.

Contrato:

```csharp
public sealed class Error : IEquatable<Error>
{
    public string Code { get; }

    public string Description { get; }

    public ErrorType Type { get; }

    public static Error None { get; }

    public Error(
        string code,
        string description,
        ErrorType type = ErrorType.Failure);

    public bool Equals(Error? other);

    public override bool Equals(object? obj);

    public override int GetHashCode();

    public static bool operator ==(Error? left, Error? right);

    public static bool operator !=(Error? left, Error? right);
}
```

---

## Error.None

`Error.None` representa exclusivamente ausência de erro.

Sua instância canônica possui `Code` vazio, `Description` vazia e
`Type` = `ErrorType.Failure`. Ela é criada pelo construtor privado sem
parâmetros (ver [Construção de Error.None](#construção-de-errornone)):

```csharp
public static Error None { get; } = new(); // construtor privado
```

O construtor público **não** pode ser usado para criar `Error.None`,
porque rejeita `Code` e `Description` vazios.

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

`Error` não oferece mecanismo de clonagem: não há construtor de cópia, não
há `with` (não é record), não implementa `ICloneable` e não expõe métodos
públicos que retornem novas instâncias de `Error`.

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

A igualdade é implementada explicitamente e considera exatamente:

- Code;
- Description;
- Type.

Regras da implementação:

- `Equals(Error?)` (`IEquatable<Error>`): `false` para `null`. Compara
  `Code` e `Description` com `StringComparison.Ordinal`, ou seja, sensível
  a maiúsculas e minúsculas, e compara `Type`.
- `Equals(object?)`: delega para `Equals(Error?)` quando o objeto é um
  `Error`; caso contrário, `false`.
- `GetHashCode()`: `HashCode.Combine` de `Code` e `Description` (hash
  ordinal) e `Type`, consistente com `Equals`.
- `==` e `!=`: igualdade por valor; `null == null` é `true`, e `null`
  comparado a uma instância é `false`.

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

A FEATURE-0001 passou por três estados. Cada registro abaixo descreve o
código e a stack vigentes **naquele momento**.

| Estado | Data | Stack | `Error` |
|---|---|---|---|
| 1 — Implementação original | 2026-09-24 | .NET 8 (`net8.0`), xUnit com asserções nativas | `sealed record` |
| 2 — Architecture Remediation | 2026-09-24 | .NET 10 (`net10.0`), xUnit + Shouldly | `sealed record` (sem alteração) |
| 3 — Correção Pós-Auditoria | 2026-10-02 | .NET 10 (`net10.0`), xUnit + Shouldly | `sealed class Error : IEquatable<Error>` |

#### Estado 1 — Implementação original em .NET 8 (2026-09-24)

Stack provisória, não aprovada: .NET SDK 8.0.423, `net8.0`, xUnit 2.9.3 com
asserções nativas, NetArchTest.Rules 1.3.2, Stryker.NET 4.8.1.

- `ErrorType` implementado com valores numéricos explícitos (0–5).
- `Error` implementado como `sealed record` com propriedades somente leitura
  (`get` sem `set`/`init`). Esse desenho vigorou nos Estados 1 e 2 e foi
  substituído no Estado 3 (H01).
- Construtor público valida `code` e `description` com
  `ArgumentException.ThrowIfNullOrWhiteSpace` (`ArgumentNullException` para
  `null`, `ArgumentException` para vazio/whitespace) e `type` com
  `Enum.IsDefined` (`ArgumentOutOfRangeException`).
- `Error.None` criado por construtor privado sem parâmetros, exposto como
  instância canônica única.
- API pública documentada com XML documentation comments.

A infraestrutura de build e qualidade foi criada nesta feature, porque os
arquivos correspondentes estavam vazios.

Resultados neste estado: build com 0 warnings; 40 testes unitários e 11 de
arquitetura aprovados; mutation testing 71,43% no modo padrão e 100% (7 de 7
avaliados) com `perTestInIsolation`. Ver `TESTS.md` → Histórico de Execução.

#### Estado 2 — Architecture Remediation: migração para .NET 10 (2026-09-24)

A implementação inicial usava .NET 8 e asserções nativas do xUnit, e essa
escolha não estava aprovada. A decisão arquitetural oficial é **.NET 10**,
com a stack de testes **xUnit + Shouldly + NetArchTest + Stryker.NET**.
A remediação migrou a solução sem alterar o código de `Error`/`ErrorType`
nem o comportamento especificado dos testes. **Neste estado, `Error`
continuava sendo `sealed record`.**

Resultados neste estado: build com 0 warnings; 40 testes unitários e 11 de
arquitetura aprovados; mutation testing 100% (7 de 7 avaliados) com
`perTestInIsolation` e `--msbuild-path`. O modo padrão não foi executado.

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

#### Mutation testing — `coverage-analysis: perTestInIsolation`

A configuração `coverage-analysis: perTestInIsolation` é mantida em
`stryker-config.json`. Esta seção separa o que foi **observado**, o que
foi **comprovado** pelos relatórios e o que é **hipótese** de explicação.

##### 1. Comportamento observado

Mutantes avaliados: os 2 mutantes de string do construtor privado de
`Error.None` (`string.Empty` → `"Stryker was here!"` em `Code` e em
`Description`).

| Execução | Modo | Mortos | Timeout | Sobreviventes | Score |
|---|---|---|---|---|---|
| Estado 1 — 2026-09-24 (`sealed record`, .NET 8) | padrão (`perTest`) | 5 | 0 | 2 — construtor privado de `Error.None` | 71,43% |
| Estado 1 — 2026-09-24 (`sealed record`, .NET 8) | `perTestInIsolation` | 7 | 0 | 0 | 100,00% |
| Estado 2 — 2026-09-24 (`sealed record`, .NET 10) | padrão (`perTest`) | — | — | — (não executado) | — |
| Estado 2 — 2026-09-24 (`sealed record`, .NET 10) | `perTestInIsolation` | 7 | 0 | 0 | 100,00% |
| Estado 3 — 2026-10-02 (`sealed class`, .NET 10) | padrão (`perTest`) | 15 | 1 | 2 — `Error.cs` linhas 19 e 20, construtor privado de `Error.None` | 88,89% |
| Estado 3 — 2026-10-02 (`sealed class`, .NET 10) | `perTestInIsolation` | 17 | 1 | 0 | 100,00% |

Os testes que deveriam detectar esses mutantes são
`None_ShouldHaveEmptyCode` e `None_ShouldHaveEmptyDescription`. Eles
passam na suíte normal e verificam exatamente os valores mutados.

##### 2. O que foi comprovado pelos relatórios

- No modo padrão, os 2 mutantes do construtor privado de `Error.None`
  foram reportados como **Survived** em duas execuções independentes: no
  Estado 1 (`sealed record`, .NET 8) e no Estado 3 (`sealed class`,
  .NET 10). O modo padrão não foi executado no Estado 2.
- Com `perTestInIsolation`, os mesmos mutantes foram reportados como
  **Killed**, sem alteração nos testes nem no código de produção.
- Nenhum outro mutante mudou de status entre os dois modos.

Portanto, está comprovado que **o resultado desses 2 mutantes depende do
modo de análise de cobertura**. Também está comprovado que, com
`perTestInIsolation`, a suíte atual os detecta.

##### 3. Hipótese técnica (não comprovada experimentalmente)

`Error.None` é uma propriedade estática com inicializador
(`public static Error None { get; } = new();`). Pela especificação do .NET,
ela é avaliada uma única vez por processo, na inicialização do tipo.

A hipótese é que, no modo padrão, o Stryker.NET reutiliza o processo de
teste entre mutantes, e que a inicialização estática de `Error` ocorre
antes de o mutante do construtor privado ser ativado. Assim,
`Error.None` manteria os valores originais e os testes não observariam a
mutação. No modo `perTestInIsolation`, a inicialização ocorreria já com o
mutante ativo.

Essa explicação é coerente com os resultados, mas **não foi verificada
diretamente**. Não houve instrumentação do momento da inicialização
estática nem inspeção do ciclo de vida dos processos de teste do
Stryker. Ela não deve ser tratada como causa confirmada.

##### 4. Interpretação do score

O score de 100% refere-se **aos mutantes efetivamente avaliados**:
`Killed + Timeout` sobre o total testado. Ele não inclui mutantes
`Ignored` (filtro *block already covered*) nem `CompileError` (mutantes que
não compilam). Ver a contagem em `TESTS.md`.

A configuração deve ser mantida enquanto componentes da Foundation
expuserem estado estático inicializado (como `Error.None`). O custo é um
tempo de execução maior, desprezível para o tamanho atual da Foundation.

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

#### Estado 3 — Correção Pós-Auditoria (2026-10-02)

Auditoria independente (Codex) com achados H01, M01, M02, L01 e L02.

- **H01:** a exigência de `sealed record` foi removida por decisão
  arquitetural. `Error` passou a ser `sealed class Error : IEquatable<Error>`,
  com `Equals(Error?)`, `Equals(object?)`, `GetHashCode()`, `==` e `!=`
  explícitos, considerando exatamente `Code`, `Description` (ordinal) e
  `Type`. Propriedades, validações, exceções e o construtor privado de
  `Error.None` foram preservados. O mecanismo `with` dos records deixou de
  existir, então consumidores não conseguem mais clonar `Error.None` para
  obter outro `Error` vazio. Efeito colateral fora do contrato:
  `ToString()` deixou de ser o gerado por record e passou a ser o padrão
  de `object` (nome do tipo).
- **M01:** a verificação arquitetural que aceitava qualquer assembly com
  nome iniciado por `System` foi substituída. Agora há uma lista explícita
  de assemblies permitidos (`System.Runtime`, conferindo também a chave
  pública da Microsoft). Os testes também verificam que o `.csproj` do
  Domain, o `Directory.Build.props` e o `Directory.Packages.props` não
  declaram `PackageReference`, `GlobalPackageReference`, `ProjectReference`,
  `Reference` nem `FrameworkReference`. Não foi adicionada biblioteca: a
  leitura usa `System.Xml.Linq`, da BCL.
- **M02:** a documentação do mutation testing foi reorganizada em
  comportamento observado, comprovação e hipótese técnica, com nova
  execução de evidência no modo padrão.
- **L01:** o exemplo conceitual de `Error.None` que usava o construtor
  público com strings vazias foi corrigido para o construtor privado.
- **L02:** o status do `CHECKLIST.md` foi corrigido para as pendências reais.

Resultados neste estado: build com 0 warnings; 53 testes unitários e 16 de
arquitetura aprovados; mutation testing 100% (18 de 18 avaliados, de 23
gerados) com `perTestInIsolation`; 88,89% no modo padrão.

#### Riscos e dívida técnica (não bloqueadores)

- **R01 — Inspeção arquitetural de referências incompleta frente ao
  MSBuild.** Os testes de arquitetura verificam referências de pacote,
  projeto, assembly e framework lendo o XML do `.csproj` do Domain e dos
  arquivos `Directory.Build.props` e `Directory.Packages.props`. Essa
  inspeção estática **não reproduz completamente a avaliação do MSBuild**.
  Não considera referências introduzidas por `Import` de outros
  `.props`/`.targets`, por `Directory.Build.targets`, por arquivos
  `Directory.*` em diretórios ancestrais, por pacotes que injetam
  `build/*.targets`, por condições avaliadas em tempo de build nem por
  SDKs adicionais. A verificação dos assemblies efetivamente referenciados
  pelo binário compilado (lista explícita, com nome e chave pública) reduz
  esse risco, mas não cobre referências que não deixem rastro no assembly.
  Um avaliador completo de MSBuild não será implementado neste momento.

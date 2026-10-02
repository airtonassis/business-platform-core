# ADR-0001 — Technology Stack

## Status

**Aceito**

Data: 2026-10-02

Origem: Architecture Review da FEATURE-0001 — Error e Architecture
Remediation — .NET 10.

---

## Contexto

Os arquivos de configuração de build do `business-platform-core`
(`global.json`, `Directory.Build.props`, `Directory.Packages.props` e
`.csproj`) estavam vazios, e não havia stack tecnológica formalizada.

A implementação inicial da FEATURE-0001 adotou provisoriamente .NET 8 e as
asserções nativas do xUnit. Essa escolha não foi aprovada. A Architecture
Remediation migrou a solução para .NET 10 e para a stack de testes oficial,
e a Architecture Review confirmou a implementação técnica de `Error`.

Este ADR formaliza a stack aprovada para que os próximos componentes da
Shared Foundation, a partir do `Result`, sigam a mesma base.

---

## Decisão

### Plataforma e linguagem

| Decisão | Valor |
|---|---|
| Plataforma | .NET 10 |
| Target Framework | `net10.0` |
| Linguagem | C# |

O SDK é fixado em `global.json`. O Target Framework é definido de forma
centralizada em `Directory.Build.props` e vale para todos os projetos.

### Configuração de compilação

Definida em `Directory.Build.props` e obrigatória para todos os projetos:

| Configuração | Valor |
|---|---|
| Nullable Reference Types | `Nullable` = `enable` |
| Implicit Usings | `ImplicitUsings` = `enable` |
| Warnings como erros | `TreatWarningsAsErrors` = `true` |

### Gerenciamento de pacotes

**Central Package Management**: as versões dos pacotes NuGet são declaradas
exclusivamente em `Directory.Packages.props`
(`ManagePackageVersionsCentrally` = `true`). Os `.csproj` referenciam pacotes
sem versão.

### Stack de testes e qualidade

| Finalidade | Ferramenta |
|---|---|
| Framework de testes | xUnit |
| Asserções | Shouldly |
| Testes de arquitetura | NetArchTest |
| Mutation testing | Stryker.NET |

As versões concretas são as registradas em `Directory.Packages.props` e
`.config/dotnet-tools.json`. Este ADR aprova as ferramentas, não versões
específicas. Atualizações de versão não exigem novo ADR, desde que não
substituam as ferramentas aprovadas.

---

## Configuração técnica atual do mutation testing

Esta seção registra uma **configuração técnica vigente**, não uma decisão
arquitetural. Ela pode ser revista se a ferramenta ou os componentes mudarem.

### `coverage-analysis: perTestInIsolation`

Definida em `stryker-config.json`.

**Comportamento observado (Error.None):** no modo padrão (`perTest`), os 2
mutantes do construtor privado de `Error.None` (`string.Empty` →
`"Stryker was here!"`) são reportados como sobreviventes. Com
`perTestInIsolation`, os mesmos mutantes são mortos, sem alteração de
testes ou código.

| Execução | Modo | Mortos | Timeout | Sobreviventes | Score |
|---|---|---|---|---|---|
| 2026-09-24 | padrão (`perTest`) | 5 | 0 | 2 | 71,43% |
| 2026-09-24 | `perTestInIsolation` | 7 | 0 | 0 | 100,00% |
| 2026-10-02 | padrão (`perTest`) | 15 | 1 | 2 | 88,89% |
| 2026-10-02 | `perTestInIsolation` | 17 | 1 | 0 | 100,00% |

**Comprovado pelos relatórios:** o resultado desses 2 mutantes depende do
modo de análise de cobertura. Com `perTestInIsolation`, a suíte atual os
detecta.

**Hipótese técnica (não comprovada experimentalmente):** `Error.None` é
inicializado uma única vez por processo, na inicialização estática do tipo.
No modo padrão, essa inicialização ocorreria antes da ativação do mutante,
num processo de teste reutilizado. Essa explicação é coerente com os
resultados, mas não foi verificada diretamente.

O score de 100% refere-se aos mutantes efetivamente avaliados. A
configuração deve ser mantida enquanto componentes da Foundation
expuserem estado estático inicializado.

Detalhes: `docs/foundation/Error/IMPLEMENTATION.md` → Mutation testing.

---

## Workaround de ambiente de desenvolvimento (não arquitetural)

> `--msbuild-path` **não é requisito arquitetural** e **não deve ser fixado**
> em `stryker-config.json` nem em outro arquivo do projeto.

Em máquinas Windows com Visual Studio 2022 instalado, o Stryker.NET resolve
o MSBuild do Visual Studio (17.x), mas o .NET SDK 10 exige MSBuild 18 ou
superior, e a análise do projeto falha. Nesses ambientes, o desenvolvedor
pode informar localmente o MSBuild do próprio SDK:

```text
dotnet stryker --msbuild-path "<diretório do SDK>\MSBuild.dll"
```

O caminho depende da máquina. O workaround deixa de ser necessário em
ambientes com MSBuild 18+ disponível ou sem Visual Studio 2022.

---

## Consequências

### Positivas

- Base única e explícita para todos os componentes da plataforma.
- Nullable e warnings como erros aumentam a segurança de tipos e impedem a
  degradação silenciosa da qualidade.
- Versões de pacotes centralizadas e auditáveis em um único arquivo.
- Qualidade dos testes medida objetivamente por mutation testing.
- Regras de dependência entre camadas verificadas automaticamente.

### Negativas / Custos

- Exige o .NET SDK 10 em todas as máquinas e pipelines.
- Com `TreatWarningsAsErrors`, qualquer warning novo, inclusive de
  atualização de SDK ou analisadores, quebra o build.
- `perTestInIsolation` aumenta o tempo de execução do mutation testing.
- Ambientes com Visual Studio 2022 exigem o workaround local de
  `--msbuild-path` para executar o Stryker.NET.

---

## Alternativas consideradas

- **.NET 8 (LTS):** usado provisoriamente na primeira implementação e não
  aprovado. A decisão oficial é .NET 10.
- **Asserções nativas do xUnit:** usadas provisoriamente e substituídas por
  Shouldly, a biblioteca de asserções oficial.

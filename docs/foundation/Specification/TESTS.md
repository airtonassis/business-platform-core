# Testes — Specification Foundation

Frameworks: xUnit, Shouldly, NetArchTest.Rules (já existentes no repositório).

## Testes unitários

`tests/UnitTests/Domain/Shared/Specifications/SpecificationTests.cs` — 58 testes (casos de `Theory` contados como um teste cada nas execuções por caso).

| Tema | Cobertura |
|---|---|
| Construção | válida; `criteria` nulo (`ArgumentNullException`, `ParamName`); preservação da instância de `Criteria` |
| And / Or | tabela verdade; tipos de nó (`AndAlso`, `OrElse`, `Not`); operandos não alterados; nova instância |
| Not | inversão; dupla negação |
| True / False | avaliação para todas as combinações; não nulos |
| Identidades | `And True`, `Or False`, `And False`, `Or True`, De Morgan |
| Argumentos nulos | `left`, `right`, `specification` em `And`, `Or`, `Not` |
| `Criteria` nulo | `ArgumentException` (não `ArgumentNullException`) com `ParamName`; ordem `left` antes de `right`; mensagem |
| Curto-circuito | contador de avaliações do operando direito em `And` e `Or`; composição não avalia |
| Sem novos `InvocationExpression` | varredura da árvore composta a partir de entradas sem `InvocationExpression`; entradas que já contêm esses nós podem preservá-los |
| Reescrita de parâmetros | parâmetro único e novo; nenhum parâmetro original; nomes diferentes; parâmetro compartilhado |
| Lambdas aninhadas | `And`, `Or`, `Not` com `Any`/`All` referenciando o parâmetro externo; parâmetros internos preservados |
| Escopo | sombreamento por lambda, bloco e `catch` (nó preservado por referência); uso livre do parâmetro em bloco e em corpo de `catch` |
| Composição encadeada | equivalência com expressão booleana; parâmetro único |
| Entradas estáveis | avaliação repetida consistente |
| Closures | valor lido na avaliação, não na composição (limitação documentada) |

## Testes arquiteturais

`tests/ArchitectureTests/Domain/Shared/Specifications/SpecificationArchitectureTests.cs` — 23 casos de execução.

- Assembly Domain e namespace `Business.Platform.Core.Domain.Shared`; tipos públicos.
- Filtro de componentes seleciona exatamente os quatro tipos.
- Interface pública e genérica; uma única propriedade `Criteria` somente leitura; nenhum método extra, evento, campo, tipo aninhado ou interface base.
- `Specification<T>` selada, construtor público único, sem setter nem estado mutável.
- `SpecificationExtensions`: assinaturas aprovadas de `And`, `Or`, `Not`; sem outros métodos públicos.
- `Specifications`: fábricas `True<T>` e `False<T>`.
- Sem `Func<,>`/coleções na superfície pública.
- Sem dependência de Application, Infrastructure, Api, Contracts, ASP.NET Core, EF Core, Dapper, NHibernate, `System.Data`, `IQueryable`.
- Somente dependências em `System` e no namespace Shared.
- Allowlist de assemblies conforme ADR-0003 (`System.Runtime`, `System.Collections`, `System.Linq.Expressions`, com public key token da Microsoft).
- Contrato `IRepository` preservado (quatro métodos, sem `ISpecification`).

`ErrorArchitectureTests.cs` foi alterado apenas na allowlist (`System.Linq.Expressions`); as verificações de public key token e de assemblies não autorizados permanecem.

## Mutation testing

Comando (a partir da raiz do repositório):

```powershell
dotnet stryker --msbuild-path "C:\Program Files\dotnet\sdk\10.0.401\MSBuild.exe" --mutate "**/Shared/Specifications/*.cs"
```

Configuração existente `stryker-config.json` (inalterada): limite `break` = 90.

| Item | Resultado |
|---|---|
| Mutantes criados (projeto Domain) | 156 |
| Fora do escopo `--mutate` (outros arquivos) | 136 |
| Erros de compilação | 2 |
| Ignorados (bloco já coberto) | 2 |
| Testados | 16 |
| Mortos | 16 |
| Sobreviventes | 0 |
| Timeout | 0 |
| Sem cobertura | 0 |
| **Mutation score** | **100,00 %** (limite 90 %) |

Histórico: a primeira execução obteve 81,25 % (3 sobreviventes: mensagem de exceção, e curtos-circuitos de `VisitBlock`/`VisitCatchBlock`); foram adicionados testes e a execução final atingiu 100 %. Thresholds não foram alterados.

Observação de ambiente: com o SDK 10.0.401, o Stryker 4.8.1 usa por padrão o MSBuild do Visual Studio 2022 (17.14), incompatível com o SDK (exige MSBuild 18). O parâmetro `--msbuild-path` apontando para o MSBuild do SDK contorna o problema; nenhum arquivo do repositório foi modificado por isso.

## Totais da solução

322 unitários + 162 arquiteturais = 484 testes aprovados (antes: 264 + 139 = 403).

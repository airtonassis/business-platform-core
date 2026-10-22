# Testes — Auditing

## Matriz

| Arquivo | Cobertura |
|---|---|
| `tests/UnitTests/Domain/Shared/Auditing/AuditActorTests.cs` | `User`/`System`, kind indefinido (0, 3, -1, `int.MaxValue`), id nulo/vazio/whitespace, espaços nas extremidades, limites 256/257, caracteres de controle (C0, DEL, C1; controle exatamente no último índice de um id com 256 unidades, que é aceito pelo limite de tamanho e rejeitado apenas pelo caractere de controle), precedência, mensagens sem o id, preservação sem normalização, ids Unicode canonicamente equivalentes não normalizados, contagem em unidades UTF-16 com caracteres suplementares, igualdade (simetria, transitividade, hash), consistência de `==`/`!=` com `Equals`, case-sensitive, null |
| `tests/UnitTests/Domain/Shared/Auditing/AuditMetadataTests.cs` | criação válida, ator nulo, `default`, offset não UTC, primeira atualização e sucessivas, anterior à criação (-1 tick), anterior à última (-1 tick), instantes iguais, mesmo ator, preservação da criação, imutabilidade da instância original, precedência, igualdade campo a campo (simetria, transitividade), consistência de `==`/`!=` com `Equals`, timestamps extremos válidos e inválidos (`MinValue`, `MinValue + 1 tick`, `MaxValue`, offset não UTC no máximo), 50 atualizações sucessivas preservando invariantes |
| `tests/ArchitectureTests/Domain/Shared/Auditing/AuditActorArchitectureTests.cs` | namespace, `sealed`, construtor privado, sem setters, sem `ValueObject`, assinaturas exatas, dependências proibidas, sem novas interfaces, allowlist, contratos anteriores |
| `tests/ArchitectureTests/Domain/Shared/Auditing/AuditMetadataArchitectureTests.cs` | idem para `AuditMetadata`, mais ausência de `IAuditable` |

## Resultados (Release)

| Projeto | Antes | Depois | Falhas | Ignorados |
|---|---|---|---|---|
| UnitTests | 402 | 485 | 0 | 0 |
| ArchitectureTests | 178 | 198 | 0 | 0 |

Totais da execução de remediação (Release, `--no-build`): 485 unitários e 198 arquiteturais aprovados, 0 falhas, 0 ignorados.

Build: 0 avisos, 0 erros. `dotnet format --verify-no-changes`: sem alterações.

## Mutation testing

```powershell
dotnet tool run dotnet-stryker --msbuild-path "C:\Program Files\dotnet\sdk\10.0.401\MSBuild.exe" --mutate "**/Shared/Auditing/*.cs"
```

| Métrica | Valor |
|---|---|
| Testados | 69 |
| Killed | 61 |
| Timeout | 8 |
| Survived | 0 |
| Score | 100,00 % (limite 90 %) |

O score de 100 % **inclui 8 mutantes classificados como timeout** (o Stryker os conta como detectados). Eles não foram eliminados por assertions: foram interrompidos por tempo limite, e o número varia entre execuções (a execução anterior registrou 2). Portanto, não se afirma que os 69 mutantes foram mortos por assertions: 61 foram mortos por falha de teste e 8 por timeout. Em uma tentativa o Stryker abortou na descoberta de testes do projeto de arquitetura; uma execução posterior foi concluída. A causa da falha inicial não foi estabelecida, e não há evidência preservada que permita afirmar que ela foi transitória nem que a repetição ocorreu em condições idênticas. A classificação de seis mutantes variou entre execuções (de killed para timeout: 67/2 na execução anterior, 61/8 na posterior).

## Limites da evidência

- **Evidência de execução:** build, testes, format e Stryker acima foram executados e seus números vêm dessas execuções.
- **Análise estática:** os testes arquiteturais inspecionam metadados dos tipos (dependências, assinaturas, visibilidade). Eles **não detectam diretamente todas as formas de leitura do relógio** (por exemplo, `DateTimeOffset.UtcNow`, `DateTime.Now`, `TimeProvider` ou `Stopwatch` dentro do corpo de um método). Os testes arquiteturais proíbem apenas a dependência de tipos como `System.TimeProvider`; `DateTimeOffset.UtcNow` e similares vivem em `System.Runtime`, que está na allowlist, e portanto não são detectados. A ausência de leitura do relógio em `AuditMetadata` repousa na revisão do código (análise estática manual) e na API, que exige instantes informados pelo chamador; os testes unitários, por usarem instantes fixos, não provam essa ausência.

## Pendências

Auditoria independente inicial, remediação e reauditoria independente (PASS WITH NON-BLOCKING FINDINGS) foram executadas. O Final QA Gate está pendente; commit e push não estão autorizados.

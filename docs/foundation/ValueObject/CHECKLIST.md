# Checklist de Qualidade (DoD) — ValueObject

## Status

**Post-Audit Corrections Applied — QA Gate Pending**

- Auditoria independente do Codex: **PASS WITH NON-BLOCKING FINDINGS** (F01–F05 corrigidos, ver abaixo).
- Architecture/QA Gate final: **PENDENTE**.
- Product Owner: **PENDENTE**.
- Versionamento: **não realizado** (sem `git add`, `commit` ou `push`).

## Contrato

- [x] Classe `public abstract ValueObject : IEquatable<ValueObject>` em `Business.Platform.Core.Domain.Shared`
- [x] `protected abstract IEnumerable<object?> GetEqualityComponents()`
- [x] `Equals(ValueObject?)`, `sealed override Equals(object?)`, `sealed override GetHashCode()`
- [x] Operadores `==` e `!=` sem recursão com `Equals`
- [x] Igualdade por tipo concreto + componentes na ordem; componentes nulos aceitos; comprimentos diferentes são diferentes
- [x] Hash incremental com `GetType()` e componentes; sem cache
- [x] Cada lado enumerado uma vez por `Equals`; mesma instância e tipos diferentes não enumeram
- [x] Coleções: nenhuma interpretação automática de `IEnumerable`
- [x] Sem novos pacotes; `Error`, `Result`, `Entity`, `AggregateRoot`, `IDomainEvent` e ADRs intocados
- [x] Nenhum Value Object de produção criado

## Achados da auditoria Codex e correções (sem alteração de produção)

| ID | Achado | Correção |
|---|---|---|
| F01 | Testes exigiam hashes diferentes para valores diferentes (risco de colisão) | Removidas todas as assertivas `ShouldNotBe` de hash. O hash é validado pela composição incremental esperada (`HashCode` com tipo e componentes na ordem) e pela igualdade de hashes de objetos iguais. Teste de mutabilidade passou a demonstrar a mudança de igualdade; o lookup em `HashSet` após mutação "pode falhar" e não é exigido que falhe. |
| F02 | Documentação sobre `List<T>` imprecisa | `API.md` e `USE_CASES.md` distinguem: lista como componente único (igualdade/hash por referência); elementos emitidos individualmente (conteúdo participa); cópia defensiva/somente leitura (não implica igualdade estrutural). Sem afirmar que `List<T>.Add` altera o hash da lista. |
| F03 | Teste de fronteiras de coleções não demonstrava a ambiguidade | Substituído por duas coleções adjacentes `(["a"],["b","c"])` × `(["a","b"],["c"])`: iguais sem delimitadores, diferentes com contagens. `USE_CASES.md` corrigido. |
| F04 | Leis de igualdade apresentadas como incondicionais | `BUSINESS_RULES.md` explicita as pré-condições: componentes estáveis, `Equals`/`GetHashCode` corretos nos componentes e sequência determinística. |
| F05 | Sem testes de enumeradores | 12 testes com enumeradores instrumentados: descarte (sucesso, diferença antecipada, comprimentos diferentes, falha na aquisição do direito), propagação de exceções de `MoveNext`, `Current`, comparação de componente e `Dispose`, `GetHashCode`, sequência compartilhada. Limitações de sequências infinitas/instáveis/compartilhadas documentadas. |

## Execução registrada (2026-10-13)

Valores da execução mais recente. Não presumir validade para execuções futuras sem reexecutar.

| Gate | Resultado |
|---|---|
| `dotnet restore` | PASS |
| `dotnet build -c Release -warnaserror` | PASS (0 avisos, 0 erros) |
| `dotnet test -c Release` | **PASS** — unitários 264 (61 de ValueObject), arquiteturais 117 (22 de ValueObject), 0 falhas |
| `dotnet format --verify-no-changes` | PASS (código de saída 0) |
| Mutation score de `ValueObject.cs` (Stryker.NET 4.8.1) | **100%** |
| Mutation score global (projeto Domain) | **98,11%** |
| `git diff --check` | Sem erros |
| Whitespace dos arquivos untracked (inspeção direta) | Sem espaços à direita nem tabs nos arquivos de código e nestes documentos |
| Auditoria Codex | PASS WITH NON-BLOCKING FINDINGS (corrigidos) |
| `ValueObject.cs` inalterado após a auditoria | SHA-256 `12424200e3596512dd9e28537b397851250eec85e85d795a25032f5e402d773f` antes e depois das correções |
| Architecture/QA Gate | PENDENTE |

### Mutation testing

- Comando: `dotnet dotnet-stryker --msbuild-path "C:\Program Files\dotnet\sdk\10.0.401\MSBuild.dll"` (raiz, `stryker-config.json`).
- Relatório: `StrykerOutput/2026-10-13.23-34-51/reports/mutation-report.json` (diretório não versionado).
- **`ValueObject.cs`:** 31 mutantes gerados; 23 elegíveis (22 mortos + 1 timeout); 8 ignorados; **0 sobreviventes**.
- **Timeout (linha 22):** `!(left == right)` → `left != right` causa recursão infinita; contado como detectado.
- **Ignorados (8):** remoções de bloco (`Removed by block already covered filter`), isto é, cobertas pelos mutantes de instrução internos; não são exceções concedidas.
- **Global:** 106 mutantes elegíveis na solução (Entity 11, Error 18, Result 54, ValueObject 23); 104 detectados (101 mortos + 3 timeouts), 2 sobreviventes, ambos em `Result.cs` (preexistentes, fora do escopo; score de `Result.cs` 96,30%).

## Desvios e riscos

- **Sem `System.Linq`:** a comparação de componentes é manual porque `ErrorArchitectureTests` restringe os assemblies referenciados pelo Domain e não pode ser alterado nesta feature. Sem desvio de contrato.
- Imutabilidade, igualdade estrutural de coleções e estabilidade do hash entre processos **não** são garantidas pela classe-base (documentado).
- O Stryker no repositório requer `--msbuild-path` explícito neste ambiente.

## Assinatura de Aprovação (A Preencher)

- **Auditoria Codex:** PASS WITH NON-BLOCKING FINDINGS
- **Architecture/QA Gate:** ________________ Data: ________
- **Product Owner:** ________________ Data: ________

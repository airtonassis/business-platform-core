# Checklist de Qualidade (DoD) — AggregateRoot

Utilize este checklist para aprovar a liberação da FEATURE-0004 — AggregateRoot Foundation.

## Status

**Implementation Complete — Auditoria Codex Pendente**

- Documentação arquitetural: ✅ completa e aprovada pelo Architecture Gate
- Implementação, testes, build e formatação: ✅ executados e reportados (ver **Execução Registrada**)
- Mutation testing de `AggregateRoot.cs`: **N/A** (nenhum mutante elegível)
- Auditoria independente Codex: ⏳ **PENDENTE**
- Architecture/QA Gate final: ⏳ **PENDENTE**
- Versionamento (`git add`/`commit`/`push`): ⏳ não executado; aguarda a auditoria

---

## Documentação

- [x] **README.md:** propósito, responsabilidades, estrutura conceitual
- [x] **BUSINESS_RULES.md:** 15 regras (RN-ARG-01 a RN-ARG-15)
- [x] **USE_CASES.md:** exemplos de agregados concretos e antipadrões
- [x] **IMPLEMENTATION.md:** contrato, invariantes, decisões D01–D08, ambiguidades A1–A4 resolvidas
- [x] **TESTS.md:** testes efetivamente implementados e rastreabilidade
- [x] **API.md:** especificação da API pública
- [x] **CHECKLIST.md:** este documento

---

## Decisões Arquiteturais

### Aprovadas (D01–D08)

- [x] **D01:** Herança simples de `Entity<TId>`
- [x] **D02:** Construtor protegido mínimo
- [x] **D03:** Sem redefinição de igualdade, hash e operadores
- [x] **D04:** Sem eventos de domínio nesta feature
- [x] **D05:** Sem persistência ou infraestrutura
- [x] **D06:** Neutralidade em multitenancy (agregados concretos implementam conforme necessário)
- [x] **D07:** Encapsulamento de filhas pertence aos agregados concretos
- [x] **D08:** Sem selagem obrigatória

### Ambiguidades Resolvidas (A1–A4)

- [x] **A1 — Ciclo de vida:** `Create` com `Result<T>` é recomendado, não obrigatório. Sem `Reconstitute` na Foundation.
- [x] **A2 — Validação:** `Result` para falhas de negócio esperadas; exceções para violações de contrato e estados inesperados. Sem `DomainException` nesta feature.
- [x] **A3 — Entidades filhas:** possuem identidade própria.
- [x] **A4 — Referências:** entre agregados, por identificador; coordenação fora da classe-base.

---

## Critérios de Aceitação

- [x] Arquivo-fonte `src/Business.Platform.Core.Domain/Shared/AggregateRoots/AggregateRoot.cs` implementado conforme o contrato aprovado
- [x] `AggregateRoot<TId> : Entity<TId>` com `where TId : notnull`
- [x] Único construtor protegido, delegando a `base(id)`
- [x] Nenhum membro, interface ou lógica adicional (verificado por testes arquiteturais)
- [x] Nenhuma redefinição de `Equals`, `GetHashCode` ou operadores (verificado por testes arquiteturais)
- [x] XML documentation na classe e no construtor
- [x] Build Release com `-warnaserror`: 0 avisos, 0 erros
- [x] 18 testes unitários e 22 execuções de testes arquiteturais aprovados
- [x] `dotnet format --verify-no-changes`: aprovado
- [x] Mutation score: **N/A** para `AggregateRoot.cs` (ver abaixo); score global 97,59%
- [x] `git diff --check`: sem erros
- [x] Error, Result, Entity e ADRs não alterados
- [x] Sem `git add`, `git commit` ou `git push`
- [ ] Auditoria Codex — **PENDENTE**
- [ ] Architecture/QA Gate final — **PENDENTE**

---

## Execução Registrada (2026-10-13)

Valores reportados na execução mais recente. Não presumir validade para execuções futuras sem reexecutar.

| Gate | Resultado |
|---|---|
| `dotnet restore` | PASS |
| `dotnet build -c Release -warnaserror` | PASS (0 avisos, 0 erros) |
| Testes unitários | **189 PASS** (171 anteriores + 18 de AggregateRoot), 0 falhas |
| Testes arquiteturais | **76 PASS** (54 anteriores + 22 de AggregateRoot), 0 falhas |
| `dotnet format --verify-no-changes` | PASS (código de saída 0) |
| Mutation score de `AggregateRoot.cs` | **N/A** (0 mutantes) |
| Mutation score global (projeto Domain) | **97,59%** (inalterado) |
| `git diff --check` | Sem erros (apenas avisos de conversão LF/CRLF, se houver) |

### Mutation testing

- Comando: `dotnet stryker --msbuild-path "C:\Program Files\dotnet\sdk\10.0.401\MSBuild.dll"`
- Relatório: `StrykerOutput/2026-10-13.21-57-54/reports/mutation-report.json` (diretório não versionado)
- `AggregateRoot.cs` aparece no relatório com 0 mutantes: o construtor tem corpo vazio e apenas delega a `base(id)`, sem instruções, operadores ou literais para o Stryker mutar. Por isso o score específico é **N/A**, não 100%.
- O restante do projeto mantém os mesmos resultados: `Entity.cs` com 10 mortos, 1 timeout e 1 ignorado; os 2 mutantes sobreviventes estão em `Result.cs` (preexistentes).
- A proteção do comportamento de AggregateRoot vem dos testes unitários e arquiteturais e dos mutantes de `Entity.cs`; não há medição por mutantes específica.

---

## Estrutura de Arquivos

```
docs/foundation/AggregateRoot/                  (7 documentos)
src/Business.Platform.Core.Domain/Shared/AggregateRoots/AggregateRoot.cs
tests/UnitTests/Domain/Shared/AggregateRoots/AggregateRootTests.cs
tests/ArchitectureTests/Domain/Shared/AggregateRoots/AggregateRootArchitectureTests.cs
```

---

## Dependências e Riscos

### Dependências

- `Entity<TId>` (FEATURE-0003) e política de assemblies (ADR-0002); AggregateRoot não adiciona nenhum assembly referenciado.
- .NET 10, xUnit, Shouldly, NetArchTest.

### Riscos Identificados

| Risco | Mitigação / Estado |
|---|---|
| Subclasse tentar alterar a igualdade | `Equals(object?)` e `GetHashCode()` são `sealed` em `Entity`; testes arquiteturais garantem que AggregateRoot não os redeclara |
| Ausência de mutantes impede medir a eficácia dos testes por mutação | Registrado como N/A; os testes verificam a estrutura por reflexão e o comportamento por herança |
| Verificação de "sem eventos/persistência/multitenancy" é estrutural (sem membros, sem dependências, sem nomes sugestivos) | Limitação documentada em `TESTS.md`; não há prova semântica |
| Futuras features (ex.: Domain Events) exigirão alterar o contrato | Os testes de "somente construtor" falharão e exigirão decisão arquitetural explícita |

---

## Próximas Features Planejadas

- **FEATURE-0005:** Domain Events
- **FEATURE-0006:** Value Objects
- **FEATURE-0007:** Agregados concretos

---

## Assinatura de Aprovação (A Preencher)

- **Auditoria Codex:** ________________ Data: ________
- **Architecture/QA Gate:** ________________ Data: ________
- **Product Owner:** ________________ Data: ________

---

## Histórico de Revisão

| Data | Versão | Autor | Notas |
|---|---|---|---|
| 2026-10-13 | 1.0 | Claude Code | Documentação arquitetural |
| 2026-10-13 | 1.1 | Claude Code | Implementação, testes e resultados de validação |

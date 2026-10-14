# Checklist de Qualidade (DoD) — Entity

Utilize este checklist para aprovar a liberação ou atualização do componente `Entity<TId>`.

## Status

**Implementation Complete — Architecture/QA Gate Pending**

- Implementação, build, testes, formatação e mutation testing: executados e reportados (ver **Execução Registrada**).
- Auditoria Codex: **PENDENTE**.
- Architecture/QA Gate final: **PENDENTE**.
- Product Owner: **PENDENTE**.

---

## Decisões Arquiteturais (Aprovadas — D01–D05)

### D01 — Identidade Genérica e Imutável

- [x] `Entity<TId> : IEquatable<Entity<TId>>` com `where TId : notnull`
- [x] `Id` obrigatório na construção
- [x] `Id` imutável (sem setter de qualquer visibilidade)
- [x] Construtor protegido valida que `Id != null`
- [x] `ArgumentNullException` se `id == null`

### D02 — Sem Relacionamentos na Base

- [x] Nenhuma propriedade de relacionamento em `Entity<TId>` (única propriedade: `Id`)
- [x] Nenhum `TenantId`, `OrganizationId`, `ParentId` em base
- [x] Nenhuma coleção de navegação em base
- [x] Relacionamentos pertencem a subclasses concretas

### D03 — Igualdade por Tipo e Identidade

- [x] `Equals(object?)` é `sealed override`
- [x] `Equals(Entity<TId>?)` implementa `IEquatable<Entity<TId>>` e não admite sobrescrita (emitido como `virtual final` pelo compilador)
- [x] Tipos concretos diferentes nunca são iguais
- [x] Outros atributos não participam da igualdade
- [x] `GetHashCode()` é `sealed override`, usa `EqualityComparer<TId>.Default`
- [x] Operadores `==` e `!=` implementados
- [x] Referências nulas tratadas corretamente

### D04 — Sem Validação Genérica de Default(TId)

- [x] `Entity<TId>` não rejeita `default(TId)` genericamente
- [x] Validação de `Guid.Empty`, `0`, `""` é responsabilidade do domínio
- [x] Documentação deixa claro este comportamento
- [x] Subclasses que consideram `default(TId)` inválido implementam validação (orientação em `USE_CASES.md`)

### D05 — Estabilidade do Identificador

- [x] Propriedade `Id` não pode ser substituída (sem setter)
- [x] Foundation não garante imutabilidade profunda de objetos `TId`
- [x] Tipos personalizados como `TId` são responsáveis pela própria estabilidade
- [x] Documentação explicita essa responsabilidade

### ADR-0002 (histórico: ADR-ENT-006) — `System.Collections` na política de assemblies permitidos

- [x] `System.Collections` adicionado a `AllowedReferencedAssemblies` em `ErrorArchitectureTests`, ao lado de `System.Runtime`
- [x] Verificação do public key token da Microsoft preservada
- [x] Justificativa: dependência legítima da BCL exigida pelo contrato genérico aprovado (`EqualityComparer<TId>.Default`)
- [x] `docs/foundation/Error/IMPLEMENTATION.md` atualizado com a nova política
- [x] Decisão formalizada em `docs/architecture/decisions/ADR-0002-entity-system-collections.md` (Status: Accepted), na estrutura existente de `ADR-0001-technology-stack.md`
- [x] O ADR não autoriza automaticamente outros assemblies
- O identificador ADR-ENT-006 é mantido apenas como referência histórica.

---

## Implementação

- [x] **Sem Dependências Externas**: nenhuma dependência fora da BCL (`System.Runtime`, `System.Collections`) e do próprio namespace
  - [x] Sem `EntityFrameworkCore`
  - [x] Sem `Microsoft.AspNetCore`
  - [x] Sem `logging`
  - [x] Sem `infrastructure`

- [x] **Invariantes Garantidas**:
  - [x] `Constructor(id = null)` lança `ArgumentNullException`
  - [x] `Id` nunca é `null` após construção
  - [x] `Id` sem setter (propriedade somente leitura)
  - [x] Comparação de tipos concretos diferentes retorna `false`
  - [x] Operadores `==` e `!=` consistentes com `Equals`
  - [x] `GetHashCode` consistente com `Equals`

- [x] **Imutabilidade**:
  - [x] `Id` sem setter de qualquer visibilidade
  - [x] Classe não é `sealed` (permite herança)
  - [x] Construtor é `protected` (não `public`)

- [x] **Testes de Unidade**: 58 testes de Entity implementados e aprovados (ver `TESTS.md`, seção Rastreabilidade)
  - [x] Exceção de `id` nulo testada com tipo exato e `ParamName`
  - [x] Operadores testados separadamente
  - [x] Igualdade testada em múltiplos cenários (reflexividade, simetria, transitividade)
  - [x] Hash testado em coleções (`HashSet`, `Dictionary`, `Distinct`) sem exigir hashes distintos
  - [x] Casos extremos testados (`null`, `Guid.Empty`, zero, string vazia, tipos concretos diferentes)
  - [x] Lacunas da matriz (J2, J3, J4, K2, L4) fechadas; nenhum cenário sem cobertura (ver rastreabilidade em `TESTS.md`)

- [x] **Testes de Mutação**:
  - [x] Score ≥90% em `Entity.cs` (100% reportado)
  - [x] Exceções documentadas (ver mutante ignorado abaixo)
  - [x] Relatório gerado (revisão pelo Gate pendente)

- [x] **Documentação em `docs/foundation/Entity/`**:
  - [x] README.md: propósito, responsabilidades, estrutura
  - [x] BUSINESS_RULES.md: 15 regras de negócio (RN-ENT-01 a RN-ENT-15)
  - [x] USE_CASES.md: 10 exemplos práticos
  - [x] IMPLEMENTATION.md: contrato público, invariantes, decisões
  - [x] TESTS.md: matriz de 68 cenários de requisito + rastreabilidade
  - [x] CHECKLIST.md: este documento
  - [x] API.md: especificação da API pública

---

## Qualidade Geral

- [x] **Nullable Reference Types**: `Nullable = enable` (via `Directory.Build.props`)
- [x] **Zero Warnings de Compilação**: build Release com warnings como erros — 0 aviso(s), 0 erro(s)
- [x] **API Pública Documentada**: XML documentation em todos os membros públicos e protegidos (`GenerateDocumentationFile` ativo; build sem avisos)
- [x] **Testes de Arquitetura**: 17 testes de Entity implementados e aprovados
  - [x] Classe é `abstract` e não é `sealed`
  - [x] Construtor é `protected`
  - [x] `Id` sem setter de qualquer visibilidade (B2)
  - [x] `Equals(object?)` e `GetHashCode()` são `sealed override`; `Equals(Entity<TId>?)` não admite sobrescrita
  - [x] Sem dependências de infraestrutura nem de Error/Result
  - [x] Namespace correto: `Business.Platform.Core.Domain.Shared`
- [ ] **Code Review / Auditoria Codex**: PENDENTE

---

## Critérios de Aceitação

- [x] Código-fonte implementado conforme IMPLEMENTATION.md e API.md
- [x] Testes unitários passando (171 no total)
- [x] Testes arquiteturais passando (54 no total)
- [x] Mutation score ≥90% (Entity: 100%; global: 97,59%)
- [x] Zero warnings de compilação
- [x] Formatação verificada
- [ ] Auditoria Codex — **PENDENTE**
- [ ] Architecture/QA Gate final — **PENDENTE**
- [ ] Product Owner — **PENDENTE**

---

## Execução Registrada (2026-10-13, após resolução dos achados da auditoria Codex)

Valores reportados na execução mais recente. Não presumir validade para execuções futuras sem reexecutar.

| Gate | Resultado |
|---|---|
| `dotnet restore` | PASS |
| `dotnet build -c Release` com warnings como erros | PASS (0 avisos, 0 erros) |
| Testes unitários | **171 PASS** (113 preexistentes de Error/Result + 58 de Entity), 0 falhas |
| Testes arquiteturais | **54 PASS** (37 preexistentes + 17 de Entity), 0 falhas |
| `dotnet format --verify-no-changes` | PASS (código de saída 0) |
| Mutation score de `Entity.cs` (Stryker.NET 4.8.1) | **100%** reportado |
| Mutation score global (projeto Domain) | **97,59%** reportado |
| `git diff --check` | Sem erros (apenas avisos de conversão LF/CRLF) |
| Auditoria Codex | PENDENTE |
| Architecture/QA Gate final | PENDENTE |
| Product Owner | PENDENTE |

### Mutation testing — `Entity.cs`

- Comando: `dotnet stryker --msbuild-path "C:\Program Files\dotnet\sdk\10.0.401\MSBuild.dll"`
- Relatório: `StrykerOutput/2026-10-13.21-25-30/reports/mutation-report.json` (diretório não versionado)
- Mutantes de `Entity.cs`: 12 — 10 mortos (Killed), 1 em timeout (detectado), 1 ignorado (Ignored); 0 sobreviventes.
- **Timeout (linha 39):** mutação de `!(left == right)` para `left != right` no operador `!=` causa recursão infinita; o Stryker a classifica como Timeout e a conta como detectada.
- **Mutante ignorado (linha 25):** remoção do bloco do construtor protegido. Motivo reportado pelo Stryker: `Removed by block already covered filter`, isto é, o filtro de blocos já cobertos descarta a mutação de bloco inteiro quando os mutantes de instrução internos (ex.: remoção de `ArgumentNullException.ThrowIfNull(id)`, linha 26, morto) já cobrem o mesmo código. Não é uma exceção concedida por esta equipe.
- Observação: os mutantes de `Equals(object?)` e `GetHashCode()` não foram gerados pelo Stryker para o código atual; o comportamento de hash é protegido pelo teste `GetHashCode_ShouldCombineConcreteTypeAndIdentifierHash`, mas esse teste não é validado por mutantes.

---

## Ambiguidades Documentadas

### A1 — Herança Múltipla de Entity

C# não suporta herança múltipla de classes. Não aplicável.

**Recomendação:** composição para casos que exigiriam herança múltipla.

### A2 — Comparação de Tipos Diferentes com GetType()

A igualdade utiliza `GetType() ==` para comparação exata. `Equals(object?)` e `GetHashCode()` são `sealed override`: subclasses não podem alterar a semântica.

### A3 — Identificadores Personalizados

A igualdade e o hash de `TId` seguem `EqualityComparer<TId>.Default`. Tipos personalizados devem definir ou possuir semântica apropriada de igualdade e hashing, estável durante a vida da entidade (D05).

---

## Sincronização Documental (Pós-Implementação)

- [x] `Error/IMPLEMENTATION.md`: política de assemblies permitidos inclui `System.Collections` (ADR-0002; histórico: ADR-ENT-006)
- [x] `docs/architecture/decisions/ADR-0002-entity-system-collections.md` criado (formalização da decisão; ADR-0001 não modificado)
- [x] `Entity/TESTS.md`: H8 corrigido; 58 + 17 testes registrados; rastreabilidade atualizada (lacunas fechadas)
- [x] `Entity/CHECKLIST.md`: resultados atualizados; gates finais pendentes
- [x] `Entity/IMPLEMENTATION.md`: status e implementação efetiva registrados

## Assinatura de Aprovação (A Preencher)

- **Auditoria Codex:** ________________ Data: ________
- **Architecture/QA Gate:** ________________ Data: ________
- **Product Owner:** ________________ Data: ________

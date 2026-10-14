# Checklist de Aprovação — Repository Foundation

## Fase 1: Validação de Requisitos

### R1 — Inspeção Arquitetural

- [x] Baseline verificado: commit `4562911` ✅
- [x] Estado do Git (início da feature): branch main, apenas docs não rastreados ✅
- [x] Contratos existentes inspecionados:
  - [x] Error (FEATURE-0001)
  - [x] Result (FEATURE-0002)
  - [x] Entity (FEATURE-0003)
  - [x] AggregateRoot (FEATURE-0004)
  - [x] DomainEvent (FEATURE-0005)
  - [x] ValueObject (FEATURE-0006)
- [x] ADRs lidos: ADR-0001 (Technology Stack) ✅
- [x] Testes arquiteturais existentes analisados ✅
- [x] Convenções de namespace, diretórios, nomenclatura identificadas ✅

### R2 — Documentação Obrigatória (7 Arquivos)

- [x] `docs/foundation/Repository/README.md` — Componente, propósito, responsabilidades
- [x] `docs/foundation/Repository/BUSINESS_RULES.md` — Invariantes (I1–I11), semântica de erros
- [x] `docs/foundation/Repository/USE_CASES.md` — Casos de uso (UC01–UC07), fluxos de código
- [x] `docs/foundation/Repository/IMPLEMENTATION.md` — Plano de implementação, decisões, Fase 1–4
- [x] `docs/foundation/Repository/TESTS.md` — Estratégia de testes arquiteturais (unitários e mutation: N/A)
- [x] `docs/foundation/Repository/CHECKLIST.md` — Este documento
- [x] `docs/foundation/Repository/API.md` — Especificação completa da API pública

---

## Fase 2: Análise de Decisões Arquiteturais (D01–D09)

### D01 — Contrato Genérico

**Decisão:** `IRepository<TAggregate, TId>` com restrições genéricas

- [x] Alternativas documentadas (IRepository<TAggregate>, IRepository<TAggregate> com inference)
- [x] Justificativa: Consistência com Entity<TId>, clareza semântica
- [x] Risco: Nenhum identificado
- [x] Aprovação Recomendada: ✅

**Questão Arquitetural:** Nenhuma

---

### D02 — Restrições de Tipo

**Decisão:** `where TAggregate : AggregateRoot<TId> where TId : notnull`

- [x] Alternativas consideradas (AggregateRoot vs. Entity vs. qualquer tipo)
- [x] Justificativa: Proteção arquitetural contra repositórios para entidades internas
- [x] Risco: Nenhum; violação é erro de compilação
- [x] Teste Arquitetural: RepositoryArchitectureTests.Repository_ShouldConstrainAggregateToAggregateRootOfSameId

**Questão Arquitetural:** Nenhuma

---

### D03 — Recuperação: Semântica de Ausência

**Decisão:** `Task<Result<TAggregate>> GetByIdAsync(TId id)` — falha legítima com `ErrorType.NotFound`

- [x] Alternativas documentadas (Task<TAggregate?>, lançar exceção, Try pattern)
- [x] Justificativa: Result pattern da Foundation; distingue ausência (falha esperada) de erro operacional
- [x] ErrorType: `ErrorType.NotFound` para agregado inexistente
- [x] Error.Code: Definido pela implementação (ex: `"Order.NotFound"`, `"Aggregates.NotFound"`)
- [x] Mitigação: Testes arquiteturais validam o tipo de retorno `Task<Result<TAggregate>>`

**Questão Arquitetural Resolvida:** ✅ Usar `ErrorType.NotFound` para ausência; código definido por implementação

---

### D04 — Operações Mínimas: 4 Métodos (GET, ADD, UPDATE, REMOVE)

**Decisão:** `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `RemoveAsync`; sem queries, paginação, bulk

- [x] Semântica clara:
  - `AddAsync` = inclusão de novo agregado (erro se existe)
  - `UpdateAsync` = atualização de existente (erro se não existe)
  - `RemoveAsync` = remoção de agregado
- [x] Não é upsert: ADD e UPDATE são operações distintas
- [x] Justificativa: DDD (CRUD de um agregado); sem ambiguidade semântica
- [x] Risco: Devs confundem AddAsync com upsert (mitigado por documentação clara)
- [x] Sem `SaveAsync` ou `CommitAsync`: Persistência = responsabilidade da implementação

**Questão Arquitetural Resolvida:** ✅ 4 métodos com semântica distinct; UpdateAsync resolve ambiguidade

---

### D05 — Assinaturas Assíncronas

**Decisão:** `Task<Result<T>>` com `CancellationToken cancellationToken = default`

- [x] Alternativas consideradas (ValueTask para otimização heap; sem CancellationToken; sem default)
- [x] Justificativa: Task é o padrão .NET adotado neste contrato; ValueTask é otimização que implementações e handlers podem usar internamente sem alterar o contrato
- [x] CancellationToken: Habilitada para shutdown gracioso, timeout; padrão permite omissão simples
- [x] Risco: Nenhum; totalmente compatível com ORMs modernos
- [x] Testes: `GetByIdAsync_ShouldReturnTaskOfResultOfAggregate` e `WriteMethods_ShouldReturnTaskOfResultAndAcceptAggregate` validam os retornos; `AllMethods_ShouldHaveOptionalCancellationTokenWithDefault` valida o token

**Questão Arquitetural Resolvida:** ✅ Task como padrão do contrato; ValueTask pode ser usado em implementações/handlers

---

### D06 — Persistência e Transações

**Decisão:** `AddAsync`, `UpdateAsync` e `RemoveAsync` retornam `Result`; persistência (commit, atomicidade) = responsabilidade da implementação

- [x] Alternativas consideradas (Unit of Work embutido, `SaveAsync` explícito — rejeitado, transações gerenciadas)
- [x] Justificativa: Contrato puro; implementação define garantias; sem promessas implícitas
- [x] Risco: UC05 (múltiplos agregados) é responsabilidade da aplicação coordenar e decidir recovery strategy
- [x] Mitigação: Documentação clara em USE_CASES.md e README.md; sem Unit of Work nem Saga obrigatórios nesta feature

**Questão Arquitetural Resolvida:** ✅ Persistência é responsabilidade da implementação; coordenação multi-agregado é Application Service

---

### D07 — Evolução: Integração Futura

**Decisão:** Não antecipar Specification (FEATURE-0008) ou Pagination (FEATURE-0009) no contrato 1.0

- [x] Roadmap documentado:
  - [x] v1.0: GetByIdAsync, AddAsync, UpdateAsync, RemoveAsync
  - [x] v2.0: ISpecificationRepository (FindAsync)
  - [x] v2.1: Paginação
  - [x] v2.0+: Domain Events (GetUncomittedEvents)
- [x] Justificativa: Simplicidade + extensibilidade sem quebra de contrato
- [x] Risco: Nenhum; extensões são aditividade

**Questão Arquitetural:** Nenhuma

---

### D08 — Dependências e Independência de Infraestrutura

**Decisão:** Contrato em Domain.Shared; zero dependências de EF Core, System.Data, Application, Infrastructure

- [x] Teste Arquitetural: RepositoryArchitectureTests (Repository_ShouldNotDependOnForbiddenLayersOrFrameworks, Repository_ShouldOnlyDependOnSystemAndSharedNamespace)
- [x] Justificativa: Isolamento de camada; domain não conhece infraestrutura
- [x] Risco: Nenhum; violação é detectada pelo teste arquitetural
- [x] Mitigação: CI/CD valida automaticamente

**Questão Arquitetural:** Nenhuma

---

### D09 — Testabilidade

**Decisão:** Testes arquiteturais (NetArchTest); sem testes unitários (interface pura); Mutation Testing N/A para interface

- [x] Testes arquiteturais implementados: 22 casos do Repository (aprovados) — validam restrições, assinaturas, abstração dos métodos, `CancellationToken`, dependências, namespace e ausência de implementação. Totais da solução: 139 arquiteturais + 264 unitários = 403
- [x] Testes unitários: N/A — interface sem implementação; contrato validado por reflexão
- [x] Mutation testing: N/A (interface sem lógica executável)
- [x] Code coverage: N/A (interface sem corpo de método)
- [x] Justificativa: Interface é contrato puro; validação por reflexão (testes arquiteturais) é suficiente. Limite: reflexão não prova semântica (ex.: `notnull` é anotação de nulabilidade, não constraint CLR)

**Questão Arquitetural Resolvida:** ✅ Mutation testing é N/A; aplicável apenas a implementações concretas em Infrastructure

---

## Fase 3: Validação de Alternativas Rejeitadas

### A1 — Interfaces Separadas: IReadRepository<> e IWriteRepository<>

**Status:** ❌ **REJEITADA**

**Razão:** CQRS read/write é padrão de Application Layer, não Foundation

**Risco se aceita:** Duplicação; complicação desnecessária do contrato

---

### A2 — Repository com Unit of Work Embutido

**Status:** ❌ **REJEITADA**

**Razão:** Agregado = unidade de persistência; múltiplos agregados = responsabilidade externa

**Risco se aceita:** Contrato gigante; difícil implementar em múltiplos ORMs

---

### A3 — SaveAsync Explícito (Sem Commit Automático)

**Status:** ❌ **REJEITADA**

**Razão:** Semanticamente vago; complica UC04 (Update)

**Risco se aceita:** Caller esquece SaveAsync; alterações não persistem

**Nota:** A necessidade de persistir alterações (UC04) foi resolvida com `UpdateAsync`; `SaveAsync` não existe nem está planejado.

---

### A4 — ValueTask no Contrato

**Status:** ❌ **REJEITADA** (o contrato usa `Task`)

**Razão:** `Task` é o padrão .NET e simplifica o contrato; `ValueTask` é otimização de alocação que pode ser adotada internamente por implementações e handlers.

**Risco da alternativa rejeitada:** custo de alocação em hot path, a ser avaliado por benchmark em implementações concretas.

---

### A5 — GetAllAsync no Contrato 1.0

**Status:** ❌ **REJEITADA**

**Razão:** Delegado a Specification Foundation (FEATURE-0008)

**Risco se aceita:** Contrato cresce; Specification fica redundante

**Roadmap:** v2.0 adiciona ISpecificationRepository com FindAsync (não GetAllAsync)

---

### A6 — Suporte a Soft Delete no Contrato 1.0

**Status:** ❌ **REJEITADA**

**Razão:** A política de exclusão é definida funcionalmente pelo domínio/módulo e executada pela implementação; não pertence ao contrato base.

**Risco se aceita:** RemoveAsync fica ambíguo (soft vs. hard)

**Roadmap:** nenhum compromisso; depende de features futuras (ex.: Auditing)

---

### A7 — Transações Explícitas (BeginTransaction, CommitAsync, RollbackAsync)

**Status:** ❌ **REJEITADA**

**Razão:** Commit e transação são responsabilidade da implementação; coordenação multi-agregado é orquestração externa

**Risco se aceita:** Contrato gigante; difícil de usar corretamente

**Padrão possível (não imposto):** Saga ou transação compensatória para coordenação multi-agregado, definido pela aplicação

---

### A8 — Eventos de Domínio Capturados (GetUncomittedEvents)

**Status:** ❌ **REJEITADA para v1.0** ⏳

**Razão:** Domain Events Foundation (FEATURE-0005) ainda está em evolução

**Roadmap:** v2.0+ adiciona suporte quando FEATURE-0005 estabiliza

**Padrão interim:** Application Service publica eventos externamente (observer pattern)

---

### A9 — Validation Method (IsValid, Validate)

**Status:** ❌ **REJEITADA**

**Razão:** Repositório não valida; assumir agregado já é válido (validado antes de AddAsync)

**Risco se aceita:** Duplicação de validação; confusion between domain validation (agregado) e data validation (constraints)

---

## Fase 4: Compatibilidade com Foundations Existentes

### Verificação com Error

- [x] Result<TAggregate> retorna Error em caso de falha
- [x] Error é imutável e type-safe
- [x] Error.Code diferencia erros (ex: "Aggregates.NotFound" vs. "Persistence.Failed")

### Verificação com Result

- [x] GetByIdAsync retorna Result<TAggregate>
- [x] AddAsync, UpdateAsync, RemoveAsync retornam Result
- [x] Result nunca é null; sempre sucesso ou falha explícita

### Verificação com Entity / AggregateRoot

- [x] IRepository<TAggregate, TId> requer AggregateRoot<TId>
- [x] TId : notnull alinhado com Entity<TId> restrição
- [x] Agregado recuperado mantém identidade (Id não muda)

### Verificação com DomainEvent

- [x] v1.0 não captura eventos; belongs to Application
- [x] Roadmap v2.0+ permite GetUncomittedEventsAsync

### Verificação com ValueObject

- [x] Value Objects NÃO têm repositórios (não herdam AggregateRoot)
- [x] VOs são composição do agregado

---

## Fase 5: Riscos Arquiteturais Identificados

| # | Risco | Probabilidade | Impacto | Mitigação |
|---|-------|---------------|--------|-----------|
| R1 | UC04 (Update) | **Baixa** | **Baixo** | Resolvido: `UpdateAsync` faz parte do contrato |
| R2 | UC05 (Múltiplos agregados) sem coordenação clara | **Média** | **Alto** | Documentado como responsabilidade da aplicação; Unit of Work/Saga não planejados neste contrato |
| R3 | Developers tentam usar GetAll/FindAsync em v1.0 | **Alta** | **Baixo** | Documentação clara; métodos inexistentes não compilam |
| R4 | Expectativa de soft delete não satisfeita | **Média** | **Médio** | Política definida pelo módulo e executada pela implementação (I9) |
| R5 | Performance: RemoveAsync+AddAsync em lugar de UPDATE | **Baixa** | **Baixo** | Resolvido: `UpdateAsync` disponível |
| R6 | Transações cross-agregado falham parcialmente | **Média** | **Médio** | Application Service responsável por retry/compensação (estratégia a definir) |

**Status de Mitigação:** ✅ Documentado; Architecture Gate decide

---

## Fase 6: Questões Resolvidas

### Q1 — UC04 (Update): RESOLVIDA ✅

**Pergunta Original:** Como persistir alterações em agregado existente?

**Decisão:** `UpdateAsync` no contrato; `AddAsync` apenas inclui (não é upsert). `SaveAsync` não existe.

**Justificativa:** Evita promessa de upsert e de atomicidade implícita; simplicidade do contrato.

---

### Q2 — UC05 (Múltiplos Agregados): RESOLVIDA ✅

**Pergunta Original:** Qual padrão de coordenação sem Unit of Work?

**Decisão:** Responsabilidade da **Application Service**. Sem Unit of Work nem Saga obrigatórios nesta feature.

**Justificativa:** Repositório é responsável apenas por um agregado; coordenação multi-agregado é responsabilidade de quem chama.

---

### Q3 — Error Codes Padrão: RESOLVIDA ✅

**Pergunta Original:** Qual convenção para Error.Code?

**Decisão:** `ErrorType.NotFound` para agregado inexistente. Error.Code definido pela implementação.

**Exemplos:**
- `error.Type == ErrorType.NotFound` — agregado não encontrado
- `error.Code` = implementação-dependente (ex: `"Order.NotFound"`, `"Aggregates.NotFound"`)

---

### Q4 — Mutation Testing Score: RESOLVIDA ✅

**Pergunta Original:** Alvo de mutation é 100% ou ≥95%?

**Decisão:** Mutation testing é **N/A** para interface sem lógica.

**Justificativa:** Priorizar testes arquiteturais + contrato. Mutation testing aplicável a implementações concretas.

---

## Fase 7: Aprovação Final

### Checklist de Aprovação

- [x] **Baseline verificado:** Commit `4562911`
- [x] **Documentação completa:** 7 arquivos revisados após a auditoria
- [x] **Análise de decisões:** D01–D09 documentadas e justificadas
- [x] **Alternativas rejeitadas:** A1–A9 com rationale
- [x] **Compatibilidade:** Foundations existentes validadas
- [x] **Riscos:** R1–R6 identificados
- [x] **Questões:** Q1–Q4 resolvidas
- [x] **Código de produção:** apenas `IRepository.cs` (interface pura)
- [x] **Testes:** arquiteturais (ver TESTS.md); unitários N/A
- [x] **Sem alterações a Foundations anteriores**
- [x] **Sem git add/commit/push**
- [ ] **Final QA Gate:** pendente de reauditoria independente

---

## Decisões Finais: Contrato Aprovado

✅ **Contrato Final:**

**Operações (4):**
- `GetByIdAsync(id)` → `Result<TAggregate>` com `ErrorType.NotFound` se ausente
- `AddAsync(new_aggregate)` → `Result` (erro se existe)
- `UpdateAsync(modified_aggregate)` → `Result` (erro se não existe)
- `RemoveAsync(aggregate)` → `Result` (política = domínio/módulo; execução técnica = implementação)

**Sem:**
- ❌ SaveAsync, CommitAsync, RollbackAsync
- ❌ Unit of Work, Garantia de atomicidade multi-agregado
- ❌ Garantia de commit/rollback automático
- ❌ Garantia de thread-safety, isolamento ou identidade de instâncias (dependem da implementação)

**Invariantes Confirmados:**
- I1–I2: Apenas AggregateRoots, ID não-nulo
- I3: Retornos sempre Result<T> ou Result
- I4: Semântica ADD ≠ UPDATE (não upsert)
- I5: Sem Commit/Rollback/Atomicidade multi-agregado
- I6–I8: Get por ID; sem queries; sem eventos
- I9: Deleção = política do domínio, não do provedor
- I10: Sem validação de regras de negócio
- I11: Concorrência = dependente da implementação

**Erros (Independentes do Provedor):**
- `ErrorType` reais: Failure, Validation, NotFound, Conflict, Unauthorized (autenticação), Forbidden (autorização)
- Error.Code definido pela implementação
- Sem pressupostos sobre constraints/FK/triggers

**Documentação:**
- ✅ 7 arquivos revisados e atualizados após a auditoria
- ✅ `IRepository.cs` e testes arquiteturais implementados
- ✅ Exemplos C# identificados como pseudocódigo ilustrativo

---

**Versão:** 1.0 (Implementado — FEATURE-0007)  
**Data de Criação:** 2026-10-13  
**Baseline:** commit `4562911`  
**Status:** Architecture Gate APPROVED — implementado; aguardando auditoria independente

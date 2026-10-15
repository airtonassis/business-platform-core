# Checklist — Specification Foundation (FEATURE-0008)

## Baseline e estado inicial

- [x] Branch `main`, HEAD `baadfd5`, árvore de trabalho limpa
- [x] Estrutura, convenções de código e testes inspecionados

## Contrato público

- [x] `ISpecification<T>` com única propriedade `Criteria`, sem métodos default
- [x] `Specification<T>` pública e selada; `criteria` nulo => `ArgumentNullException`; mesma instância preservada
- [x] `SpecificationExtensions`: `And` (`AndAlso`), `Or` (`OrElse`), `Not` (`Not`)
- [x] `Specifications.True<T>()` e `False<T>()` (sem `null`)
- [x] Sem `Expression.Invoke`, sem `Compile()` interno, sem `Func<T, bool>` como contrato, sem `IQueryable`

## Ajustes arquiteturais

- [x] A — validação: operando nulo => `ArgumentNullException`; `Criteria` nulo => `ArgumentException`; argumento identificado; `Result` não utilizado
- [x] B — `ExpressionVisitor` por identidade de referência; escopos aninhados (lambda, bloco, `catch`) respeitados; `And`/`Or` usam parâmetro novo (evita captura); `Not` reutiliza os parâmetros do operando

## ADR e arquitetura

- [x] `ADR-0003-expression-trees-in-domain.md` criado (contexto, alternativas, decisão, justificativa, consequências, dependências, relação com ADR-0002, limitações de ORM)
- [x] Allowlist atualizada somente com `System.Linq.Expressions`; public key token preservado
- [x] Nenhuma outra dependência de assembly necessária

## Testes

- [x] Unitários (58) e arquiteturais (23) aprovados
- [x] Totais da solução: 322 unitários + 162 arquiteturais, sem regressão
- [x] Mutation testing: 16 testados, 16 mortos, 0 sobreviventes; score 100 % (limite 90 %); `stryker-config.json` e `CLAUDE_DEFINITION_OF_DONE.md` inalterados

## Validação

- [x] `dotnet restore`, `dotnet build -c Release --no-restore` (0 avisos, 0 erros), `dotnet test -c Release --no-build`, `dotnet format --verify-no-changes`

## Documentação

- [x] Sete documentos em `docs/foundation/Specification/`
- [x] Não promete imutabilidade profunda, tradução por ORM nem desempenho; limitações de closures descritas
- [x] Fora do escopo explicitado; Repository 1.0 inalterado

## Governança

- [x] Sem `git add`, `commit`, `push` ou `reset`
- [ ] Auditoria independente (Codex) pendente

---

**Versão:** 1.0 (Implementado — FEATURE-0008)  
**Baseline:** commit `baadfd5`  
**Status:** Architecture Gate APPROVED — implementado; aguardando auditoria independente

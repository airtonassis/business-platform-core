# Checklist — DomainEvent

Critérios de aceitação da FEATURE-0005 — Domain Events Foundation.

**Status:** Implementado e corrigido após auditoria — aguardando QA Gate final e autorização expressa do Product Owner. Nada foi commitado nem enviado (sem `commit` ou `push`); o índice do Git está vazio.

---

## Decisões Aprovadas

| # | Decisão | Aprovado |
|---|---------|----------|
| D01 | Contrato com `EventId` e `OccurredOnUtc` | Sim |
| D02 | `EventId` obrigatório (`Guid`) | Sim |
| D03 | `OccurredOnUtc` obrigatório (`DateTimeOffset`, offset UTC por convenção) | Sim |
| D04 | Registros estáveis de fatos; interface com apenas getters | Sim |
| D05 | `AggregateRoot<TId>` preservado integralmente | Sim |
| D06 | Ciclo de vida de eventos pendentes fora do escopo | Sim |
| D07 | Independência de infraestrutura | Sim |
| D08 | Testes do contrato aprovado | Sim |

---

## Código

- [x] `src/Business.Platform.Core.Domain/Shared/DomainEvents/IDomainEvent.cs` criado
- [x] Namespace `Business.Platform.Core.Domain.Shared`
- [x] `Guid EventId { get; }` e `DateTimeOffset OccurredOnUtc { get; }`, sem outros membros, heranças ou atributos
- [x] Documentação XML no padrão do projeto
- [x] `Entity`, `AggregateRoot`, `Error`, `Result` e ADRs não modificados

## Testes

- [x] `DomainEventTests.cs`: 14 testes unitários
- [x] `DomainEventArchitectureTests.cs`: 19 execuções arquiteturais (15 métodos: 14 `Fact` e 1 `Theory` com 4 casos)
- [x] Verificação explícita de ausência de atributos personalizados em `IDomainEvent`, `EventId` e `OccurredOnUtc` (atributos do compilador desconsiderados)
- [x] Filtro de componente que evita regras vacuosas (`ComponentFilter_ShouldSelectOnlyIDomainEvent`)

## Validação final (2026-10-13, após correções da auditoria)

| Item | Resultado |
|---|---|
| `dotnet restore` | Projetos atualizados |
| `dotnet build -c Release -warnaserror` | 0 avisos, 0 erros |
| `dotnet test -c Release --no-build --no-restore` | UnitTests 203/203; ArchitectureTests 95/95; 0 falhas, 0 ignorados |
| `dotnet format --verify-no-changes` | Sem alterações necessárias (exit 0) |
| Mutation testing (Stryker.NET 4.8.1) | `IDomainEvent.cs`: zero mutantes elegíveis, **score N/A**. Score global 97,59% (execução anterior às correções), de arquivos preexistentes |
| Whitespace dos 10 arquivos novos | Sem tabs, CR nem espaços finais indevidos. Há 10 linhas Markdown com dois espaços finais intencionais (quebra de linha) em API, IMPLEMENTATION e README |
| `git diff --check` | Não examina arquivos untracked; a inspeção acima foi feita com `grep`, sem preparar conteúdo |

## Auditoria Codex (PASS WITH NON-BLOCKING FINDINGS) e Resolução

| Achado | Resolução |
|---|---|
| README afirmava timestamps únicos e `EventId` único | Corrigido: `EventId` identifica a ocorrência; sem garantia de unicidade global, deduplicação ou idempotência; timestamps não são únicos |
| README não distinguia getters de imutabilidade profunda | Corrigido: getters somente leitura no contrato não implicam imutabilidade profunda |
| BUSINESS_RULES tratava `Guid`/`DateTimeOffset` como referências mutáveis | Corrigido: são tipos de valor; o risco está nas implementações concretas e em seus dados adicionais |
| Atributos não verificados nos testes arquiteturais | Adicionados 3 testes (tipo, `EventId`, `OccurredOnUtc`), desconsiderando atributos do compilador; contrato de produção inalterado |
| Exemplo `CustomerCreated` não compilaria com nullable e warnings como erros | Substituído por `record` com construtor primário e alternativa com `required` |

## Uso de Git durante a feature

- Na validação de whitespace da etapa de implementação, `git add -N` (intent-to-add) foi executado temporariamente nos caminhos novos para que `git diff --check` os examinasse, seguido de `git reset -q`.
- Estado final: índice vazio (nenhum arquivo preparado) e os quatro caminhos novos como não rastreados.
- Nesta etapa de correções, nenhum comando `git add` foi executado; a inspeção usou `grep`.
- Nenhum `commit` nem `push` foi executado.

## Documentação

- [x] README, BUSINESS_RULES, USE_CASES, IMPLEMENTATION, TESTS, CHECKLIST e API sincronizados com o contrato implementado
- [x] Sem afirmação de imutabilidade profunda, UTC em tempo de execução, deduplicação, idempotência ou exatamente-uma-vez

## Pendências

- [ ] Auditoria independente do Codex
- [ ] Versionamento (somente após a auditoria)

## Riscos e Limitações

- O offset UTC é convenção: a interface não o valida, e os testes documentam esse comportamento.
- Nenhum mecanismo de eventos pendentes existe em `AggregateRoot`; qualquer evolução requer decisão arquitetural própria.
- A cobertura por mutação é N/A para a interface; a proteção vem dos testes de contrato e arquiteturais.

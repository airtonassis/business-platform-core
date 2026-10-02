# Checklist de Qualidade (DoD) — Error

Este documento define os critérios obrigatórios para considerar o
componente `Error` concluído e apto para utilização pela Shared Foundation.

Nenhum item deve ser marcado como concluído antes de sua validação efetiva.

---

## 1. Especificação

- [x] `README.md` completo e atualizado.
- [x] `BUSINESS_RULES.md` completo e atualizado.
- [x] `USE_CASES.md` completo e atualizado.
- [x] `IMPLEMENTATION.md` completo e atualizado.
- [x] `TESTS.md` completo e atualizado.
- [x] Não existem contradições entre os documentos. _(FEATURE-0001 em `CLAUDE_IMPLEMENTATION_ORDER.md` corrigida; Markdown de `USE_CASES.md` corrigido)_
- [x] Todas as decisões arquiteturais necessárias estão definidas. _(stack formalizada em `docs/architecture/decisions/ADR-0001-technology-stack.md`)_

---

## 2. Implementação

- [x] `Error` implementado no projeto `Business.Platform.Core.Domain`.
- [x] `ErrorType` implementado no projeto `Business.Platform.Core.Domain`.
- [x] Namespace conforme especificação.
- [x] `Error` implementado como `sealed class Error : IEquatable<Error>`, sem ser `record`. _(H01; a exigência de `sealed record` foi removida por decisão arquitetural)_
- [x] Não existe mecanismo de clonagem equivalente a `with`. _(sem construtor de cópia, sem `ICloneable`; ERR-TST-037)_
- [x] Propriedades imutáveis.
- [x] `Error.None` implementado.
- [x] Construtor público valida seus argumentos.
- [x] `ErrorType` inválido é rejeitado.
- [x] Nenhuma funcionalidade fora do escopo foi adicionada.

---

## 3. Contrato de Error

- [x] `Code` obrigatório para erros válidos.
- [x] `Description` obrigatória para erros válidos.
- [x] `Type` obrigatório e válido.
- [x] `Error.None` é a única representação permitida para ausência de erro.
- [x] `Error.None` possui `Code` vazio.
- [x] `Error.None` possui `Description` vazia.
- [x] `Error.None` utiliza `ErrorType.Failure`.
- [x] Igualdade por valor validada.
- [x] Igualdade explícita e consistente: `Equals(Error?)`, `Equals(object?)`, `GetHashCode`, `==` e `!=`, considerando exatamente `Code`, `Description` (ordinal) e `Type`. _(ERR-TST-022 a 033, 036)_
- [x] Não existem setters públicos mutáveis.

---

## 4. ErrorType

- [x] `Failure = 0`.
- [x] `Validation = 1`.
- [x] `NotFound = 2`.
- [x] `Conflict = 3`.
- [x] `Unauthorized = 4`.
- [x] `Forbidden = 5`.
- [x] Nenhum tipo adicional foi introduzido sem decisão arquitetural.

---

## 5. Dependências

- [x] Não depende de `Result`.
- [x] Não depende de Application.
- [x] Não depende de Infrastructure.
- [x] Não depende de API.
- [x] Não depende de ASP.NET Core.
- [x] Não depende de Entity Framework Core.
- [x] Não depende de logging.
- [x] Não depende de mensageria.
- [x] Não possui dependências externas além das autorizadas pelo projeto.

---

## 6. Qualidade de Código

- [x] Nullable Reference Types habilitado.
- [x] Compilação sem erros.
- [x] Zero warnings.
- [x] `TreatWarningsAsErrors` habilitado conforme padrão do projeto.
- [x] API pública consistente com `IMPLEMENTATION.md`.
- [x] Código formatado conforme padrões do projeto.
- [x] Análise estática aprovada, quando aplicável. _(analisadores .NET padrão + `EnforceCodeStyleInBuild` + `dotnet format --verify-no-changes`)_

---

## 7. Testes Unitários

- [x] Todos os cenários obrigatórios de `TESTS.md` implementados.
- [x] Todos os testes unitários aprovados.
- [x] Validação de argumentos coberta.
- [x] `Error.None` coberto.
- [x] Igualdade por valor coberta.
- [x] Imutabilidade coberta.
- [x] `ErrorType` coberto.

---

## 8. Testes de Arquitetura

- [x] Testes de arquitetura implementados quando aplicáveis.
- [x] Dependências proibidas verificadas. _(M01: lista explícita de assemblies permitidos, com nome e chave pública, e ausência de referências de pacote, projeto, assembly ou framework no `.csproj` do Domain e nos `Directory.*.props`)_
- [x] Localização arquitetural do componente verificada.
- [x] Todos os testes de arquitetura aprovados.

---

## 9. Mutation Testing

- [x] Mutation testing executado.
- [x] Mutation Score >= 90%.
- [x] Mutantes sobreviventes analisados. _(0 sobreviventes com `perTestInIsolation`. No modo padrão, 2 sobreviventes de `Error.None`; comportamento observado, comprovação e hipótese documentados separadamente em `IMPLEMENTATION.md`)_
- [x] Nenhum mutante sobrevivente crítico ignorado sem justificativa.

---

## 10. Segurança

- [x] `Code` não contém informações sensíveis. _(responsabilidade do consumidor ao criar erros; orientação registrada na documentação XML de `Error` e em `BUSINESS_RULES.md`)_
- [x] `Description` não contém credenciais, tokens ou segredos. _(idem)_
- [x] Nenhum detalhe técnico interno é exposto indevidamente.

---

## 11. Documentação Pós-Implementação

- [x] `IMPLEMENTATION.md` atualizado de `Specification / Design` para o estado real.
- [x] `TESTS.md` atualizado com os resultados reais.
- [x] Histórico da feature atualizado.
- [x] Nenhum teste está marcado como aprovado sem ter sido executado.
- [x] Documentação permanece sincronizada com o código.

---

## 12. Aprovação

- [x] Implementação revisada. _(Architecture Review da implementação técnica e revisão das correções pós-auditoria concluídas)_
- [x] Auditoria independente realizada. _(auditoria Codex com achados H01, M01, M02, L01 e L02, todos corrigidos; reauditoria independente concluída)_
- [x] Pendências críticas resolvidas. _(a reauditoria não encontrou bloqueador técnico remanescente)_
- [x] Decisões arquiteturais adicionais registradas quando necessárias. _(stack no ADR-0001; decisão H01 registrada em `IMPLEMENTATION.md`; por orientação da governança, não foi criado ADR adicional para Error)_
- [x] Architecture/QA Gate aprovado. _(aprovado pelo Solution Architect / Tech Lead)_
- [x] FEATURE-0001 aprovada.

### Dívida técnica não bloqueadora

- **R01:** a inspeção arquitetural de referências não reproduz completamente
  a avaliação do MSBuild para referências introduzidas por imports/targets.
  Ver `IMPLEMENTATION.md` → Riscos e dívida técnica.

---

## Status Final

**Status atual:** ✔ APPROVED — FEATURE-0001 concluída

Todas as seções do checklist foram cumpridas. A revisão das correções e a
reauditoria independente foram concluídas sem bloqueadores técnicos
remanescentes, e o Architecture/QA Gate foi aprovado pelo Solution
Architect / Tech Lead. R01 permanece registrado como dívida técnica não
bloqueadora.

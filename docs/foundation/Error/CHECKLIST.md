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
- [ ] Não existem contradições entre os documentos. _(pendente: o corpo da FEATURE-0001 em `CLAUDE_IMPLEMENTATION_ORDER.md` ainda descreve o Result; blocos de código de `USE_CASES.md` estão com cercas Markdown quebradas)_
- [ ] Todas as decisões arquiteturais necessárias estão definidas. _(pendente: a pilha de build e testes foi adotada provisoriamente e precisa ser ratificada em `TECHNOLOGY_STACK.md`/ADR; ver `IMPLEMENTATION.md` → Histórico)_

---

## 2. Implementação

- [x] `Error` implementado no projeto `Business.Platform.Core.Domain`.
- [x] `ErrorType` implementado no projeto `Business.Platform.Core.Domain`.
- [x] Namespace conforme especificação.
- [x] `Error` implementado como `sealed record`.
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
- [x] Dependências proibidas verificadas.
- [x] Localização arquitetural do componente verificada.
- [x] Todos os testes de arquitetura aprovados.

---

## 9. Mutation Testing

- [x] Mutation testing executado.
- [x] Mutation Score >= 90%.
- [x] Mutantes sobreviventes analisados. _(0 sobreviventes; sobreviventes falsos do modo padrão foram analisados e documentados)_
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

- [ ] Implementação revisada.
- [ ] Auditoria independente realizada.
- [ ] Pendências críticas resolvidas.
- [ ] Decisões arquiteturais adicionais registradas quando necessárias.
- [ ] FEATURE-0001 aprovada.

---

## Status Final

**Status atual:** 🔎 Implemented — Em Revisão (aguardando itens 1 e 12)

O status somente poderá ser alterado para:

**✔ APPROVED**

após o cumprimento integral dos critérios aplicáveis deste checklist.
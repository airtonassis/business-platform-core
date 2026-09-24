# Checklist de Qualidade (DoD) — Error

Este documento define os critérios obrigatórios para considerar o
componente `Error` concluído e apto para utilização pela Shared Foundation.

Nenhum item deve ser marcado como concluído antes de sua validação efetiva.

---

## 1. Especificação

- [ ] `README.md` completo e atualizado.
- [ ] `BUSINESS_RULES.md` completo e atualizado.
- [ ] `USE_CASES.md` completo e atualizado.
- [ ] `IMPLEMENTATION.md` completo e atualizado.
- [ ] `TESTS.md` completo e atualizado.
- [ ] Não existem contradições entre os documentos.
- [ ] Todas as decisões arquiteturais necessárias estão definidas.

---

## 2. Implementação

- [ ] `Error` implementado no projeto `Business.Platform.Core.Domain`.
- [ ] `ErrorType` implementado no projeto `Business.Platform.Core.Domain`.
- [ ] Namespace conforme especificação.
- [ ] `Error` implementado como `sealed record`.
- [ ] Propriedades imutáveis.
- [ ] `Error.None` implementado.
- [ ] Construtor público valida seus argumentos.
- [ ] `ErrorType` inválido é rejeitado.
- [ ] Nenhuma funcionalidade fora do escopo foi adicionada.

---

## 3. Contrato de Error

- [ ] `Code` obrigatório para erros válidos.
- [ ] `Description` obrigatória para erros válidos.
- [ ] `Type` obrigatório e válido.
- [ ] `Error.None` é a única representação permitida para ausência de erro.
- [ ] `Error.None` possui `Code` vazio.
- [ ] `Error.None` possui `Description` vazia.
- [ ] `Error.None` utiliza `ErrorType.Failure`.
- [ ] Igualdade por valor validada.
- [ ] Não existem setters públicos mutáveis.

---

## 4. ErrorType

- [ ] `Failure = 0`.
- [ ] `Validation = 1`.
- [ ] `NotFound = 2`.
- [ ] `Conflict = 3`.
- [ ] `Unauthorized = 4`.
- [ ] `Forbidden = 5`.
- [ ] Nenhum tipo adicional foi introduzido sem decisão arquitetural.

---

## 5. Dependências

- [ ] Não depende de `Result`.
- [ ] Não depende de Application.
- [ ] Não depende de Infrastructure.
- [ ] Não depende de API.
- [ ] Não depende de ASP.NET Core.
- [ ] Não depende de Entity Framework Core.
- [ ] Não depende de logging.
- [ ] Não depende de mensageria.
- [ ] Não possui dependências externas além das autorizadas pelo projeto.

---

## 6. Qualidade de Código

- [ ] Nullable Reference Types habilitado.
- [ ] Compilação sem erros.
- [ ] Zero warnings.
- [ ] `TreatWarningsAsErrors` habilitado conforme padrão do projeto.
- [ ] API pública consistente com `IMPLEMENTATION.md`.
- [ ] Código formatado conforme padrões do projeto.
- [ ] Análise estática aprovada, quando aplicável.

---

## 7. Testes Unitários

- [ ] Todos os cenários obrigatórios de `TESTS.md` implementados.
- [ ] Todos os testes unitários aprovados.
- [ ] Validação de argumentos coberta.
- [ ] `Error.None` coberto.
- [ ] Igualdade por valor coberta.
- [ ] Imutabilidade coberta.
- [ ] `ErrorType` coberto.

---

## 8. Testes de Arquitetura

- [ ] Testes de arquitetura implementados quando aplicáveis.
- [ ] Dependências proibidas verificadas.
- [ ] Localização arquitetural do componente verificada.
- [ ] Todos os testes de arquitetura aprovados.

---

## 9. Mutation Testing

- [ ] Mutation testing executado.
- [ ] Mutation Score >= 90%.
- [ ] Mutantes sobreviventes analisados.
- [ ] Nenhum mutante sobrevivente crítico ignorado sem justificativa.

---

## 10. Segurança

- [ ] `Code` não contém informações sensíveis.
- [ ] `Description` não contém credenciais, tokens ou segredos.
- [ ] Nenhum detalhe técnico interno é exposto indevidamente.

---

## 11. Documentação Pós-Implementação

- [ ] `IMPLEMENTATION.md` atualizado de `Specification / Design` para o estado real.
- [ ] `TESTS.md` atualizado com os resultados reais.
- [ ] Histórico da feature atualizado.
- [ ] Nenhum teste está marcado como aprovado sem ter sido executado.
- [ ] Documentação permanece sincronizada com o código.

---

## 12. Aprovação

- [ ] Implementação revisada.
- [ ] Auditoria independente realizada.
- [ ] Pendências críticas resolvidas.
- [ ] Decisões arquiteturais adicionais registradas quando necessárias.
- [ ] FEATURE-0001 aprovada.

---

## Status Final

**Status atual:** ⏳ Specification / Design

O status somente poderá ser alterado para:

**✔ APPROVED**

após o cumprimento integral dos critérios aplicáveis deste checklist.
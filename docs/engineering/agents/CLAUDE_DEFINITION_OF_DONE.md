### 6. `CLAUDE_DEFINITION_OF_DONE.md`

```markdown
# Definition of Done (DoD)

Uma tarefa só é considerada **CONCLUÍDA** se atender 100% dos critérios abaixo.

## 1. Código
- [ ] O código segue as convenções do projeto e do Clean Code.
- [ ] Não há dependências diretas de bibliotecas externas não autorizadas na `foundation/`.
- [ ] Sem avisos (warnings) de compilação.

## 2. Testes
- [ ] **Unit Tests**: Cobertura de 100% dos caminhos felizes e exceções do componente.
- [ ] **Architecture Tests**: Validação de dependências e regras de camada aprovada.
- [ ] **Mutation Tests**: Escore de sobrevivência de mutantes dentro do limite estipulado (> 80%).

## 3. Documentação (Docs-as-Code)
- [ ] Pasta correspondente criada em `docs/`.
- [ ] `BUSINESS_RULES.md` descreve todas as regras invariantes.
- [ ] `API.md` possui as assinaturas e tipos expostos atualizados.
- [ ] `TESTS.md` contém o histórico de implementação e os status de cobertura preenchidos.
- [ ] `CHECKLIST.md` do componente devidamente marcado.
# Checklist de Qualidade (DoD) — Result

Utilize este checklist para aprovar a liberação ou atualização do componente `Result`.

- [ ] **Sem Dependências Externa**: O componente não importa nada fora do ecossistema nativo do framework/language.
- [ ] **Invariantes Garantidas**: Não é possível instanciar um `Result.Success` com um Erro, nem um `Result.Failure` sem um Erro.
- [ ] **Imutabilidade**: A classe não possui propriedades expostas com modificadores mutáveis.
- [ ] **Testes de Unidade**: 100% dos caminhos e exceções testados.
- [ ] **Testes de Mutação**: Escore Stryker / Mutação acima de 90%.
- [ ] **Documentação em `docs/`**: Todos os arquivos (`README`, `BUSINESS_RULES`, `USE_CASES`, `IMPLEMENTATION`, `TESTS`, `CHECKLIST`) devidamente preenchidos e sincronizados.

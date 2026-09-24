# Ordem Oficial de Implementação

Nenhum componente deve ser construído se suas dependências diretas na sequência abaixo ainda não estiverem concluídas e validadas.

## Fase 1 — Foundation

**Objetivo**: Construir todos os componentes fundamentais e abstrações reutilizáveis da plataforma.

### Sequência Obrigatória:

1. **`Error`**: Estrutura padronizada de erros de domínio e infraestrutura.
2. **`Result`**: Modelo genérico para representar sucesso e falhas esperadas sem utilizar exceções como mecanismo de controle de fluxo.
3. **`Entity`**: Abstração base de entidade com igualdade por ID.
4. **`AggregateRoot`**: Extensão de Entity com suporte a ciclo de vida e eventos de domínio.
5. **`ValueObject`**: Abstração de objeto de valor com igualdade estrutural por componentes.
6. **`DomainEvent`**: Contrato base para eventos assíncronos e de domínio.
7. **`Repository`**: Interfaces genéricas de persistência (Read/Write).
8. **`Specification`**: Padrão de especificação de regras encadeáveis para consultas e validações.
9. **`Pagination`**: Abstrações e registros para paginação e ordenação de dados.
10. **`Auditing`**: Tipos e contratos para rastreabilidade e log de alterações.
11. **`TimeProvider`**: Abstração para manipulação determinística de data e hora em testes.

### Critério de Conclusão da Fase:

- Código 100% implementado.
- Suíte de testes (Unidade, Arquitetura e Mutação) verde.
- Documentação sincronizada em `docs/foundation/[Componente]`.
- Checklist do componente aprovado.

# Claude Implementation Order

## Objetivo

Este documento define a ordem oficial de implementação da
Business Management Platform.

O agente deve respeitar a sequência definida neste documento e
não deve avançar para componentes posteriores sem concluir
integralmente o componente atual.

---

# Regras Gerais de Implementação

Antes de iniciar qualquer implementação, o agente deve:

1. Ler a documentação geral da plataforma.
2. Ler os Architecture Principles.
3. Ler o Technology Stack.
4. Ler os ADRs aplicáveis.
5. Ler os Development Playbooks aplicáveis.
6. Ler a documentação específica do componente.
7. Verificar se existem decisões arquiteturais pendentes.

O agente NÃO deve inventar regras arquiteturais.

Quando encontrar uma decisão não especificada ou uma
contradição entre documentos, deve interromper a implementação
e solicitar decisão arquitetural.

---

# Fase 1 — Shared Foundation

A Shared Foundation contém os componentes técnicos
fundamentais utilizados pelas demais camadas e produtos
da plataforma.

A implementação deve seguir obrigatoriamente esta ordem:

1. Error
2. Result
3. Entity
4. AggregateRoot
5. ValueObject
6. DomainEvent
7. Repository
8. Specification
9. Pagination
10. Auditing
11. TimeProvider

---

# FEATURE-0001 — Error

## Objetivo

Implementar o componente Result da Shared Foundation conforme
sua especificação oficial.

## Documentação obrigatória

Antes de implementar, o agente deve ler:

- Result/README.md
- Result/BUSINESS_RULES.md
- Result/USE_CASES.md
- Result/IMPLEMENTATION.md
- Result/TESTS.md
- Result/CHECKLIST.md

Além da documentação arquitetural geral aplicável ao projeto.

## Escopo

Implementar exclusivamente o componente Result.

Não implementar:

- Entity
- AggregateRoot
- ValueObject
- DomainEvent
- Repository
- outros componentes da Foundation

## Processo

1. Ler toda a documentação obrigatória.
2. Validar as dependências necessárias.
3. Implementar o código conforme a especificação.
4. Implementar os testes definidos.
5. Executar os testes.
6. Executar as verificações de qualidade definidas pelo projeto.
7. Atualizar a documentação de implementação.
8. Atualizar o CHECKLIST.
9. Apresentar resumo da implementação.

## Regra de parada

Se existir alguma decisão arquitetural não definida,
contradição entre documentos ou requisito ambíguo:

- NÃO assumir uma decisão;
- NÃO alterar unilateralmente a arquitetura;
- interromper a implementação;
- registrar a questão;
- solicitar decisão.

## Critérios de conclusão

O Result somente será considerado concluído quando:

- código implementado;
- testes implementados;
- testes aprovados;
- verificações arquiteturais executadas;
- documentação atualizada;
- CHECKLIST concluído.

Após a conclusão, o agente deve aguardar autorização
antes de iniciar o próximo componente.


Result
   ↓
Claude implementa
   ↓
Você revisa
   ↓
Base44 audita
   ↓
ChatGPT revisa arquitetura
   ↓
APROVADO
   ↓
Entity


# FEATURE-0002 — Result

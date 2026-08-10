# Fluxo Oficial de Trabalho (Workflow)

Ao receber qualquer nova solicitação ou instrução de desenvolvimento, siga estritamente o pipeline abaixo:
[Nova Tarefa]
│
▼
[1. Leitura do Contexto Global]
├── PROJECT_OVERVIEW.md
├── ARCHITECTURE_PRINCIPLES.md
└── TECHNOLOGY_STACK.md
│
▼
[2. Leitura do Contexto Arquitetural]
├── SYSTEM_OVERVIEW.md
├── Shared Kernel
├── Foundation
└── Feature / Módulo Alvo
│
▼
[3. Análise da Ordem de Implementação]
└── Validação via CLAUDE_IMPLEMENTATION_ORDER.md
│
▼
[4. Implementação do Código]
└── Aplicação das regras do componente
│
▼
[5. Criação e Execução de Testes]
├── Unit Tests
├── Architecture Tests
└── Mutation Tests
│
▼
[6. Atualização da Documentação]
└── Atualização de README, BUSINESS_RULES, API e TESTS em docs/
│
▼
[7. Checklist e Validação final]
└── Verificação contra CLAUDE_DEFINITION_OF_DONE.md
│
▼
[8. Abertura do Pull Request / Conclusão]

## Diretrizes de Execução por Etapa

- **Etapas 1 e 2 (Leitura)**: Confirme explicitamente os documentos lidos antes de gerar o código.
- **Etapa 5 (Testes)**: Garanta que cenários de exceção e limites (edge cases) estejam cobertos.
- **Etapa 6 (Docs)**: Atualize a seção `## Implementação` e `## Cobertura` no arquivo `TESTS.md` correspondente.
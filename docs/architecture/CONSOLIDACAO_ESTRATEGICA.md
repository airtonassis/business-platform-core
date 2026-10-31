# Consolidação estratégica — modelo híbrido ERP/CRM

## Status e origem

**Visão estratégica aprovada pelo gestor; implementação não autorizada.**

Origem: orientação explícita de consolidação estratégica e preparação documental da Sprint 1. Complementa o [Plano Diretor](decisions/PLANO_DIRETOR_BUSINESS_PLATFORM_CORE.md), seção 20. Não altera ADRs aceitos ou contratos da Shared Foundation. Os desenhos técnicos em [Sprint 1](SPRINT_1_PLANEJAMENTO.md) permanecem propostas.

## Conta empresarial única — nova diretriz aprovada

Uma empresa é uma Organization isolada e um ambiente empresarial, com assinatura futura independente. Produtos/módulos individuais ou pacotes e seus direitos/configurações pertencem a essa empresa. Conta de usuário entra diretamente na empresa vinculada; sem seleção/troca padrão ou administração multiempresa no MVP. PlatformOperator permanece separado e sem dados empresariais automáticos.

[Modelo de identidade/conta e matriz de impacto](CONTA_EMPRESARIAL_UNICA.md): identidade técnica, e-mail e conta empresarial são conceitos distintos. A unicidade permanente por identidade e a exigência de novo login/identidade e convite para outra empresa são regras de produto aprovadas. Modelo físico, constraints, registro histórico e política do IdP exigem aprovação técnica; nenhuma pessoa/contato recebe múltiplos vínculos automaticamente. Templates versionados orientam parametrização específica por empresa e aplicação; não compartilham configuração/dados. Contratação/licenças/templates são futuras, sem antecipação na Sprint 1. A diretriz preserva o modelo híbrido ERP/CRM abaixo.

## Decisão estratégica

Organizar o ecossistema em quatro responsabilidades:

| Responsável | Capacidades e limites |
|---|---|
| Core | Identidade/autenticação, organizações/memberships, acesso/isolamento, catálogo, planos/entitlements, provisionamento/integrações, auditoria/observabilidade e registro futuro de templates e governança de IA |
| ERP horizontal multissetorial | Módulos empresariais reutilizáveis e capacidades opcionais; regras fiscais, contábeis, comerciais e operacionais dependem de contexto e jurisdição |
| CRM autônomo | Clientes/relacionamentos, oportunidades/negociações, funis, atividades e campos/processos configuráveis por contratos versionados; contratação independente ou integração opcional |
| Produtos especializados | GIV, ERP Imobiliário e outros domínios com processos substancialmente específicos; dados, implantação e regras próprios |

O ERP horizontal não é um módulo do Core. O CRM não é obrigatório para usar o ERP ou um produto especializado. Reutilização não autoriza consultas diretas a bancos de outros produtos, entidades de negócio universais ou extração de serviços sem consumidores reais.

## Alternativas e consequências

| Alternativa estratégica | Benefício | Risco / motivo para não adotar como padrão |
|---|---|---|
| Um ERP universal com todas as verticais | Aparente uniformidade | Superdomínio, regras incompatíveis e acoplamento operacional |
| Produtos completamente isolados, sem Core | Autonomia máxima | Duplicação de identidade, contratação e governança |
| Modelo híbrido aprovado | Reutilização transversal com autonomia de domínio | Exige ownership, contratos de integração e UX coerente |

Esta aprovação define direção de produto e limites. Não escolhe fornecedores, schemas, políticas de isolamento, framework de interface ou datas de execução.

## Integrações opcionais e ownership

Cada integração deverá explicitar fonte de verdade por dado, direções de sincronização, IDs de correlação, compatibilidade, consentimento/autorização, idempotência, reconciliação e recuperação de indisponibilidade. A aplicação responsável decide operações no seu backend. Não pressupor que cliente no CRM e contratante no ERP sejam o mesmo agregado.

Contratos REST/eventos são versionados e independentes de linguagem. Produtos não dependem de chamadas síncronas ao Core a cada operação sem justificativa. Eventos de integração são distintos do contrato mínimo `IDomainEvent`. Um mecanismo genérico de plugins não faz parte desta etapa.

## Templates e assistência futura

Catálogo de templates versionados por aplicação e atividade econômica, incluindo CNAE quando aplicável, será capacidade futura. O Core registra metadados e compatibilidade; cada aplicação define parâmetros, valida e executa suas alterações. Classificação econômica fornece sugestões, sem impor configuração ou substituir análise fiscal/contábil.

Fluxo desejado: selecionar → visualizar efeitos → personalizar → validar → confirmar → aplicar → auditar → oferecer recuperação conforme a operação. Compatibilidade, repetição, rollback e alterações destrutivas exigirão contratos próprios antes de implementação.

Agentes poderão explicar e sugerir, sem autoridade administrativa permanente. Prévia, confirmação humana para operações relevantes, autorização no serviço de negócio e auditoria são obrigatórias. A aplicação continua utilizável sem IA. Não há implementação de catálogo de templates, assistente ou agente na Sprint 1.

## Experiência transversal

- Navegação consistente e contexto de organização sempre visível.
- Design system compartilhável, sem impor implantação única.
- Acessibilidade, responsividade e navegação por teclado.
- Onboarding progressivo e configuração guiada.
- Erros compreensíveis e recuperação de operações interrompidas.
- Poucos passos em tarefas frequentes e recursos avançados apresentados conforme necessidade.

A escolha de biblioteca visual depende de aprovação posterior. A Etapa 1 detalha a meta WCAG 2.2 AA solicitada, seus critérios e testes em [rastreabilidade UX](SPRINT_1_RASTREABILIDADE_UX.md); não declara conformidade ou Gate aprovado. A interface demonstrativa proposta da Sprint 1 deverá comprovar apenas a jornada mínima, não um Console completo; nenhuma interface foi criada por esta revisão documental.

## Impactos futuros por produto

| Produto | Preparação necessária | Preservação obrigatória |
|---|---|---|
| GIV | Mapear usuários/organizações, registrar cliente OIDC próprio, adaptar permissões e integrar contratação progressivamente | Operação e dados atuais; jornada viagem → frete → despesas → encerramento; migração somente com plano aprovado |
| ERP Imobiliário | Delimitar imóveis/locações/contratos/financeiro e integração opcional com CRM | Domínio e banco próprios; a jornada lead → visita/proposta não obriga CRM compartilhado nem tabelas comuns |
| ERP horizontal | Definir módulos por consumidores e contextos reais | Variações setoriais e jurisdicionais explícitas |
| CRM | Definir ownership de relacionamentos e contratos de integração | Contratação e operação autônomas; configuração versionada |

Os repositórios dos produtos não foram inspecionados nesta consolidação. Impactos são hipóteses arquiteturais, não diagnóstico desses sistemas.

## Gate de governança

Aprovação estratégica registrada neste documento não aprova implementação. Rever os ADRs propostos e a matriz de decisões da Sprint 1 antes de qualquer desenvolvimento. Não alterar silenciosamente gates históricos ou contratos CLOSED da Foundation.

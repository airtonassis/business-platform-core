PLANO DIRETOR
BUSINESS PLATFORM CORE
Plataforma SaaS multiproduto, multiempresa e extensível por aplicações, serviços e agentes de IA
Versão 1.2 | Consolidação documental da visão estratégica aprovada; origem: versão 1.0 de 30 de outubro de 2026.
Situação: visão estratégica validada pelo gestor. Decisões técnicas da Sprint 1, contratos, estimativas e implementação permanecem sujeitos à aprovação humana. Esta revisão altera somente documentação; não executa implementação, testes, migração ou deploy.
Leitura de status: a seção 1 registra a inspeção original; as seções 18 e 19 preservam o processo originalmente proposto. A Sprint 0 foi posteriormente recebida e revisada, e o planejamento da Sprint 1 foi autorizado. A seção 20 e a revisão de conta empresarial única consolidam a orientação vigente sem converter propostas técnicas em decisões aprovadas.

Decisão estratégica
Construir um núcleo de plataforma que gerencie identidade, organizações, catálogo, direitos contratados, administração e serviços compartilhados; cada produto de negócio mantém domínio, dados e operação próprios. A primeira prova de valor é habilitar duas aplicações para uma organização, com login único, isolamento comprovado e administração centralizada.
Sumário executivo
•	Aproveitar a base .NET existente no repositório business-platform-core; não reescrever sem auditoria.
•	Criar dois consoles distintos: Platform Admin (operador da plataforma) e Tenant Admin (administrador da empresa contratante).
•	Contratação configurável por produto, plano, módulo, recurso, limite e agente de IA; autorização aplicada no backend.
•	Começar com monólito modular no Core e aplicações implantáveis independentemente; microserviços internos somente com justificativa.
•	Usar o GIV como piloto de integração progressiva e o ERP Imobiliário como segundo produto para provar reutilização.
•	Tratar AI & Agent Foundation como capacidade opcional, com governança, segurança, custo e auditoria.
1. Estado atual e limites da avaliação
Inspeção de leitura do repositório GitHub airtonassis/business-platform-core, branch main: solução .NET com projetos Domain, Application, Infrastructure, Contracts e Api; componentes em Domain/Shared (Entities, ValueObjects, AggregateRoots, DomainEvents, Repositories, Results, Specifications, Pagination, Auditing, Errors); projetos UnitTests e ArchitectureTests; diretórios docs/foundation e docs/architecture. Arquivos de referência na raiz, como PLATFORM_ARCHITECTURE.md, CORE_MODULES.md e AI_INTEGRATION.md, aparecem vazios na branch examinada.
Isso comprova a presença de uma fundação de código, mas não comprova identidade centralizada, tenancy, licenciamento, billing, console, deploy, testes passando ou aplicações integradas. Não foi realizada execução local, revisão completa de classes, consulta ao banco nem inspeção de alterações não commitadas.
Gate obrigatório:  auditoria local de código, git status, testes e infraestrutura antes de qualquer modificação; preservar trabalho existente.
2. Premissas não negociáveis
Princípio	Regra arquitetural / operacional
Independência	Produtos implantáveis, versionáveis e escaláveis de forma independente; Core não importa regras de negócio de produtos.
Multi-tenancy	Tenant e organização definidos por identidade validada no servidor; nunca confiar em tenant_id informado isoladamente pelo cliente.
Contratabilidade	Catálogo → plano → assinatura → entitlement → provisionamento → autorização; cobrança não substitui autorização.
Segurança	Least privilege, segregação Platform/Tenant, auditoria, LGPD, segredos em vault/secret store, testes de isolamento.
Resiliência	Falha de um produto não derruba os demais; serviços opcionais de IA e notificações degradam sem bloquear operações críticas.
Evolução	APIs versionadas, eventos de integração, compatibilidade e estratégia de migração sem big bang.
Qualidade	Vertical slices, critérios de aceite antes do código, teste automatizado e homologação real antes de concluir.
Governança	Nenhum agente ou desenvolvedor faz mudanças destrutivas, migração ou deploy sem aprovação explícita.

3. Delimitação do Core e das aplicações
Core obrigatório	Serviços opcionais compartilhados	Produtos independentes
Identidade, sessão e federação	Notificações e templates	GIV: veículos, viagens, fretes
Tenants, organizações, membership	Arquivos e armazenamento	ERP Imobiliário: imóveis e locações
RBAC/ABAC, políticas, auditoria	Integrações, webhooks, eventos	CRM: leads, pipeline e atividades
Catálogo, planos e entitlements	AI/Agents, MCP, RAG	ERP Agro: operações do setor
Provisionamento e Console Admin	Observabilidade e automações	Outras aplicações futuras

Contatos, financeiro e workflows não entram automaticamente no Core: só se tornam serviços compartilhados quando houver contrato comum estável e pelo menos dois consumidores reais. Evitar Shared Kernel amplo com entidades de domínio de setores distintos.
4. Arquitetura lógica e topologia de implantação
1.	Portal de acesso: autenticação e entrada direta na empresa vinculada, sem seletor padrão. Catálogo de produtos contratados e links/launch são capacidades futuras por empresa.
2.	Control Plane: Core API + módulos Identity, Tenancy, Access, Catalog, Subscription, Entitlements, Provisioning, Audit e Admin.
3.	Data Plane: GIV, ERP Imobiliário, CRM e demais serviços, com banco de domínio independente e validação local de autorização.
4.	Shared Services: notificações, documentos, mensageria, integrações e AI Platform, opcionais e isolados por tenant.
5.	Edge: gateway/reverse proxy, TLS, rate limiting, WAF conforme risco; observabilidade e pipeline por serviço.
Começar com monólito modular para o Control Plane, PostgreSQL próprio e separação por módulos. Serviços de negócio podem permanecer monólitos modulares independentes. Extrair microserviços somente quando existirem requisitos concretos de escala, disponibilidade, isolamento, domínio ou autonomia de equipe.
Integrações síncronas: REST/JSON versionado e OpenAPI; assíncronas: eventos de integração com outbox/inbox, idempotência, retry, dead-letter e correlação. Nunca fazer joins diretos entre bancos de aplicações.
5. Identidade, tenants e autorização
5.1 Modelo de identidade
•	Identidade técnica não é e-mail, pessoa civil ou empresa. A conta empresarial de usuário é vinculada a uma única Organization no MVP; Membership define papel/estado nesse ambiente. Administração multiempresa é estudo futuro, sem vínculo automático.
•	Login único com OpenID Connect/OAuth 2.0; usar provedor maduro (gerenciado ou self-hosted) após ADR, evitando construir protocolo de identidade do zero.
•	Tokens de curta duração, validação de issuer/audience/assinatura/expiração; revogação e refresh conforme risco e canal.
•	Empresa resolvida pelo vínculo único validado no servidor após autenticação; APIs revalidam conta, empresa, membership, permissão e escopo. Produtos contratados exigirão entitlements futuros; sem seletor de empresa.
•	Perfis separados: PlatformOwner/PlatformOperator/PlatformSupport e TenantOwner/TenantAdmin/TenantMember; suporte com acesso excepcional temporário e auditado.
5.2 Multi-tenancy e proteção
•	Banco separado por aplicação como padrão inicial; dentro de cada produto, escolher banco por tenant ou compartilhado com RLS e tenant_id conforme custo, risco e escala.
•	TenantContext construído por identidade validada + conta empresarial/vínculo único + membership ativo; tenant_id do payload/rota não é autoridade nem troca a empresa.
•	Testes de acesso cruzado obrigatórios em consultas, comandos, jobs, arquivos, relatórios, cache, índices de busca e eventos.
•	Backup, retenção, exclusão e exportação por organização; plano LGPD e acordos com provedores externos.
6. Catálogo, planos, contratos e licenciamento
6.1 Modelo comercial
Entidade	Responsabilidade
Product	Aplicação publicável: GIV, ERP Imobiliário, CRM etc.
Module / Feature	Capacidade habilitável, com chave estável e documentação.
Plan / PlanVersion	Pacote comercial versionado, limites e regras de preço.
Subscription	Contrato independente da empresa/organização, ciclo, status e vigência; produtos/módulos individuais ou pacotes não compartilham assinatura entre empresas.
Entitlement	Direito efetivo de uso: feature, limite, escopo, validade e origem.
UsageRecord	Consumo medido por produto/recurso/agente, deduplicável.
ProvisioningJob	Criação, ativação, suspensão, reativação e desprovisionamento com estados auditáveis.

6.2 Fluxo de ativação
6.	Platform Admin publica produto e versão de plano no catálogo.
7.	Organização solicita contratação; cobrança/contrato são confirmados por processo idempotente.
8.	Subscription gera entitlements versionados; ProvisioningJob solicita criação de contexto no produto.
9.	Produto confirma provisionamento; acesso é liberado somente após sucesso.
10.	Em cada operação, o backend do produto verifica identidade, organização, permissão e entitlement; pode usar cache com TTL curto e invalidação.
11.	Suspensão/redução de plano bloqueia novas operações conforme política; dados não são excluídos automaticamente.
Política de falha:  definir explicitamente se falha temporária no Core permite período de tolerância para produtos já ativos; jamais liberar recursos não contratados por padrão.
7. Console do gestor e console do cliente
7.1 Platform Admin — seu console
•	Visão geral: organizações, produtos ativos, assinaturas, receita, inadimplência, erros, incidentes e consumo de IA.
•	Organizações: ciclo de vida, contatos administrativos, status, contratos e trilha de alterações.
•	Produtos: registro, versões, endpoints, status de saúde, módulos, features e políticas de compatibilidade.
•	Planos: composição, limites, precificação, trials, cupons, vigências e versões; mudanças auditadas.
•	Assinaturas: ativar, suspender, reativar, cancelar, conceder exceções com justificativa e expiração.
•	Agentes: catálogo, modelos, ferramentas permitidas, budgets, logs de ações e políticas de aprovação.
•	Operação: alertas, deploys por produto, incidentes, jobs de provisionamento, auditoria e suporte com acesso controlado.
7.2 Tenant Admin — console da empresa
•	Perfil e dados da organização, convites e membros, grupos e permissões locais.
•	Produtos contratados, recursos disponíveis, consumo, solicitações de upgrade e faturamento.
•	Configurações próprias de cada aplicação, respeitando permissões e limites do contrato.
•	Gestão de agentes contratados e consentimentos, quando disponíveis.
Separação de UI e API: console Platform Admin com API e políticas privilegiadas distintas. Não permitir bypass de tenant em endpoints convencionais; break-glass com motivo, prazo, aprovação e auditoria.
8. Contrato de integração de uma aplicação
Capacidade	Contrato mínimo
Identidade	Validar OIDC; mapear user_id e organization_id sem confiar em dados de interface.
Registro de produto	ProductId estável, versão, URLs de launch, health, OpenAPI e compatibilidade.
Provisionamento	POST/activate, suspend, resume e status; idempotency key, retries e confirmação.
Entitlements	API ou token/manifest assinado com versão, TTL, limites e invalidação.
Permissões	Escopos e políticas do produto registrados; decisão de autorização no backend do produto.
Eventos	Envelope com event_id, tenant_id, correlation_id, schema_version, occurred_at.
Medição	UsageRecords assinados/autenticados, idempotentes, agregáveis e auditáveis.
Observabilidade	Health, readiness, trace_id, métricas, SLOs e versão de build.

Fornecer SDK .NET inicialmente, mas publicar especificações independentes de linguagem. Um produto não deve precisar ser .NET para aderir à plataforma.
9. AI & Agent Foundation — serviço opcional
9.1 Capacidades compartilhadas
•	Agent Registry: identidade, especialidade, versão, owner, ferramentas, modelo permitido, instruções e política de aprovação.
•	Orquestrador: autenticação, seleção de agente, estado da tarefa, rate limits, budgets, timeouts, circuit breakers e fallback compatível.
•	MCP Gateway/Servers: acesso a ferramentas sob identidade e escopos delegados; toda ação revalidada pelo serviço de negócio.
•	RAG: índices e filtros por tenant, aplicação e permissão; recuperação autorizada antes de enviar contexto ao modelo.
•	LLM Router: escolha de modelos por custo/latência/capacidade, com observabilidade e avaliação de qualidade.
•	LLMOps: traces, custo por agente/tenant, taxa de erro, qualidade, incidentes e auditoria de ações.
9.2 Segurança e limites
•	Históricos, embeddings, documentos e cache segregados por tenant; chave de cache inclui tenant, usuário/escopo quando aplicável, versão do modelo e política.
•	Tratar conteúdo recuperado como não confiável: mitigar prompt injection, exfiltração, uso indevido de ferramentas e saída com dados sensíveis.
•	Agentes não recebem credenciais administrativas permanentes nem acesso SQL irrestrito.
•	Ações de alto impacto (exclusão, pagamento, envio externo, alterações contratuais) exigem política e aprovação humana explícita.
•	Logs de ferramentas com agente, usuário delegante, tenant, escopos, horário, payload protegido/redigido, resultado e correlação.
•	Schemas estritos para saídas estruturadas; validação de domínio no backend, não somente no modelo.
•	Uso de dados em provedores externos sujeito a contratos, políticas de retenção, privacidade e consentimentos aplicáveis.
MCP não substitui REST/gRPC/eventos entre microserviços. É uma interface de ferramentas/contexto para agentes quando necessário. Transportes modernos e streaming devem ser escolhidos conforme suporte real dos clientes e servidores; SSE não é requisito universal.
10. Modelo de dados conceitual
Bounded context	Agregados / entidades candidatas
Identity	UserIdentity, Session, IdentityProviderLink
Tenancy	Organization (ambiente/tenant), EnterpriseUserAccount, Membership, Invitation; nomes candidatos, vínculo único por conta
Access	Role, Permission, Policy, RoleAssignment
Catalog	Product, ProductVersion, Module, Feature
Commercial	Plan, PlanVersion, Subscription, SubscriptionItem
Entitlements	Entitlement, Limit, UsageCounter, Override
Provisioning	ProvisioningJob, ProductTenantBinding, IntegrationEndpoint
Audit	AuditEvent, AdminAction, AccessGrant
AI	AgentDefinition, AgentVersion, ToolGrant, AgentRun, ModelUsage, KnowledgeCollection

Os nomes são candidatos conceituais, não tabelas aprovadas. Na Sprint de modelagem, especificar invariantes, índices, unicidade, ownership, retenção, eventos e política de migração. Evitar tabela genérica única para todos os produtos.
11. Padrões de engenharia e qualidade
•	DDD e Clean Architecture sem abstrações prematuras; fronteiras explícitas e dependências voltadas para domínio.
•	ADR para decisões irreversíveis: IdP, tenancy, billing, mensageria, gateway, IA, política de entitlements.
•	Contratos OpenAPI e testes de contrato entre Core e produtos; versionamento semântico e de schemas.
•	Testes unitários de invariantes, integração com PostgreSQL real, autorização cruzada, migrações e testes E2E das jornadas.
•	Segurança: análise de dependências, secrets scanning, SAST, revisão de permissões e testes de isolamento.
•	CI/CD: PR obrigatório, checks, migrations controladas, imagens imutáveis, rollback, smoke tests e promoção por ambiente.
•	Ambientes dev/homologação/produção segregados; segredos não versionados; observabilidade com correlação ponta a ponta.
•	Definition of Done: código, testes, documentação mínima, evidência funcional, revisão independente e aceite humano.
12. Roadmap proposto por entregas verificáveis
Fase	Entregas principais	Gate de aceite
0 — Auditoria e ADRs	Inventário local, git status, testes, análise de lacunas, decisão IdP e tenancy.	Relatório com evidências, riscos e plano aprovado; nenhuma alteração destrutiva.
1 — Identidade e tenancy	OIDC, organização, conta empresarial/membership, resolução única de contexto, RBAC e isolamento.	Contas distintas acessam suas empresas diretamente; conta A não acessa B; testes negativos e unicidade automatizados.
2 — Catálogo e entitlements	Produtos, módulos, planos versionados, assinaturas simuladas, entitlement check.	Dois planos distintos habilitam features corretamente em API.
3 — Provisionamento e Console	Jobs idempotentes, Platform Admin MVP, Tenant Admin MVP, auditoria.	Gestor ativa/suspende produto; tenant só vê próprios recursos.
4 — Integração GIV piloto	Adapter de identidade, vínculo org, contrato de provisionamento e feature gates.	GIV opera fluxo real sem regressão nem perda de dados.
5 — ERP Imobiliário piloto	Primeira jornada CRM/imóveis usando Core; isolamento e contratação.	Segundo produto opera com mesmo login e dados independentes.
6 — Serviços opcionais	Notificações, arquivos, eventos, billing real conforme demanda.	Cada serviço tem consumidor real, SLO e contrato.
7 — AI & Agent Foundation	Registry, agente read-only, tools autorizadas, budgets, RAG se necessário.	Agente isolado por tenant; auditoria, custo e testes de abuso aprovados.

As fases são sequência de valor e dependência, não promessas de prazo. Entregas podem ocorrer em paralelo apenas com contratos estáveis, donos definidos e worktrees separados.
13. Backlog inicial recomendado (primeiras sprints)
Sprint 0 — Diagnóstico técnico, somente leitura
12.	Executar git status/branch/log e mapear alterações locais antes de sincronizar com remoto.
13.	Inventariar todos os projetos, classes, testes, migrations, Docker, pipelines e documentação efetiva.
14.	Executar build e testes existentes em ambiente controlado; registrar falhas sem corrigi-las automaticamente.
15.	Mapear dependências entre Domain/Application/Infrastructure/Contracts/API e lacunas da fundação.
16.	Comparar GIV e ERP Imobiliário com contratos propostos; produzir matriz de reaproveitamento.
17.	Propor ADR-001 identidade, ADR-002 tenancy, ADR-003 contrato de produto, ADR-004 licenciamento e ADR-005 dados.
Sprint 1 — Primeiro vertical slice SaaS
18.	Criar/ligar provedor OIDC aprovado e login de demonstração.
19.	Cadastrar empresa e conta de usuário vinculada, resolver contexto após login e aplicar permissão no backend, sem seleção/troca de empresa.
20.	Persistir recurso de demonstração por tenant e testar acesso cruzado negado.
21.	Expor auditoria de criação, convite e alteração de permissão.
22.	Entregar uma jornada web demonstrável e testes automatizados; sem dependência de GIV.
Sprint 2 — Produto contratável
23.	Registrar produto demonstrativo e duas versões de plano.
24.	Criar assinatura de homologação e calcular entitlements determinísticos.
25.	Implementar API de verificação com testes de feature negada, limite excedido e suspensão.
26.	Criar tela inicial do Platform Admin para organização, catálogo, assinatura e auditoria.
27.	Implementar job de provisionamento idempotente e testes de falha/retry.
14. Critérios de aceite do primeiro marco
Caso	Resultado obrigatório
CA-01	Conta empresarial entra diretamente em sua única empresa; contas independentes de A/B têm isolamento comprovado, sem seletor padrão.
CA-02	Usuário sem membership não acessa recursos de outra organização.
CA-03	Empresa A contrata GIV; empresa B não acessa GIV sem entitlement.
CA-04	Uma organização habilita dois produtos sem recriar a identidade.
CA-05	Suspensão do produto é aplicada no backend e auditada, sem exclusão indevida de dados.
CA-06	Admin do tenant não acessa Console Platform Admin.
CA-07	Falha de provisionamento é recuperável sem duplicar organização no produto.
CA-08	Logs e métricas correlacionam tenant, produto e requisição sem expor segredos.
CA-09	Build, testes, migrações e rollback possuem evidências reproduzíveis.
CA-10	Acesso e operação do GIV continuam funcionando durante integração progressiva.

15. Estratégia de convergência com GIV e ERP Imobiliário
GIV — preservar e adaptar
Não substituir autenticação e organizações atuais em um único deploy. Introduzir camada de compatibilidade e mapeamento de IDs; migrar sessões e dados com estratégia aprovada; manter o produto operando de forma independente durante a transição. O fluxo viagem → frete → despesas → encerramento deve continuar prioridade funcional do GIV, sem esperar a plataforma inteira.
ERP Imobiliário — construir já aderente
Aproveitar a especificação ERP + CRM + portais + omnichannel como visão de produto, mas começar com jornada mínima: organização contratante → cadastro de imóvel → lead → atendimento → visita/proposta. Identidade, catálogo e entitlements vêm do Core; imóveis, contratos, locações e financeiro permanecem no domínio imobiliário.
16. Riscos e mitigação
Risco	Mitigação
Core virar supermonólito de regras de negócio	Bounded contexts, contratos e regra explícita de não importar domínio de produtos.
Centralização gerar ponto único de falha	Tokens verificáveis localmente, cache com TTL, tolerância definida e provisionamento assíncrono.
Vazamento entre tenants	Testes negativos em todas as camadas, RLS onde adequado e revisão de autorização.
Cobrança divergir de acesso	Entitlements como fonte técnica, eventos idempotentes e reconciliação periódica.
Migração quebrar GIV	Compatibilidade progressiva, backup, ambiente de homologação, rollback e aceite.
IA gerar custo e risco imprevisíveis	Agentes opcionais, budgets, limites, políticas de ferramenta e auditoria.
Documentação virar substituto de produto	Gates com jornadas reais demonstradas, evidência de testes e aceite.
Muitos microserviços cedo demais	Começar modular e extrair somente com métricas e justificativa.

17. Governança, papéis e decisões pendentes
Gestor/PO: visão, preços, priorização, autorização de mudança e aceite. Arquitetura: limites, ADRs, segurança e contratos. Codex/Claude Code: inspeção, implementação aprovada, testes e documentação de evidências. Revisor independente: validação de riscos, testes, autorização e aderência. Operação: CI/CD, observabilidade, backup, restauração e incidentes.
•	Escolher provedor de identidade e política de migração de usuários do GIV.
•	Preservar Organization como fronteira empresarial; detalhar identidade/conta única e constraints. Grupo econômico, múltiplas filiais/empresas e gestão consolidada são estudo futuro fora do MVP.
•	Escolher estratégia de isolamento físico/lógico de dados por produto e perfil de cliente.
•	Definir se billing será manual/simulado no MVP ou integrado a gateway real.
•	Definir escopo mínimo do Console Admin e do Tenant Admin.
•	Decidir catálogo de recursos comuns e quais precisam de dois consumidores antes de serem extraídos.
•	Aprovar piloto GIV e primeiro vertical slice do ERP Imobiliário.
•	Definir metas de disponibilidade, latência, custos, retenção e RPO/RTO por produto.
18. Instrução de auditoria para Codex — copiar no repositório
Atue como arquiteto sênior. Leia o Plano Diretor do Business Platform Core, AGENTS.md (se houver), solution, projetos, testes e documentação. Faça auditoria somente de leitura do repositório local. Antes de tudo, registre git status, branch e alterações não commitadas. Compare a arquitetura real com as fases 0–2 deste plano. Liste evidências por arquivo, lacunas, riscos, dependências, ADRs propostos e um backlog de vertical slices com critérios de aceite. Execute build/testes apenas se não modificarem dados ou infraestrutura; caso contrário solicite aprovação. Não edite arquivos, não rode migrations, não faça commit, push ou deploy. Ao final, apresente diagnóstico e aguarde autorização explícita.
19. Aprovação proposta
Aprovar neste momento apenas a visão arquitetural, as premissas, os limites do Core e a Sprint 0 de auditoria. Após o diagnóstico, revisar o roadmap, estimativas e ADRs antes de autorizar qualquer implementação. O sucesso da plataforma será medido pela reutilização comprovada entre produtos, segurança e experiência de contratação, não pela quantidade de microserviços.

## 20. Consolidação estratégica e preparação da Sprint 1

### 20.1 Visão estratégica aprovada

O ecossistema combina um ERP horizontal multissetorial, um CRM autônomo e configurável e produtos especializados independentes, incluindo GIV e ERP Imobiliário. Todos reutilizam capacidades transversais do Core, sem transferir para ele suas regras de domínio ou compartilhar indevidamente bancos de negócio.

O ERP horizontal evolui por módulos empresariais reutilizáveis e capacidades opcionais. Regras fiscais, contábeis, comerciais e operacionais não são presumidas universais. O CRM pode ser contratado isoladamente ou integrado opcionalmente ao ERP e aos produtos especializados; seu núcleo abrange clientes e relacionamentos, oportunidades, funis, atividades e campos/processos configuráveis sob contratos versionados. Ownership de dados compartilhados deverá ser definido por integração; esta visão não aprova um cadastro universal.

O Core contempla, como arquitetura-alvo, identidade, organizações/memberships, autorização/isolamento, catálogo, planos/entitlements, provisionamento/integrações, auditoria/observabilidade, registro de templates/configuração assistida e governança futura de agentes. Esta enumeração não comprova implementação. O registro de templates de configuração é distinto de templates de notificações mencionados na seção 3: conteúdo e validação de negócio permanecem na aplicação responsável.

Integrações entre aplicações são opcionais e ativadas de forma controlada, com APIs/eventos versionados, autorização, auditoria, idempotência, reconciliação, tratamento de indisponibilidade e compatibilidade. Não haverá mecanismo genérico de plugins nesta etapa nem dependência síncrona desnecessária entre produtos.

Templates futuros são versionados por aplicação e atividade. Atividade econômica/CNAE pode sugerir configurações, nunca impor regras rígidas. O administrador seleciona, visualiza, personaliza e confirma; a aplicação valida os parâmetros e aplica somente mudanças autorizadas.

IA é assistência opcional futura: pode sugerir templates, explicar parâmetros e orientar o administrador, sob escopos delegados. Operações relevantes exigem prévia, confirmação, autorização e auditoria; agentes não alteram diretamente dados ou permissões fora dos contratos autorizados.

A experiência transversal deve oferecer navegação consistente, design system compartilhável, acessibilidade, responsividade, onboarding progressivo, configuração guiada, erros compreensíveis, recuperação de operações interrompidas e poucos passos para tarefas frequentes. Recursos avançados aparecem conforme contexto. Compartilhar UX não obriga compartilhar implantação ou domínio.

O registro detalhado e os impactos nos produtos estão em [Consolidação estratégica](../CONSOLIDACAO_ESTRATEGICA.md).

### 20.2 Estado documental e próximas decisões

A Sprint 0 confirmou no checkout auditado a Foundation implementada, Application estrutural, projetos Api/Infrastructure/Contracts vazios e fora da solução, build sem avisos/erros e 532 testes unitários + 243 arquiteturais aprovados. Trata-se de evidência histórica da auditoria, não de nova execução nesta revisão nem de plataforma SaaS funcional. Não houve validação dos repositórios ou ambientes dos produtos.

Os ADRs 0001–0006 e contratos existentes permanecem intactos. A numeração sugerida historicamente no item 17 da seção 13 não deve ser reutilizada: os próximos identificadores foram destinados às propostas ADR-0007 a ADR-0012. As propostas não substituem ADRs aceitos nem autorizam implementação. Catálogo, billing, console completo, templates e agentes não entram na Sprint 1.

O pacote [Planejamento da Sprint 1](../SPRINT_1_PLANEJAMENTO.md) reúne propostas, matriz de aprovação, contratos mínimos, backlog S1-00 a S1-11, dependências, segurança, E2E e entregas menores. A primeira jornada usa um recurso demonstrativo, sem habilitar produtos comerciais; o marco multiproduto das seções 12 e 14 permanece posterior.

### 20.3 Governança vigente

A autorização atual é exclusivamente documental. Não encerra gates pendentes da Foundation, não aprova Repository 2.x retroativamente e não aprova IdP, modelo de tenancy, RLS, MaxOffset, dependências, schemas ou contratos públicos propostos. Desenvolvimento, provisionamento de infraestrutura, execução de migrações e deploy exigem autorizações correspondentes. Não há datas de entrega ou compromissos de capacidade aprovados.

### 20.4 Etapa 1 — fechamento documental após diagnóstico UX

A auditoria UX-01–UX-16 identificou lacunas de jornadas e contratos sem interface executável. O [registro de tratamento](../SPRINT_1_FECHAMENTO_UX.md) acompanha cada achado; [contratos e fluxos](../SPRINT_1_CONTRATOS_FLUXOS.md) detalham transporte web/API, convite, identificação, ETag, idempotência, sessão, matriz de autorização, estados e operação Platform mínima; [rastreabilidade](../SPRINT_1_RASTREABILIDADE_UX.md) liga requisitos, quatro perfis, telas/estados, casos de uso, permissões e testes.

Todos os novos contratos e ajustes nos ADRs 0007–0012 continuam propostos. A revisão não altera os ADRs aceitos 0001–0006 ou contratos da Foundation. Meta de acessibilidade: WCAG 2.2 AA conforme critérios propostos, sem alegação de conformidade anterior à evidência do processo real. S1-10 foi desdobrada em revisão antecipada e UI final; estimativa anterior precisa de revisão para o escopo ampliado, sem promessa de prazo.

Conclusão documental prepara revisão humana para eventual prototipação separadamente autorizada. Não aprova Gate UX, arquitetura técnica, desenvolvimento, migração ou deploy. Estratégia híbrida ERP/CRM, produtos independentes e integrações opcionais permanecem preservados; nenhuma dessas capacidades futuras é implementada na Sprint 1 por esta documentação.

### 20.5 Conta empresarial única — revisão de produto aprovada

A diretriz vigente é uma conta empresarial/ambiente por empresa, representada por Organization isolada, assinatura futura independente e produtos/módulos individuais ou pacotes. Usuário empresarial entra diretamente na empresa vinculada, sem lista/seletor/troca na experiência padrão. Uma empresa pode utilizar GIV, ERP Imobiliário, ERP Multissetorial e CRM conforme direitos contratados; permissões/licenças/parametrizações pertencem ao seu ambiente. Templates futuros versionados sugerem configurações ajustáveis por atividade, não compartilham dados nem impõem regra universal.

A proposta anterior de um mesmo usuário operar A/B com seleção foi substituída antes de implementação. Contas distintas em A/B permanecem fixtures obrigatórias de isolamento. O [modelo e matriz de impacto](../CONTA_EMPRESARIAL_UNICA.md) distinguem empresa, identidade técnica, e-mail e conta de usuário, analisam outra empresa pela mesma pessoa e registram alternativas. Recomendações técnicas, constraints e ADRs 0007–0012 continuam propostos. PlatformOperator é separado e não recebe dados empresariais automaticamente. Fundação SaaS não antecipa contratação, billing, licenças, catálogo, templates ou IA; demonstração técnica é somente homologação. Nenhuma mudança em Foundation, código ou infraestrutura foi autorizada.

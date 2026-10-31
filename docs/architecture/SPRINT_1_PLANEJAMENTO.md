# Sprint 1 — pacote de decisão e backlog proposto

## Status, autoridade e escopo

**Planejamento proposto para aprovação humana; execução não autorizada.**

A autorização vigente permite somente documentação. A [visão estratégica ERP/CRM](CONSOLIDACAO_ESTRATEGICA.md) foi aprovada; as escolhas abaixo não foram. Referências: [Plano Diretor](decisions/PLANO_DIRETOR_BUSINESS_PLATFORM_CORE.md), ADRs aceitos 0001–0006 e regras em [engenharia](../engineering/agents/README.md).

Objetivo funcional: login OIDC → resolução da empresa vinculada → início direto → autorização → recurso demonstrativo persistido e isolado → auditoria. Não antecipar catálogo, planos, billing, entitlements, provisionamento comercial, console completo, templates, agentes ou integração efetiva dos produtos. O recurso demonstrativo não é um produto contratado. Duas aplicações integradas permanecem marco posterior.

Revisão vigente: [conta empresarial única](CONTA_EMPRESARIAL_UNICA.md); diretriz de produto aprovada, desenho técnico proposto. Substitui listagem/seleção de empresas por contexto único. Revisão documental da Etapa 1: [contratos/fluxos C01–C10](SPRINT_1_CONTRATOS_FLUXOS.md), [rastreabilidade RF-01–RF-20/UAT-01–UAT-19](SPRINT_1_RASTREABILIDADE_UX.md) e [tratamento UX-01–UX-16](SPRINT_1_FECHAMENTO_UX.md). Esses documentos detalham os contratos propostos deste pacote, não representam aprovação de ADRs/Gate UX, protótipo ou implementação. Quando houver escolha técnica, o resumo abaixo remete ao detalhamento revisado, sem manter a alternativa antiga como decisão vigente.

## Evidências e limites

Sprint 0, baseline 5f734bf: build sem avisos/erros e 532 testes unitários + 243 arquiteturais aprovados. Esses resultados são históricos, não reexecutados nesta consolidação. Domain contém Foundation; Application é estrutural; Api/Infrastructure/Contracts não são executáveis no estado auditado. Não inferir funcionalidade externa a partir deste checkout.

ADRs 0001–0006 continuam intactos. Não declarar Foundation encerrada por esta revisão: checklists contêm gates pendentes, registros históricos de não versionamento e critérios de mutação divergentes. Resolver rastreabilidade sem alterar contratos ou conceder aprovação retroativa. Repository 2.x existe no código auditado; sua documentação/autorizações precisam ser reconciliadas. Qualquer exceção aos guards exige decisão explícita; não relaxar testes para acomodar implementação.

## Matriz de decisões pendentes

| ID | Proposta e alternativa principal | Aprovação necessária / evidência antes de executar |
|---|---|---|
| D01 | [ADR-0007](decisions/ADR-0007-oidc-identity-and-web-session.md): comparar OIDC próprio operado e gerenciado por critérios de operação, identidade estável e logins independentes; nenhum fornecedor escolhido | Gestor/arquitetura/operação: fornecedor, responsável, custos, MFA, sessão e ambiente |
| D02 | [ADR-0008](decisions/ADR-0008-organization-membership-and-context.md): Organization=ambiente empresarial; conta de usuário com vínculo único; multiempresa fora do MVP | Gestor/arquitetura: identidade distinta de conta/e-mail, constraints, estados, bootstrap e segunda empresa pela mesma pessoa |
| D03 | ADR-0008/C02–C06: referência de destinatário, convite Member 24 h, código separado, prévia e cancel/replace | Gestor/segurança/privacidade: TTLs, armazenamento transitório, idempotência e sem reativação nesta sprint |
| D04 | [ADR-0009](decisions/ADR-0009-persistence-isolation-and-concurrency.md): PostgreSQL compartilhado + filtros/RLS; alternativa banco por tenant | Arquitetura/segurança/operação: esquema físico e matriz operação/papel/grant/policy, bootstrap restrito e teste de pooling |
| D05 | ADR-0009: EF Core/provedor e IDs escalares; MaxOffset=10.000 hipotético | Arquitetura/gestor: dependências/versões verificadas, limite operacional e M1–M5; sem C-B habilitada |
| D06 | ADR-0009: serializar mutação/revogação por locks e versão otimista | Arquitetura/gestor: comportamento concorrente e limites de operações já iniciadas |
| D07 | [ADR-0010](decisions/ADR-0010-rbac-and-platform-tenant-segregation.md): papéis fixos; alternativa papéis configuráveis | Gestor/segurança: matriz de acesso, conta Platform separada, claims administrativas e concessão/revogação |
| D08 | [ADR-0011](decisions/ADR-0011-persistent-operational-audit.md): auditoria no mesmo commit; alternativa serviço externo | Gestor/arquitetura/privacidade/operação: ações, whitelist, acesso, retenção, expurgo e falhas |
| D09 | [ADR-0012](decisions/ADR-0012-executable-api-and-composition.md): host modular, Razor→Application e API bearer; operador mínimo | Arquitetura/gestor/UX: transporte C01, UI C09, grafo de referências; nenhum pacote da Application por conveniência |
| D10 | Contratos e testes deste pacote, gates Foundation/Repository 2.x | Arquitetura/revisor/gestor: resolver lacunas de governança e congelar critérios de aceite |
| D11 | Backlog, estimativas e divisão em entregas | Gestor/equipe: capacidade real, responsáveis e sequência; sem prazo aprovado |
| D12 | Início do desenvolvimento e execução em ambientes | Autorizações explícitas para código, infraestrutura, schema/migrações e deploy, conforme ação; aprovação documental não as substitui |

Todos os itens permanecem pendentes. As recomendações e parâmetros não podem ser tratados como valores já aprovados. O responsável indicado é um papel necessário, não uma pessoa nomeada ou aprovação concedida.

A matriz H01–H10 em [fechamento UX](SPRINT_1_FECHAMENTO_UX.md) detalha estas aprovações: sessão revisada, referências, continuation, rascunhos, versões, novos comandos e meta AA. A visão estratégica permanece aprovada; esses desenhos, seus prazos e execução não.

## Contratos mínimos sugeridos

### Domínio e Application

| Módulo / contrato candidato | Invariantes e fronteiras |
|---|---|
| Identity: UserIdentity e ExternalIdentityLink | UserId interno; vínculo issuer/subject único; e-mail não associa contas automaticamente |
| Tenancy: Organization | Nome/estado; criação com Owner na mesma transação |
| Identity: EnterpriseUserAccount | IdentityId UNIQUE inclusive revogados, OrganizationId imutável; conta de usuário empresarial não é conta/empresa duplicada |
| Tenancy: Membership | AccountId UNIQUE e organização coerente por FK; papel/estado explícitos; sem vínculo múltiplo |
| Tenancy: Invitation | Destinatário fixo, expiração, segredo protegido e consumo único; não concede papel superior a Member |
| Access: contexto validado e políticas | User/Organization/Membership/papel/correlação definidos pelo servidor; nenhum tenant global mutável na sessão |
| Sandbox: DemoRecord | ID escalar, organização imutável, título 1–200 e versão; não mover entre tenants |
| Audit: OperationalAuditEvent | Evento append-only, escopo, ator existente, ação/resultado, UTC/correlação; sem payload irrestrito |

Novos tipos em módulos, fora de Domain/Shared. Reutilizar Entity/AggregateRoot/Result/Error e Auditing onde compatíveis; não criar relógio, Result, repositório genérico ou infraestrutura na Foundation. Nomes são contratos candidatos, não tabelas ou APIs aprovadas.

Casos de uso: resolver identidade; emitir/conferir referência voluntária de destinatário; criar organização/conta de proprietário; resolver vínculo empresarial único após login; consultar contexto sem lista/seletor; listar/consultar membros; emitir/pré-visualizar/aceitar/listar/consultar/cancelar/substituir convite; alterar papel/revogar; operar recurso e consultar auditoria. Todas as mutações e replays autorizam no caso de uso, não apenas no endpoint. Reativação de membership revogado está adiada, não disponível por novo convite.

Portas específicas em Application: identidade autenticada, resolução de contexto/membership, consultas de módulo, persistência de convite, unidade transacional do Core e escrita/consulta de auditoria. Reutilizar IRepository e ISpecificationRepository somente onde adequados; não expor IQueryable ou ORM. TimeProvider nativo é injetado na Application, nunca assinatura nova na Foundation.

### API e experiência mínima

| Método / rota proposta | Política e resultado |
|---|---|
| GET /api/v1/me | Autenticado; identidade interna/nome sem segredo; referência não é recuperável por GET |
| POST /api/v1/me/recipient-references | Próprio usuário; referência voluntária exibida uma vez, validade proposta 24 h |
| GET /api/v1/me/context | Autenticado; uma conta/empresa coerente e ativa; zero vínculo permite só preparação; inconsistência nega |
| POST /api/v1/platform/owner-preview | PlatformOperator; par UserId/referência, sem diretório global |
| POST /api/v1/platform/organizations | PlatformOperator; identidade sem vínculo; cria empresa/EnterpriseUserAccount/Owner atomicamente |
| GET /api/v1/organizations/{orgId}/context | Conta de empresa única ativa; orgId da rota deve corresponder ao vínculo; sem selecionar empresa |
| GET /api/v1/organizations/{orgId}/memberships | Owner/Admin; lista local paginada |
| GET /api/v1/organizations/{orgId}/memberships/{id} | Owner/Admin; item do mesmo tenant, ETag individual |
| POST /api/v1/organizations/{orgId}/invitation-recipient-preview | Owner/Admin; par UserId/referência; retorno mínimo |
| POST /api/v1/organizations/{orgId}/invitations | Owner/Admin; destinatário conhecido sem conta empresarial+referência, Member fixo, Idempotency-Key |
| GET /api/v1/organizations/{orgId}/invitations e /{id} | Owner/Admin; metadados/estado/versão, sem segredo |
| POST /api/v1/organizations/{orgId}/invitations/{id}/cancel | Owner/Admin; Pending, If-Match |
| POST /api/v1/organizations/{orgId}/invitations/{id}/replace | Owner/Admin; Pending/Expired, If-Match e Idempotency-Key; antigo invalidado |
| POST /api/v1/me/invitations/{id}/preview | Destinatário autenticado+código; leitura mínima sem aceite/transição |
| POST /api/v1/me/invitations/{id}/accept | Destinatário autenticado+código; If-Match na primeira transição; replay conforme C06 |
| PATCH /api/v1/organizations/{orgId}/memberships/{id} | Matriz C08; Owner promove/rebaixa/revoga outro Admin/Member; Admin só revoga outro Member; If-Match |
| POST /api/v1/organizations/{orgId}/demo-records | Owner/Admin; organização vem do contexto validado |
| GET /api/v1/organizations/{orgId}/demo-records | Membership ativo; lista paginada e ordenada |
| GET /api/v1/organizations/{orgId}/demo-records/{id} | Membership ativo; recurso do mesmo tenant |
| PATCH /api/v1/organizations/{orgId}/demo-records/{id} | Owner/Admin; If-Match, versão e organização imutável |
| GET /api/v1/organizations/{orgId}/audit-events | Owner/Admin; filtro de tenant obrigatório e paginação |

DTOs não recebem ator, papel efetivo ou ownership autoritativos do cliente. Dados de organização divergentes são rejeitados. 401 sem autenticação; 403 por ação proibida em org acessível; 404 para org inacessível/recurso de outro tenant; 409 para conflito de estado, 412 versão obsoleta e 428 precondição ausente. Validações de payload retornam 400 com Problem Details sanitizado/código estável/correlação; exceções não classificadas não são convertidas indiscriminadamente em falha de domínio.

Formato de paginação reutiliza semântica existente, não serializa tipos internos diretamente. SortKeys permitidas são declaradas pelo domínio; MaxOffset depende de D05. C05 define ETag individual/versão por linha, If-Match, 412/428 e recuperação sem sobrescrita. C06 define chave por ator/escopo/ação, retenção proposta 48 h, replay reautorizado e resultado desconhecido. Não guardar código de convite no registro de replay: resposta perdida exige substituição explícita, nunca reconstrução de hash. Retenções e storage continuam pendentes.

UI Razor mínima proposta: login/logout; preparação de identificação sem empresa; início direto da empresa e contexto informativo, sem seletor; demonstração somente homologação; convite com URL sem segredo+código separado, prévia, cancel/replace; membros; auditoria básica; formulário Platform mínimo de proprietário/organização com revisão/comprovante. Razor chama Application diretamente; API externa aceita bearer, não cookie implicitamente. C07 especifica sessão/tempo/rascunho, perda de acesso, histórico e abas; C09 define feedback por status. Meta WCAG 2.2 AA e testes em rastreabilidade; nenhum teste runtime foi criado; protótipos existentes são estáticos e revisados para conta única. Console completo e design system genérico permanecem fora do escopo.

## Desenho de persistência e segurança

O [ADR-0009](decisions/ADR-0009-persistence-isolation-and-concurrency.md) contém tabelas, chaves, índices, constraints, transações, RLS e concorrência propostos. Antes do desenvolvimento, revisar SQL/grants e testes em papel real. Identidade e bootstrap não justificam acesso geral a todos os tenants. Administração usa porta restrita, autorização Platform e auditoria, nunca ignoreTenant em repositório comum.

Operações de convite e mutações de dados confirmam auditoria no mesmo commit; falha de auditoria impede mutação. Sem migrations automáticas ao iniciar aplicação. Scripts/esquemas futuros e seus testes exigem ambiente autorizado; nenhum é criado ou aplicado nesta fase documental.

## Plano de testes anterior à execução

| ID | Categoria | Casos obrigatórios e evidência esperada |
|---|---|---|
| T01 | OIDC real | Login/callback; expiração; issuer/audience/assinatura incorretos; ID token rejeitado como access token; rotação de chaves e indisponibilidade |
| T02 | Sessão web | CSRF em mutações; logout; redirects; cookies; expiração; esquemas web/bearer explícitos; segredos ausentes nos logs |
| T03 | RBAC | Operação × papel × organização; autoelevação; Platform/Tenant; Owner protegido; usuário global não enumerável |
| T04 | Isolamento | Ler/listar/contar/criar/alterar cruzados, IDs conhecidos de B em A, payload divergente e contexto ausente |
| T05 | PostgreSQL real | RLS sob runtime não privilegiado, USING/WITH CHECK, FK/UNIQUE, pooling, rollback e grants administrativos/bootstrap |
| T06 | Concorrência | Duas conexões/barreiras: revogação antes/depois do lock; alteração de papel, ETag e último Owner |
| T07 | Convite | Destinatário errado, validade, replay, segredo inválido, limite de tentativas, duas aceitações e não reativação implícita |
| T08 | Auditoria | Commit/rollback conjuntos, falha de escrita, ator/UTC/correlação, whitelist, leitura cruzada e negação sem atribuição falsa |
| T09 | Regressão/arquitetura | Suíte existente sem remoção/relaxamento; contratos Foundation preservados; dependências e pacote/SDK aprovados |
| T10 | Repository 2.x, se consumido | MaxOffset antes de I/O, M1–M5, whitelist de ordenação, C-B desabilitada, sem fallback client-side |
| T11 | Contrato e E2E | OpenAPI/códigos, jornada abaixo, duas abas e recuperação de conflito/expiração |

Fakes servem a testes unitários; não substituem IdP/PostgreSQL reais nos testes correspondentes. Asserções de desempenho precisam de carga/ambiente/metas aprovados, ainda não definidos. Build e health são auxiliares, nunca prova funcional. Mutation testing segue regras aplicáveis aprovadas, distinguindo killed/timeout/survived e N/A; não assumir cobertura universal ou score atual dos números históricos.

## Jornada E2E e aceite funcional

Fixtures em ambiente isolado: Ana_A Owner A, Bruno_B Owner B com identidade distinta, Carla sem vínculo para convite A e conta Platform separada. Nenhuma identidade tem conta empresarial em A e B. Preparação conhecida após login não concede dados empresariais. Fixtures não concedem privilégios extras aos usuários empresariais.

1. Ana_A autentica e entra diretamente em A; Bruno_B entra em B. Sem lista ou seletor.
2. Operador tenta criar B usando identidade Ana_A já vinculada: falha sem ambiente órfão, membership ou privilégio extra.
3. Carla autentica sem conta empresarial e prepara referência, sem dados de A/B; Ana_A confere e emite convite Member para A.
4. Carla captura código, autentica/retoma, vê prévia e confirma; conta de usuário A+Membership+auditoria são atômicos e entrada resolve A.
5. Convites A/B concorrentes para identidade sem vínculo: uma vinculação vence; não há conta/membership em duas empresas.
6. Conta A tenta rota/payload/header/ID de B: zero dados, contagens e gravações cruzados, inclusive por RLS/pooling.
7. Owner promove/revoga outro membro conforme C08; Member não escreve, Admin não altera Admin/Owner e Platform não acessa tenant.
8. Duas gravações com mesmo ETag: um commit e um 412; revogação concorrente segue locks, nova requisição após commit nega.
9. Duas abas da mesma sessão usam A. Logout/relogin como identidade B exige revalidação; resposta/rascunho de A não aparece em B.
10. Auditoria confirma vínculo/ações sem segredos; GET contexto não gera seleção. Falha de auditoria impede mutação; timeout indica resultado desconhecido.

Complementos UAT-01–19: IdP e PostgreSQL reais, recuperação não altera empresa, unicidade inclusive revogados, conta sem vínculo/inconsistente, ciclo de convite, AA, falhas do store e sessão.

Aceite: 100% dos cenários definidos aprovados; zero registros/contagens de B para Bruno; zero gravações cruzadas; zero elevação indevida; um único membership por org/usuário; cada mutação confirmada com evento correspondente; nenhum segredo nas evidências. Revisão independente e aceite humano são gates separados do relatório automático.

## Backlog S1-00 a S1-11

Todos os itens têm **fase Sprint 1**, **status Pendente — execução não autorizada**. Estimativas são hipóteses de esforço ativo de engenharia, não compromisso, e incluem testes locais do item. Checklist comum de cada tarefa: contexto aprovado; implementação autorizada; testes unitários/arquitetura/integração aplicáveis; mutação quando aplicável; documentação/evidência; revisão e aceite. Nada desse checklist está concluído por escrever este planejamento.

| Tarefa | Prioridade / dependências | Subtarefas pequenas | Testes / critério de aceite | Hipótese de esforço |
|---|---|---|---|---|
| S1-00 Revisar/congelar decisões | P0; revisão deste pacote + S1-10a; D01–D12 conforme ação | Revisar ADRs, RF/UAT, C08, grants e H01–H10; gates Foundation/Repository; aprovação humana antes de congelar | G01–G08 e decisões registradas; nenhuma aprovação automática | A reestimar |
| S1-01 Ambiente de teste | P0; S1-00 + autorização operacional | IdP/DB/store isolados, papéis e fixtures; credenciais fora do Git | IdP/PostgreSQL reais; autorização antes de infraestrutura/esquema | A reestimar |
| S1-02 Host e adapters | P0; S1-00 | Razor→Application; bearer separado; erros/correlação; OpenAPI/grupos | UAT-01/02/19; sem migration automática ou dependência proibida | A reestimar |
| S1-03 Identidade/sessão | P0; S1-01/02/05 | OIDC; usuário; cookie revogável; referência própria; retomada segura, inatividade/logout/rascunho; suporte de tempo C07 | UAT-01/02/03/05/12/13/15; sem token/código no browser como sessão ou URL | A reestimar |
| S1-04 Domínios mínimos | P0; S1-00 | IDs/estados Identity/Organization/EnterpriseUserAccount/Membership/Invitation e contratos técnicos sessão/referência/continuation/rascunho; invariantes C04/C08 | UAT-06/16/19; Owner protegido, sem ampliar Foundation | A reestimar |
| S1-05 Persistência/RLS | P0; S1-01/04 | Mapeamentos, grants, bootstrap restrito; versões/locks; storage técnico C02/C03/C07 e idempotência C06 | UAT-03/06/08–13/19; contexto de pooling e retenção aprovados | A reestimar |
| S1-06 Auditoria | P0; S1-05 | Evento/transação/whitelist; ciclo de convite; vínculo/resolução sem comando de seleção | UAT-11/14/15; rollback conjunto e sem segredo | A reestimar |
| S1-07 Contexto/policies | P0; S1-03/05/06 | C08 em ambos adapters; prévia de proprietário sem vínculo; resolução única e GET contexto; acesso perdido e replay reautorizado | UAT-03/07/08/14/16/17; sem bypass Platform/Tenant | A reestimar |
| S1-08 Convite/membership | P1; S1-07 | Prévia destinatário; emitir/listar/item/preview/aceitar/cancel/replace; versão/papel/revogação e resposta perdida | UAT-03–06/08–10/16; nenhum segredo recuperado do hash ou reativação | A reestimar |
| S1-09 Recurso demonstrativo | P1; S1-07 | Criar/listar/item/PATCH; filtros/paginação; ETag/chave/recuperação | UAT-07–11/18/19; zero acesso cruzado/sobrescrita | A reestimar |
| S1-10a Revisão UX antecipada | P0 documental; este pacote + autorização separada de prototipação, se houver | Percorrer quatro perfis/RF/UAT; revisar estados e contratos; protótipo só se autorizado | Revisão humana antes de S1-00 congelar; nenhuma UI executável nesta etapa | A reestimar |
| S1-10b UI mínima final | P1; S1-00/03/07/08/09 + S1-10a | Operator mínimo; início direto/usuários/convite/auditoria; demonstração apenas homologação; foco/mobile/AA e recuperação | UAT-01–18 aplicáveis, E2E e evidências do IdP; sem console completo | A reestimar |
| S1-11 Evidências/aceite | P0 para concluir; S1-00–09 e S1-10a/b | Automatizar jornada/negativos; regressão; AA manual+automação; revisão independente | UAT-01–19 e T01–T11 aplicáveis; Gate UX humano separado de aceite técnico/arquitetural | A reestimar |

S1-10 mantém sua identidade de tarefa e foi desdobrada em S1-10a (revisão antecipada) e S1-10b (execução final). S1-10a depende deste pacote/revisão, não de S1-00 concluída; S1-00 depende de S1-10a; S1-10b depende do congelamento e dos casos de uso. Não há ciclo de dependência entre revisão e implementação. Prototipação e desenvolvimento têm autorizações separadas; este documento não inicia nenhuma delas.

A faixa anterior **14,5–26,5 dias de engenharia** é histórico do planejamento anterior e não estimativa válida do escopo revisado: novos contratos, armazenamento técnico e testes precisam de reestimativa. Nenhuma capacidade, equipe, prazo ou paralelismo confirmado. Rever esforço por tarefa após decisões e experiência operacional; não retirar isolamento/auditoria/AA para caber num calendário.

## Entregas menores e verificáveis

| Entrega | Conteúdo | Gate para demonstrar e prosseguir |
|---|---|---|
| E0 — Revisão e decisões | S1-10a → S1-00 | Revisão UX antes de congelar contratos; ADRs/parâmetros aprovados por humanos e autorização separada de execução |
| E1 — Fundação executável e identidade | S1-01/02/04/05/03, nesta ordem de dependências | Persistência técnica/RLS mínima aprovada, login/logout reais, identidade única e negativos; não declarar SaaS completo |
| E2 — Organização protegida | S1-06/07 sobre S1-04/05 | Contas distintas A/B com vínculo único, Platform segregado, RLS e auditoria; teste de acesso cruzado real |
| E3 — Operação empresarial | S1-08/09 | Convite/papel/revogação, recurso com versão e auditoria; concorrência e replay testados |
| E4 — Jornada e aceite | S1-10b/11 | UI mínima Platform/Tenant + E2E/AA/negativos + regressão + revisão independente; Gate UX separado |

Cada entrega requer autorização do escopo/ambiente correspondente. A divisão não autoriza implementação nem permite concluir sem o gate final. Se a capacidade não comportar tudo, repartir em ciclos adicionais mantendo escopo e evidência explícitos; não cortar isolamento/auditoria para cumprir prazo. Paralelismo somente após contratos estáveis e sem presumir agentes/equipe disponíveis.

## Riscos, lacunas e impactos futuros

| Prioridade | Risco/lacuna | Tratamento / decisão |
|---|---|---|
| P1 | Bootstrap/admin sob RLS não detalhados em SQL | Revisar matriz de grants/policies antes de persistência; nenhuma conexão de bypass genérica |
| P1 | IdP sem responsável/capacidade operacional | D01; considerar gerenciado antes de codificar |
| P1 | Revogação não cancela operação iniciada | D06 e testes dos dois ordenamentos; comunicar semântica |
| P1 | Gates Foundation pendentes e rastreabilidade Repository 2.x incompleta | D10; preservar código/ADRs existentes, não conceder aprovação retroativa |
| P1 | Retenção/privacidade e chaves de idempotência indefinidas | D08/contratos: definir antes de dados reais e comandos correspondentes |
| P2 | Convite somente para usuário conhecido exige passo prévio | D03 e UX; email/onboarding externo fora desta entrega |
| P2 | Sessão, polling/cache ou pooling podem reter autorização | Sem cache inicial; contexto por transação e testes reais |
| P2 | RLS não impede DBA/comprometimento completo do servidor | Menor privilégio, parametrização, operação e recuperação; não prometer proteção absoluta |
| P2 | Documentos globais vazios, workflow com referências ausentes e DoD/mutação divergentes | Registrar e revisar governança antes de executar; esta consolidação não reescreve regras antigas |
| P2 | Estimativas anteriores sem capacidade/benchmark | Faixas hipotéticas; MaxOffset sem alegação de desempenho; aprovar metas operacionais |
| P2 | Ownership CRM/ERP especializado ainda indefinido | Contrato por integração, sem cadastro/banco universal |

GIV: nenhuma migração nesta sprint; preparar IDs internos estáveis e protocolos, preservando login/operação atuais até plano específico. ERP Imobiliário: novos módulos aderem ao Core quando autorizado; lead/CRM/imóvel continuam domínios explícitos, sem obrigar CRM autônomo a compartilhar entidades. Integrações futuras opcionais usam versionamento, idempotência/reconciliação, auditoria e degradação sem joins interbancos.

## Revisão e autorização

Solicitar revisão do pacote documental e decisões D01–D11. Depois de aprovadas, pedir autorização explícita para desenvolvimento e ações operacionais D12. Não executar migrações, deploy, commits ou push por aprovação implícita. A revisão documental não é conclusão da Sprint 1 nem prova de funcionalidades implementadas.

Critérios de revisão documental G01–G08 e cobertura RF/UAT em [rastreabilidade](SPRINT_1_RASTREABILIDADE_UX.md). O [fechamento UX](SPRINT_1_FECHAMENTO_UX.md) registra situação dos 16 achados e riscos residuais. Conclusão desta Etapa 1 somente prepara revisão/autorização de prototipação; não aprova ADRs, Gate UX, desenvolvimento ou operação.

## Escopo e dependências futuras da conta empresarial única

Sprint 1: identidade OIDC, empresa vinculada, constraints de conta, resolução direta, usuários/RBAC, convites, isolamento, auditoria e demonstração somente avaliação/homologação. S1-03 valida política IdP para conta única/segunda empresa; S1-05 testa UNIQUE/FKs inclusive revogado e convite A/B concorrente; S1-07 não implementa listar/selecionar empresas; S1-08 vincula conta ao aceitar, sem transferência; S1-10a/b revisam P02/P03 sem seletor e P07 sem confundir licença; S1-11 prova RF/UAT atualizados. Todas as estimativas continuam a reestimar.

Futuro: assinatura independente por empresa; produtos/módulos individuais ou pacotes; catálogo, cobrança, habilitação/provisionamento comercial, licenças/assentos, templates/configuração por atividade e IA. Contratos conceituais mínimos constam em M05, sem implementar tabelas/endpoints. Consolidação/grupo econômico exigem estudo próprio e não podem entrar como conveniência da UI. Reutilizar a mesma identidade técnica entre empresas é incompatível com a regra permanente aprovada, inclusive após revogação. Novo emprego em outra empresa exige novo login/identidade e convite. Revisão humana dos seis temas em M08 não é aprovação de parâmetros.

## Consolidação final de identidade

A regra permanente de produto está aprovada; ADRs 0007–0012 e mecanismos continuam Propostos. O backlog de identidade, convite, bootstrap, revogação e sessão deve cobrir [I01–I12 e decisões pendentes](IDENTIDADE_CENARIOS_E_DECISOES.md), incluindo unicidade histórica sem filtro Active, recuperação sem reativação e limites de propagação para produtos independentes. A proposta C09 de 200 com estado NotLinked substitui a hipótese anterior de 409 somente no planejamento e depende de revisão humana. Nenhum parâmetro de sessão ou fornecedor foi aprovado.

# Sprint 1 — rastreabilidade funcional, estados e aceite UX

## Status e escopo

**Proposto — critérios não executados, Gate UX não aprovado.**

Referências: [contratos/fluxos C01–C10](SPRINT_1_CONTRATOS_FLUXOS.md), [planejamento](SPRINT_1_PLANEJAMENTO.md) e [registro UX-01–UX-16](SPRINT_1_FECHAMENTO_UX.md). Documentação é o único artefato criado nesta etapa. Não há tela, protótipo, endpoint ou teste de navegador implementado por estes documentos.

Visão estratégica híbrida ERP/CRM e ADRs 0001–0006 mantêm seus estados anteriores. Requisitos RF abaixo, rotas, políticas e ADRs 0007–0012 são propostas pendentes. Implementação de Foundation pré-existente não implementa esses requisitos. A coluna de aceite indica cenário futuro, não resultado aprovado.

Revisão vigente: [conta empresarial única M01–M10](CONTA_EMPRESARIAL_UNICA.md). Diretriz aprovada: login direto, sem seletor/troca, uma empresa por conta. Identidade técnica e conta empresarial são diferentes. RF/UAT mantêm IDs para rastreabilidade, mas substituem as hipóteses multiempresa antigas; nenhuma implementação ou teste executável foi alterado.

## Matriz requisito → perfil → jornada → tela/estado → caso de uso/API → permissão → teste

Prefixo API `/api/v1`. Razor chama os mesmos casos de uso diretamente, conforme C01; as operações web não precisam de loop HTTP. Os identificadores RF e UAT são locais à documentação, não código de teste criado.

| Requisito | Perfil / jornada | Tela e estados | Caso de uso / contrato proposto | Autorização | Aceite |
|---|---|---|---|---|---|
| RF-01 Autenticar e resolver | Todos | Login → empresa vinculada ou preparação/negado | OIDC; GET /me/context | Identidade + conta única/empresa/membership ativos | UAT-01/02/07 |
| RF-02 Preparar identificação | Identidade sem empresa | Preparar meu convite: referência temporária | GET /me; POST /me/recipient-references | Própria identidade, sem dados empresariais | UAT-03/16 |
| RF-03 Criar ambiente empresarial | PlatformOperator | Conferir proprietário sem vínculo → criar → resultado | Preview owner; CreateOrganizationAndOwnerAccount | Platform/MFA; vínculo único; sem acesso ao tenant | UAT-03/10/17 |
| RF-04 Início da empresa | Owner/Admin/Member | P02 início direto, empresa/papel, carregamento/falha | GET /me/context | Vínculo permanente único; 200 NotLinked proposto só para identidade nunca vinculada; revogada não está livre | UAT-04/07/15 |
| RF-05 Resolver sem seleção | Owner/Admin/Member | P03 cabeçalho informativo e navegação direta | ResolveEnterpriseContext | Servidor deriva organização; orgId divergente nega | UAT-07/14 |
| RF-06 Abas e restauração | Owner/Admin/Member | Mesmo vínculo nas abas; revalidar relogin/histórico | GET /me/context e revalidação nos comandos | Conta atual; resposta antiga descartada | UAT-07/12/14 |
| RF-07 Consultar recurso | Owner/Admin/Member | Lista/item: vazio, carregando, indisponível, 404 | GET demo-records coleção/item | Mesmo tenant; backend decide | UAT-07/15/18 |
| RF-08 Criar/alterar recurso | Owner/Admin | Formulário: revisão, erro, gravando, conflito, resultado incerto | POST/PATCH demo-records; C05/C06 | Mesmo tenant; versão individual/idempotência; sem Member | UAT-08/09/10 |
| RF-09 Convidar para a empresa | Owner/Admin | P05 identidade sem vínculo → Member nesta empresa | PreviewRecipient; CreateInvitation | Emissor ativo; não anexar conta já ligada a A/B | UAT-03/04/06/16 |
| RF-10 Retomar convite | Destinatário, qualquer papel ou sem membership | Código → login → conta errada/correta → prévia | Web captura/continuation C03; POST /me/invitations/{id}/preview | Código + destinatário, sem enumeração | UAT-04/05/15 |
| RF-11 Aceitar e vincular | Destinatário conhecido sem empresa | P06 prévia → confirmar → início direto da empresa | AcceptInvitation cria Account+Membership+Audit | Código/destinatário/estado/versão; UNIQUE da identidade | UAT-05/06/09 |
| RF-12 Gerir ciclo de convite | Owner/Admin | Lista/item: Pending/Expired/Accepted/Cancelled/Replaced | GET invitations; POST cancel/replace | Mesmo tenant, ator atual; If-Match e chave na substituição | UAT-06/09/10 |
| RF-13 Usuários/papéis | Owner/Admin | P07 usuários desta empresa; licença futura é distinta | Query/PatchMembership por Account | C08; sem troca de empresa ou licença concedida por papel | UAT-08/09/16 |
| RF-14 Perda de acesso | Conta empresarial | P10 remover dados e orientar recuperação/saída | Revalidar conta/membership | Revogação nega; sem escolha de outra empresa | UAT-08/12 |
| RF-15 Consultar auditoria | Owner/Admin | Auditoria: vazia, lista, indisponível, paginação | GET audit-events | Mesmo tenant; sem Member/Platform amplo | UAT-11/14/17 |
| RF-16 Expirar/reautenticar | Todos | Aviso, continuar, expirado, rascunho recuperado ou inacessível | GET /account/session; POST continue/reauthenticate; C07 | Sessão e antiforgery; mesmo usuário/org na recuperação | UAT-02/12/13 |
| RF-17 Sair | Todos | Saindo, local concluído, remoto incerto, outras abas | POST /account/logout; OIDC logout | Sessão/antiforgery; revogar store antes do remoto | UAT-02/12/15 |
| RF-18 Falhas e desconhecido | Todos conforme ação | 400/401/403/404/409/412/428/429/500/503/timeout | C06/C09; caso de uso original | Nunca liberar acesso ou repetir sem revalidar | UAT-09/10/15 |
| RF-19 Acessibilidade/mobile | Todos | Processo completo, inclusive IdP | C07; matriz WCAG abaixo | Mesmas permissões em todo viewport/dispositivo | UAT-13/18 |
| RF-20 Independência/regressão | Todos / qualidade | Não depende de UI dos produtos | Contratos e guards existentes; T09/T10 do planejamento | Foundation preservada; sem regra de produto no Core | UAT-19 |

## Cenários UAT propostos e observáveis

Cada cenário deverá registrar ambiente, versão, fixture, ação, resultado esperado/observado e evidência sanitizada. Estado atual de todos: **não executado**. T01–T11 anteriores continuam famílias de teste; UAT detalha cobertura sem substituir regressão técnica.

| ID | Preparação e ações | Resultado mensurável / negativos |
|---|---|---|
| UAT-01 | IdP real, clientes web/API/Platform; login e token inválido/expirado/audience errada/ID token | Login válido funciona; 100% das credenciais inválidas negadas; bearer não redireciona para HTML; cookie isolado não autentica API |
| UAT-02 | Sessão por store real; POST sem/errado antiforgery, logout e replay de cookie revogado | Comando não chamado em falha CSRF; sessão revogada não aceita cópia do cookie; cada alcance de logout informado corretamente |
| UAT-03 | Identidade sem empresa gera ID/referência; emissor e operador conferem; tentar conta já vinculada | Sem diretório/global; não revelar empresa de origem; owner já vinculado não cria outra empresa; nenhum vínculo por e-mail |
| UAT-04 | Ana_A Owner A; Bruno sem conta prepara identificação e aceita convite A | Início direto em A após vínculo; sem lista/seletor; papel Member; sem acesso B |
| UAT-05 | Abrir URL sem código; capturar por POST; iniciar OIDC, conta errada, retomar conta correta e visualizar | GET/callback não aceitam convite; conta errada não vê condições; state/return/logs não contêm código; TTL/replay de continuation bloqueados |
| UAT-06 | Pending/Accepted/Expired/Cancelled/Replaced; aceitar convites A/B simultaneamente para mesma identidade | Apenas uma conta/membership empresarial confirma; segunda empresa não recebe vínculo; código antigo inválido; revogado não reativa |
| UAT-07 | Ana_A em A e Bruno_B em B com identidades distintas; forjar rota/payload/header/claims e pooling | Conta A lê/escreve só A; zero dados/contagens B; GET /me/context retorna um vínculo; nunca vinculada retorna estado NotLinked proposto; revogada nega; múltiplo é falha de integridade e não escolhe empresa; backend/RLS continuam |
| UAT-08 | Bruno Admin A; Ana revoga/rebaixa com duas conexões/barreiras, antes/depois do lock de comando | Se revogação confirma primeiro, comando nega; se comando conclui primeiro, resultado é informado sem promessa de rollback retroativo; nova requisição após revogação nega |
| UAT-09 | GET item/lista retorna versão; dois PATCHs, If-Match ausente/*/malformado; convite expira com versão inalterada | Apenas uma gravação; segunda 412, ausente 428, wildcard/malformado 400; expiração não autoriza aceite; UI exige revisão e não sobrescreve |
| UAT-10 | Perder resposta inicial de criar/substituir convite e criar org/recurso; repetir chave, trocar payload e revogar emissor | Um efeito; replay de convite não retorna código; substituição explícita recupera caminho sem reconstruir hash; usuário revogado não recebe replay/dados |
| UAT-11 | Induzir falha em INSERT de auditoria com PostgreSQL/papéis reais; consultar A como Member e B como Admin A | Mutação não confirma sem evento; rollback conjunto; zero segredo em eventos; nenhuma leitura/exclusão de histórico fora da permissão |
| UAT-12 | Duas abas da mesma sessão A, logout/relogin com conta B, histórico e respostas de A atrasadas | Mesma empresa por sessão; revalidar antes de renderizar/salvar; B não recebe rascunho/resposta A; sem seletor ou acesso automático |
| UAT-13 | Relógio controlado + browser + IdP real; aviso, ao menos dez extensões de inatividade, limite absoluto, reauth e rascunho | Tempo/ação/feedback conforme política aprovada; outra conta não restaura rascunho; testes WCAG aplicáveis manuais+automatizados; nenhum sucesso automático após reauth |
| UAT-14 | Login/resolução única, GET contexto, reload/poll; tentar antigas operações de seleção | Nenhum comando/evento de seleção no MVP; GET não altera vínculo; login/vínculo têm auditoria própria e resolução incoerente nega |
| UAT-15 | IdP/DB/store indisponíveis, rate limit, timeout e erro de auditoria conhecido/desconhecido | Estado vazio não mascara erro; timeout indica desconhecido; 429 respeita Retry-After; store indisponível não autoriza operação; logout remoto não é alegado como concluído |
| UAT-16 | Matriz C08 completa; Owner/Admin/Member, próprio usuário, outro Member/Admin/Owner e estados Active/Revoked | Toda combinação permitida/negada coberta; zero autoelevação, remoção de Owner ou reativação implícita; UX e backend consistentes |
| UAT-17 | Operator cria A com Ana_A e B com identidade distinta Bruno_B; tenta reutilizar Ana_A como Owner B | Criação atômica ou rollback sem órfão; unicidade por identidade; operador sem membership não acessa tenant e Tenant não acessa Platform |
| UAT-18 | Navegar por teclado/leitor, 320 CSS px, zoom, falha de rede e lista longa paginada | Sem perda de contexto/função, foco restaurado, rótulos/erros/status acessíveis e critérios WCAG aplicáveis atendidos; nenhuma ação só por hover |
| UAT-19 | Suíte existente + integração/PostgreSQL e contracts; comparar assinaturas/dependências | Foundation e ADRs aceitos preservados; sem relaxar guards; M1–M5/MaxOffset e sem fallback se Repository 2.x for consumido |

## Matriz de acessibilidade proposta — WCAG 2.2 AA

Meta solicitada nesta etapa: WCAG 2.2 AA em páginas e processos completos. Não constitui certificação nem aprovação automática. Detalhar aplicabilidade de cada critério A/AA, inclusive IdP; tabela abaixo cobre pontos prioritários, não substitui a norma completa.

| Critério | Aplicação verificável | Método / teste |
|---|---|---|
| 1.3.1 / 1.3.2 | Rótulos, cabeçalhos, tabelas e ordem de leitura semânticos | Leitor de tela + inspeção; UAT-18 |
| 1.4.1 / 1.4.3 / 1.4.11 | Não depender só de cor; contraste textual 4,5:1, grande 3:1 e controles aplicáveis 3:1 | Medição de cores + revisão dos estados |
| 1.4.4 / 1.4.10 / 1.4.12 | Texto 200%, reflow 320 CSS px e espaçamento sem perda funcional, ressalvadas exceções normativas | Browser em desktop/mobile; UAT-18 |
| 2.1.1 / 2.1.2 / 2.4.3 | Todas as ações por teclado; sem armadilha; ordem coerente | Percurso manual dos quatro perfis |
| 2.4.7 / 2.4.11 | Foco visível e não inteiramente encoberto por conteúdo criado | Testar avisos, navegação e overlays |
| 2.5.3 / 2.5.8 | Nome acessível corresponde ao rótulo; alvos ≥24×24 CSS px ou exceções válidas | Medição e inspeção, inclusive mobile |
| 2.2.1 | Limites avaliados individualmente: sessão/inatividade/IdP/continuation; C07 e alternativas/retomada | UAT-13, evidência de tempo e revisão normativa |
| 3.2.1 / 3.2.2 / 3.2.3 | Foco/entrada não causam navegação inesperada; menu consistente | Percurso da navegação/formulários sem seletor |
| 3.3.1 / 3.3.2 / 3.3.3 / 3.3.7 | Erros identificados, instruções, correção e não redigitar dados sem necessidade | Formulários inválidos, conflito e reauth |
| 3.3.8 | Login/MFA com mecanismos acessíveis; permitir gerenciador/colagem apropriada | Validar processo do IdP, não só página local |
| 4.1.2 / 4.1.3 | Nome/papel/valor e anúncios de carregamento, erro e resultado | Leitor de tela + automação |

Tempo de reautenticação sem perda também é objetivo de produto de C07; não apresentar critérios AAA como obrigação AA. Automação sozinha não comprova AA; incluir leitor de tela, teclado e teste manual do processo real. Para prototipação não executável, avaliar sequência/texto/foco projetado; contraste/semântica/comportamento runtime só serão comprovados no artefato correspondente.

Fontes: [norma WCAG 2.2](https://www.w3.org/TR/WCAG22/), [tempo](https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html), [autenticação](https://www.w3.org/WAI/WCAG22/Understanding/accessible-authentication-minimum.html). Consultar versão normativa antes de afirmar conformidade; nenhum teste foi executado nesta revisão.

## Proposta de Gate UX/Documentação

- G01: UX-01–UX-08 possuem contratos/estados/negativas e testes definidos; divergências entre resumos e detalhamento resolvidas.
- G02: UX-09–UX-16 têm tratamento explícito verificável, sem encerramento genérico ou aprovação implícita.
- G03: RF-01–RF-20 revisados para conta única cobrem quatro perfis, A/B, acesso perdido, expiração, concorrência, convite e falha; nenhuma ação de tela sem caso de uso/política.
- G04: matriz C08 e catálogo C09 mapeiam toda ação permitida/negada e feedback de recuperação.
- G05: reviewer confirma escopo e alternativas pendentes; design e protótipos futuros são identificados como tais.
- G06: critérios WCAG são rastreados, com distinção entre evidência documental, protótipo e runtime; conformidade não é declarada antecipadamente.
- G07: backlog antecipa revisão UX antes de congelar contratos e exclui capacidade/prazos não confirmados.
- G08: revisão humana separa aceite documental, aprovação de ADRs, autorização de prototipação e de desenvolvimento/operação.

Concluir a documentação permite pedir revisão para prototipação, não aprova este Gate. Gate documental pode registrar critérios técnicos ainda pendentes; iniciar prototipação exige autorização e explicitar quais hipóteses serão exploradas, sem apresentá-las como decisões aprovadas.

## Invariantes adicionais desta revisão

UAT-06/07/12/17 devem provar UNIQUE da identidade inclusive revogada, FK de Membership/Account/Organization, resolução sem seletor e reautenticação sem alteração do vínculo. Mesma pessoa em outra empresa usa identidade/conta independentes somente se o IdP/política permitirem; não unir por e-mail. Casos futuros de assinatura/licença/template usam OrganizationId e não são testes executáveis ou escopo comercial desta sprint. RF-20 inclui preservar GIV/ERP/CRM independentes e P12 somente homologação.

## Consolidação final — cobertura obrigatória

A matriz [I01–I12](IDENTIDADE_CENARIOS_E_DECISOES.md) complementa RF-01–RF-20 e UAT-01–UAT-19; não representa testes executados. Inclui identidade revogada impedida de ingressar em B, novo login/identidade para novo emprego, mesmo e-mail condicionado ao IdP, recuperação sem reativação, concorrência entre convites e indisponibilidade do IdP. Aplicar as condições a emissão, preview e aceite; não confundir sessão de produto independente com revogação já efetivada no Core. A proposta de resultado 200 NotLinked em C09 aguarda aprovação técnica; ausência de vínculo histórico é distinta de revogação e de erro de integridade.

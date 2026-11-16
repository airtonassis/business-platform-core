# Sprint 1 — contratos e fluxos para fechamento UX

## Recorte E2-05 autorizado — 04/11/2026

Transporte aprovado Razor→Application. UI fortemente tipada usa os mesmos casos
de uso dos contratos HTTP, sem cliente HTTP interno, BFF ou acesso SQL em páginas.
/api/v1 permanece bearer; /app e /account usam somente cookie web explícito.
Extensão P1-B autorizada: POST HTTPS `/account/backchannel-logout` é endpoint de
protocolo servidor→servidor, autenticado exclusivamente por logout token OIDC
assinado/validado, sem cookie/Bearer ou antiforgery de formulário. Não altera o
antiforgery do logout/continuação Razor. Contrato detalhado e limites na seção 7.3
de [prontidão pós-E2](PRONTIDAO_OPERACIONAL_POS_E2.md).
O login não permite escolher organização. Contexto vem do vínculo único corrente.

WebAuthentication:Enabled habilita o adapter local somente em Development;
Authority, ClientId e ClientSecret (somente se o cliente exigir) vêm da configuração
externa. OIDC e aplicativo web exigem HTTPS, incluindo callbacks e cookies Secure.
O provedor descartável usa certificado efêmero e CertificateAuthorityBase64 com
validação de CA e hostname reutilizada; não altera trust do Windows nem aceita
certificados indiscriminadamente. Não desabilitar assinatura, issuer, audience,
state, nonce ou PKCE. Não versionar chaves,
senhas ou tokens. Configurar no provedor os callbacks /account/oidc-callback e
/account/oidc-signed-out com a origem HTTPS exata do aplicativo.

Sessão local E2-05: store em memória; P1-A/P1-B acrescentam perfil PostgreSQL
técnico explícito e Data Protection persistente, conforme contrato de prontidão.
Ambos preservam revogação no servidor, expiração absoluta 24 h e
inatividade 30 min. Cookie opaco Secure/HttpOnly/Lax; cookies de nonce/correlação
do middleware mantêm Secure. Tokens ficam no ticket servidor para logout remoto,
nunca em localStorage/sessionStorage ou no cookie de autenticação. GET
/account/session consulta expiração/fingerprint não autorizativo e revalida contexto
sem renovar. POST /account/session/continue exige antiforgery, estende inatividade
até o limite absoluto. POST /account/logout revoga antes do logout remoto; GET
não efetua logout. Alteração de identidade invalida sessão antiga. Abas/histórico
revalidam; fingerprint divergente limpa o conteúdo antes de recarregar. Sem drafts
persistidos nesta etapa e sem alegação automática de conformidade WCAG.

Jornadas /app: DemoRecord (list/item/create/update), Membership (list/item/comandos
C08), Invitation (list/item/issue/cancel/replace/preview/accept) e referência própria
C02. Formulários mutáveis têm antiforgery; título/versão/chave permanecem vinculados
à operação, mas organização e papel são sempre resolvidos no servidor. 400/401/
403/404/409/412/428 possuem mensagens sanitizadas; nada sobrescreve conflito ou
repete mutação automaticamente. Referências e códigos são enviados apenas no body,
mostrados uma vez quando emitidos e não reapresentados após erro/preview.

Novo GET /api/v1/organizations/{orgId}/memberships: Owner/Admin ativo; 25 itens por
padrão, limit 1–100, offset 0–10000; ordenação membershipId crescente, items/hasMore.
Item expõe membershipId/accountId/organizationId/role/status/resourceVersion forte;
sem issuer/subject, nomes, e-mail ou SELECT geral. Member/Platform negados; escopo
divergente 404, papel proibido 403, paginação inválida 400. Porta SQL incremental
read-only, sem novos grants em tabelas e sem alterar comandos/Owner existentes.

Validação reproduzível local: scripts/validate_web_oidc_local.py --execute-local
--output TestResults/<nova-rodada> cria Keycloak sintético somente em loopback,
executa protocolo Code/PKCE/PAR contra Razor TestServer HTTPS e remove seu container
e import temporário. scripts/validate_tenant_audit_local.py --execute-local
--membership-list-only --output TestResults/<nova-rodada> valida a porta/readiness
e o adapter em PostgreSQL 17 descartável. Nenhum script acessa VPS ou produção.
O store local não é infraestrutura de sessão de produção; habilitação fora de
Development falha fechada. Gate E2 permanece não aprovado.

## Recorte E2-04 autorizado ? 04/11/2026

DemoRecord ? o recurso de demonstra??o Sandbox previsto, sem dom?nio comercial
novo. Esta aprova??o local abrange os novos objetos e suas portas, n?o grants
adicionais sobre tabelas existentes, opera??o externa ou aprova??o do gate E2.

- POST `/api/v1/organizations/{orgId}/demo-records`: Owner/Admin ativos,
  `{ "title": "..." }`, Idempotency-Key UUID can?nico obrigat?rio. 201 com Location
  e ETag na primeira execu??o; 200 com representa??o original em replay autorizado.
  Chave por identidade/org/a??o, conte?do can?nico SHA-256, 48 h; payload divergente
  ou recibo vencido 409, sem reexecu??o. Expurgo n?o implementado.
- GET cole??o: Owner/Admin/Member ativos, `limit` padr?o 25, 1?100, `offset` padr?o
  zero, 0?10000. Retorna `{ items, hasMore }`, ID crescente como ordem total fixa;
  sem sort SQL arbitr?rio, contagem global ou ETag de cole??o.
- GET `/{id}`: pap?is ativos acima; item do mesmo Tenant com ETag individual.
- PATCH `/{id}`: Owner/Admin, somente t?tulo e If-Match concreto; ausente 428,
  malformado/weak/lista/wildcard/outro recurso 400, vers?o obsoleta 412 ap?s
  autoriza??o. Sem sobrescrita autom?tica; organiza??o/ID imut?veis.

Representa??o p?blica: `id`, `organizationId`, `title`, `resourceVersion`. ETag e
resourceVersion usam base64url can?nica sem padding de
`d1|organizationId:D|id:D|version decimal`, entre aspas; contador persistido, n?o xmin.
T?tulo obrigat?rio, trim no servidor, 1?200 escalares Unicode. Campos JSON extras
(incluindo organization_id, organizationId ou version) s?o rejeitados com 400.

401 sem autentica??o; 403 a??o proibida no Tenant acess?vel; 404 organiza??o/item
inacess?vel, inclusive ap?s revoga??o; 400 entrada/pagina??o inv?lida; 503 depend?ncia
ou resultado incerto, sem afirmar falha definitiva de commit. Erros s?o sanitizados.
A Application deriva issuer/subject do ator autenticado e resolve v?nculo permanente,
Account, Organization e Membership correntes; rota/claims Tenant n?o autorizam.

Portas SQL p?blicas separadas: read_demo_records, create_demo_record e
update_demo_record. Muta??es reautorizam ap?s locks e gravam DemoRecord + evento
sandbox.demo_events + recibo (somente create) no mesmo commit. RLS/FORCE e ACL m?nima
s?o barreiras adicionais; GUC n?o ? autentica??o. Auditoria transacional dedicada
n?o amplia o contrato/whitelist ou os privil?gios do leitor HMAC E2-02A.

## Recorte E2-03 autorizado — 04/11/2026

Autorização expressa para implementação e testes locais do ciclo de convites API/
Application/PostgreSQL, preservando E2-01/E2-02/E2-02A e migrations 001–007.
O responsável aprovou **convite 24 h e janela de recibos 48 h**, mantendo produção
e expurgo fora desta execução, e confirmou **preservar estados e negativas existentes**.
Não existe novo estado Rejected/Declined: abandonar a jornada não altera o convite.
Esses parâmetros deixam de ser pendência neste recorte; não aprovam o documento
integral, UI/continuation/sessão web, envio de e-mail, operação ou gate E2.

C04/C06 neste recorte: convite somente Member; Owner/Admin do contexto atual emite,
consulta, cancela e substitui. Destinatário conhecido com referência C02 válida;
aceite exige identidade autenticada correspondente e código de 256 bits, com hash
persistido. URL `/app/invitations/{id}` não contém código e é endereço canônico para
a futura UI; esta entrega executa o ciclo pelos endpoints bearer, sem implementar
a página web. Preview/GET não alteram estado nem renovam prazos. Validade usa relógio
do PostgreSQL, inclusive após espera por locks. Cancelled/Replaced/Expired não são
aceitáveis. Replay de Accepted recupera o mesmo vínculo somente se ainda ativo.
Cancel repetido não cria efeitos e pode retornar conflito; não há reativação.

Idempotency-Key é UUID D não vazio, consistente com as operações existentes.
Emissão/substituição retornam código somente na primeira resposta 201; replay 200
contém metadados e secretUnavailable, sem recuperar código. A janela de 48 h não
autoriza apagamento automático; chave expirada não reexecuta a criação. Recibos
guardam IDs e fingerprint de parâmetros técnicos/hashes, sem código recuperável.
ETag de convite é forte e tipada: base64url sem padding de
`i1|organizationId:D|invitationId:D|version decimal`, entre aspas. If-Match rejeita
weak/wildcard/lista/outro item; autenticação/autorização precedem 428/400/412.
GET de lista aceita limit 1–100 (padrão 25), cursor canônico de timestamp/UUID e
state opcional Pending/Accepted/Expired/Cancelled/Replaced, com ordenação decrescente.

POSTs de convite reutilizam o limitador existente (10/identidade/min e 30/IP/min),
com Retry-After, sem fixar nova política operacional de convites por hora. Falhas
após confirmação de conflito SQL podem reler estado autorizado uma vez para
classificar Expired/Cancelled/Replaced/AlreadyAccepted; nunca repetem a mutação.
Falha de transporte em escrita é resultado desconhecido, sem alegar rollback.

## Status e relação com decisões existentes

**Proposto — revisão humana obrigatória; nenhuma implementação autorizada.**

Revisão vigente: [conta empresarial única M01–M10](CONTA_EMPRESARIAL_UNICA.md), diretriz de produto aprovada; desenho técnico proposto. Substitui a experiência multiempresa/seletor anterior. Detalhamento de UX-01–UX-16. Complementa [planejamento](SPRINT_1_PLANEJAMENTO.md), ADRs propostos 0007–0012 e [rastreabilidade](SPRINT_1_RASTREABILIDADE_UX.md). As especificações deste documento substituem os resumos anteriores somente nos pontos explicitamente revisados nos ADRs. Não altera ADRs aceitos 0001–0006, contratos da Foundation, código ou testes. Contratos aqui são candidatos, não endpoints implementados.

Consolidação final: a regra de identidade OIDC→no máximo uma EnterpriseUserAccount durante todo seu ciclo de vida foi aprovada pelo gestor, inclusive após revogação. Novo ingresso em B exige novo login/identidade e novo convite; e-mail não vincula. O mecanismo técnico e este contrato permanecem Propostos. [Cenários I01–I12 e decisões](IDENTIDADE_CENARIOS_E_DECISOES.md).

## C01 — Transporte, autenticação e autorização única (UX-01)

| Alternativa | Benefício | Custo/risco |
|---|---|---|
| Razor Pages → Application no mesmo host | Reutiliza casos de uso sem loop HTTP e sem token no navegador | Exige mapear erros HTTP e web de forma consistente |
| Adapter web/BFF → API por HTTP | Isola frontend e API; útil se forem processos separados | Tokens, rede, CSRF, disponibilidade e rastreamento adicionais |
| Browser → API com cookie e bearer misturados | Aparente simplicidade | Esquemas ambíguos e ampliação da superfície CSRF; não recomendado |

**Recomendação:** Razor → Application no mesmo monólito modular. Minimal APIs bearer e handlers Razor são adapters distintos dos mesmos casos de uso. Não criar BFF separado nem chamar a própria API por HTTP. Uma interface não precisa de endpoint REST próprio para executar um caso de uso já autorizado.

- `/app/*`, `/platform/*` e `/account/*`: cookie web explícito, com scheme/cliente separado para Platform. Após login, resolver conta empresarial e organização pelo vínculo único no servidor, nunca pelo cliente. `/api/v1/*`: somente bearer válido para a audience da rota. Cookie isolado em API não autentica; access token não substitui sessão/antiforgery web.
- Cookies/sessões Tenant e Platform têm nomes e escopos separados; login empresarial não satisfaz scheme administrativo. Rotas de sessão/logout derivam o escopo do adapter e sessão validada, não de um papel/escopo enviado livremente pelo cliente. Só encerrar outra sessão exige operação própria, fora desta sprint.
- Web: challenge OIDC padrão, Authorization Code/PKCE, correlação/nonce e redirect URI permitida. Callback é controlado pelo middleware; seu protocolo não é formulário empresarial. Credenciais/MFA/recuperação permanecem no IdP.
- Formulários POST/PATCH equivalentes, sessão, logout e captura/aceitação de convite exigem antiforgery, inclusive captura anônima. SameSite é defesa complementar, não substituto de antiforgery. Falha resulta em 400 sanitizado, sem chamada ao comando.
- Policies na borda restringem o esquema; autorização empresarial única em Application revalida usuário, organização, membership e alvo. Claims/DTOs do browser não criam contexto confiável. Razor e API testam a mesma matriz C08, inclusive acesso direto ao caso de uso por adapter não HTTP.
- Application retorna Result/Error existentes para falhas esperadas. Api/Razor traduzem para status ou estado de formulário; exceções não classificadas mantêm a causa e resposta sanitizada. Não retornar HTML/redirect em falha bearer.
- Sucesso de POST web usa Post/Redirect/Get quando o resultado não contém segredo de uso único. Resposta com código de convite é no-store e exibida uma única vez; navegação posterior recupera metadados, não o código.
- Mutação e replay exigem autorização atual; não confiar na decisão que exibiu o botão ou na decisão anterior à transação. Falha do IdP/DB não habilita acesso por padrão.

Dependências: ADR-0007, ADR-0009, ADR-0010, ADR-0012; aprovação do desenho e da política de sessão. Testes: UAT-01/02/07/08/15.

## C02 — Identificação e conferência do destinatário (UX-04)

`GET /api/v1/me` (ou caso de uso web GetMyIdentity): 200 `{userId, displayName, enterpriseAccountStatus}` e, se necessário, metadados de validade das próprias referências, nunca seus segredos. Não retornar issuer/subject, token ou lista de usuários. `recipientReference` é capacidade temporária de identificação, não autenticação ou permissão, gerada e mostrada uma vez por operação explícita abaixo; hash não permite recuperá-la no GET.

Proposta: `POST /api/v1/me/recipient-references` (web: formulário antiforgery). Retorna referência opaca aleatória de 256 bits, validade 24 h e dados mínimos do próprio usuário; armazenamento com hash e UserId/validade. Não é reutilizável para autenticar. Usuário copia seu UserId e a referência para o emissor por canal controlado. Emissão limitada por identidade; nenhuma busca por e-mail/nome. Gerar referência de preparação de convite exige elegibilidade: identidade nunca vinculada. Identidade com conta ativa/revogada/bloqueada/histórica não pode gerar capacidade para novo vínculo. GET /me pode apresentar identificação própria sem tornar a identidade elegível.

`POST /api/v1/organizations/{orgId}/invitation-recipient-preview` recebe `{userId, recipientReference}`. Owner/Admin ativo consulta somente esse par; PlatformOperator usa `POST /api/v1/platform/owner-preview` pelo mesmo mecanismo. Retorno 200 somente se destinatário nunca foi vinculado: UserId e displayName fornecido/verificado no contexto da identidade (não alegar identidade civil verificada), sem e-mail. Referência válida pode servir a mais de um emissor autorizado até expirar; revogação manual de referências e diretório global ficam adiados. Referência expirada/incorreta/identidade inelegível (inclusive revogada) gera mesmo 404/`Recipient.Unavailable`, sem distinguir existência, com rate limit e resposta mínima.

A criação de convite/organização reapresenta par e confere validade no servidor; preview não é autorização futura. Revisão mostra organização, nome de exibição e UserId completo acessível; o operador confirma por canal controlado quando necessário. Referências não aparecem em logs/URLs. Se expirar, destinatário emite outra. Privacidade: há identificação voluntária limitada, não enumeração global. Risco: compartilhamento da referência permite descoberta do nome mínimo por emissor autorizado; requer aprovação explícita.

Parâmetros iniciais para revisão, não aprovados: emissão de referência até 5/usuário/hora; preview de identificação/convite e aceite até 10 tentativas/usuário/minuto e 30/IP/minuto; captura anônima até 30/IP/minuto; emitir/substituir convite até 10/ator/organização/hora. Resposta 429 com Retry-After; não bloquear a vítima apenas por invitationId/UserId alegado de outro usuário. NAT, acessibilidade e carga devem ser avaliados antes de fixar valores. Testar limites configuráveis deterministicamente, sem benchmark fictício.

Empresa do convite é a do emissor validada no servidor. Destinatário deve estar sem conta empresarial; vínculo existente, inclusive revogado, impede criação em outra empresa. Conferência não revela a empresa de origem ao emissor. Uma identidade tem no máximo uma conta empresarial durante toda sua vida, não só enquanto Active. Uma conta já ativa na mesma empresa não recebe outro membership; desligamento não libera identidade para novo convite. "Preparar meu convite" é linguagem proposta; IDs/referências ficam em detalhes sem retirar as provas. Bootstrap de identidade sem empresa é estado de preparação, não ambiente empresarial.

Alternativa: somente UserId e conferência fora do sistema reduz estado técnico, mas não resolve adequadamente confirmação visual de destinatário. Diretório global foi rejeitado por exposição e escopo. Dependências: identidade conhecida e decisões de privacidade/TTL/rate limit. Testes UAT-03/04/16.

## C03 — Recebimento, autenticação e retomada de convite (UX-02)

**Recomendação:** entregar dois elementos: URL `/app/invitations/{invitationId}` sem segredo e código copiável separado. Não embutir código em path/query/fragment ou return URL. Custo aceito como proposta: um passo de colagem, com suporte a teclado, colagem e tecnologia assistiva. Link com segredo no fragmento e JavaScript de extração foi considerado, mas aumenta complexidade e exposição ao browser nesta entrega Razor.

1. Destinatário abre URL sem segredo. GET mostra instrução genérica e campo para colar código, sem organização/destinatário. Não altera convite nem aceita associação.
2. Antes da captura, usuário pode ajustar a janela da retomada de 15 até 150 minutos, sem revelar código ou dados do convite; padrão proposto 15 min. POST web antiforgery captura código e a opção validada. Sem sessão autenticada, cria continuation técnica no servidor com esse TTL; segredo temporariamente criptografado, isolado por fluxo e cookie anônimo seguro/HttpOnly. Cookie contém somente identificador opaco. O banco de Invitation continua guardando apenas hash. Não usar TempData em cookie para transportar segredo.
3. Iniciar OIDC padrão. State é opaco e protegido pelo middleware, associado ao fluxo, sem código/segredo/retorno externo. Callback confirma correlação e transfere a continuation somente à sessão que completou o mesmo fluxo; rotaciona identificadores. Nunca combinar sessão anônima arbitrária com usuário já logado.
4. Com sessão válida, validar código e UserId destinatário no caso de uso. Conta errada: resposta genérica, nenhum detalhe da organização; oferecer trocar conta ou cancelar. Troca usa novo fluxo OIDC correlacionado e rebind explícito, dentro do TTL original; não estende TTL nem compartilha com outras abas.
5. Prévia C04 exibe condições mínimas. Usuário pode cancelar a jornada sem cancelar o convite de negócio, ou confirmar explicitamente.
6. POST de aceite usa segredo mantido no servidor, destino interno fixo, versão vista e antiforgery. Revalida tudo na transação; não aceita no callback ou GET.
7. Aceitação cria EnterpriseUserAccount vinculada à empresa e Membership Member atomicamente, com unicidade de identidade inclusive contra outro convite de B. Exibir resultado e entrar no início da empresa somente após confirmação e nova resolução segura pelo servidor; não mostrar lista/seletor de organizações. Apagar material transitório após aceite, cancelamento, timeout ou erro definitivo. Em expiração da continuation, pedir novamente o código; não reemitir convite automaticamente.

Continuation não é sessão de negócio nem poder de aceite sem identidade. Tratar dados transitórios com criptografia padrão/Data Protection, acesso mínimo, limites e exclusão por expiração; sem segredo em logs de proxy, requests/responses, analytics, referrer ou telemetria. Página sem scripts/recursos de terceiros e política de referrer restritiva. Uma aba não consome continuation de outra. Falha de IdP preserva somente até TTL e oferece tentar login novamente, sem comando automático.

Testes UAT-04/05/06/15: verificar tráfego, logs, URLs, OIDC state/return, replay de continuation, conta errada, duas abas, expiração e cancelamento. A estratégia de armazenamento transitório precisa de aprovação; não prometer recuperação do segredo original do banco.

## C04 — Prévia e contratos de convite (UX-03/06)

Prefixo HTTP `/api/v1`; adapters web executam os mesmos casos de uso. Bearer válido é obrigatório nestas rotas; no web, cookie e antiforgery conforme C01. POST de preview transporta segredo no corpo, sem expô-lo em URL; não modifica convite/membership nem renova validade. GET nunca aceita ou cancela convite.

| Operação proposta | Entrada / autorização | Resultado e privacidade |
|---|---|---|
| POST `/organizations/{orgId}/invitations` | `{recipientUserId,recipientReference}`, Idempotency-Key; Owner/Admin ativo; papel inicial Member fixo | 201: ID, condições, versão, URL sem segredo e código uma vez; resposta no-store |
| GET `/organizations/{orgId}/invitations` e `/{id}` | Owner/Admin ativo; paginação/filtro de estado | Metadados/versões; nenhum hash/código; item fora do tenant 404 |
| POST `/me/invitations/{id}/preview` | `{code}`; destinatário autenticado + código válido | 200: organização nome/ID, papel Member, validade, effectiveStatus e ETag individual; nenhum dado pessoal do emissor |
| POST `/me/invitations/{id}/accept` | `{code}`, If-Match; destinatário autenticado; ativo no Core | 200: accepted + EnterpriseUserAccountId/OrganizationId/MembershipId; revalidar org/convite/estado e não reativar revogado |
| POST `/organizations/{orgId}/invitations/{id}/cancel` | Owner/Admin atual, If-Match; estado Pending efetivo | 200 metadados Cancelled; auditoria atômica |
| POST `/organizations/{orgId}/invitations/{id}/replace` | Owner/Admin atual, If-Match, Idempotency-Key; Pending/Expired | 201 novo convite/código uma vez; anterior Replaced e novo Pending no mesmo commit |

Cancel/replace são comandos explícitos; não há PATCH arbitrário de destinatário/papel. Campo de código não é guardado no browser após captura. Prévia 404/`Invitation.Unavailable` para ID/código inválido ou destinatário errado, indistinguíveis em conteúdo; rate limiting e comportamento de tempo revistos para reduzir enumeração. Depois de provar código+destinatário, negar com 403 Account.Ineligible quando houver vínculo histórico com outra empresa, mesmo revogado; não revelar dados da empresa de origem. Se já foi aceito para a mesma conta, a consulta/replay autorizado segue C06, sem novo vínculo. Somente após prova válida de código+destinatário, revelar Expired/Cancelled/Replaced/Accepted mínimos, sem referências a outros convites/usuários. Backend não informa como encontrar conta destinatária.

Prévia retorna estado atual sem efeito de aceite. Identidade já vinculada a outra empresa não pode aceitar; não transferir a conta, duplicar Membership ou revelar dados da outra empresa. Ao destinatário autenticado, informar que a conta já possui vínculo empresarial; ao emissor, erro mínimo sem identificar a empresa existente. A concorrência de convites de A/B permite uma única vinculação. Entre prévia e confirmação pode haver expiração/cancelamento/revogação; o comando revalida e retorna conflito seguro. Estados conhecidos e autorizados: 409 `Invitation.Expired`, `.Cancelled`, `.Replaced` ou `.AlreadyAccepted`; usuário global bloqueado 403 genérico; infraestrutura 503 sem detalhes; 429 com Retry-After. Nunca inferir validade apenas do relógio do browser.

### Estados e transições

| Estado efetivo | Transições permitidas na Sprint 1 | Recusas |
|---|---|---|
| Pending, dentro da validade | Accept → Accepted; Cancel → Cancelled; Replace → Replaced + novo Pending | Papel/usuário/organização não podem ser editados |
| Expired | Replace → Replaced + novo Pending | Accept/cancel recusados |
| Accepted | Somente consulta autorizada/recuperação do próprio resultado | Nenhum novo membership ou novo código; revogação ocorre no Membership |
| Cancelled | Consulta; nova emissão explícita pode ocorrer se domínio permitir | Não aceitar, substituir ou restaurar esse convite |
| Replaced | Consulta mínima; sucessor somente ao emissor autorizado | Código antigo nunca ativa ou recupera segredo do novo |

`effectiveStatus=Expired` é calculado no servidor quando Pending passou expiresAt; GET/preview não gravam transição. Na próxima mutação sobre a fila org/destinatário, materializar expiração e liberar pendência sob lock, com auditoria, antes de criar/substituir. A versão persistida não muda por leitura. Expiração concorrente é checada também por horário, mesmo com ETag igual.

Lista/item/cancel/replace e expiração materializada pertencem à Sprint 1 proposta. Adiados: e-mail, entrega automatizada, usuário ainda desconhecido, reativação de membership revogado, papéis de convite além de Member, transferência de Owner, expiração por job e gestão global de convites. Membership já ativo na mesma empresa retorna 409 `Membership.AlreadyActive`; revogado retorna 409 `Membership.ReactivationDeferred`, sem reativação implícita.

Dependências: C02/C03/C05/C06, locks de ADR-0009 e política C08. Testes UAT-04/05/06/09/10/16.

## C05 — Versão individual, ETag e conflito (UX-05)

GET de item DemoRecord, Membership e Invitation retorna ETag forte, opaca e entre aspas; listas incluem `resourceVersion` por item, utilizável como If-Match individual, com encoding único especificado no contrato de transporte. Nunca usar ETag da coleção para alterar item. Adicionar GET `/organizations/{orgId}/memberships/{id}` para recarregar alvo autorizado. Contexto pode expor própria versão/papel sem liberar consulta de outros membros.

Versão é contador persistido crescente por agregado (não timestamp de browser), codificado com identidade/recurso para impedir comparação entre itens. Não usar xmin como contrato público nem expor row/SQL internos. PATCHs de DemoRecord/Membership, cancel/replace/accept de Invitation exigem uma versão concreta; não aceitar If-Match `*` nesta sprint. Cabeçalho malformado retorna 400, ausente 428, divergente 412, sempre depois de autenticação/autorização e sem revelar versão de recurso inacessível.

Após 412, manter rascunho não sensível, informar alteração por outro usuário e carregar versão atual. Usuário revisa diferenças e confirma nova gravação; nunca substituir silenciosamente nem atualizar ETag e reenviar automaticamente. Perda de acesso precede recuperação e elimina exposição dos dados atuais. Não há troca de organização. Funcionário desligado de A só ingressa em B por identidade técnica nova e convite novo, nunca recuperação da identidade A. Troca explícita de identidade/login invalida a tela anterior e não transfere rascunho para outra conta/empresa; recuperar exige mesmo usuário e vínculo autorizado.

Mutação revalida e coordena locks com revogação conforme ADR-0009: operação já autorizada com lock pode terminar antes da revogação; a próxima iniciada após o commit é negada. Não prometer desfazer operação concluída. Simular os dois ordenamentos com barreiras/diferentes conexões.

Testes UAT-07/08/09: lista/item/versionamento, conflito, header ausente/malformado, acesso cruzado e revogação.

## C06 — Idempotência, resposta perdida e recuperação (UX-07)

Proposta: Idempotency-Key obrigatória em criar organização, emitir/substituir convite e criar DemoRecord; escopo identidade/conta empresarial + organização/Platform + operação. Retenção inicial proposta 48 h, sujeita à aprovação de privacidade/operação. Cada POST define hash canônico do payload; comandos concorrentes usam unicidade/lock e registro no mesmo commit da mutação/auditoria. Não implementar motor genérico da plataforma.

Autenticar e autorizar novamente o ator/escopo antes de lookup/replay e de retornar resultado. Replay de criação já confirmada não executa nova vinculação nem reaplica a condição de identidade sem conta como se fosse novo bootstrap: a conta criada pela própria operação pode ser retornada como metadado autorizado. Nova chave/operação com identidade já vinculada continua proibida; não usar essa exceção de leitura para criar segundo ambiente. Mesma chave/payload: mesmo ID/resultado não sensível; payload diferente 409 `Idempotency.PayloadMismatch`; em andamento 409 `Operation.InProgress` + Retry-After. Ausência de chave 400. Primeiro commit 201, replay 200 com indicador `replayed`. Não renovar validade ou criar novo efeito. Depois de expirar janela, repetição não é garantida: UI não reenvia silenciosamente; reconcilia com lista/item e exige nova decisão.

**Exceção de apresentação de segredo:** resposta inicial de convite mostra código uma vez; idempotency store nunca guarda plaintext/código recuperável. Replay retorna ID/metadados + `secretUnavailable=true`, não um código reconstruído do hash. Se resposta inicial se perdeu, consultar metadados e oferecer substituir explicitamente. Replace invalida código antigo e cria novo código; se nova resposta se perder, mesma regra se repete. Não cancelar ou substituir automaticamente em retry, logout ou falha de rede.

Accept: primeira confirmação 200; repetição provada por mesmo destinatário/código retorna resultado Accepted existente somente se membership ainda ativo e autorização atual permitir. Não gera novo evento de sucesso/membership. Prévia pode informar Accepted; replay não exige ETag antiga se é consulta idempotente do resultado já concluído, mas nunca executa nova transição. Se conta ou membership foi revogado, retorna 403 genérico, sem dados de tenant e sem reativação. Código inválido/conta errada continuam 404 genérico. Cancel repetido após sucesso pode retornar mesmo estado quando autorizado, sem nova mutação; versão não permite cancelar sucessor.

Resultado desconhecido: mostrar "Não foi possível confirmar o resultado"; desabilitar reenvio cego; oferecer consultar estado por ID ou repetir a mesma chave dentro da janela. Falha de auditoria com rollback confirmado informa que nada foi aplicado; timeout de transporte não permite essa afirmação. Falha de IdP/DB/ratelimit não transforma acesso negado em sucesso. Testes UAT-06/08/09/10/15.

## C07 — Sessão, expiração, logout e múltiplas abas (UX-08/10/11)

**Diretriz funcional aprovada aplicável:** [DCSE-01](IDENTIDADE_CENARIOS_E_DECISOES.md#dcse-01). A Core prepara organização, permissões, produtos licenciados e unidades autorizadas no login; ativa automaticamente uma única unidade autorizada ou restaura a última válida entre várias; permite alternância sem novo login. A inatividade é política central configurável integrada ao Keycloak. Os tempos BASE-03 já aprovados permanecem válidos. Esta diretriz não torna implementados os mecanismos e contratos candidatos descritos nesta seção.

### Alternativas e recomendação pendente

| Alternativa | Vantagens | Riscos / escolha |
|---|---|---|
| Limite absoluto anterior de 8 h sem mecanismo adicional | Janela menor de sessão | Não justifica conformidade de tempo por aviso; revisão necessária |
| 24 h absoluto + inatividade extensível + retomada | Limite absoluto maior que 20 h; possibilita extensão de inatividade e recuperação | Aumenta janela potencial; requer sessão revogável e aprovação de segurança |
| 8 h com exceção essencial fundamentada | Mantém janela curta | Exceção não pode ser presumida; exige justificativa específica e validação humana |

Recomenda-se como hipótese **24 h absoluto; inatividade 30 min Tenant e 15 min Platform; aviso 2 min antes**, com ação simples para continuar pelo menos dez vezes, sem mover o limite absoluto. Backend decide tempo; heartbeat/polling não conta como atividade humana nem renova sozinho. Access token de 5 min e skew de 30 s continuam hipóteses de OIDC, não duração da sessão web. Se a equipe exigir 8 h, revisar o tratamento normativo antes de alegar WCAG; não manter duas durações concorrentes como decisões.

No modelo recomendado, o limite absoluto usa a condição de duração maior que 20 h do critério de tempo; a inatividade usa extensão com aviso e tempo de resposta suficiente. Isso não comprova conformidade do processo: atraso de rede e limites do IdP têm avaliação própria. Continuation usa ajuste anterior à captura de 15 até 150 min conforme C03; permitir também recapturar código/recomeçar sem perda do rascunho. Não alegar exceção essencial sem evidência.

### Sessão e retomada

Sessão web revogável no servidor com ID opaco no cookie, UserId, cliente/escopo, instantes, estado e versão de revogação; implementar via mecanismos padrão de ticket/session store ASP.NET Core, não protocolo próprio. Uma sessão não autoriza membership por cache. Falha do store impede autenticação privilegiada/novas operações protegidas, não cria sessão offline. Renovação de sessão não renova permissão empresarial.

Antes de expirar, aviso acessível informa efeito e permite continuar ou sair. Endpoint web POST `/account/session/continue` exige sessão/antiforgery; GET `/account/session` retorna somente expiração/estado necessário, no-store e sem renovar. Reautenticação POST `/account/reauthenticate` inicia challenge padrão; callback retorna a caminho interno validado e rascunho vinculado ao mesmo usuário/org. Não executar POST empresarial após callback.

Rascunhos de formulários: campos permitidos (título demonstrativo, escolhas não secretas) são mantidos no servidor, criptografados e vinculados a usuário/org/fluxo, TTL proposto 24 h, sem sessão de autorização embutida. Não guardar token, código de convite ou referência do destinatário em rascunho. Reautenticar como outra pessoa não recupera rascunho alheio; acesso perdido invalida o rascunho daquele tenant. Confirmar novamente antes de gravar. Decisão de retenção/criptografia é gate de privacidade.

### Logout e perda de acesso

POST `/account/logout` com antiforgery revoga sessão no servidor e expira cookie antes de tentar logout OIDC. Encerramento local é confirmado mesmo se remoto falhar; informar que outros sistemas/IdP podem continuar conectados, sem alegar logout global. Se servidor de sessão está indisponível, expirar cookie local e informar que a revogação remota não foi confirmada; não prometer invalidar cópia de cookie. Essa falha gera sinal operacional.

Outras abas recebem sinal de encerramento sem dados sensíveis, mas servidor é autoridade. Antes de ação mutável e ao restaurar por histórico/pageshow/retornar ao foco, revalidar sessão/contexto; interromper carregamento antigo e descartar resposta se seu usuário/org/geração diferir da tela atual. Respostas protegidas no-store, sem cache de dados de tenant em service worker/localStorage. Não garantir apagar conteúdo já visto, screenshots ou memória do usuário.

Ao detectar conta/membership revogado: remover dados/ações protegidos, cancelar pedidos pendentes quando possível e oferecer orientação de recuperação autorizada ou saída. Não oferecer outra empresa. 404 genérico não confirma existência de tenant; distinguir sessão expirada (401 → reautenticar) de acesso perdido (conta/vínculo não autorizado → recuperação controlada). Não alegar logout global dos produtos. Abas da mesma sessão pertencem à mesma empresa; troca de identidade exige revalidar todas e descartar respostas/rascunhos anteriores.

Testes UAT-02/07/08/11/12/13/15; validar aviso e extensões, limite absoluto, outra conta, rede, foco, histórico e múltiplas abas. Não alegar acessibilidade somente por ter aviso. Dependências: IdP/store e decisões de segurança/privacidade/acessibilidade ainda pendentes.

## C08 — Matriz de autorização por ator, alvo, estado e ação (UX-16)

**Aplicação de DCSE-01:** operações dependentes da unidade ativa usam o contexto central autenticado e revalidado; consultas entre filiais avaliam autorização para cada alvo sem depender da unidade ativa. O contrato compartilhado, sua versão e a validação local segura continuam dependências técnicas anteriores a uma integração de produto.

Pré-condições globais: identidade ativa, esquema adequado, conta empresarial com vínculo único, organização ativa e membership do ator ativo para rotas Tenant. Licença/assinatura futura não é concedida pelo papel. Platform sem membership não opera dados Tenant. Conta Platform separada e sem poder de impersonação. Tudo não listado é negado; ocultar botão não substitui a mesma regra no caso de uso.

| Ator | Alvo/estado | Ação permitida | Negativas explícitas |
|---|---|---|---|
| PlatformOperator | Referência de proprietário válida | Prévia restrita; criar organização+Owner | Não ler registros/membros/auditoria Tenant por privilégio Platform |
| TenantOwner | Outro Member ativo | Promover a Admin; revogar | Não editar UserId/org ou criar Owner |
| TenantOwner | Outro Admin ativo | Rebaixar a Member; revogar | Não alterar Owner ou si próprio |
| TenantAdmin | Outro Member ativo | Revogar | Não promover/rebaixar, alterar Admin/Owner ou si próprio |
| TenantMember | Qualquer membership | Nenhuma gestão | Não listar outros membros/auditoria nem se elevar |
| Owner/Admin | Destinatário conhecido sem conta empresarial + referência válida | Conferir; convidar como Member | Não convidar como Admin/Owner; sem diretório global |
| Owner/Admin | Convite do próprio tenant Pending/Expired | Listar/consultar; cancel Pending; replace Pending/Expired | Não ler código/hash; não operar convite de outro tenant |
| Destinatário autenticado | Convite provado por código + UserId | Preview; accept Pending; recuperação Accepted se ainda autorizado | Não aceitar conta errada; não reativar membership revogado |
| Member/Admin/Owner | Próprio tenant ativo | Ler contexto/registros | Nenhum acesso cruzado; contexto visual não comprova autorização |
| Admin/Owner | DemoRecord do próprio tenant | Criar/alterar com versão | Não mover org, sobrescrever conflito ou operar após revogação |
| Admin/Owner | Auditoria do próprio tenant | Consultar paginado | Não alterar/excluir; Member/Platform sem direito negados |

Membership revogado não é reativado nesta sprint; Owner é imutável quanto a papel e não revogável por estes endpoints, inclusive se houver mais de um Owner. Admin pode revogar Member, Owner pode revogar Admin/Member, nenhum ator altera si próprio. Leitura individual de Member requer Owner/Admin e mesmo tenant; contexto de Member expõe somente seu próprio vínculo/permissões. Testes UAT-03/07/08/16/17.

## C09 — Operador Platform, estados e auditoria de vínculo (UX-09/12/14)

Proposta de UI Platform mínima: login administrativo/MFA → identificador/referência do proprietário → prévia → formulário nome de organização → revisão explícita → criar com Idempotency-Key → comprovante com IDs. Não incluir lista global, receitas, contratos ou suporte/break-glass. Operator não ganha membership. POST `/platform/organizations` recebe nome e par de referência do proprietário; revalida, cria Organization/EnterpriseUserAccount/Owner/auditoria atomicamente; a identidade do proprietário precisa estar sem vínculo empresarial. Conta vinculada causa rollback completo. Página usa o mesmo caso de uso, não cliente HTTP interno. Erro de identificação é genérico; resposta perdida usa a mesma chave/resultado seguro.

Resolução direta proposta: `GET /api/v1/me/context` retorna 200 `state=Linked` com conta/empresa/papel/permissões se ativos/coerentes; 200 `state=NotLinked`, código `Account.NotLinked`, sem OrganizationId/AccountId/papel empresarial, somente se identidade nunca consumida historicamente. 401 para autenticação inválida, 403 `Access.Unavailable` para acesso empresarial negado (conta/membership/empresa revogados ou bloqueados), 500 sanitizado e alerta para inconsistência histórica/múltiplos vínculos e 503 para dependência conhecida indisponível. Nenhuma falha é convertida em NotLinked. Registro histórico sem conta corrente não é primeiro acesso.

Mudança explícita de proposta: 200 NotLinked substitui a hipótese anterior de 409 Account.NotLinked nesta consulta. Recomenda-se estado esperado de consulta, pois a própria identidade foi resolvida com sucesso, em vez de erro de onboarding; consumidores devem verificar state, não tratar 200 como autorização. Alternativas 409 e 404 e impactos estão em [H01](IDENTIDADE_CENARIOS_E_DECISOES.md); aprovação humana ainda necessária, sem alterar endpoint implementado. Result/DTO próprios da Application usam Foundation existente, sem expandi-la. Consulta GET não cria identidade/conta/vínculo/sessão/seleção. Bootstrap técnico idempotente de identidade na autenticação é operação distinta. Login e alterações reais de vínculo têm auditoria própria; GET/reload/poll apenas consulta/telemetria sanitizada.

Não propor no MVP GET /me/organizations ou POST /me/organization-selections: hipóteses anteriores retiradas, não endpoints implementados. Rotas /organizations/{orgId}/... mantêm escopo explícito, validado contra o único vínculo; divergência nega, nunca troca contexto.

| Situação | API proposta | UI/recuperação |
|---|---|---|
| Payload/antiforgery inválido | 400 + código sanitizado | Erros por campo/geral, foco no resumo; nenhum comando |
| Sessão/token inválido | 401 (API sem HTML) | Reautenticar por ação explícita; manter rascunho seguro |
| Ação proibida em tenant acessível | 403 | Informar permissão insuficiente, não sugerir autoelevação |
| Recurso/tenant inacessível | 404 genérico | Orientar recuperação ou sair, sem confirmar existência de outra empresa |
| Estado inválido / repetição incompatível | 409 + código | Explicar próximo passo permitido, sem repetir mutação |
| Versão obsoleta / ausente | 412 / 428 | Recarregar/revisar antes de reenviar |
| Limite de tentativas | 429 + Retry-After | Espera informada, sem loop automático |
| Falha não classificada | 500 sanitizado | Correlação e orientação; não expor SQL/stack |
| Dependência indisponível conhecida | 503 | Mensagem temporária; preservar edição e não liberar acesso |
| Timeout/conexão perdida | Sem resultado HTTP confiável | Estado desconhecido C06; não anunciar rollback sem prova |
| Lista vazia / carregamento | 200 vazio / estado local separado | Orientar ação permitida; nunca apresentar erro como vazio |

Datas visíveis com timezone claro, auditoria UTC no armazenamento e apresentação local identificada. Confirmações de acesso mostram organização, alvo, papel/efeito e alternativa cancelar; ação relevante não depende de cor/ícone. Testes UAT-01/10/14/15/17.

## C10 — Dependências e aprovações do detalhamento

Regra aprovada e permanente: identidade única→conta única, vínculo imutável inclusive revogado, novo login+convite para B e e-mail sem autoridade. Não escolher fornecedor ou aprovar tempo de sessão. Continuam propostas técnicas: transporte Razor/Application, referência de destinatário, link+código separado, continuation técnica, novos endpoints, estados/transições, idempotência/48 h, sessão/rascunho, mecanismos de unicidade histórica/constraints, matriz de acesso e UI Platform mínima. Revisor humano pode escolher alternativa documentada; nesse caso atualizar contratos/testes juntos antes do desenvolvimento.

Nenhum ajuste altera Foundation ou aprova grants/IdP/versões/MaxOffset. Infraestrutura técnica candidata (sessão, continuation, referências, rascunhos e idempotência) não é novo domínio compartilhado da plataforma; requer estimativa e aprovação de armazenamento/privacidade. Aprovação deste detalhamento não executa o esquema, testes, UI ou integração dos produtos.

## DCSE-CTX-V1 — contexto empresarial ativo (recorte local DCSE-01)

**Versão técnica:** 1.0. **Origem:** autorização expressa de implementação local DCSE-01, 07/11/2026. Código no worktree operacional `business-platform-core-s2-1-ui`, branch `feat/s2-1-araia-ui`; consumidor `erp-imobiliario-platform`, branch `imob-end-01-address`. Não aprova integralmente ADRs propostas, gates, homologação humana ou publicação.

### Contrato e responsabilidade

- Core: novo `GET /api/v1/access/context` autenticado por bearer existente, `schemaVersion=1`. Retorna issuer/subject, organização/conta/membership, papel administrativo, perfil operacional selecionado, produtos licenciados e vigências, grants efetivos, unidades autorizadas, unidade ativa/última válida, contextVersion e issuedAtUtc/expiresAtUtc. A lista de unidades não constitui escolha de unidade ativa.
- A sessão usa o `sid` já autenticado pelo Keycloak, associado a issuer/subject; somente o hash dessa associação é persistido. O login web prepara o contexto e a consulta nova permite preparação idempotente após autenticação. Mantém uma organização por identidade e o limite absoluto BASE-03 de 24 h; não implementa nova política de inatividade.
- Uma unidade autorizada autoativa. Várias restauram somente a última ainda autorizada; sem última válida, selectionRequired=true e activeEstablishmentId=null. Sem unidades, operações dependentes ficam negadas. Consultas autorizadas entre filiais não dependem da unidade ativa.
- `POST /api/v1/access/context/active-establishment` recebe somente establishmentId e versão esperada; bearer existente. O equivalente web é `GET/POST /account/context`, com sessão web e antiforgery no POST. A seleção é solicitação, nunca autoridade: backend revalida empresa/membership/unidade na transação. Versão obsoleta: 409. Sem autenticação: 401; sem autorização: 403; seleção inválida: 400; sessão/contexto ausente: 409; dependência inválida/indisponível: 502/503.
- Interface central: seletor de unidades autorizadas e atualização controlada, sem novo login. O ERP não possui seletor paralelo. Registros já cadastrados conservam organização, unidade e criador originais.

### Persistência, autorização e eficiência

Migration aditiva `034_active_business_context.sql`: preferências por membership, contexto por sessão e eventos; FK de unidade composta com organização, RLS/FORCE e owner técnico restrito. A porta SECURITY DEFINER tem search_path fechado, sem EXECUTE público nem DML de tabelas para o runtime. Atualização, preferência, versão e auditoria são transacionais; migrations anteriores permanecem imutáveis.

Direitos vêm exclusivamente da interseção existente de perfil selecionado, catálogo publicado e licença/concessão válida. Papel Owner/Admin não concede recursos de produto por si. A prova usa concessão temporária de homologação pelo operador existente e reutiliza o perfil já atribuído, somente no banco descartável; licenciamento comercial e usuários reais não são alterados.

O snapshot HTTP não é token de autenticação nem JWT emitido pela Core. O ERP o obtém em HTTPS confiável com o access token existente e valida versão, issuer, subject, organização, unidades, grants e validade. Quando o cookie OIDC omite a claim `iss`, usa a autoridade já configurada e validada pelo mecanismo existente.

Lease central de no máximo 5 s, limitada também à validade de licenças e sessão. Cache do ERP somente no servidor, chaveada por origem/issuer/subject/hash do token, expira conservadoramente e não se prolonga na falha. Consultas reutilizam o snapshot; abertura de novo Draft e comandos mutáveis obtêm um snapshot fresco uma vez e reutilizam-no nas conferências daquele comando. Assim, uma cache de leitura não escolhe a unidade anterior numa gravação. Leituras podem refletir alterações de direitos/unidade por até 5 s; não há promessa de revogação global instantânea. Propagação crítica completa continua fora deste recorte.

Ativação técnica explícita: Core `ActiveContext:Enabled=true` após migration/EXECUTE da porta no ambiente autorizado; ERP `Core:ContextVersion=1`. O modo legado permanece disponível por configuração. Não há fallback silencioso de um contrato novo indisponível para uma autorização antiga. Endpoints Core preexistentes e APIs imobiliárias v1/v2 não mudam de payload.

### Evidências e limites

Evidências executadas no worktree operacional: `TestResults/dcse-01/` (testes .NET, SQL restrito/rollback, navegador real, persistência e limpeza); inventário final e fingerprints identificam os fontes e ambientes. As evidências históricas do CAD-1A (104) e integração anterior (25) são reutilizadas apenas nos domínios não alterados, sem contabilizá-las como testes novos ou promover todo o produto por inferência.

A implantação da migration foi validada somente em clones privados descartáveis; os bancos originais não são migrados pelo runner. Hosts de prova são encerrados e containers removidos. Homologação humana/mobile e publicação permanecem pendentes. DCSE-01.05–07 não são declaradas integralmente implementadas.

## Referências técnicas

- [ASP.NET Core: antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).
- [ASP.NET Core: cookie authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0).
- [WCAG 2.2: Timing Adjustable](https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html).
- [WCAG 2.2: Accessible Authentication](https://www.w3.org/WAI/WCAG22/Understanding/accessible-authentication-minimum.html).

## Rastreabilidade da consolidação final

I01–I12 em [cenários](IDENTIDADE_CENARIOS_E_DECISOES.md) cobrem login direto, A→B negado, convite de B para identidade A ativa/revogada, novo login de funcionário, concorrência, recuperação, NotLinked, bootstrap, revogação em sessão, e-mail em identidades distintas e falha do IdP. C01 mantém web/bearer/Platform separados; C02/C04 bloqueiam emissão e aceite inelegíveis; C03/C07 não aceitam/renovam acesso por indisponibilidade; C05/C06 preservam locks/ETag/replay atual; C08/C09 não confundem conta com licença/assinatura. Propagação de revogação a produtos e janela de risco dependem de contrato futuro: sem promessa global imediata ou dependência síncrona imposta. Nenhum item comercial é implementado nesta etapa.

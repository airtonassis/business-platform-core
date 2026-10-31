# Sprint 1 — contratos e fluxos para fechamento UX

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

## Referências técnicas

- [ASP.NET Core: antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).
- [ASP.NET Core: cookie authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0).
- [WCAG 2.2: Timing Adjustable](https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html).
- [WCAG 2.2: Accessible Authentication](https://www.w3.org/WAI/WCAG22/Understanding/accessible-authentication-minimum.html).

## Rastreabilidade da consolidação final

I01–I12 em [cenários](IDENTIDADE_CENARIOS_E_DECISOES.md) cobrem login direto, A→B negado, convite de B para identidade A ativa/revogada, novo login de funcionário, concorrência, recuperação, NotLinked, bootstrap, revogação em sessão, e-mail em identidades distintas e falha do IdP. C01 mantém web/bearer/Platform separados; C02/C04 bloqueiam emissão e aceite inelegíveis; C03/C07 não aceitam/renovam acesso por indisponibilidade; C05/C06 preservam locks/ETag/replay atual; C08/C09 não confundem conta com licença/assinatura. Propagação de revogação a produtos e janela de risco dependem de contrato futuro: sem promessa global imediata ou dependência síncrona imposta. Nenhum item comercial é implementado nesta etapa.

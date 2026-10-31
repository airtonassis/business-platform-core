# ADR-0009 — Modelo relacional, RLS e concorrência

## Estado vigente — aceite G1 em 30/10/2026

Atualização de registro em **31/10/2026**, autorizada pelo responsável, sem alteração do modelo ou de decisões técnicas. **G1 tecnicamente aprovado em 30/10/2026**; **D2 aprovada com 178 privilégios de coluna justificados** e cinco grupos de owners; **D3/D5 ratificadas** nos limites do harness descartável existente, sem bypass ou ampliação de privilégios. Evidências: baseline `9ba11b81e986`, complemento `ece79aa930df` e matriz do `tests/TechnicalValidation/ADR0009.G1/EXECUTION_REPORT.md`, mantidos em integração separada.

**G2 não autorizado; G3/V11 estrutural e G4/Application/API/OIDC/recuperação fora do escopo G1.** Não há conclusão integral dos 54 protocolos, aprovação global de ADRs, liberação de Gate UX ou Sprint 1, ou autorização automática de produção/deploy. O restante deste documento preserva a especificação e os registros históricos da fase G0: afirmações de G1 bloqueado, D3/D5 pendentes ou nenhuma execução descrevem aquele momento, não o estado vigente.

O [plano de validação, estado vigente](PLANO_VALIDACAO_TECNICA_ADR_0009.md) registra a divergência temporal nos documentos congelados ADR-0008, ADR-0009 e C01–C10, cujos conteúdos/hashes permanecem intactos. Esta atualização não aprova nem modifica seus mecanismos ou status globais.

## Estado, autoridade e referências

**Especificação proposta, sem DDL executável, schema criado ou testes de banco executados.**
Complementa o [ADR-0009](decisions/ADR-0009-persistence-isolation-and-concurrency.md); não o aprova.
Regra permanente de produto: [ADR-0008](decisions/ADR-0008-organization-membership-and-context.md).
Contratos de comportamento pertencem a [C01–C10](SPRINT_1_CONTRATOS_FLUXOS.md); cenários pertencem a [I01–I12](IDENTIDADE_CENARIOS_E_DECISOES.md).
Este documento concentra detalhes físicos e transacionais para evitar repetir suas definições em cada contrato.

### Registro histórico D1/D2 — fase G0 anterior ao aceite G1

Conforme [registro oficial no ADR-0009](decisions/ADR-0009-persistence-isolation-and-concurrency.md), o responsável aprovou formalmente:
- **D1:** Application é a fronteira confiável de autenticação, autorização e atestação do contexto; PostgreSQL aplica RLS ao contexto transacional recebido. Credencial SQL comprometida é risco residual explícito, sem isolamento comprovado.
- **D2:** manter exec_identity, exec_tenant, exec_bootstrap, exec_context e exec_integrity. Desenho aprovado; comprovação dos grants efetivos e grafo permanece pendente antes da liberação de G1.
- **D3/D5:** pendentes de preparação e validação técnica.

G0 aprovado com condicionantes; **G1 BLOQUEADO**. Aprovações parciais não alteram outras decisões nem executam provas; ver [plano, 1.2/8](PLANO_VALIDACAO_TECNICA_ADR_0009.md). Q10/V08/F10 e os 54 cenários mantêm suas obrigações de evidência.

## M — Modelo relacional candidato

### M1 — Convenções e identidade técnica

PKs UUID escalares com tipos de domínio específicos; FKs compostas só asseguram coerência de escopo. Colunas obrigatórias NOT NULL, inclusive participantes de unicidade. Instantes timestamptz, UTC via TimeProvider da Application; limites textuais/payload e geração UUID ainda precisam de especificação aprovada. Versões bigint positivas, incrementadas em cada mudança persistida, sem wrap silencioso; ETag opaca por recurso conforme C05, sem xmin público.

Identity técnica interna = identity.identities (IdentityId); IdentityLink = identity.identity_links, associação OIDC validada a esse ID. Substituem nesta proposta os nomes anteriores identity.users/external_identities: não são tabelas adicionais nem fusão de pessoas por e-mail. UserId dos contratos é o ID técnico interno exposto pelo adapter, sujeito a aprovação do mapeamento, sem modificar Foundation. Login comum não une sujeitos distintos. Aliases de migração de IdP exigem procedimento especial e prova, não endpoint de linking no MVP.

### M2 — Tabelas, chaves, índices e estados

| Ref. / tabela proposta | PK, FKs e unicidade | Estados, checks, versão e índices adicionais |
|---|---|---|
| M-ID identity.identities | PK identity_id | Active/Blocked/Archived; versão; sem empresa/e-mail como chave; linha mínima retida se consumida |
| M-LINK identity.identity_links | PK link_id; FK identity_id RESTRICT; UNIQUE(issuer,subject); índice identity_id | Active/Retired; versão; issuer/subject não vazios e comparados exatamente, sem normalização ad hoc; par jamais reatribuído a outro IdentityId; e-mail opcional fora das chaves |
| M-ORG tenancy.organizations | PK organization_id | Active/Blocked/Archived; versão; nome limitado; tombstone mínimo se referenciado por histórico; criação só pela porta Platform |
| M-HIST identity.identity_consumptions | PK identity_id/FK M-ID RESTRICT; UNIQUE(account_id); UNIQUE(identity_id,organization_id,account_id); FK organization_id RESTRICT | identity_id, account_id, organization_id, consumed_at e origem técnica mínima; insert-only; sem Active que libere unicidade; índice organization_id para operação restrita |
| M-ACCOUNT identity.enterprise_user_accounts | PK account_id; UNIQUE(identity_id) sem filtro; UNIQUE(organization_id,account_id); FK org RESTRICT; FK (identity_id,organization_id,account_id) para M-HIST, DEFERRABLE INITIALLY DEFERRED | Active/Revoked/Archived; versão; organization_id/identity_id/account_id imutáveis; índice (organization_id,status,account_id) |
| M-MEMBER tenancy.memberships | PK membership_id; UNIQUE(account_id); UNIQUE(organization_id,membership_id); FK (organization_id,account_id) para M-ACCOUNT RESTRICT | Active/Revoked; role Owner/Admin/Member; versão; índice (organization_id,status,role,membership_id); exclusão física negada no runtime |
| M-SLOT tenancy.invitation_slots | PK (organization_id,recipient_identity_id); FKs M-ORG/M-ID RESTRICT | Linha técnica de coordenação, não nova entidade pública; evita lock em convite ainda inexistente; sem segredo; índice recipient_identity_id |
| M-INV tenancy.invitations | PK invitation_id; UNIQUE(organization_id,invitation_id); FK org/recipient; FK composta (org,issuer_membership_id) para M-MEMBER; secret_hash UNIQUE; predecessor_id/successor_id únicos quando presentes e FKs compostas à própria tabela | Pending/Accepted/Expired/Cancelled/Replaced; versão; expires_at > created_at; índice UNIQUE parcial (org,recipient) WHERE status=Pending, sem relógio no predicado; índice (org,status,created_at,id) e (recipient,id); Accepted exige accepted_at/account_id e FK composta ao M-ACCOUNT |
| M-DEMO sandbox.demo_records | PK id; FK org RESTRICT; UNIQUE(org,id) | versão; título 1–200; organização imutável; índices e ordenação total conforme consultas C09/ADRs 0004–0006 |
| M-AUDIT audit.events | PK event_id; FK org RESTRICT quando Tenant; actor_identity_id técnico/tombstone, sem cascata; operation_id para correlação | scope Tenant exige org, Platform/Identity sem org; ação/resultado/UTC/correlação; append-only; índice (org,occurred_at,event_id) e operation_id; sem tokens/códigos/payload integral |
| M-IDEM operation.idempotency_records | PK id; UNIQUE(actor_identity_id,scope_key,operation,key); scope_key não nulo, codifica Tenant+UUID ou Platform | request_hash, operation_id, resultado mínimo/IDs, expires_at; somente Completed visível após commit; sem código recuperável; índice expires_at; registro transacional por módulo, não engine Foundation |

M-IDEM é nome físico candidato de armazenamento técnico restrito, sem API genérica. Recomendação: unicidade de chave estável inclui a identidade, e AccountId é atributo validado, não parte mutável da chave que mudaria após primeiro aceite. Para Tenant incluir OrganizationId canônico no scope_key; Platform nunca depende de NULL. Sem chave de idempotência em accept: a transição Accepted do próprio convite é o recibo C06. Outras operações seguem lista exata de C06.

M-INV: nome físico org representa organization_id. Destinatário, organização, emissor e hash não são editáveis; substituição cria outro ID. Checks locais validam presença de campos segundo estado; coerência predecessor/successor e proibição de ciclos dependem da porta de substituição. Sem cascata sobre auditoria, consumo ou recibos. M-HIST não tem FK reversa obrigatória à conta: pode sobreviver a expurgo autorizado da conta arquivada. Conta corrente sempre referencia seu consumo exato; registro histórico sem conta nunca significa NotLinked.

### M3 — Garantias e limites

O mecanismo exato das validações diferidas é detalhado em P0-02 abaixo: portas adquirem todos os locks antes do DML; triggers diferidas apenas consultam o estado final, sem adquirir novos locks de linha. Não se deve inferir um lock tardio no commit a partir desta matriz.

| Invariante | Garantia PostgreSQL proposta | Responsabilidade transacional/aplicação |
|---|---|---|
| Duas contas para uma identidade | UNIQUE M-ACCOUNT.identity_id + PK M-HIST e associação exata por FK | Porta recusa consumo já existente, mesmo sem conta corrente; não inserir novo AccountId para consumo antigo |
| Membership em outra empresa | FK composta org/account, ambas NOT NULL | Org vem da resolução autenticada; sanitizar violações sem revelar tenant |
| Conta Active sem Membership Active coerente | Constraint trigger diferida, ao fim da transação, em INSERT/UPDATE/DELETE de Account e Membership; verifica exatamente um Membership do mesmo org/account e Active quando Account Active | Locks de organização e conta/membros impedem write skew; criar ambos no mesmo commit; FK simples do membro NÃO assegura existência inversa |
| Conta revogada com Membership ativo | Mesmo trigger exige Membership revogado quando Account Revoked/Archived; Membership Active exige Account Active | Revogar membership também revoga conta no mesmo commit; sem reativação implícita; regra candidata sujeita a aprovação |
| Organização Active sem Owner inicial | Trigger diferida verifica ao menos Owner Active com Account Active; alterações relevantes também revalidam organização ativa | Bootstrap cria tudo junto; bloquear org serializa contagem/alteração de Owners; bloqueio/arquivamento de empresa não precisa reativar membros |
| Organização/identidade imutáveis | Privilégios de coluna e trigger BEFORE comparando OLD/NEW para campos imutáveis; referências RESTRICT | Não expor PATCH/transferência; operações excepcionais só procedimento revisado |
| Histórico não reciclável | M-HIST sem UPDATE/DELETE no runtime; IdentityLink não reassociável; FKs RESTRICT | Expurgo, restauração e migração podem quebrar garantia se operador apagar histórico; controles H e revisão humana |
| Empresa/proprietário sem criação parcial | Transação única; FKs/trigger diferidas validadas no commit | Org+consumo+Account+Owner+auditoria+idempotência na mesma conexão/transação; falha em qualquer etapa desfaz tudo |
| Mutação sem auditoria | Banco garante atomicidade se ambos estão na mesma transação; grants limitados reduzem caminhos alternativos | Portas de comando exigem escrita de evento; CHECK não prova presença de evento para toda mutação; se houver escrita direta fora das portas não se pode prometer completude |

Não usar CHECK com consulta a outra tabela para simular existência de Membership. A opção recomendada de constraint trigger diferida ainda exige desenho e testes reais; alternativa é FK reversa diferida com ciclo Account/Membership mais triggers de estado, porém aumenta acoplamento e não resolve sozinho Active/Owner. Só aplicação+FK é alternativa mais simples com garantia menor contra caminhos indevidos, não recomendada para invariantes críticos.

## R — RLS, privilégios e contexto

### R1 — Papéis candidatos e fronteiras

Todos os logins runtime: NOSUPERUSER, NOBYPASSRLS, NOINHERIT, sem ownership/DDL/TRUNCATE/CREATE em schemas, sem SET ROLE para executores, sem acesso a credencial migrator. Pools separados por login/porta, nunca pool administrativo compartilhado com Tenant.

- core_tenant: leitura empresarial RLS e EXECUTE nas portas de mutação Tenant; sem INSERT/UPDATE/DELETE direto nas tabelas sensíveis.
- core_identity: EXECUTE somente resolver contexto próprio, referência/preview/aceite e preparação autenticada; sem SELECT genérico ou DML nas tabelas.
- core_platform: EXECUTE somente bootstrap e conferência restrita; nenhum SELECT empresarial.
- exec_integrity: NOLOGIN, sem bypass/ownership, somente leitura técnica restrita para funções de trigger; sem EXECUTE externo ou DML. Não reutiliza o helper de autorização empresarial após uma revogação.
- exec_context: NOLOGIN, sem bypass/ownership, leitura apenas de colunas técnicas próprias para predicado booleano de autorização, sem listagem ou DML.
- exec_tenant, exec_identity, exec_bootstrap: NOLOGIN, sem BYPASSRLS, não owners das tabelas, privilégios por tabela/coluna estritamente necessários às respectivas funções. EXECUTE pode diferir por função, mas privilégios de tabela/coluna e policies formam a união efetiva do owner compartilhado; não há ACL de DML isolada por função. Mantêm-se grupos restritos, não um executor universal.
- core_migrator/ddl_owner: manutenção excepcional com DDL/ownership; fora do processo web e de seus segredos. Não considerado prova de isolamento; pode alterar políticas/triggers e é risco operacional explícito.
- Manutenção/expurgo futuros não reutilizam logins de runtime; autorização e escopo próprios antes de executar.

As matrizes P0-01 e P0-03 abaixo refinam esta visão resumida e fixam o contrato de confiança e os grants por função; flags SQL não constituem provas autônomas.

Funções SECURITY DEFINER são recomendação candidata para portas estreitas, não SQL implementado. Owners das funções são os executores NOLOGIN; revogar EXECUTE de PUBLIC, fixar search_path em schemas confiáveis com pg_temp por último, nomes totalmente qualificados, sem SQL dinâmico. Validar ator, esquema Platform/Tenant, destinatário e prova de segredo no caminho correto antes de devolver dados; grants internos não são endpoint de leitura ampla. RLS ENABLE + FORCE em tabelas empresariais inclusive sob executor. Owner de tabela distinto; runtime não é membro de executor/migrator. Referência: [segurança de funções](https://www.postgresql.org/docs/current/sql-createfunction.html).

### R2 — Predicados candidatos da matriz

T = contexto transacional válido contendo IdentityId, AccountId e OrganizationId iguais ao vínculo corrente; ator/conta/membership/empresa ativos. A permissão de negócio vem de C08, não de papel enviado pelo cliente.
SELF = IdentityId autenticado no contexto e igualdade ao proprietário técnico da linha.
REC = convite por ID exato, destinatário SELF e segredo provado pela função; nunca todos os convites do usuário.
BOOT = operação de bootstrap autenticada/Platform aprovada na aplicação, alvo elegível por referência C02; contexto de operação restrito a IDs novos/alvo da transação.
A = escopo de auditoria autorizado.
Contexto ausente/vazio/inválido deve resultar em predicado false; UUID malformado deve ser rejeitado na instalação do contexto, nunca cair em contexto anterior. Mecanismo candidato para T: helper SECURITY DEFINER próprio sob exec_context, retornando apenas booleano da própria identidade e escopo instalado, sem parâmetro de usuário arbitrário. Suas policies não invocam T: Identity/Link usam igualdade SELF; Account usa identity_id=SELF; Membership usa account_id do contexto; Organization usa org do contexto. O helper faz o join completo para confirmar IdentityId→AccountId→Membership→Organization e estados, antes de retornar true. Grants só de SELECT nas colunas técnicas necessárias, sem devolver linhas/nomes/papéis alheios. Como essas policies-base não chamam helper, evitam a recursão de policies sobre as mesmas tabelas. Verificar plano, índices, privilégio de EXECUTE e ausência de canais laterais em teste real; esse desenho ainda não está implementado. O helper de leitura não substitui locks/revalidação do comando.

As expressões abaixo são especificações lógicas, não policies SQL prontas. SELECT usa USING e não WITH CHECK; INSERT usa WITH CHECK e não USING; UPDATE requer ambos; DELETE sem grant/policy no runtime.

### R3 — Matriz tabela × operação × executor

| Schema/tabela | Operação / executor | Contexto | USING (linha anterior/leitura) | WITH CHECK (linha nova) / sem contexto |
|---|---|---|---|---|
| Colunas técnicas Identity/Account/Membership/Organization | SELECT somente interno exec_context | SELF e IDs do contexto | Policies-base sem T descritas em R2 | Sem INSERT/UPDATE/DELETE; contexto ausente nega |
| identity.identities / identity_links | Resolver/preparar própria identidade, via core_identity → exec_identity | SELF validado após OIDC | Apenas SELF, nunca diretório; claim issuer/subject validado é entrada do servidor | Inserção própria e associação canônica sem fusão; update só campos permitidos; sem SELF nega |
| identity.identity_consumptions | Consultar própria elegibilidade exec_identity; consultar alvo provado exec_tenant/exec_bootstrap; inserir exec_identity/exec_bootstrap | SELF ou referência C02 provada/BOOT | Só identidade alvo da operação; não retornar org a emissor | Novo consumo exato dos IDs do comando; sem UPDATE/DELETE; sem prova nega |
| identity.enterprise_user_accounts | SELECT próprio exec_identity; SELECT tenant autorizado core_tenant/exec_tenant; INSERT exec_identity/exec_bootstrap; UPDATE estado exec_tenant | SELF/T/REC/BOOT conforme porta | Próprio vínculo para resolução mesmo revogado; leitura empresarial T e C08; sem acesso genérico identity | Inserção somente conta alvo de REC/BOOT e FK consumo; update T e campos estado/versão; sem contexto nega |
| tenancy.organizations | SELECT tenant core_tenant; resolver próprio exec_identity; INSERT exec_bootstrap | T/SELF/BOOT | T; resolver só org do próprio consumo, metadados mínimos | BOOT só novo org; sem UPDATE/DELETE runtime nesta sprint; sem contexto nega |
| tenancy.memberships | SELECT core_tenant com C08/exec_tenant ou próprio exec_identity; INSERT exec_identity/exec_bootstrap; UPDATE exec_tenant | T/SELF/REC/BOOT | T e permissão de listar, ou próprio para contexto; sem exposição de outros membros a Member | Org/account coerentes; Owner só BOOT, Member só REC; alteração conforme C08; sem contexto nega |
| tenancy.invitation_slots | SELECT/INSERT/lock exec_tenant/exec_identity | T+destinatário provado ou REC | Par org/destinatário da operação | Mesmo par; nenhuma enumeração; sem contexto nega |
| tenancy.invitations | SELECT tenant core_tenant/exec_tenant; SELECT/UPDATE exato exec_identity; INSERT/UPDATE exec_tenant | T com Owner/Admin ou REC | T+C08 para gestão; REC só ID e destinatário; prova do segredo ocorre na função antes de expor | Tenant: campos e transições C04; REC: somente Accepted no mesmo convite; sem contexto nega |
| sandbox.demo_records | SELECT core_tenant; INSERT/UPDATE exec_tenant | T, escrita Owner/Admin | T para leitura; T+C08 para update | T e mesmo org, versão válida; sem DELETE ou contexto nega |
| audit.events | SELECT core_tenant com C08; INSERT executores específicos | A: Tenant=T; Identity=SELF; Platform=BOOT | Tenant próprio Owner/Admin; sem leitura global Platform | Escopo/org/ator/evento coerentes com operação; append-only; sem contexto nega |
| operation.idempotency_records | SELECT/INSERT executor correspondente à operação; atualização técnica dentro da mesma transação se necessária | Ator+scope+operação exatos | Somente recibo exato, autorização atual antes do replay | Mesmo ator/escopo/hash; org Tenant coerente; sem contexto nega |
| Armazenamento C03/C07: RecipientReference/WebSession/Continuation/Draft | Porta técnica separada, sem grants Tenant/Platform genéricos | Proprietário/fluxo e autorização atual | SELF+flow; Draft empresarial exige T | Mesmo proprietário/fluxo, limites/criptografia C03/C07; sem contexto nega; localização física ainda pendente |
| Todas acima | Migrator/manutenção explicitamente autorizada | Fora de runtime | Pode alterar/contornar garantias por ownership | Acesso controlado, registro operacional e revisão; não rota administrativa web |

Policies separadas por executor/comando; revisar combinação OR de policies permissivas e usar restritivas quando necessário para que SELF/BOOT não ampliem leitura Tenant. RLS não é RBAC completo: C08 continua no caso de uso/porta. Privilégio de UPDATE necessário para locks de linha deve limitar colunas técnicas; conferir grants reais mesmo em executor sem login. Todos os caminhos de alteração mantêm triggers. PK/FK/UNIQUE podem produzir sinais de existência apesar de RLS: nunca expor nome de constraint, valor ou mensagem SQL ao cliente. [Limites de RLS](https://www.postgresql.org/docs/current/ddl-rowsecurity.html).

### R4 — Contexto transacional e pooling

Sequência candidata por unidade de trabalho: obter conexão do pool correto → BEGIN explícito → instalar com parâmetros via set_config(..., true) os valores app.identity_id/app.account_id/app.organization_id/app.operation_id necessários → resolver/revalidar → executar → COMMIT/ROLLBACK → liberar. Não confiar em org de rota, claim empresarial, UI ou header; derivar no servidor. Na porta sem empresa, organization_id começa ausente e só recebe o alvo validado dentro da transação. Contextos de prova/BOOT são internos à função estreita, não uma flag pública ignoreTenant.

Nunca usar SET de sessão para tenant. SET LOCAL dura a transação, mas pode restaurar valor de sessão preexistente; portanto também exigir baseline vazio, proibir contexto persistente e validar/resetar conexão ao checkout conforme driver. Sob rollback de savepoint anterior à instalação, reinstalar contexto validado antes de qualquer I/O ou abortar unidade completa. Não continuar transação abortada. Em exceção/cancelamento, rollback em finally; se não for possível confirmar encerramento/reset, descartar conexão física. Não compartilhar conexão entre requisições ou transações concorrentes.

Teste futuro deve tentar SELECT após commit/rollback sem reinstalar contexto e obter zero linhas/negação; depois alternar A/B repetidamente no mesmo backend físico, incluindo falha, cancelamento e savepoint. Não confiar só no reset do pool. Configurações customizadas são controláveis pelo próprio processo: comprometimento de runtime pode forjar contexto. Pools distintos/portas estreitas reduzem alcance, mas não provam identidade SQL contra aplicação totalmente comprometida. [SET LOCAL](https://www.postgresql.org/docs/current/sql-set.html).

## C — Concorrência, locks e resultados

### C1 — Protocolo global candidato

READ COMMITTED com transações curtas e locks explícitos é recomendação inicial; SERIALIZABLE é alternativa com abortos/retries adicionais, não substitui constraint/grant/autorização. Sem I/O IdP, e-mail ou produto dentro da transação.

Correção mínima P0-02: para cada conjunto, Accounts em UUID crescente vêm antes de Memberships em UUID crescente, sem intercalar tabelas. Todo lock deve anteceder DML relevante; constraint triggers não fazem SELECT FOR UPDATE/SHARE. Execução deve separar aquisição de locks e consultas de revalidação em instruções distintas, inclusive dentro de funções VOLATILE, para não reutilizar snapshot anterior à espera. A garantia é para caminhos runtime controlados; manutenção privilegiada requer protocolo próprio.

Ordem única para todos os caminhos: (1) reserva/lock da chave C06 quando aplicável; (2) IdentityId envolvidos, UUID crescente, FOR UPDATE nos consumos/identidades a consumir ou alterar, FOR SHARE no ator apenas validado; se identidade se repete, adquirir modo mais forte de início; (3) organizações em UUID crescente FOR UPDATE para toda mutação Tenant; (4) Accounts e Memberships envolvidos em ID crescente, FOR UPDATE; (5) slot org/destinatário; (6) Invitations em ID crescente; (7) DemoRecord/alvos; (8) inserções de consumo/conta/membro/auditoria e recibo, respeitando FKs/validação diferida no commit.

IDs necessários devem ser descobertos por leitura limitada sem locks e revalidados depois. Se descoberta mudar conjunto/ordem, abortar e reiniciar; não adquirir lock retroativo em ordem inversa. Slots inexistentes: criar de forma concorrente controlada sob lock da identidade/org e então bloquear; UNIQUE é salvaguarda. Não usar lock em linha inexistente como proteção. Chave C06 descoberta antes dos locks; não reservar chave depois de travar org. Uma operação não chama outra transação/porta com ordem inversa.

FOR UPDATE na organização serializa inclusive diferentes mutações da mesma empresa: escolha conservadora, com risco de contenção a medir. Leituras comuns não adquirem esse lock; novas requisições revalidam estado e não usam decisão empresarial em cache. Locks globais não garantem ausência absoluta de deadlocks (FKs, triggers e novas operações também contam). Timeouts/retry precisam de limites humanos aprovados. Sem promessa de cancelamento retroativo de leitura já iniciada. [Locks PostgreSQL](https://www.postgresql.org/docs/current/explicit-locking.html).

### C2 — Matriz dos dez casos

| ID / disputa | Transação, locks e revalidação | Resultado esperado |
|---|---|---|
| K01 Aceites A/B | Identidade destinatária primeiro; org/slot/convite; provar código e destinatário; verificar histórico e prazo; inserir consumo+Account+Member+Accepted+auditoria | Um commit; perdedor reavalia elegibilidade e recebe negativa C04, sem transferência. UNIQUE é última defesa; nenhum órfão. Replay do vencedor só autorizado |
| K02 Bootstrap × aceite | Ambos bloqueiam mesma identidade antes de qualquer org; bootstrap cria org somente após confirmar elegibilidade; recibo Platform antes da identidade | Um consome; bootstrap perdedor rollback completo inclusive org, Owner/auditoria/recibo; aceite perdedor sem Accepted. Não bloquear org A antes de identidade para depois disputar B |
| K03 Revogação × comando | Mesmo protocolo de identidade/org/membership; comando mantém locks até commit; revogação atualiza Account+Membership e audita | Comando primeiro pode terminar; revogação primeiro impede próximo comando após revalidação. Sem revogação retroativa nem invalidação global de JWT de produtos |
| K04 Membership/DemoRecord concorrentes | Autorizar antes de revelar versão; lock org/alvo e UPDATE condicionado à versão; incrementar versão | Um vence, outro 412 após direitos revalidados; 428 sem If-Match, 400 malformado. Revogação pode tornar resposta 403/404 antes de comparação; sem sobrescrita |
| K05 Replace simultâneo | Chave C06 antes; identidade/org/slot/convites; comparar ETag; old Replaced+new Pending+auditoria+recibo no commit | Uma substituição. Outra chave com ETag antigo → 412 se ainda autorizada; mesma chave/payload → replay sem segredo. Índices impedem dois Pending/sucessores |
| K06 Pending vencido | Depois dos locks, obter instante fresco TimeProvider, validar expires_at; materializar Expired antes de emitir/substituir sob mesmo slot | GET não grava nem incrementa versão; accept nega 409 Expired mesmo ETag igual. Pending vencido não bloqueia indefinidamente novo convite; não usar now() em índice parcial. Validade aferida no ponto de transição, não promessa de commit antes do prazo |
| K07 Resposta perdida | Estado+auditoria+recibo no mesmo commit; nova requisição autoriza antes de ler recibo; accept lê transição própria | Sem novo efeito; C06 replay 200/Accepted autorizado, segredo indisponível. Falha de rede no commit = desconhecido; não assumir rollback nem repetir com chave nova |
| K08 Auditoria falha | INSERT do evento obrigatório na mesma transação do comando; sem gravação externa entre etapas | Rollback confirmado desfaz mutação/recibo; erro sanitizado de infraestrutura. Falha de conexão no commit pode deixar resultado desconhecido. Registro de tentativa negada é evento separado, não sucesso fictício |
| K09 Último Owner | Lock org+conta/membros; C08 proíbe alterar Owner/si próprio antes de contar; trigger diferida preserva Owner da org Active | Nesta sprint qualquer alteração de Owner é negada, ainda que exista outro; proteção final não amplia C08. Caminho futuro autorizado teria de preservar ao menos um Owner sob mesmo lock |
| K10 Pool após rollback/exceção | Rollback completo, descarte se incerto; próximo checkout baseline vazio; BEGIN + novo contexto; não reutilizar transação falha | Sem contexto não lê/escreve Tenant; A não reaparece em B; savepoint não permite continuar com contexto perdido. Evidência futura deve identificar mesmo backend físico |

Revogação de Membership implica Account Revoked nesta proposta física, sem apagar consumo. Revogação global de identidade exigirá bloquear a mesma identidade em modo conflitante e revalidar estado, não apenas invalidar cookie. Expiração é aferida após espera por locks; não usar timestamp antigo do início da requisição para estender convite. Prazo transacional e amostragem exata do relógio ainda precisam de aprovação e teste.

### C3 — Classificação de falhas, sem motor genérico

| Classe | Tratamento candidato |
|---|---|
| Negócio/inelegibilidade | C04/C08: 403 genérico para conta inelegível após prova; 404 não enumerável sem prova; 409 para estados conhecidos autorizados. Nenhuma mensagem SQL |
| Versão | 412 obsoleta; 428 ausente; 400 malformada; nunca atualizar ETag e reenviar automaticamente |
| UNIQUE 23505 | Classificar somente constraint conhecida: consumo disputado → inelegibilidade; chave C06 → comparar hash/resultado; Pending → conflito/recarregar. Constraint inesperada = falha interna, não qualquer 409 |
| FK/check/trigger | Nome conhecido indica invariante violada; rollback, sanitização e diagnóstico. Não converter automaticamente em erro de cliente; pode ser defeito de implementação |
| Deadlock 40P01 / serialização 40001 | Transação inteira abortada; retry interno limitado apenas se rollback certo, comando seguro/idempotente e autorização refeita; caso esgotado, indisponibilidade transitória sanitizada, não 412 |
| Lock timeout/cancelamento | Diferenciar timeout operacional, cancelamento do cliente e negócio; nunca declarar sucesso; retry só após certeza de rollback |
| Rede/commit desconhecido | Reconciliação por recibo/estado C06; sem retry cego de mutação. 503 para dependência conhecida, 500 para falha inesperada, sem expor SQL |

Janela C06 de 48 h é hipótese preservada, não aprovação. Estado concorrente em andamento pode ser coordenado por UNIQUE/lock da própria transação, sem Pending visível permanente: espera limitada ou 409 Operation.InProgress quando comprovado; não confundir todo lock timeout com operação idempotente em progresso. Nunca guardar código/token em recibo ou hash canônico de payload contendo segredo sem desenho seguro. Replay revalida autorização antes do retorno e não refaz auditoria de sucesso; telemetria de retry é distinta.

## H — Histórico, privacidade, restauração e IdP

| Alternativa | Benefício | Risco e condição |
|---|---|---|
| H-A IdentityLink + consumo mínimo retidos, conta arquivada | Unicidade e rastreio simples; recomendação candidata inicial | Identificadores OIDC/IDs pseudônimos ainda podem ser pessoais; finalidade, acesso, prazo/revisão e descarte exigem privacidade/jurídico |
| H-B Ledger mínimo separado de PII, chaves OIDC em forma protegida/tokenizada | Expurgar perfil e reduzir exposição mantendo bloqueio | Encriptação exige chaves; token/HMAC exige versionamento/rotação e busca multi-chave. Hash simples não é anonimização garantida; destruir chaves pode permitir reciclagem |
| H-C Somente conta soft-deleted | Menos estruturas | Só funciona se linha/IdentityLink nunca forem removidos; frágil a expurgo/restauração. Não suficiente sem controle operacional e prova |
| H-D Apagar todo identificador/histórico e aceitar ausência como livre | Minimiza retenção | Incompatível com regra permanente: não permite reconhecer identidade consumida; requer decisão jurídica/produto se obrigação de apagar impedir garantia, nunca bypass silencioso |

Mínimo candidato: ID técnico estável, prova/mapeamento do par OIDC consumido, AccountId original, OrganizationId mínimo/tombstone e instante/origem técnica estritamente necessários. Sem nome, e-mail, telefone, documento civil, credencial, código de convite ou payload de negócio no ledger. Forma bruta/protegida, fundamento, prazo, titulares com acesso e procedimento de descarte não foram aprovados. Não afirmar anonimização, conformidade LGPD ou retenção indefinida.

Arquivamento e exclusão lógica mantêm restrição. Expurgo físico de perfil/conta só por procedimento aprovado que mantém consumo/alias mínimo; não habilita recriação normal do mesmo AccountId. Se precisar restaurar a representação original, usar operação excepcional revisada preservando identidade/empresa/AccountId, sem reativação implícita. Operador privilegiado pode apagar ledger: segregação de funções, revisão, auditoria externa operacional e validação de recuperação são controles compensatórios.

Backup anterior a um consumo pode perder a marca: restore não pode voltar ao tráfego antes de reconciliar consumos posteriores com fonte de recuperação/WAL/registro durável aprovada, validar constraints/contagens e quarentenar identidades incertas. RPO não zero sem fonte reconciliável impede prometer permanência; definir tratamento de perda irrecuperável, sem liberar como NotLinked. Restauração por empresa nunca remove consumo global ou importa AccountId de outra empresa.

Migração IdP: inventário e prova de mapeamento antigo→novo, preservar aliases anteriores e consumo no mesmo IdentityId, sem fusão por e-mail. Subject reciclado pelo provedor pode autenticar outra pessoa como identidade antiga: histórico sozinho bloqueia segunda conta, mas não impede acesso indevido à conta original. Exigir não reatribuição do sujeito, desativação do link e revisão de segurança. Pairwise por cliente não é correlação universal; decidir mapeamento antes de integrar produtos. Novo login para novo emprego é nova identidade, não alias de migração.

Responsáveis: arquitetura define invariantes e procedimento; operação protege ledger/chaves, backup e reconciliação; segurança valida IdP e privilégios; privacidade/jurídico avaliam necessidade/base/prazo/descarte e conflitos. Nenhum deles é automaticamente substituído por este ADR.

## T — Rastreabilidade e evidências futuras

| Referência | Cobertura física/transacional | Cenários |
|---|---|---|
| ADR-0008: conta única permanente, Membership e bootstrap | M-ID/LINK/HIST/ACCOUNT/MEMBER; M3; K01/K02/K09; H | I01/I03–I09/I11 |
| C01 autenticação/esquemas | R1/R2/R4; SELF validado e Platform segregado | I01/I02/I07/I11/I12 |
| C02 identidade/referência | M-ID/LINK/HIST; R3 conferência restrita | I03/I04/I05/I08/I11 |
| C03 continuation | R3 armazenamento técnico; sem fallback IdP | I12 |
| C04 convites | M-SLOT/INV; K01/K05/K06; elegibilidade histórica | I03–I06 |
| C05 ETag | M1; K03/K04/K05; autorização antes de versão | I02/I06/I10 |
| C06 idempotência | M-IDEM; K02/K05/K07/K08; classificação C3 | I06/I09/I10 |
| C07 sessão/revogação | R4; K03/K10; sem invalidação global prometida | I07/I10/I12 |
| C08 autorização | M3; R1–R3; K09 | I02/I03/I04/I09/I10 |
| C09 contexto/bootstrap/demo | M3; R3; K02/K04; NotLinked só sem consumo | I01/I02/I08/I09 |
| C10 limites/gates | Aprovações P; Foundation imutável e módulos comerciais fora | I01–I12 |
| I01–I12, todos | Matriz acima cobre separadamente identidade, dados, concorrência e falha | Resultados esperados, não ensaiados |

Evidência exigida futuramente: constraints reais inclusive COMMIT diferido; tentativas de conta sem Membership e Membership cruzado; cada grant/policy com login real e sem contexto; leitura sem filtro; SQL errors sanitizados; mesma conexão física A/B após commit/rollback/cancelamento/savepoint; cada K01–K10 com duas conexões/barreiras e ambos os ordenamentos; injeção de falha na auditoria e resposta perdida; restauração/migração em ambiente explicitamente autorizado. Build/health check não comprovam essas propriedades. Não há testes executáveis nesta revisão.

## P — Aprovações humanas ainda necessárias

1. PostgreSQL/EF/provedor/versões compatíveis .NET 10, isolamento compartilhado e benchmark de contenção.
2. Nomes físicos/IdentityId–UserId, estados de conta/membro, constraints diferidas e triggers de existência/Owner/imutabilidade.
3. Papéis, grants por coluna, policies finais sem recursão/ampliação, funções SECURITY DEFINER, owners e pools separados.
4. Ordem/modos de locks, READ COMMITTED ou alternativa, limites de espera/retry e instante de validade.
5. ETag, hash canônico por operação, armazenamento/retenção C06 e recuperação de resultado desconhecido; 48 h não aprovada.
6. Ledger mínimo, criptografia/tokenização, retenção/descarte, procedimento excepcional e avaliação de privacidade/jurídico.
7. RPO/RTO, reconciliação antes de restore, migração IdP/aliases, não reciclagem de subject e responsabilidade operacional.
8. Armazenamento/chaves C03/C07, parâmetros de sessão e propagação de revogação para produtos; continuam fora de decisão automática.
9. MaxOffset e gates Repository 2.x/ADRs 0004–0006 já existentes.
10. Aprovação do ADR-0009, demais ADRs, Gate UX e autorização de implementação continuam separadas.

## Fontes técnicas consultadas

Somente documentação oficial: [constraints](https://www.postgresql.org/docs/current/ddl-constraints.html), [RLS](https://www.postgresql.org/docs/current/ddl-rowsecurity.html), [locks](https://www.postgresql.org/docs/current/explicit-locking.html), [SET LOCAL](https://www.postgresql.org/docs/current/sql-set.html) e [funções e privilégios](https://www.postgresql.org/docs/current/sql-createfunction.html). Referências explicam mecanismos; tabelas e protocolos acima são propostas do projeto, não garantias já demonstradas. A versão apontada por current não constitui escolha de versão para produção.

## P0-01 — Contrato de confiança do contexto

### Q1 — Autoridade, autenticação e ciclo da requisição

1. Middleware padrão ASP.NET Core autentica OIDC/cookie ou bearer no scheme apropriado de C01/ADR-0007, valida assinatura/issuer/audience/expiração e, quando aplicável, correlação/MFA. Token inválido não chega ao caso de uso. Banco não valida JWT nem recebe senha/ID token como prova de Membership.
2. Adapter entrega à Application principal autenticado imutável de requisição, com issuer/subject e scheme validados. O mapeamento IdentityLink→IdentityId vem da persistência restrita; nenhum IdentityId fornecido pelo cliente substitui o sujeito. Preparação inicial de identidade ocorre no fluxo autenticado explícito, não em GET.
3. A Application autoriza a operação, abre unidade transacional no pool específico, instala SELF derivado desse mapeamento, resolve consumo/Account/Membership/Organization no servidor e instala o contexto Tenant apenas se coerente/ativo. Revalida após locks antes da mutação. OrganizationId solicitado é comparado com o resolvido; divergência nega, nunca seleciona outra empresa.
4. Rota, body, header, cache e claims empresariais não são autoridade, mesmo se constarem de token assinado: identificam alvo solicitado ou pistas; papel/permissões vêm do estado atual. O scheme Platform e MFA são validados na aplicação, não inferidos do login SQL core_platform ou de app.is_platform.
5. Requisição vinculada a outra identidade não reutiliza contexto, rascunho, conexão transacional ou recibo protegido. Nunca permitir SQL arbitrário/expressões de filtro livres, nem parâmetros de ator/scope escolhidos pelo cliente para funções.

**Fronteira explícita:** Application/adapter de persistência e suas credenciais são confiáveis para atestar SELF e a prova de scheme/MFA. SET LOCAL/set_config são somente transporte transacional; não são assinatura nem prova criptográfica de identidade. O mesmo login SQL compartilhado que pode configurar GUC pode falsificar SELF. PostgreSQL não distingue esse processo comprometido de uma requisição legítima. Este desenho protege contra escopo omitido/divergente e caminhos públicos indevidos; não promete isolamento contra comprometimento completo da aplicação/credencial.

Menor privilégio ainda limita esse ataque: login Tenant não executa bootstrap, identidade não lê DemoRecord, Platform não recebe diretório/SELECT Tenant, e nenhuma porta permite SQL livre. Mas atacante com core_tenant e SELF de outro usuário pode tentar agir como ele nos limites de C08. SQL injection é portanto risco crítico a prevenir, não resolvido por RLS. Exigir parametrização, gestão de segredos/pools, controles de acesso ao processo e registro operacional. Autenticação independente de cada usuário pelo banco/prova criptográfica adicional seria outra decisão arquitetural; não introduzida aqui nem exigida como token próprio de contexto, vedado pelo ADR-0007.

### Q2 — Matriz operação × contexto × prova

| Contexto | Operações admitidas | Provas/derivação obrigatórias | Operações negadas / retorno |
|---|---|---|---|
| Sem autenticação | Challenge/retorno padrão IdP fora dos comandos SQL protegidos | Validações C01 | Sem leitura de identidade/tenant ou mutação; 401 quando API |
| SELF autenticado ainda não resolvido | Resolver própria identidade/consumo/contexto | Par OIDC validado, IdentityLink canônico, scheme | Não aceitar IdentityId arbitrário; inconsistência não vira NotLinked |
| NotLinked: SELF Active sem consumo | Próprio identificador/referência C02; preview/aceite de convite próprio provado | Histórico vazio, identidade ativa, ID+segredo+destinatário e estado/prazo no aceite | Sem leitura Tenant, criação automática de empresa, catálogo ou autoatribuição de papel; C09 200 NotLinked proposto |
| SELF consumido/revogado/bloqueado | Resolução mínima de acesso indisponível e encerramento de sessão; consulta de estado só quando contrato autorizar | Estado corrente e histórico | Sem acesso empresarial, novo consumo, aceite/replay protegido ou reativação; preservar 403/inconsistência conforme C09 |
| T: SELF+Account+Organization+Membership ativos | Operações Tenant listadas em C08 e leitura própria | Helper + autorização por ação/alvo; revalidação com locks nas mutações | Member não lista membros/auditoria, não escreve; rota divergente nega; retorno só próprio tenant |
| T Owner/Admin + alvo ainda sem empresa | Conferir referência e emitir convite como Member | Referência C02 válida, alvo conhecido/elegível; prova revalidada no comando | Sem diretório, dados da empresa de origem ou convite a identidade consumida |
| SELF destinatário + REC | Preview/accept por ID exato; replay Accepted somente com direito atual | Código por hash, mesmo destinatário, org ativa, prazo/ETag/transição/histórico | REC não é Membership e não libera SELECT empresarial; sem prova 404 mínimo |
| Platform autenticado + BOOT | Conferência restrita e bootstrap atômico | Scheme Platform/MFA atestados pela aplicação; referência C02 e elegibilidade do Owner, chave C06 | Não lê dados de clientes nem ganha Membership; só comprovante autorizado |
| Validador interno de integridade | Ler fatos técnicos dos IDs/org afetados após DML | Invocação por trigger de tabelas fixas dentro da operação controlada | Não é contexto de usuário, não concede RBAC; sem DTO nem função de consulta externa |
| Recibo C06 | Consulta/replay exato | Ator+escopo+operação+hash e autorização atual | Não repetir mutação ou recuperar segredo; aceitação usa recibo do próprio convite |

Q2 não amplia C01–C10. NotLinked não deve ser globalmente recusado antes de preview/aceite: são precisamente operações de preparação autorizadas sem Membership. Revogado não é NotLinked. Checagem de segredo/referência é feita pela função sobre hash/registro, não aceita um booleano supplied pelo cliente.

### Q3 — Helper, falha fechada e pooling

Helper authz.own_tenant_allowed retorna somente booleano da própria identidade/Account/Organization e ação fixa/permitida (sem nome de tabela ou predicado livre). EXECUTE somente core_tenant/exec_tenant; owner exec_context. Lê IDs/estados, Membership.role apenas para ação conhecida. As policies-base do exec_context usam SELF/IDs instalados sem invocar T/helper: Identity→SELF; Account→SELF; Membership→AccountId contextual; Organization→OrganizationId contextual. O join confirma todos os IDs e estados; falsos IDs não combinados retornam false. Nenhum resultado parcial, contagem, papel de outro usuário ou identificador é retornado. Resolver contexto próprio usa exec_identity e policies-base próprias, nunca passa pelo helper T que pressupõe conta ativa.

Grafo sem ciclo: policy Tenant → helper → policies-base exec_context → igualdade de contexto; policies-base NÃO chamam helper, resolver ou funções de comando. Policies por owner não são herdadas pelo login: NOINHERIT sozinho não basta; negar membership/SET ROLE também. Helper não é IMMUTABLE, LEAKPROOF nem cache global; leitura STABLE no máximo por instrução, sem usá-lo para pular revalidação após locks.

Contexto ausente/vazio/inconsistente nega; erro de conversão aborta a unidade, sem fallback para valor anterior. Aplicação limpa baseline ao checkout, BEGIN, configura GUCs locais completos; campos não aplicáveis explicitamente ausentes. Após COMMIT/ROLLBACK deve voltar a baseline sem dados. Se houver savepoint que reverta configuração, abortar a unidade inteira por padrão; eventual reinstalação só após revalidação completa, nunca continuar automaticamente. Falha/cancelamento no rollback/reset exige descartar conexão física. Não usar session-level tenant, multiplexar requisições na mesma conexão ou devolver conexão ainda em transação. Testes Q: token errado; org forjada; falta/inconsistência de cada ID; NotLinked/revogado; helper sem recursão; SQL direto com papel real; mesmo backend A/B após commit/rollback/savepoint/cancelamento. Nenhum foi executado.

## P0-02 — Validação diferida sem locks tardios

### V1 — Menor correção ao protocolo C1

Adquirir locks no commit poderia inverter C1: uma trigger de Membership poderia tentar bloquear organização depois de a transação já bloquear convite/recurso. **A proposta exclui esse comportamento.** Só as portas de comando adquirem locks explícitos em C1, antes de qualquer DML relevante; triggers diferidas são validadores de leitura do estado final, sem SELECT FOR UPDATE/SHARE, advisory lock, DML reparador ou chamada a porta de comando.

Detalhe obrigatório de C1: chave C06 → identidades ordenadas (modo mais forte antecipado) → organizações ordenadas FOR UPDATE → Accounts ordenadas → Memberships ordenados → slots → convites → alvos. Não colocar consumo inexistente como linha a bloquear: bloquear identity.identities existente. Bootstrap reserva UUIDs e insere a nova Organization após lock da identidade; a própria inserção é proteção exclusiva do novo org ainda não visível. Não há linha prévia para SELECT FOR UPDATE. FKs/UNIQUE continuam adquirindo locks internos: esta correção elimina inversão EXPLÍCITA na trigger, não promete ausência de deadlocks internos.

READ COMMITTED: aquisição/espera e revalidação são instruções separadas. Depois de obter lock da organização, executar nova leitura de Account/Membership/Owner/convite, que vê commits anteriores à nova instrução e as próprias mudanças. Funções de comando/trigger são VOLATILE; não esconder revalidação em função STABLE que preserve snapshot pré-espera. O lock de organização fica até fim da transação e impede outro escritor runtime da mesma empresa de avançar. Isso elimina write skew entre os escritores que seguem o protocolo; consulta diferida sozinha não o elimina.

Runtime não tem DML direto nessas tabelas, não pode chamar validators manualmente, desabilitar triggers, alterar replication role ou virar owner. Todas as funções gravadoras seguem C1 e chamam validators apenas pelo mecanismo de trigger. Não usar GUC protocol_ok como prova de lock nem alegar que pg_locks demonstra ownership de todos os locks de linha. A garantia depende da cobertura de todas as portas gravadoras e dos grants; migrator que desative triggers/escreva fora do protocolo está fora dela e exige manutenção segregada.

### V2 — Matriz de triggers candidatas

| Trigger / eventos | Momento e conjunto consultado | Locks / ordem / concorrência | Falha e provas futuras |
|---|---|---|---|
| V-ACCOUNT-MEMBER: Account AFTER INSERT/UPDATE/DELETE e Membership AFTER INSERT/UPDATE/DELETE, FOR EACH ROW, CONSTRAINT DEFERRABLE INITIALLY DEFERRED | No commit, reler estado final dos AccountIds de OLD/NEW: Account Active exige exatamente um membro Active mesmo org; membro Active exige Account Active; Account Revoked/Archived exige membro não Active | Nenhum lock novo na trigger. Porta já detém org, Accounts e Memberships C1. Account recém-criada/membro novo protegidos pela transação/UNIQUE; validar referências em FK | Abortar commit se inválido, sem corrigir. Provar conta sem membro, membro cruzado/Active com conta revogada, delete de membro e revogação parcial; criação/revogação completas passam |
| V-ORG-OWNER: Organization AFTER INSERT/UPDATE/DELETE, Account AFTER INSERT/UPDATE/DELETE, Membership AFTER INSERT/UPDATE/DELETE; constraint row-level diferida | Para org afetada OLD/NEW, reler estado final; se Organization Active exigir pelo menos um Member role Owner Active com Account Active mesma org. Examinar role/status/account/org, não só mudança direta de Membership | Nenhum lock novo; escritor detém org antes de qualquer mudança. Validador lê todos os fatos de integridade dessa org, não apenas linhas visíveis ao ator | Provar bootstrap sem Owner, Account Owner revogada, último Owner removido e dois rebaixamentos concorrentes; C08 continua negando qualquer alteração de Owner nesta sprint |
| V-IMMUTABLE: Account e Membership BEFORE UPDATE; IdentityLink BEFORE UPDATE; Organization BEFORE UPDATE; Consumption BEFORE UPDATE/DELETE | Imediato por linha: bloquear mudança de IDs/OrganizationId/IdentityId/AccountId do vínculo, par OIDC e reassociação; consumo nunca editável/removível no runtime | Apenas OLD/NEW, sem consulta, lock extra ou reparo. Atualizações de estado/versão permitidas somente pelos comandos aprovados | Provar mudança de org/conta/identidade negada, consumo não reciclável; mudar status não remove marca. Migração IdP cria alias por procedimento futuro, não reatribui link |
| V-DELETE-GUARD: Account/Membership/IdentityLink/Consumption/Organization BEFORE DELETE | Imediato: proibir DELETE no caminho runtime; ledger permanece. FKs RESTRICT e grants são defesa adicional | Sem busca/lock tardio; exceção de manutenção não pode ser flag alterável no contexto web | Provar DELETE negado por login runtime/executor. Expurgo privilegiado só procedimento H fora da sprint |
| Validação de FKs/UNIQUE/checks M2 | Momento definido pela constraint; FK consumo diferida, demais conforme M2 | Locks internos PostgreSQL não são substituídos por validators; violações abortam operação/commit | Provar consumo duplicado, Account→consumo errado e FK cruzada, inclusive no COMMIT |

Os eventos são registrados sem condição WHEN dependente do estado intermediário que suprima uma validação necessária. Reexecuções por linha podem repetir a mesma consulta: otimização/deduplicação só após prova; não usar transition tables como se fossem suportadas por toda constraint trigger. NEW de um evento antigo não representa estado final: reler tabela no disparo. SET CONSTRAINTS antecipado pode recusar estado intermediário; portas não o usam como forma de permitir inconsistência e nunca capturam erro para continuar. Runtime não recebe mecanismo de desabilitar constraints.

V-IMMUTABLE: Organization pode mudar estado/nome por futuro comando autorizado, não seu ID. Membership mantém org/account/id; Account mantém identidade/org/id; IdentityLink mantém issuer/subject/identity_id; histórico mantém todas as colunas de vínculo. DELETE de Identity com consumo é impedido por FK RESTRICT; referências protegidas não usam cascade.

### V3 — Visibilidade de integridade e efeitos de falha

Validators rodam em função SECURITY DEFINER sob exec_integrity, owner distinto das tabelas, NOBYPASSRLS/NOLOGIN. Esclarecimento V08 desta rodada: contexto técnico local à transação contém operation_id/tipo, identificação da transação obtida do servidor, organization_id afetada e âncoras/IDs esperados derivados da porta. É instalado depois das provas/locks C1 necessários e antes do DML relevante, mantido após retorno da função até COMMIT/ROLLBACK e confrontado com a transação atual e OLD/NEW de cada evento. Não é T, SELF, REC ou BOOT e não atesta autenticação/posse de lock; permanece no limite de confiança Q1. Leem somente IDs/status/role e referências de Organization, Account, Membership e Consumption afetados, via policies-base por igualdade de organization_id ao escopo técnico válido, cobrindo TODOS os fatos técnicos da organização afetada, sem filtro por ator, estado Active ou lista externa de membros; sem helper T/SELF empresarial ativo. A conta/membro/Owner esperados são verificados positivamente: Organization e Accounts âncora devem existir; Account Active exige exatamente um membro Active coerente; Membership Active exige Account Active; org Active exige Owner válido no conjunto completo. Ausência inesperada/contexto inválido/erro RLS aborta, não aprova por conjunto vazio. Contexto, âncoras, oracle e provas estão detalhados em 3.8/V08 do [plano de validação](PLANO_VALIDACAO_TECNICA_ADR_0009.md); essa remissão não autoriza execução. Assim não confundem linhas ocultas após revogação com ausência real e não exigem Membership do Operator no bootstrap. Escopo técnico é instalado pela porta restrita após autorizar/validar o alvo e permanece até commit; não é concessão empresarial nem parâmetro externo. Se escopo estiver ausente/divergente com OLD/NEW, falhar e abortar, nunca aprovar por conjunto vazio. Runtime sem EXECUTE direto; validator retorna apenas sucesso ou erro sanitizado de invariante, nenhum DTO. O papel não tem UPDATE para locks nem SELECT de perfil/DemoRecord/segredos.

Todas as consultas do validador são leituras frescas VOLATILE, com locks de org já mantidos; funções de autorização base não filtram fatos de integridade. A porta mantém contexto de validação até COMMIT e não libera org em savepoint; rollback até antes dos locks exige abortar unidade. Um comando por transação de aplicação; lote/múltiplos tenants sem plano C1 completo não autorizado. Sem isso, o desenho perde a prova de serialização e é P0.

Revogação modifica Account e Membership antes do commit, mantendo histórico e auditando no mesmo commit; não se considera sucesso após primeiro UPDATE/SaveChanges. Resposta só depois de confirmação do commit e validação diferida. Violação diferida aborta toda mutação/auditoria/recibo; tratar como invariante/defeito, não 412 automático. Deadlock 40P01 ou serialização 40001: rollback completo e retry limitado segundo C3, refazendo autenticação válida/autorização/locks; não retry só do COMMIT. Conexão perdida durante commit: resultado desconhecido K07/K08, reconciliar C06, nunca anunciar rollback sem prova.

Provas adicionais V: dois escritores em ambos os ordenamentos com barreiras; esperar lock org e confirmar leitura de estado pós-espera; logs de locks mostram validator sem aquisição explícita tardia; grants impedem DML fora de porta; trigger ainda vê Owner/membro revogados e org nova do bootstrap; falha diferida remove evento/recibo; savepoint não deixa locks/contexto incompletos. Usar duas conexões e logins reais, não owner/migrator. São especificações de testes futuros, não testes criados/executados.

## P0-03 — Fronteiras das funções privilegiadas

### F1 — Regras comuns e superfície de grants

Nomes abaixo são candidatos internos, não endpoints nem novas APIs Foundation. Sem assinatura executável de SQL. Cada função tem lista fixa de campos e operação; nenhuma aceita SQL, tabela, esquema, função, filtro livre, actor_id empresarial arbitrário ou ignoreTenant. Credencial SQL escolhe porta, não comprova usuário/scheme/MFA; vale Q1. Função não abre conexão privilegiada nem commita: participa da transação da Application; mutações são VOLATILE. Sem chamadas ao IdP/produto dentro da transação.

Regras para TODAS: owner NOLOGIN sem ownership/BYPASSRLS; schema privado sem CREATE runtime; search_path fixo pg_catalog + schemas confiáveis + pg_temp por último, nomes de objetos/tipos/operadores qualificados quando necessário; nenhuma dependência de objeto temporário; REVOKE EXECUTE PUBLIC para assinaturas exatas e default privileges seguros, sem overload não revisado. Grants de EXECUTE nominalmente ao login/owner necessário, sem herança/SET ROLE lateral. SQL dinâmico desnecessário proibido; parâmetros de dados nunca viram identificadores. ENABLE/FORCE RLS; policies por executor com alcance definido abaixo, sem policy universal true. Owner de tabelas e migrator separados dos owners de funções.

R3 é resumo; a matriz F2 fixa o alcance por operação. Esses campos/ações são limites exigidos do corpo/retorno de cada função, não grants SQL exclusivos por função. O owner compartilhado possui a união dos grants/policies/arestas dos seus comandos. A matriz 3.9/F10/D2 do [plano](PLANO_VALIDACAO_TECNICA_ADR_0009.md) documenta essa união e registra D2 aprovada para manter os grupos existentes, com comprovação dos grants efetivos e grafo ainda pendente antes de liberar G1; separação adicional somente por risco concreto não contido. Nenhuma ACL real foi inspecionada/aprovada nesta revisão. SELECT interno inclui apenas colunas enumeradas por conjunto: identidade=(IDs, issuer/subject no mapeamento, status); vínculo=(IDs,status,role,version); convite=(IDs,recipient/issuer,status,hash,expires,version,predecessor/successor); referência=(dono,hash,expiração); recibo=(ator,scope,operação,hash,IDs/resultado mínimo,expiração). UPDATE para lock, quando requerido, limitado a colunas técnicas e não exposto ao login. Nunca conceder acesso a perfil/segredo só para poder bloquear uma linha.

### F2 — Matriz função × executor × privilégio × prova

| Função candidata / quem executa → owner | Leitura/gravação fixa | Prova / contexto / RLS | Resultado e limite |
|---|---|---|---|
| resolve_own_context: core_identity → exec_identity | SELECT identidade/link, consumo e próprio Account/Membership/Organization (IDs/estados/nome/permissões autorizadas) | SELF atestado pela aplicação; policies-base SELF e join próprio, sem T recursivo; sem Membership pode consultar só próprio estado | Linked/NotLinked/indisponível/inconsistente C09; nenhum vínculo alheio; GET não grava |
| prepare_own_identity / own_recipient_reference: core_identity → exec_identity | Preparação explícita: INSERT Identity/Link próprio se comprovadamente novo; referência: INSERT hash/expiração própria e SELECT ID | OIDC validado; unicidade do par, sem linking por e-mail; referência somente nunca consumido e Active | Próprio ID/referência uma vez C02; não cria conta/membro; ausente prova nega |
| check_recipient_reference: core_tenant ou core_platform → exec_tenant ou exec_bootstrap, funções/assinaturas separadas | SELECT identidade alvo, referência hash/validade, existência de consumo; locks só se parte de comando | T Owner/Admin ou Platform/MFA da aplicação; ID+referência real C02, alvo Active sem consumo | Confirmação mínima/Recipient.Unavailable; sem empresa de origem, perfil ou enumeração. Não concede leitura de conta do alvo |
| issue_invitation: core_tenant → exec_tenant | SELECT vínculo ator/alvo/referência e recibo; INSERT slot/convite/recibo/evento; UPDATE expiração materializada/version de convite exato | T+C08, referência elegível; C1; revalida depois dos locks; scoped RLS do alvo C02, nunca booleano de elegibilidade do cliente | ID/metadados/versão + código inicial uma vez; hash só no banco; negativa mínima; sem NotLinked como ator empresarial |
| preview_own_invitation: core_identity → exec_identity | SELECT convite ID exato, hash/recipient/status/prazo, nome/ID da org após prova, histórico próprio | SELF Active, ID+código provado; REC não instala T, pode ser NotLinked | C04 preview sem efeito, sem dados emissor; inválido/destinatário errado 404; inelegível após prova 403; GET/callback não aceita |
| accept_own_invitation: core_identity → exec_identity | SELECT/lock próprio ID, org/slot/convite, consumo; INSERT consumo/Account/Membership Member/evento; UPDATE convite Accepted/version | SELF+REC, C1 completo, prazo/ETag/estado/elegibilidade histórica; policies de INSERT/UPDATE só IDs do convite provado; validators V ao commit | C04/C06 IDs do aceite; replay só se mesmo destinatário/código e direito atual; sem nova conta, Owner ou retorno Tenant genérico |
| cancel_or_replace_invitation: core_tenant → exec_tenant, funções separadas | Ler/lock vínculo, slot/convites/recibo; UPDATE estado/version old; replace INSERT new e recibo; evento | T Owner/Admin, C1, C04/C05; chave C06 no replace; alvo org próprio | Metadados; novo código só primeiro replace; antigo inválido; conflito sem revelar sucessor a destinatário indevido |
| bootstrap_organization_owner: core_platform → exec_bootstrap | SELECT identidade/ref/consumo alvo; INSERT nova Organization/consumo/Account/Owner/eventos/recibo | Platform scheme/MFA da aplicação + referência C02 + alvo Active nunca consumido; chave C06; C1; validators V sem exigir T do Operator | Comprovante IDs/operação C09, não lista empresas nem dados Tenant; rollback integral; Operator sem Membership |
| change_membership / revoke_membership: core_tenant → exec_tenant, funções separadas | Ler/lock vínculo ator e alvo; UPDATE role/version autorizado, ou status/version de Membership+Account; evento | T+C08 e ETag; C1 antes de DML; validar alvo outro Member/Admin conforme papel; Owner/si próprio negados | DTO mínimo autorizado/versão; sem IdentityId/org editável; revogação atômica sem ativação implícita; sem NotLinked |
| create_or_update_demo_record: core_tenant → exec_tenant, funções separadas | SELECT vínculo; INSERT/UPDATE título/versão do próprio DemoRecord; recibo no create e evento | T Owner/Admin, C1, C05/C06; WITH CHECK org derivado, sem transferência | Próprio item/versão; Member só lê por SELECT RLS; nenhuma leitura por core_identity/core_platform |
| append_operation_audit / complete_or_read_receipt: somente owners de comandos compatíveis → mesmo owner do comando ou helper privado de mesmo alcance | INSERT evento e INSERT/SELECT/UPDATE técnico de recibo exato; sem UPDATE/DELETE evento | Escopo/ator/operação já validados dentro do comando; policies locais fixas; não EXECUTE de login externo, sem audit API genérica; replay autoriza antes de retornar | Nenhum dataset; IDs/resultado mínimo C06; sem segredo; falha desfaz comando. Validador não chama esses helpers |
| own_tenant_allowed: core_tenant/exec_tenant → exec_context | SELECT técnico próprio Q3 | Policies-base não T, saída booleana; sem DML/locks | True/false, nunca DTO/lista, sem conferir Membership arbitrário |
| validate_account_membership / validate_org_owner / guard_immutable: invocação por trigger → exec_integrity para consultas; guard local sem privilégios adicionais | SELECT final restrito V2/V3 ou OLD/NEW local | Sem EXECUTE externo; fatos org/IDs afetados, policies de integridade não T; sem locks tardios/DML | Apenas aprovação interna/erro de invariante; não autoriza usuário ou bootstrap por si só |

Acesso a org/slot/IDs técnicos do aceite não é concessão de SELECT geral de tenant: exec_identity só os alcança por porta de ID provado, e não tem SELECT DemoRecord/auditoria empresarial/lista de membros. A operação interna deve provar código/destinatário antes de revelar metadados; depois segue locks C1 e refaz prova/estado. A consulta inicial necessária à descoberta de IDs é restrita e não devolve resultado ao chamador até prova completa.

Contextos internos de alvo e validação (REC/BOOT/org afetada) são derivados/rechecados dentro da porta e não são credenciais autoautenticadas; GUCs auxiliares podem transportar IDs mas não substituir prova em variáveis locais e consultas aos hashes. Login sem EXECUTE adequado não alcança o caminho. Execução aninhada não deve trocar owner para função mais ampla: grants só na cadeia explicitamente necessária; analisar grafo de chamadas, RLS e current_user por função. Nenhum owner é membro de outro executor ou migrator.

### F3 — Verificação cruzada e riscos remanescentes

| Classificação | Situação e tratamento |
|---|---|
| P0 documental tratado por proposta | Confiança Q1 explícita, sem autenticação SQL fictícia; V1 sem locks tardios e V3 com visibilidade final; matriz F2 substitui grants vagos. Sujeito a revisão humana, não evidência de implementação |
| P0 antes de afirmar segurança em runtime | DDL/grants/funções/triggers concretos ainda inexistentes: demonstrar testes Q/V/F com papéis reais, cobertura de todos escritores, não recursão, snapshot pós-espera, rollback/commit e K01–K10. Não há liberação de implementação por este texto |
| P1 risco residual explícito na fronteira D1 aprovada | Processo/credencial comprometido pode forjar SELF e scheme atestado; policies não fornecem autenticação independente. D1 aprova Application como fronteira confiável, sem alegação de isolamento comprovado; provas permanecem pendentes. Não reabrir sem contradição P0 concreta |
| P1 | Contenção do lock org, deadlocks internos/FKs, migração/restauração, ledger/privacidade, janelas de revogação entre produtos e gestão de segredos continuam pendentes nos gates anteriores |
| Melhoria futura | Medir e reduzir locks só após evidência; deduplicar consultas de triggers sem perder validação; não ampliar MVP com protocolo próprio, console completo ou serviço Foundation |

Compatibilidade: I01/I08 usam resolve; I02 usa T/filtros/RLS e não autoridade de org solicitada; I03–I06 usam prova+histórico+C1; I07/I11 preservam OIDC/identidade sem fusão; I09 usa BOOT sem acesso Tenant; I10 usa revogação atômica; I12 não admite fallback do IdP. K01–K10 e C01–C10 mantêm resultados/ETags/idempotência; nenhuma transferência/reativação/segundo vínculo foi introduzida. Sem mudança de Foundation. Nenhuma divergência impeditiva foi identificada nos documentos de referência fora do escopo; mecanismos candidatos aqui não aprovam ADR-0008 nem alteram seu texto.

### F4 — Aprovação documental e evidência futura

G0 documental aprovado com condicionantes. D1 aprovou a fronteira Q1 e D2 os cinco grupos de owners; grants efetivos/grafo continuam sem comprovação e D3/D5 pendentes. G1 permanece bloqueado, sem aprovação automática dos demais protocolos V/F. Não afirmar garantias físicas antes da execução autorizada das provas.

Decisões já aprovadas: Application como fronteira confiável (D1) e manutenção dos cinco grupos de owners, incluindo helper/validator distintos (D2). Pendências reais: comprovar grants efetivos/grafo antes de G1; preparar/validar D3/D5; demais protocolos/autorizações já pendentes permanecem, sem reabrir decisões aprovadas. Validators sem locks tardios, C1, revalidação pós-espera e cobertura dos escritores continuam exigências das provas, não evidência obtida. Demais decisões P continuam vigentes, sem fornecedor/versão/tempo aprovado.

Fontes adicionais dos mecanismos: [constraint triggers](https://www.postgresql.org/docs/current/sql-createtrigger.html), [READ COMMITTED](https://www.postgresql.org/docs/current/transaction-iso.html), [snapshots e volatilidade](https://www.postgresql.org/docs/current/xfunc-volatility.html). São referências técnicas; Q/V/F são desenho proposto do Core. Nenhum teste PostgreSQL/RLS/concorrência foi executado.

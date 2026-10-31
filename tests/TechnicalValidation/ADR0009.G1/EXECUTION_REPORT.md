# ADR-0009 / G1 — Fechamento dos P1 e candidata D2 não circular

## Aceite humano definitivo — D2 e G1 APROVADOS — 2026-10-30

Data do registro: **2026-10-30** (America/Sao_Paulo). Fonte de autoridade: mensagem expressa do responsável nesta sessão — “o responsável pelo Business Platform Core AUTORIZOU EXPRESSAMENTE o aceite humano definitivo de D2 e G1 da ADR-0009” — que determina o encerramento documental com a baseline e a execução complementar abaixo. Trata-se de aceite humano comunicado, não aprovação emitida pelo executor.

- **Baseline técnica:** `9ba11b81e986`. **Execução complementar:** `ece79aa930df`.
- **D2 APROVADA:** candidata revisada de **178 privilégios de coluna**, conforme inventário, justificativas, grafo e revisão semântica documentados neste relatório.
- **D3 e D5 RATIFICADAS:** aprovações anteriores mantidas, sem ampliação de escopo ou privilégios.
- **G1 APROVADO:** exclusivamente no escopo das evidências técnicas exigidas para este gate, conforme a matriz de cobertura abaixo.

Evidências do aceite: matriz final de evidências G1 e revisão semântica neste relatório; `evidence/runs/9ba11b81e986/` e `evidence/runs/ece79aa930df/`, incluindo results/assertions, catálogo, comparação de privilégios, expectativa congelada, grafo e hashes de fontes. O responsável aprovou expressamente inventário, justificativas e grafo da candidata de 178 privilégios. D2 deixa de ser candidata pendente no estado decisório vigente; seus arquivos congelados e evidências anteriores conservam os rótulos originais para rastreabilidade.

A aprovação não equivale à conclusão integral dos 54 protocolos e não autoriza antecipar G3/G4. **V11 estrutural permanece em G3; Application/API/OIDC e recuperação permanecem em G4.** Não há autorização automática para merge, deploy, produção ou início da Sprint 1. Gate UX não é alterado por esta deliberação.

Este é o estado decisório vigente. As referências abaixo a aceite humano pendente ou candidata ainda não aprovada descrevem o momento anterior à deliberação e permanecem como histórico. Os resultados e status brutos dos runs não foram reclassificados nem sobrescritos; a aprovação humana do recorte G1 não transforma protocolos multigate em PASS integral ou em revisão independente executada.

Nesta atualização houve somente encerramento documental: este relatório, o estado corrente do README e o ponteiro `manifests/security.latest-observation.json` foram sincronizados. Nenhum teste reexecutado, evidência histórica sobrescrita, expectativa congelada alterada, ADR/contrato modificado ou próxima etapa iniciada. Para conferência da tarefa G1 existente no monday.com: **G1 aprovado por aceite humano definitivo no escopo documentado**, sem atualização remota ou criação de tarefa.

### Git e versionamento conferidos no encerramento

O pacote está integralmente não rastreado (`??`): **272 arquivos**, nenhum staged e nenhuma diferença em arquivos rastreados. Esta rodada altera apenas os três registros documentais indicados acima; os demais arquivos são trabalho local preexistente.

- **Código/harness a versionar:** 12 arquivos em `runtime/`, `harness/` e `sql/`, incluindo fontes .NET, SQL exclusivo de ensaio, scripts de coleta/comparação e parâmetros do harness.
- **Evidências a versionar:** 248 arquivos de `evidence/runs/` e `manifests/history/`, preservando baselines, FAILs históricos, execuções complementares, snapshots e hashes; não excluir evidências anteriores ao aceite.
- **Documentação e especificações de prova a versionar:** 12 arquivos restantes do pacote: relatório, README, exemplo de ambiente, manifestos correntes, fixtures sintéticas, protocolos/oracles e template de evidência. Isso inclui o ponteiro do aceite, separado da expectativa pré-observação imutável.
- **Não incluir:** 30 artefatos gerados já ignorados em `runtime/bin/` (8) e `runtime/obj/` (22), credenciais/segredos ou configuração real de ambiente. Nenhum arquivo foi adicionado ao index ou removido. Arquivos locais fora deste pacote ficam fora deste encerramento e não devem ser incluídos por um `git add` indiscriminado.

**Próximo gate previsto no plano: G2 — banco, papéis e invariantes**, não iniciado por este aceite. Depende de autorização própria para a próxima execução, alvo isolado autorizado, fixtures/probes aplicáveis aprovados e revisão das evidências determinísticas E-DB/E-ACL/E-POOL, reutilizando o que já é válido e executando somente lacunas. G1 aprovado satisfaz seu pré-requisito, mas não declara G2 concluído. G3 exige G2 sem P0 e parâmetros/probes D4/D5 aplicáveis; V11 estrutural não é antecipado. Liberação da Sprint 1 permanece inalterada.

## Histórico — fechamento técnico G1 — 2026-10-30

**Parecer técnico: PRONTO PARA ACEITE HUMANO, exclusivamente para o recorte G1 do harness atual.** O aceite humano de G1 e da candidata D2 não é emitido pelo executor. Não há impedimento P0/P1 comprovado ainda aberto nesse recorte após a redução de quatro grants descrita abaixo. A validação global dos 54 protocolos, G2/G3/G4, Gate UX e Sprint 1 não estão aprovados por este parecer.

Esta seção registra o parecer técnico anterior à deliberação humana acima; as seções seguintes e os runs anteriores são histórico preservado. Ela substituiu, para aquela execução, as indicações históricas de D3/D5 pendentes e de falta de evidências G1. As ADRs e o plano original não foram alterados.

### Decisões humanas registradas e limites

| Decisão | Estado comunicado pelo responsável | Aplicação nesta rodada |
|---|---|---|
| D1 | Aprovada anteriormente; Application é a fronteira confiável | Mantida. PostgreSQL aplica RLS ao contexto recebido, sem autenticar OIDC. Credencial SQL comprometida com contexto integralmente forjado continua risco residual aprovado; não é prova de isolamento pela Application. |
| D2 | Cinco grupos aprovados; candidata aceita para execução, aceite definitivo condicionado | Revisão semântica pontual dos corpos, união de capacidades, grafo e ddl_owner realizada pelo executor; expectativa reduzida e congelada antes da nova observação. Aceite independente/humano continua pendente. |
| D3 | **APROVADA nesta autorização** | SQLSTATE nativo com diagnóstico específico; códigos internos explícitos dos guards/validators. Erro de ACL, integridade, versão e infraestrutura não são intercambiáveis. Não forçar 23502 quando RLS/ACL rejeita antes. |
| D5 | **APROVADA nesta autorização, somente no harness descartável existente** | Mesmos papéis/probe/observer, sem bypass ou aumento de privilégios. Duas instâncias locais descartáveis, imagem já fixada, tmpfs e cleanup por identificador/label. A observabilidade adicional track_functions=all não concede privilégios. Não autoriza probes futuros de recuperação/produção. |
| V11 estrutural | **Permanece G3 por decisão humana** | Não executado nesta rodada; a negativa histórica de revogação de Owner não foi promovida a prova integral de write skew/zero Owner. |
| Application/API/OIDC e recuperação | **G4** | Não implementados, executados ou considerados aprovados. Não impedem a avaliação do escopo G1 atual. |

### Execuções e reaproveitamento

- **B = 9ba11b81e986**, baseline preservada byte a byte: 29 subprovas históricas PASS e 431 registros de assertions/observações. Os protocolos completos continuam com seus estados originais (25 BLOQUEADO, 29 NOT_EXECUTED).
- **S = 8db7a67ce01b**, ensaio complementar antes da redução: 8 subprovas PASS, 573 registros. Demonstra accept/replay, update_demo, revoke e replace com quatro SELECTs retirados somente dentro de transações revertidas. Usa SET LOCAL SESSION AUTHORIZATION a partir do operador exclusivamente nessa subprova, explicitamente distinto de login runtime. As demais negativas usam logins reais. Mantido como evidência histórica.
- **E = ece79aa930df**, execução final após congelamento da candidata reduzida: **9 subprovas PASS, 592 registros**, nenhum FAIL ou stop P0. Realiza as negativas faltantes e regressões das portas afetadas com logins reais core_identity/core_tenant; bootstrap de duas organizações usa core_platform real. Inventário observado: 12 tabelas, 11 funções, 40 policies, 78 triggers incluindo internos e 63 constraints.
- Os 54 identificadores foram preservados. Em E, `results.json` mantém **9 protocolos BLOQUEADO e 45 NOT_EXECUTED**, pois registra protocolos completos, não o aceite restrito de G1. Não se somam subprovas a cenários nem se transforma cobertura parcial em PASS integral. O campo automático `gate=BLOCKED_PENDING_HUMAN_REVIEW` é mantido: somente o responsável pode aprovar o gate.
- `001-test-only.sql`, `002-test-ports.sql`, `003-test-replace.sql`, projeto do harness e parâmetros operacionais têm os mesmos hashes de B. Nenhum guard de Owner/convite, policy, constraint ou corpo SQL foi reaberto. Somente o instalador final de grants, sua intenção normativa e o harness de provas foram ajustados.

Evidências relativas a este relatório: `evidence/runs/<run>/results.json`, `assertions.json`, `catalog.jsonl`, `source-hashes.json`, `environment.json` e cronologia em results. E acrescenta `security.expected.snapshot.json`, `security.observed.json`, `security.comparison.json`, `graph.observed.json`, `comparison-sensitivity.json`, `semantic-subtraction.json`, `function-stats.json` e `post-rollback-privileges.json`. `expectation-freeze.json` e `preflight.expected.json` registram a candidata anterior à instalação/observação. A cópia JSON normalizada de preflight tem hash próprio, diferente do arquivo-fonte por serialização, não por alteração de capacidades.

### D2 — achado concreto e correção mínima

A revisão encontrou **quatro SELECTs sem necessidade nos corpos instalados**. A justificativa anterior do exec_identity tratava o validator de cadeia como INVOKER, mas ele efetivamente é SECURITY DEFINER de exec_integrity. No exec_tenant, autorização lê a conta sob exec_context; a verificação de accepted_account ocorre somente no caminho de aceite sob exec_identity. Logo esses quatro grants não satisfaziam o critério de necessidade de F10; não bastava o snapshot concordar com a expectativa anterior.

| Owner | Capacidade retirada | Evidência da desnecessidade | Correção |
|---|---|---|---|
| exec_identity | SELECT invitations.predecessor_id e successor_id | Corpos de accept/replay não projetam esses campos; validator usa exec_integrity. S/F10 executou aceite/replay e validação com revogação transacional; E repetiu sob os grants mínimos. | Retirar dois SELECTs da intenção e do instalador final. |
| exec_tenant | SELECT enterprise_user_accounts.organization_id e status | update/revoke usam id/identity_id/version para locks/versão; helper usa exec_context e guards usam OLD/NEW. S/F10 exercitou as três portas sem essas leituras; E/F05/F11 confirmou as portas/replay com logins reais. | Retirar dois SELECTs; preservar UPDATE(status,version) legítimo e os guards. |

Não houve demonstração de escalada ou vazamento entre organizações por esses quatro grants. A deficiência era concreta na candidata de menor privilégio e foi eliminada, sem nova ampliação de hardening. O histórico da expectativa anterior foi preservado em `manifests/history/baseline-33fbf35732987457db0a95704bfe9c8a2c9737577fdd982b1c4bdc2fc6bcfdca.json`; S preserva a observação anterior. `freeze_candidate.py` continua derivando a intenção manualmente das operações/contratos, sem ler o observado para preencher a expectativa. A candidata foi congelada antes de E.

### D2 — matriz semântica final

Contagem abaixo = capacidades por coluna e operação, não número de colunas distintas. Todos os cinco grupos são NOLOGIN/NOSUPERUSER/NOBYPASSRLS/NOINHERIT, sem ownership de tabela, CREATE ou associação a outro papel. Nenhum recebeu grant de tabela inteira. As justificativas individuais permanecem em `manifests/security.expected.json`.

| Owner SQL → funções reais | Grants finais / operações necessárias | Capacidades compartilhadas e conclusão semântica |
|---|---|---|
| exec_identity → accept_invitation | **46**: prova hash/destinatário/expiração/versão; locks Identity/Org/Slot; INSERT consumo/Account/Member/auditoria; UPDATE estado/conta/versão do convite; replay próprio. | Uma porta instalada. Sem INSERT convite, SELECT predecessor/successor ou leitura Tenant geral. Validator técnico pertence a outro owner. Não encontrada capacidade remanescente sem justificativa no corpo/cadeia atual. |
| exec_tenant → update_demo, revoke_membership, replace_invitation | **66**: locks e predicados C1; UPDATE Demo e Account/Member.status/version; consulta de papel; INSERT novo Pending/Slot/auditoria/recibo; transição/sucessor de convite. | As três funções compartilham a união: Demo pode tecnicamente usar privilégios de revogação/replace se seu corpo fosse alterado. Corpos fixos, checagem session_user+helper, revalidação após locks, RLS e guards contêm os caminhos existentes. Sem UPDATE role, SELECT hash, INSERT Account/Owner ou DELETE. Compartilhamento não equivale a segregação SQL, mas não foi encontrada aresta lateral explorável no desenho instalado. |
| exec_bootstrap → bootstrap | **39**: prova de referência e consumo; lock Identity; INSERT Org/Account/Owner/ledger/auditoria/recibo; leitura do resultado idempotente. | Escrita administrativa intencional de criação, acessível somente por core_platform com referência. Não tem SELECT/UPDATE Account/Member ou DML Tenant geral. Autenticação/MFA Platform permanece fronteira Application futura. |
| exec_context → own_tenant_allowed | **12**, somente SELECT mínimo em Identity/Account/Member/Org. | Retorna booleano; policies-base não chamam novamente o helper. Nenhum writer, reparo ou privilégio de integridade agregado. |
| exec_integrity → validate_integrity, validate_invitation_chain; guard_immutable e guard_invitation | **15**, somente SELECT técnico em Org/Account/Member/Invite. | Os dois validators DEFINER compartilham leitura técnica da organização/tx, independente do ator, sem DML/locks explícitos. Os dois guards são **INVOKER**: ownership não lhes transfere o SELECT de exec_integrity durante o disparo. A chamada condicional do guard de convite ao helper ocorre somente no ramo exec_tenant. |
| ddl_owner → ctx INVOKER; schemas e 12 tabelas | Ownership intrínseco permite DDL/alterar RLS/triggers e administrar objetos; não é contado nos 178 grants dos cinco executores. | Poder administrativo intencional e confiável, não proteção fornecida por FORCE RLS. NOLOGIN, sem memberships, sem SET ROLE concedido aos runtimes; ctx é INVOKER e apenas converte GUC em UUID. Nenhum caminho runtime demonstrado até DDL/ownership. Proteção contra comprometimento do operador de instalação/DDL não é alegada. |

Total: **178 capacidades**, antes 182; exec_identity 48→46, exec_tenant 68→66 e outros grupos inalterados. `security.comparison.json` de E confirma igualdade exata de coluna/EXECUTE/schema/ownership/modos/atributos para os cinco owners; nenhuma associação herdada, PUBLIC EXECUTE, EXECUTE runtime inesperado ou violação de RLS/FORCE/ownership. O campo `additionalObservedTables=[identity.identity_links]` é diferença **de inventário**, não grant excedente: a lista `tables` da expectativa deriva das relações com capacidades dos cinco grupos e não inclui essa tabela de fixture sem grants para eles. Ela está no inventário completo observado, com ddl_owner, RLS/FORCE e guard; F01 nega acesso dos três runtimes. Não é normalizada para dentro da allowlist a partir do snapshot.

Grafo revisado contra os corpos reais e as dependências implícitas (168 arestas extraídas, incluindo referências de relação e triggers internos):

- core_platform→bootstrap→ctx; core_identity→accept_invitation→ctx; core_tenant→update_demo/revoke_membership/replace_invitation→ctx e own_tenant_allowed→ctx.
- DML→guards INVOKER no contexto do writer; os writes em Account/Member/Org disparam validate_integrity DEFINER exec_integrity; writes em Invite disparam guard_invitation INVOKER e validate_invitation_chain DEFINER exec_integrity.
- Policies tenant_read chamam o helper DEFINER exec_context; as policies-base desse owner só usam ctx INVOKER, sem ciclo. Policies de integridade filtram por organização técnica, sem helper empresarial. A chamada lexical do guard ao helper é condicional; não representa EXECUTE irrestrito para exec_identity.
- System calls são fixas (GUC local, UUID/tempo/hash/conversão/JSON, pg_current_xact_id); nenhum corpo de negócio usa SQL dinâmico, nome de objeto recebido do usuário ou chamada a função de outro writer. Os DO/EXECUTE do instalador de grants não pertencem à superfície runtime.
- Ownership, triggers/FKs e policies não são omitidos só porque não aparecem como chamada PL/pgSQL explícita. `graph.observed.json` continua com aprovação independente pendente; a análise semântica aqui é do executor e **não** declaração de revisão independente concluída.

Recomendação: manter os cinco owners com a candidata reduzida. Não há razão concreta demonstrada para novo split. O aceite humano deve abranger a união intencional de exec_tenant, a leitura técnica compartilhada de exec_integrity e ddl_owner como autoridade DDL segregada; o residual D1 já aprovado é preservado, não reaberto.

### Matriz final de evidências G1

Nesta matriz, **ATENDIDO G1** significa evidência suficiente do escopo instalado para submissão humana. Não significa PASS integral do cenário multigate; **REVISÃO** é a análise documental/semântica realizada, não teste de API.

| Requisito | Evidência verificável | Resultado G1 / eventual bloqueio |
|---|---|---|
| D3/D5 | Decisão humana desta autorização registrada acima; códigos/constraints/corpos no catálogo E; SQLSTATE, rotina, objeto, papel e comando em assertions. | ATENDIDO no harness autorizado; sem ampliação de probe ou garantia de implantação futura. |
| F01 — grants efetivos | E/security.comparison, F01 matriz de 3 logins × 12 tabelas × SELECT/INSERT/UPDATE/DELETE; SELECT legítimo retorna apenas um canário próprio; outros DMLs negados sem persistência. | ATENDIDO G1 técnico; candidata aguarda aceite D2. |
| F02 — ownership/bypass | E/catalog: 12 RLS+FORCE ddl_owner; atributos de roles; F02 negativas reais de TRUNCATE, ALTER RLS, desativar trigger e transferência de ownership; F04 nega ddl_owner/migrator. | ATENDIDO G1 no alvo descartável. Segregação de credenciais de uma futura implantação é G4/operacional. |
| F03 — EXECUTE/default ACL | E/F03 tenta todas as assinaturas privadas sem EXECUTE por cada runtime, com tipos explícitos; catálogo/default ACL e comparador sem PUBLIC/overload oculto. | ATENDIDO G1. |
| F04 — herança/SET ROLE | E/F04 tenta 3 runtimes × 7 papéis, mais auto-GRANT; todos negados com diagnóstico de ACL. Catálogo sem memberships ou administração. | ATENDIDO G1. |
| F05 — composição RLS instalada | B/Q07,Q08,V02,V08 + E/F05,F01 e chamadas reais de bootstrap/aceite/revogação; tuple divergente, Member sem escrita, revogação efetiva na próxima instrução; catálogo/policies/grafo revisados. | ATENDIDO G1 para helper T, BOOT, destinatário e integridade existentes. Resolver SELF/preview REC e atestação Application não existem: G4, sem PASS desses protocolos completos. |
| F06 — search_path | B/F06 `fixed-path-and-denied-shadow-object`; hashes 001–003 idênticos; E configurações das 11 funções/ACL de schema e banco; nenhum namespace runtime gravável ou TEMP concedido. | ATENDIDO G1 por reutilização válida + revisão do catálogo final. Não concedido CREATE/TEMP para fabricar ataque. |
| F07 — grafo | E/graph.observed, corpos/hash e função por owner; análise semântica acima; F03/F04 negam chamadas laterais; function-stats e cronologia vinculam chamadas às portas instaladas. | REVISÃO técnica concluída; aceite humano das arestas permanece. Os owners efetivos no corpo são determinados pelos modos DEFINER/INVOKER e catálogo, não por alegado log de current_user interno inexistente. |
| F10 — união e D2 | Matriz semântica acima, S/F10, E/F10/F11; post-rollback-privileges mostra os quatro SELECTs ausentes; comparação exata 178 capacidades; expectativa congelada antes de observar. | ATENDIDO G1 técnico após redução mínima. Aceite definitivo humano da candidata ainda necessário. |
| F11 — locks com grants mínimos | E/bootstrap real; F05/V09 revoke real; F11 update/replace/replay/accept/replay reais sob 178 capacidades; B cronologias K01–K06 como histórico, sem declarar nova execução dessas corridas. | ATENDIDO G1 da necessidade de grants/portas. Homologação completa de concorrência permanece G3. |
| F12 — recorte documental | Comparação de hashes da árvore inicial/final: Foundation/src/ADRs/contratos inalterados; 001–003 e assinaturas estáveis; csproj do harness isolado, sem referência de projeto ao Core; Api/Infrastructure csproj ainda vazios, Application referencia Domain. | ATENDIDO documental G1; nenhum teste arquitetural/API futuro declarado executado. Integração arquitetural real é G4. |
| V06 — eventos/estado final | E/V06: três validators Account/Member/Org INSERT+UPDATE, enabled/deferrable/deferred; updates múltiplos+SET CONSTRAINTS válido; estado inválido falha com P1003 e rollback observado; DELETE negado nos cinco objetos protegidos com P1005. B inclui falhas no COMMIT e controles válidos. | ATENDIDO G1. DELETE é bloqueado por guard, sem inventar trigger DELETE diferida ou bypass. |
| V09 — ausência de locks explícitos tardios | E/V09: corpos dos dois validators sem locks/reparo/SQL dinâmico; observer bloqueado pelo comando revoke **antes do COMMIT**, depois liberado com estado final coerente. Revisão dos cinco writers abaixo e cronologias B. | ATENDIDO G1 de superfície/corpos; não prova toda interleaving/lock interno ou V11. Restante estrutural em G3. |
| V12 — cobertura de escritores atuais | Inventário explícito abaixo, catálogo de cinco portas e triggers; F01/F03/F04 eliminam caminhos runtime diretos; E demonstra as cinco portas por login real, fontes SQL sem alteração. | ATENDIDO G1 atual. Novos writers/handlers/jobs/rotas reabrem análise; não estão aprovados antecipadamente. |

### V12 — inventário dos cinco escritores e C1

| Porta / login | Locks e validação nos corpos reais | Integridade/auditoria/commit e prova |
|---|---|---|
| bootstrap / core_platform | Identity ator/destinatário ordenadas; Org é criada na mesma transação (não há linha prévia para FOR UPDATE); prova referência e consumo após lock; receipt/replay. | Org+ledger+Account+Owner+audit atômicos, validators diferidos no contexto técnico; B/K02 e setup positivo E. Não alegar lock de Org inexistente. |
| accept_invitation / core_identity | Identity destinatária→Org→Slot→Invite, reconsulta após espera; estado/expiração/versão; replay confere Account/Member ativos. | Consumo+Account+Member+Invite+audit no mesmo commit; B/K01/K02/K06, E/F05/F11. |
| update_demo / core_tenant | Identity SHARE→Org→Account→Member→Demo; helper antes/depois dos locks; versão e alvo no escopo. | Demo+audit atômicos; B/K03/K04/K08 e E/F11; sem writer de Account/Member nesse corpo. |
| revoke_membership / core_tenant | Identities ordenadas→Org→Accounts ordenadas→Members ordenadas; reautoriza ator e papel/versão do alvo; Owner/self negados. | Account+Member+audit e validators finais; B/K03/K09/V04, E/F05/V09. |
| replace_invitation / core_tenant | Receipt antes dos locks; Identity ator/destinatário ordenadas→Org→Account→Member→Slot→Invite; reautoriza e reconfirma elegibilidade/estado/versão. | Expire/Replaced+novo Pending+sucessor+audit/recibo no mesmo commit; validator técnico da cadeia; B/K05/K06/F09 e E/F11. |

O receipt antecipado de bootstrap/replace é parte da serialização de idempotência existente; não foi apresentado como lock empresarial fora de C1 nem como prova de todas as combinações de deadlock. Guards/validators não são novas portas públicas: INVOKER protege OLD/NEW, DEFINER relê fatos finais sem reparar dados ou adquirir locks explícitos. Fixtures/observer postgres e g1_probe são superfície de ensaio segregada, não writers empresariais autorizados. Não há API, OIDC, job ou handler adicional a aprovar silenciosamente.

### Comandos, limpeza e preservação

1. Inspeção read-only: Git/status, agents.md e referências ADR/plano/contratos, corpos SQL e manifestos; leitura e hashes de arquivos. Nenhuma ferramenta externa de revisão foi acionada.
2. `dotnet run --project tests/TechnicalValidation/ADR0009.G1/runtime/G1.Validation.csproj -- --execute-g1-closing` → S, exit 0, 8 subprovas PASS, container/tmpfs removidos. Sem executar a suíte antiga inteira.
3. `python tests/TechnicalValidation/ADR0009.G1/harness/freeze_candidate.py` → candidata normativa 46/66/39/12/15, anterior arquivada, antes de E. Não lê snapshot observado como intenção.
4. Mesmo comando dotnet, somente perfil G1 → E, exit 0, 9 subprovas PASS. PostgreSQL **17.11**, Npgsql **10.0.3**, runtime .NET **10.0.12**, READ COMMITTED, zero retry automático; parâmetros não homologam D4 para produção.
5. `python tests/TechnicalValidation/ADR0009.G1/harness/compare_observed.py ece79aa930df` → exit 0; 5 owners/12 tabelas/11 funções; zero PUBLIC EXECUTE, runtime EXECUTE inesperado, atributos inseguros ou divergência dos grants exatos. Sensibilidade offline detecta concessão hipotética proibida e ausência hipotética necessária, sem concedê-las no banco.
6. `docker ps -a --filter label=bpc.adr0009.run=<S ou E> --format '{{.Names}}'` → exit 0, nenhum container de ambas as execuções restante; results de cada uma registra `REMOVED_RUN_CONTAINER_AND_TMPFS`. Não foram removidos recursos de outros runs/projetos nem evidências históricas.
7. Hashes de toda a árvore inicial comparados ao final: modificações limitadas ao pacote G1 e às cinco entradas abaixo; nenhum arquivo prévio de evidência ou fonte protegida modificado. Nenhum commit/push/merge/deploy, teste de Application ou banco externo.

Arquivos existentes alterados nesta rodada: **EXECUTION_REPORT.md**, **runtime/Program.cs** (perfil complementar, diagnósticos e nove subprovas), **harness/freeze_candidate.py** (retirada justificada dos quatro SELECTs), **runtime/004-candidate-grants.sql** e **manifests/security.expected.json** (candidata regenerada). Arquivos adicionados: evidências dos runs S/E e uma cópia histórica da candidata anterior em manifests/history. Nenhuma ADR/plano/documento redundante criado; nenhum arquivo fora do pacote alterado. Não houve alteração dos três scripts SQL de proteções/portas.

### Parecer e próxima decisão

**PRONTO PARA ACEITE HUMANO de G1 no escopo atual.** Evidências técnicas complementares concluídas e revisão semântica do executor registrada; não há P0/P1 comprovado ainda aberto nesse recorte. Pendência real de G1: o responsável verificar/aceitar a candidata D2 reduzida, seu grafo e capacidades administrativas confiáveis de ddl_owner, e emitir o aceite do gate. O executor não declara revisão independente concluída nem altera gates automaticamente.

V11 estrutural e homologação integral de concorrência permanecem G3; Application/API/OIDC e recuperação permanecem G4; seus bloqueios e NOT_EXECUTED são preservados. Não iniciar próxima etapa por inferência deste relatório. Para conferência da tarefa G1 existente no monday.com: **execução técnica concluída / aguardando revisão e aceite humano**, baseline B + complemento E; nenhuma nova tarefa, registro remoto ou aprovação automática criada.

---

## Histórico preservado — atualização anterior: caminho mínimo para conclusão de G1

Baseline mantida: **9ba11b81e986**, 29 subprovas PASS hist?ricas, 25 protocolos completos BLOQUEADOS e 29 NOT_EXECUTED. Nesta rodada houve somente inspe??o e atualiza??o deste relat?rio: nenhum teste/build/banco/novo run, nenhuma altera??o em Owner, convites, grants, harness ou candidata D2. N?o se reclassificou evid?ncia parcial como PASS integral.

### Escopo correto dos gates e prioridade

A se??o 7 do PLANO_VALIDACAO_TECNICA_ADR_0009 distingue **G1 (decis?es/superf?cie congeladas, ACL/grafo, D3/D5)** de G2 (banco/invariantes), G3 (concorr?ncia/falhas) e **G4 (Application/API/IdP/recupera??o)**. Os 54 protocolos atravessam esses gates. A falta de API impede os protocolos completos correspondentes e a conclus?o global, mas n?o ?, por si s?, motivo para bloquear o fechamento espec?fico de G1. Esta distin??o corrige a leitura agregada dos bloqueios neste relat?rio; n?o reduz crit?rios nem aprova gate automaticamente.

Caminho m?nimo de G1: reutilizar cat?logo/expectativa/corpos da baseline, fechar a rastreabilidade F01?F07/F10/F11/V06/V09/V12 e F12 documental no recorte atual, executar somente negativas/variantes faltantes pertinentes ao gate e obter o aceite humano de D2 candidata, oracles D3 e probes D5. Revis?o de seguran?a/arquitetura permanece necess?ria; n?o precisa ser outro agente e Claude Code n?o foi acionado. D1/D2 desenho n?o s?o reabertos. Novos escritores voltar?o ? compara??o de superf?cie quando existirem; n?o ficam aprovados antecipadamente.

**Prioriza??o por depend?ncia comum, sem 54 tarefas novas:**

- **A ? 15 cen?rios com avan?o SQL/revis?o local poss?vel:** Q07/Q08/Q09/Q11/Q12, V06/V07/V09, F02/F03/F04/F06/F11/F12 e K10. A n?o significa protocolo completo implementado ou aprovado: S/P abaixo distinguem isso. Priorizar o subconjunto de G1 F02/F03/F04/F06/F11/F12/V06/V09 e a evid?ncia atual de escritores V12; depois variantes SQL G2/pool.
- **H ? 3 centrados no aceite da candidata/revis?o humana:** F01/F07/F10. Os artefatos j? existem; preencher apenas lacunas concretas, sem nova rodada de hardening ou cat?logo id?ntico.
- **B ? 29 com depend?ncia de Application/API/fluxo ainda inexistente para integralidade:** muitos j? t?m SQL suficiente. N?o bloquear suas subprovas de banco nem exigir sua conclus?o G4 para avaliar G1.
- **C ? 7 com decis?o/probe/ensaio espec?fico:** V11/V13/K07/H02/H03/H04/H05. N?o s?o todos bloqueios de G1; observar o gate original de cada um.

### Matriz dos 54 protocolos ainda sem aprova??o integral

SQL: **S** = implementa??o de banco/driver suficiente para pr?ximas variantes, embora bindings/assertions possam faltar; **P** = parte existe, h? variante/probe/porta ausente; **N** = componente essencial ausente. A coluna API distingue execu??o SQL de fechamento integral. Decis?es: **H** = aceite/revis?o de D2/superf?cie; **O** = D3/D5 oracle/probe/failpoint; **T** = D4 par?metros/retry/tempos; **P** = D6 privacidade/opera??o; **IdP** = escolha/homologa??o e pol?tica do provedor. S?o depend?ncias j? documentadas, n?o novas ADRs. ??? n?o dispensa os aceites comuns existentes.

| ID | Grupo | SQL | Application/API/OIDC | Decis?o | Depend?ncia real e menor pr?xima a??o verific?vel |
|---|---|---|---|---|---|
| Q01 | B | N | Sim | IdP | Middleware OIDC/schemes/antiforgery ausentes; implementar adapters padronizados s? ap?s autoriza??o e testar negativas C01. |
| Q02 | B | P | Sim | ? | Adultera??o de rota/header/DTO exige endpoints; pr?ximo passo ? ligar contexto can?nico ao adapter e demonstrar zero acesso B. |
| Q03 | B | N | Sim | IdP | N?o h? resolver SELF/GET me; implementar resolu??o m?nima pr?pria, sem lookup de terceiros ou linking por e-mail. |
| Q04 | B | P | Sim | O | Ledger/estados existem, resolver e respostas C09 n?o; ligar IN/IR/IX ao caso de uso e provar 200/403/500/503 distintos. |
| Q05 | B | P | Sim | ? | Aceite SQL existe, preview REC n?o; criar adapter/caso de uso m?nimo e provar que preview n?o vincula nem concede T. |
| Q06 | B | S | Sim | IdP | Bootstrap SQL j? funciona; falta atesta??o Platform/scheme/MFA e comprovante C09. Reusar negativa SQL, testar fronteira no adapter futuro. |
| Q07 | A | S | N?o no protocolo SQL | O | Completar omiss?o de cada ID, vazio, malformado e tuple cruzada em leitura/escrita, com rollback. N?o depende de middleware para executar essas variantes. |
| Q08 | A | P | N?o no recorte SQL | H | Enumerar superf?cies realmente concedidas e completar SELECT/DML/portas por perfil, sem criar lista/API nova. Superf?cies futuras continuam n?o provadas. |
| Q09 | A | P | N?o no helper SQL | O/H | Helper e revoga??o existem: ligar grafo, booleano pr?prio e leitura ap?s revogar. Altera??o de role hoje ? negada; registrar aplicabilidade dessa variante, sem liberar UPDATE(role). |
| Q10 | B | P | Sim na parte a | ? | Parte b ? residual D1 j? caracterizado; parte a exige middleware real. N?o reabrir D1 nem chamar b de autentica??o. |
| Q11 | A | S | N?o | O | Pool Npgsql e evid?ncia hist?rica de mesmo PID existem; reconciliar variantes commit/rollback/exce??o e descarte aplic?vel antes de repetir s? lacunas. |
| Q12 | A | P | N?o para ensaio do driver | O | Savepoint existe; faltam variantes instrumentadas de cancelamento/reset/rollback incerto e descarte. Acrescentar apenas probes de driver autorizados, sem hardening do modelo. |
| V01 | B | S | Sim para cl?usula API | O | SQL Account sem Member j? falha; completar observer de evento/recibo e provar na Application que n?o h? cria??o parcial. |
| V02 | B | S | Sim para cl?usula API | O | SQL e oracle separados j? evidenciados; reusar baseline e acrescentar negativa de adi??o arbitr?ria no adapter futuro. |
| V03 | B | P | Sim para erro sanitizado | O | FK Member cruzada provada; completar demais FK/CHECK/null/version no probe e depois mapear falha sanitizada no adapter. |
| V04 | B | P | Sim para integra??o completa | O | Revoga??o e terminais provados; completar combina??es iniciais de estados com fixtures e validar auditoria/negativa de pr?xima requisi??o na Application. |
| V05 | B | P | Sim para cl?usula C08 | O | Org sem Owner provada; faltam fixtures de Owner/Account inv?lidos e negativa API. N?o autorizar mudan?a de Owner. |
| V06 | A | S | N?o | O | Cat?logo/triggers e probe existem; ligar eventos INSERT/UPDATE/DELETE, OLD/NEW, SET CONSTRAINTS e estado final v?lido. Operador com guard n?o prova login runtime. |
| V07 | A | S | N?o | O | Completar IDs/links/deletes em todas as tabelas com guards ativos e negativas runtime; sem conceder DELETE ou contornar guard. |
| V08 | B | P | Sim para resposta p?s-COMMIT | O/H | Contexto t?cnico existe; completar ?ncoras, estados invis?veis ao ator, tx/opera??o/savepoint e observer; resposta Application antes/depois do commit ainda n?o test?vel. |
| V09 | A | P | N?o para escopo SQL | H/O | Corpos/grafo existem; falta vincular trace pr?-commit/commit de cada porta ? inspe??o sem locks tardios. Revis?o humana obrigat?ria. |
| V10 | B | P | Sim no protocolo integral | T | K03 j? cobre leitura ap?s espera; vincular cronologia/duas ordens e completar resultado C05/API quando houver adapter. |
| V11 | C | P | Sim na variante API | O | Harness tem sess?es/barreiras, mas o guard pro?be a primeira remo??o/rebaixamento. Falta probe estrutural D5 autorizado ou decis?o de oracle; n?o desabilitar guard. |
| V12 | B | P | Sim para escritores futuros | H | Inventariar cinco portas existentes e seus locks/auditoria/commit agora; handlers/rotas/jobs futuros n?o existem e reabrir?o a compara??o de superf?cie quando surgirem. |
| V13 | C | P | N?o para deadlock SQL | T/O | 40P01 j? tem prova hist?rica; falta fechar pol?tica de retry/reautoriza??o e aplicabilidade de 40001. Registrar decis?o antes de agregar protocolo, sem mudar isolamento para for?ar erro. |
| V14 | B | P | Sim | O | Falhas de COMMIT/integridade existem; ligar observer de todos os efeitos e provar retorno s? ap?s commit na unidade de trabalho futura. |
| F01 | H | S | N?o para G1 | H | Candidata/cat?logo/diferen?as existem; revisar justificativas e fechar matriz de opera??es permitidas/negadas, registrando aceite humano da superf?cie atual. |
| F02 | A | S | N?o | H | Atributos/ownership/FORCE capturados; completar negativas reais de DDL/TRUNCATE/desligar RLS/trigger e separa??o migrator, todas sem efeito. |
| F03 | A | S | N?o | H | Assinaturas/default ACL capturados; completar chamadas privadas/overloads e conferir nenhum EXECUTE al?m da candidata, sem nova fun??o empresarial. |
| F04 | A | S | N?o | H | Reusar cat?logo recursivo e SET ROLE hist?rico; completar tentativa de auto-GRANT com login real e registrar flags de membership da vers?o atual. |
| F05 | B | P | Sim para SELF/REC completos | H | RLS das portas atuais exercit?vel; resolver/preview/fluxos SELF/REC completos n?o existem. Testar agora s? composi??o realmente instalada e vincular futuros adapters depois. |
| F06 | A | S | N?o | H/O | CREATE public/TEMP j? negados; revisar qualifica??es/path e documentar que n?o h? namespace runtime grav?vel. N?o conceder TEMP para fabricar a variante. |
| F07 | H | P | N?o para G1 | H | Grafo expl?cito/impl?cito e corpos dispon?veis; falta revis?o sem?ntica/aceite de arestas e cadeia atual, n?o outra coleta autom?tica equivalente. |
| F08 | B | P | Sim | IdP/H | Portas SQL permitem negativas de prova; fun??es planejadas e MFA/scheme/adapters ainda ausentes. Completar variantes por assinatura existente e manter futuras bloqueadas. |
| F09 | B | P | Sim no protocolo integral | H | Privacidade de audit/recibo e negativas novas provadas; faltam scopes/replay ap?s revoga??o e contratos do adapter. N?o tratar teste de convites como cobertura de todo F09. |
| F10 | H | S | N?o para G1 | H | Uni?o efetiva e justificativa por privil?gio prontas; revis?o humana de necessidade/controles do owner compartilhado. D2 desenho permanece aprovado; candidata ainda n?o. |
| F11 | A | S | N?o | H | Locks das portas passam com grants de coluna; ligar cada lock ? capacidade necess?ria e negativas de campo, reutilizando evid?ncias sem aumentar grants. |
| F12 | A | S | N?o documental G1; sim futuro G4 | H | Confrontar Foundation/assinaturas e cinco escritores com baseline; executar guards arquiteturais existentes quando cab?vel. N?o inventar consumo Repository 2.x nem provar API inexistente. |
| K01 | B | S | Sim para C04/HTTP | T | Duas ordens SQL j? passam; reusar cronologia e complementar 403/tradu??o de constraint no adapter. |
| K02 | B | S | Sim no protocolo integral | T | Race bootstrap/aceite j? passa; completar replay autorizado/audit/recibo do vencedor e resposta API, sem repetir prova de lock sem motivo. |
| K03 | B | S | Sim no protocolo integral | T | Comando/revoga??o em ambas as ordens j? passam; ligar evento ?nico e pr?xima requisi??o Application. Fixture Admin inicial n?o ? autoriza??o de provisionamento. |
| K04 | B | P | Sim | T | DemoRecord SQL concorrente existe; edi??o de Membership e ETag HTTP n?o. N?o criar PATCH de role/status s? para fechar cen?rio. |
| K05 | B | S | Sim para HTTP | T | Replace/replay SQL provados; completar 201/200/412 e autoriza??o atual no adapter, preservando segredo n?o recuper?vel. |
| K06 | B | P | Sim | T | Aceite ap?s prazo/espera provado; falta preview/GET sem escrita e emiss?o inicial/materializa??o no caminho ainda n?o implementado. |
| K07 | C | P | Sim no protocolo integral | O/T | Resposta descartada ap?s commit conhecido n?o ? commit incerto. Falta failpoint de transporte/reconcilia??o aprovado e integra??o C06. |
| K08 | B | P | Sim para retorno | O | Rollback por falha imediata de audit provado; completar falha diferida e todos escritores existentes, depois resposta Application. Rede incerta pertence a K07. |
| K09 | B | S | Sim | O | Dois Owners/porta/direto j? negados; falta negativa API e depend?ncia estrutural V11. N?o criar sucess?o ou repetir negativa j? comprovada. |
| K10 | A | P | N?o no pool SQL | O | Reuso real ap?s aborto j? existe historicamente; ligar cada K03/K06/K08/V14 ao mesmo PID/contexto limpo e descarte quando incerto. |
| H01 | B | P | Sim | O/IdP | Consumo ap?s revogar impede aceite B; faltam arquivamento/refer?ncia/emiss?o e nova identidade via IdP/adapter. N?o ampliar transi??es para produzir cobertura. |
| H02 | C | N | Sim | IdP/P | IdP homologado e pol?tica de subject/e-mail/recupera??o ausentes; verificar comportamento real ap?s escolha, sem simula??o declarada como homologa??o. |
| H03 | C | P | N?o ? pr?-requisito do ensaio DB | P | Ledger existe, procedimento/colunas de expurgo e reten??o n?o aprovados. Definir m?nimo t?cnico/privacidade antes de ensaio de descarte; n?o apagar prova hist?rica. |
| H04 | C | P | N?o para restore isolado | P | Faltam fonte posterior confi?vel, reconcilia??o, RPO/RTO e procedimento autorizado. Autorizar/ensaiar destino separado sem tocar origem/produ??o. |
| H05 | C | P | Sim para homologa??o IdP | IdP/P | Links suportam representa??o, n?o h? procedimento de migra??o/prova/rollback. Aprovar mapeamento e contrato pairwise antes do ensaio; nunca linking por e-mail. |
| H06 | B | N | Sim | IdP/T/P | Login/refresh/continuation/session store ausentes; implementar jornada m?nima e falhas de depend?ncia depois de autorizar o incremento de produto. |

### V11 ? decis?o de execu??o nesta rodada

O protocolo original exige negativa API e, separadamente, **probe estrutural D5 explicitamente aprovado**: duas transa??es removem/rebaixam Owners diferentes, serializam por Organization, testam ambas as ordens e a segunda revalida o estado confirmado. O harness j? disp?e de duas conex?es reais, observer e barreiras; a infraestrutura PostgreSQL n?o ? a depend?ncia ausente.

Entretanto, guard_immutable nega qualquer Active?Revoked de Owner (P1007), mudan?a de role e DELETE. revoke_membership tamb?m nega Owner. O probe g1_probe n?o ? uma porta excepcional que possa confirmar a primeira remo??o. Assim, uma nova corrida com as portas atuais apenas obteria duas negativas; n?o demonstraria o write skew/segunda valida??o exigidos. A prova de reativa??o ap?s revogar e a negativa com dois Owners da baseline s?o v?lidas, mas distintas.

**V11 permanece BLOQUEADO para o protocolo integral. N?o foi implementado/executado outro probe.** Depend?ncias exatas: (1) decis?o D5 sobre probe estrutural isolado capaz de alcan?ar o estado exigido, sem desativar/relaxar as prote??es atuais, ou decis?o humana formal sobre aplicabilidade/oracle diante da proibi??o absoluta de alterar Owner; (2) adapter API para sua variante HTTP em G4. Nenhuma dessas decis?es ? inferida. N?o se cria sucess?o, bypass, grant ou trigger alternativo nesta rodada. V11 ? G3 e n?o deve ser confundido com pr?-requisito autom?tico de toda a etapa documental/catalogal G1.

### Candidata D2 e decis?es humanas restantes

Candidata preservada, pendente de aprova??o humana. O JSON esperado, 182 privil?gios justificados, c?pia pr?-banco, compara??o sem diferen?as e grafo expl?cito/impl?cito continuam os da baseline; n?o houve nova observa??o nem ?aprova??o pela compara??o?. Receber o aceite de H/F01/F07/F10 e registrar D3/D5 aplic?veis ? o pr?ximo fechamento de G1. D4 (retry/tempos) e D6/IdP (opera??o/privacidade/recupera??o) devem ser resolvidos antes dos ensaios dependentes, sem reabrir D1 ou os cinco grupos D2. TTL final continua na situa??o documental existente; n?o se congela novo limite nesta transi??o.

### Higiene e classifica??o dos 78 arquivos da rodada anterior

Fonte fechada do conjunto: evidence/runs/9ba11b81e986/verification.json.newPackageFiles. Os **78 existem**, sem exclus?es. N?o confundir esse conjunto com todos os arquivos untracked acumulados no reposit?rio.

| Classe | Quantidade | Conte?do / tratamento |
|---|---:|---|
| C?digo de produ??o | 0 | Nenhum desses 78 pertence ? produ??o. |
| C?digo execut?vel de teste/infra do harness | 2 | harness/freeze_candidate.py e runtime/004-candidate-grants.sql. Preservar como mecanismo candidato de ensaio, n?o migration de produ??o. |
| Evid?ncias necess?rias de execu??o/diagn?stico | 70 | Runs 15a36f6b1584: 19; 1696d84d0406: 11; 278c7b089faa: 11; 9ba11b81e986: 18; ab759ecd086f: 11. Incluem FAIL/triage, snapshots, fontes/hash, cat?logo, resultados e cleanup. Revis?es/snapshots alternativos s?o hist?ricos, n?o tempor?rios a apagar. |
| Evid?ncias de proveni?ncia da candidata | 5 | manifests/history/baseline-*.json. Preservam expectativas anteriores, inclusive a origem da cr?tica de circularidade. |
| Metadado de rastreabilidade | 1 | manifests/security.latest-observation.json; ponteiro separado para n?o alterar a expectativa congelada. |
| Tempor?rios dentro dos 78 | 0 | Nenhum arquivo desse conjunto classificado como tempor?rio descart?vel. |

Total: **2 artefatos de teste + 76 de evid?ncia/proveni?ncia/rastreabilidade = 78**. ? parte h? **30 arquivos ignorados em runtime/bin e runtime/obj**, produtos locais de build regener?veis; n?o integram os 78 e n?o foram removidos. N?o h? __pycache__ encontrado no pacote. N?o se fez limpeza de evid?ncia nem reorganiza??o/rename.

Git no in?cio desta rodada: branch main, 176 arquivos rastreados, nenhum diff rastreado/staged e 262 arquivos untracked acumulados (incluindo documenta??o pr?-existente e o pacote). Sem altera??o de hist?rico. Como o relat?rio tamb?m ? untracked, a preserva??o dos demais conte?dos ? conferida por SHA-256, n?o apenas por git diff. Nenhum commit/push/merge/deploy.

Na raiz existe **um ?nico arquivo f?sico agents.md**, cujo conte?do inicia com t?tulo AGENTS.md. Neste filesystem Windows, os caminhos AGENTS.md e agents.md resolvem ao mesmo arquivo (os.path.samefile verdadeiro), com SHA-256 fec850412446d27372dce9b90e1d594b17d3b8ab9253d9dd03ce698590335d22. Foi lido integralmente; orienta reuso, evid?ncia antes de conclus?o, n?o copiar observado para esperado e n?o reabrir corre??es sem regress?o. N?o h? segundo documento distinto para mesclar; nenhum dos nomes foi sobrescrito ou renomeado. Isso n?o comprova que o arquivo esteja publicado no GitHub.

### Pr?ximo incremento recomendado, sem execu??o autorizada nesta entrega

**Primeiro, fechamento localizado do G1:** revis?o humana de D2/D3/D5 e complemento apenas dos bindings/assertions faltantes de F/V pertinentes ao gate, aproveitando 9ba11b81e986. Se uma evid?ncia existente j? satisfaz uma variante, vincul?-la e revisar sua agrega??o; n?o repetir o teste por ritual nem modificar SQL de seguran?a sem falha real.

**Pr?ximo incremento de produto, ap?s autoriza??o e gates aplic?veis:** host m?nimo .NET 10 + adapters Application/API e autentica??o OIDC padronizada de homologa??o; resolu??o SELF/contexto can?nico e unidade transacional ligadas ? superf?cie j? ensaiada, come?ando por login?identidade/contexto?leitura/comando demonstrativo autorizado e auditoria. Negativas com dois usu?rios/duas organiza??es devem entrar desde esse incremento. N?o implementar auth pr?pria, cat?logo/billing/console completo/IA nem promover os scripts de ensaio a migration produtiva por c?pia autom?tica. Esse incremento elimina depend?ncias G4 reais, em vez de endurecer indefinidamente as mesmas portas SQL. Aqui ? somente recomenda??o, n?o in?cio da Sprint 1 nem libera??o de desenvolvimento.

**Parecer desta transi??o:** classifica??o conclu?da, sem nova prova executada ou reabertura de P1. G1 continua BLOQUEADO at? cumprir seus crit?rios e obter decis?o humana; a valida??o global dos 54 permanece aberta nos gates correspondentes. Somente este relat?rio foi atualizado nesta rodada; registros e manifestos hist?ricos mant?m seus status originais.

---

## Registro t?cnico anterior preservado

A classifica??o de depend?ncias acima prevalece para a distin??o entre G1 e G2/G3/G4; o registro abaixo mant?m as corre??es e evid?ncias hist?ricas, sem transformar a aus?ncia de Application em bloqueio autom?tico do G1 espec?fico.


**Referência: 9ba11b81e986. Correções e evidências prontas para revisão independente; G1 continua BLOQUEADO até revisão humana.** D1/D2 e ADR-0008 preservadas. Nenhuma Application/API/OIDC, sucessão de Owner ou emissão inicial empresarial implementada. Trabalho restrito a este pacote de ensaio.

## Resultado e rastreabilidade

[Resultados dos 54 IDs](evidence/runs/9ba11b81e986/results.json), [431 observações/diagnósticos](evidence/runs/9ba11b81e986/assertions.json), [cobertura por ID](evidence/runs/9ba11b81e986/coverage.review.json), [hashes executados](evidence/runs/9ba11b81e986/source-hashes.json), [comandos](evidence/runs/9ba11b81e986/commands.json).

**29 subprovas PASS, zero FAIL na referência final.** Protocolos completos: **25 BLOQUEADO, 29 NOT_EXECUTED, zero PASS/FAIL/INCONCLUSIVO**. São os mesmos 54 cenários; nenhum ID/plano novo. NOT_EXECUTED nesta rodada seletiva não apaga execução anterior. Parent BLOQUEADO não desqualifica a subprova SQL, mas impede aprovar o protocolo completo com dependências ausentes.

Selecionados Q07/Q08; V01/V02/V03/V04/V05/V07/V08/V11; F01/F02/F03/F06/F09; K01–K09; H01. As provas novas foram anexadas a K09/F09, sem recriar a suíte. V11 permanece parcial.

## P1-1 — revogação direta de Owner

**Corrigido:** guard BEFORE de Membership agora rejeita OLD.role=Owner, OLD.status=Active e NEW.status=Revoked, com P1007/Integrity.OwnerRevocationDenied. A regra está no trigger, não apenas na porta revoke_membership. Independe da quantidade de Owners e não introduz sucessão. Mantidos role imutável, proibição de reativação e validação diferida Account/Member/Owner.

A fixture privilegiada insere segundo Owner inicial e sua conta/consumo na organização A; isso não é uma porta de provisionamento ou sucessão. A prova confirma dois Owners e contas Active após:

- UPDATE direto do segundo Owner com current_user=exec_tenant: rejeitado imediatamente.
- UPDATE da Account para Revoked seguido do UPDATE proibido do Membership, na mesma transação: rejeitado e rollback restaura a Account.
- Porta SECURITY DEFINER revoke_membership, por login real core_tenant/Owner A contra o outro Owner: Access.Denied, 42501.

O primeiro caso usa conexão privilegiada SET LOCAL ROLE exec_tenant, com papel efetivo capturado; não é login runtime. O terceiro prova a porta real. Revogação legítima de Member/Admin foi reexecutada em V04/K03/H01; as corridas revogar/comando e reativar após espera continuam rejeitando ressurreição. Não se atribui PASS integral a K09/V11.

## P1-2 — criação e cadeia de convites

**Corrigido no escopo efetivamente implementado:** guards e grants por coluna, combinados com RLS, protegem INSERT/UPDATE. Não se depende de uma policy AS RESTRICTIVE isolada.

| Operação | Colunas/estado permitidos e controles |
|---|---|
| Emissão inicial | Não existe porta SQL empresarial. Fixtures iniciais privilegiadas inserem Pending/version=1, sem accepted_account_id/successor/predecessor. Não há constraint indiscriminada predecessor NOT NULL. Tentativa inicial direta por exec_tenant é negada como InitialIssueUnavailable; futura emissão exigirá capacidade e prova C02 próprias. |
| Replace | exec_tenant só insere id/org/destinatário/emissor/hash/status/prazo/predecessor. Novo estado Pending/version padrão 1, accepted_account_id e successor nulos; emissor deve ser Membership ativo do Account/contexto ator Owner/Admin, não o emissor histórico de outro administrador. Destinatário deve corresponder ao contexto, estar Active e nunca consumido. Predecessor deve estar Replaced, mesma org/destinatário e ainda sem sucessor. |
| Aceite | exec_identity não insere convite. UPDATE restrito a status/accepted_account_id/version; transição Pending→Accepted exige Account Active da mesma identidade destinatária e organização. Prova do segredo, locks, elegibilidade e prazo continuam na porta; não expõe hash ao Tenant. |
| Cadeia | Destinatário/emissor/hash/org/prazo/predecessor são imutáveis em UPDATE. successor só NULL→novo Pending cujo predecessor é o convite antigo Replaced, mesma org/destinatário. Validator diferido exige âncora e reciprocidade, rejeitando Replaced sem sucessor. |
| Replay | Reautoriza e consulta recibo existente; não insere sucessor, não altera prazo, não retorna/reconstrói segredo. K05 confirma mesmo ID e um Pending. |

O replace passa a atribuir o emissor à Membership atual autorizada, em vez de copiar silenciosamente o emissor do convite anterior. Secret generation/hash permanecem iguais; INSERT(hash) é necessário, SELECT(hash) continua proibido ao exec_tenant.

Guard imediato de convite é INVOKER: dentro da porta executa como exec_tenant; no probe SET ROLE usa esse papel efetivo. Regras estruturais de INSERT valem também para fixtures: só Pending/version 1, sem resultado de aceite ou sucessor preenchido. Condições de autorização empresarial são exigidas para o único owner com INSERT de convites, exec_tenant; os outros owners/runtime/probe não recebem esse INSERT. O operador de instalação continua uma autoridade privilegiada de fixture/DDL, não um usuário empresarial.

O validator diferido da cadeia é SECURITY DEFINER sob **exec_integrity**, com seis SELECTs adicionais e RLS técnico por organização. Exige organização e transação corretas e falha se a âncora não for visível; ausência de linha não é integridade válida. Não obtém locks tardios. Replace/aceite e fixtures fornecem o contexto transacional. Isso evita conceder SELECT de convites ao login externo no COMMIT, quando o contexto da função de comando já terminou.

**TTL:** C04 descreve estados e expiração sem fixar um teto. SPRINT_1_PLANEJAMENTO D03 menciona convite 24 h como proposta; C02 define outra validade de 24 h para referência de destinatário. O replace mantém os 24 h já usados pelo ensaio, sem novo limite máximo SQL/decisão de produto. Fixtures usam 1 h ou 500 ms para prazo real. TTL final permanece sujeito à aprovação documental pertinente, sem alterar contratos.

### Negativas e regressões de convite

Execução efetiva sob exec_tenant, com SQLSTATE, mensagem exata, routine da versão, papel, identidade do statement e estado final:

- INSERT Accepted → P1010/InvalidInsert.
- INSERT com accepted_account_id ou successor_id explicitamente fornecidos → 42501 de ACL; nenhum grant dessas colunas.
- Emissor divergente, mesmo sendo outro Owner ativo da mesma organização → P1011/IssuerDenied.
- Destinatário empresarial já consumido → P1012/RecipientIneligible.
- Predecessor ainda Pending → P1014/InvalidChain.
- Tentativa de emissão inicial pelo owner sem porta de emissão → P1013/InitialIssueUnavailable; fixture inicial com predecessor NULL permanece válida.
- UPDATE de successor incompatível → P1014; Replaced sem sucessor é recusado no COMMIT por IncompleteChain.

Observer confirma convite inicial Pending:1 como único registro do destinatário após as tentativas. K05 comprova replace/replay legítimos e concorrentes, um sucessor Pending, código antigo inválido e perdedor com versão obsoleta. K01/K02/K06/H01 reexecutam aceite, bootstrap concorrente, expiração após espera e não reutilização de consumo. Nenhum resultado de fixture privilegiada foi contado como autorização de runtime.

## D2 — metodologia reconstruída

A implementação anterior e seu snapshot são baseline histórico; não foram apagados. [Modelo manual e congelador](harness/freeze_candidate.py), [candidata atual](manifests/security.expected.json), [cópia pré-banco](evidence/runs/9ba11b81e986/preflight.expected.json), [registro de congelamento](evidence/runs/9ba11b81e986/expectation-freeze.json).

1. Modelo manual de capacidades parte de ADR-0008/0009, C02/C04/C06/C08/C09 e dos statements necessários de bootstrap, aceite, update, revogação, replace, helpers/validators. Cada um dos **182 privilégios de coluna** tem justificativa e operação/contrato no JSON.
2. O arquivo esperado é um objeto novo, construído do modelo literal: não lê catálogo, resultados, flags allowed, parser de GRANTs ou valores do snapshot anterior. O arquivo antigo é somente arquivado por hash em manifests/history. Não há ponteiros observed herdados na candidata final.
3. O mesmo modelo instala a candidata em 004-candidate-grants.sql; a inspeção real verifica instalação, permissões ambientais/defaults e ownership. Essa correspondência **não comprova por si só que o modelo normativo está correto**: a justificativa e a suficiência/menor privilégio exigem a revisão independente, além das provas comportamentais.
4. Antes de criar o banco, o harness captura a expectativa e seu hash/instante; somente depois instala, executa inspeção de catálogo e provas. O comparador lê exclusivamente preflight.expected.json daquele run, não o manifesto global mutável. A candidata permaneceu semanticamente idêntica após a observação; hash original do manifesto registrado no congelamento.
5. Comparação calcula ausentes e excedentes por papel, incluindo negativas de tabela/coluna, schema, EXECUTE, atributos, ownership de função e modos SECURITY DEFINER/INVOKER. [Sensibilidade offline](evidence/runs/9ba11b81e986/comparison-sensitivity.json) mostra detecção de hipotético SELECT(hash) excedente e SELECT(role) obrigatório ausente, sem alterar expectativa ou banco; não é prova runtime nem cenário adicional.

Instalação suportada: harness aplica **001–004** no novo cluster, antes de liberar credenciais runtime. O quarto script revoga grants antigos de tabela/coluna e instala os mínimos; executar somente 001–003 não representa esta candidata. Nada é migration de produção. Candidata permanece **pendente de aprovação independente**, sem reaprovar D2 pelo executor.

### Resultado da comparação

[Observado](evidence/runs/9ba11b81e986/security.observed.json), [comparação](evidence/runs/9ba11b81e986/security.comparison.json), [catálogo bruto](evidence/runs/9ba11b81e986/catalog.jsonl).

| Owner aprovado | Funções | Privilégios de tabela / coluna | Diferenças ausentes/excedentes |
|---|---:|---:|---|
| exec_identity | 1 | 0 / 48 | 0 / 0 |
| exec_tenant | 3 | 0 / 68 | 0 / 0 |
| exec_bootstrap | 1 | 0 / 39 | 0 / 0 |
| exec_context | 1 | 0 / 12 | 0 / 0 |
| exec_integrity | 4 | 0 / 15 | 0 / 0 |

Também zero diferenças de USAGE de schema, EXECUTE, ownership esperado de funções, modos DEFINER/INVOKER e atributos dos cinco owners; zero CREATE inesperado, PUBLIC EXECUTE ou role com super/bypass/createRole/createDb. Todos NOLOGIN/NOINHERIT. Doze tabelas sob ddl_owner com ENABLE/FORCE RLS, onze funções incluindo ctx INVOKER de ddl_owner. Nenhum membership relevante observado no fechamento recursivo.

As permissões de schema foram reduzidas aos schemas referenciados por cada grupo. USAGE public de PUBLIC é capacidade ambiental conhecida, sem CREATE e fora do search_path fixo, explicitada na candidata. CONNECT explícito aos logins do ensaio, PUBLIC sem TEMP/CONNECT; não se presume que futura migração de produção reproduza essa configuração. F06 testa negativa TEMP/public, não shadowing num ambiente que conceda TEMP.

Menor privilégio: nenhum SELECT de tabela ampla nos cinco owners; SELECT(hash) apenas ao aceite e ao bootstrap/referência quando necessário. Tenant não insere status de resultado/versão/sucessor/accepted_account_id; não promove role nem cria DemoRecord. Bootstrap não lê Account/Member; Identity não atualiza seus versions. UPDATE(version) técnico permanece onde FOR UPDATE o exige, com policies de escrita que negam mutação fora das portas.

### Grafo explícito e dependências implícitas

[Grafo estruturado](evidence/runs/9ba11b81e986/graph.observed.json), [corpos completos](evidence/runs/9ba11b81e986/graph.json). São **168 arestas/referências implícitas**: triggers/validators/FKs, policies→helpers e referências de corpos→relações. Há ainda chamadas explícitas fixas, dependências pg_depend, capacidades de ownership e arestas de herança observadas. Cobre os novos guard/validator de convite, não apenas a porta.

- Comandos e helpers chamam ctx; Tenant chama own_tenant_allowed. Guard de convite INVOKER pode chamar helper dentro do contexto do escritor.
- Triggers BEFORE chamam guards INVOKER; constraint triggers de Account/Member/org e cadeia chamam validators técnicos DEFINER. Estes leem relações sob policies específicas de exec_integrity, sem dependência da autorização empresarial do ator.
- Funções do mesmo owner compartilham toda a união de capacidades. Ownership de função permite autoridade inerente/EXECUTE e administração do objeto mesmo sem GRANT explícito; não se considera função distinta como segregação SQL.
- ddl_owner possui tabelas/schemas e pode administrar/desabilitar proteções por DDL; FORCE não limita essa autoridade. Essa capacidade não pertence a runtime/exec_* e não é omitida da análise.

O grafo contém definições de policies/triggers e distingue arestas de FK internas. Referências lexicais e dependências de catálogo não substituem revisão semântica de caminhos condicionais, funções de sistema e permissões transitivas. Não foi declarada revisão independente concluída.

## Regressões, oracles e limites

Q07: contexto ausente/divergente e UUID malformado SQL; Q08: login core_tenant real, acesso ao canário A e isolamento demonstrativo. Continuam bloqueados atestação/contexto pela Application e matriz completa de APIs/consultas. D1 não foi reaberta nem reinterpretada como isolamento contra contexto integralmente forjado.

Regressões positivas/negativas: bootstrap, aceite, Member/Admin revogados legitimamente, reativação negada, FK cruzada, NOT NULL/RLS/rollback V02, Owner inicial obrigatório, contexto técnico, imutabilidade de histórico, auditoria atômica, replace/replay, versões e expiração. K01/K02/K03 em ambas as ordens, K04/K05 com bloqueio real; V11 mantém corridas de revogação versus reativação de Account e Membership após espera. Barreiras por pg_blocking_pids, não por duração de sleep.

Novos oracles usam igualdade da operação esperada, papel efetivo, mensagem/estado/routine e observer. Role é capturado antes da falha; parâmetros e segredos não são gravados. As rotinas são diagnóstico específico do PostgreSQL 17.11, não contrato portátil entre versões. V02 histórico continua preservado.

**V11 não completo:** os novos testes de dois Owners demonstram negativas diretas e rollback, não o protocolo concorrente integral de write skew/último Owner nem todos escritores C1. Owner atacante por SET ROLE não é login runtime nem prova de disciplina de locks do escritor legítimo. Fixtures iniciais de Admin/segundo Owner são apenas preparação privilegiada.

## Histórico desta rodada e cleanup

| Run | Resultado delimitado |
|---|---|
| 1696d84d0406 | Candidata omitiu SELECT Identity.id/status do helper; Q08 negado. Modelo corrigido por necessidade explícita de join Active, congelado novamente antes da próxima observação. |
| 278c7b089faa | Validator diferido INVOKER tentou ler convites sob core_identity no COMMIT; negado. Corrigido para contexto técnico exec_integrity. Cleanup de falha concorrente exigiu observar sessões e terminar somente backend 87 idle-in-transaction do próprio banco descartável; depois o harness removeu o container. Não contado PASS. |
| ab759ecd086f | Guard Owner inicialmente colocado na validação diferida não recusou o UPDATE imediatamente como exigia o novo probe; rodada interrompida/rollback. Movido para BEFORE. Não houve commit inválido comprovado. |
| 15a36f6b1584 | 29 subprovas PASS; candidata intermediária ainda continha metadados legados. Preservada, seguida de reconstrução integral do JSON esperado sem ler seus valores antigos. |
| 9ba11b81e986 | Referência final com candidata limpa pré-observação: 29 PASS SQL, 25 protocolos BLOQUEADO/29 não iniciados. |

FAILs brutos e [triages](evidence/runs/1696d84d0406/triage.json) separados foram preservados. As categorias brutas de parada conservadora não são, por si só, demonstração de P0. Os runs anteriores, inclusive V02 original 5efad327c7d7, e792ea797e94 e 7dad9c5a2703, permanecem intactos. Ausência de P0 demonstrado nas variantes finais não comprova ausência nas não executadas.

**Cinco containers desta rodada removidos**, tmpfs descartados, pools encerrados e conferência por label sem remanescentes. Mesmo PostgreSQL 17.11/Npgsql 10.0.3/.NET 10/Docker local já fixados, loopback/porta dinâmica, imagem local sem pull e sem volumes host/nomeados. O psql dentro do container foi usado apenas para diagnóstico/intervenção da rodada falha 278c7b089faa; catálogo final coletado por Npgsql READ ONLY. Nenhum banco compartilhado/produção ou recurso de terceiros alterado.

## Pendências e parecer

**Para revisão humana:** correção dos P1 e modelo manual D2, incluindo cada justificativa, schema/EXECUTE/ownership e grafo. O executor não aprova a candidata nem conclui a revisão independente. TTL de produto permanece na situação documental existente; esta rodada não o congela. Nenhuma mudança de D1/D2/ADR-0008 foi necessária.

**Cobertura integral ainda ausente:** Application/OIDC/API, emissão inicial/cancel/listas não implementados no ensaio, ETag HTTP, todos escritores/C1 e V11 integral, commit de transporte realmente incerto/reconciliação, arquivamento/restore/migração IdP, demais protocolos originais. K07 apenas descarta resposta após commit conhecido; não prova commit incerto. Nenhuma funcionalidade empresarial adicional autorizada por estas provas.

**Melhorias não bloqueantes para esta revisão:** desempenho, contenção prolongada, validação em outras versões, visualização adicional do grafo. Mudança de versão exige rever diagnósticos do oracle. Build não conta como funcionalidade; regressão completa da solução não foi executada.

**Parecer: pronto para revisão independente das correções e da candidata D2; G1 permanece bloqueado até revisão humana e fechamento do escopo exigido.** Sem novo plano/ADR/tarefa monday.com; nenhuma escrita externa. Não iniciar próxima etapa.

## Arquivos e verificações

Alterados: scripts 001/002/003 de ensaio, Program.cs, compare_observed.py, inspect-catalog.sql, security.expected.json, dependencies.json, cenários/protocolos/oracles, README.md e este relatório. Novos: freeze_candidate.py, 004-candidate-grants.sql, baseline histórico em manifests/history, security.latest-observation.json e evidências dos cinco runs. Inventário exato e comparação SHA-256 em [verification.json](evidence/runs/9ba11b81e986/verification.json).

Nenhuma alteração em produção, Foundation, contratos, ADRs ou plano. Sem commit/push/merge/deploy. JSONs, 54 IDs/status, fontes executadas, congelamento e comparação foram conferidos; cleanup e preservação de evidências antigas verificados separadamente.
Classifica??o final: P1 Owner/convites = **CORRIGIDO** no ensaio; protocolos sem Application/API e V11 integral = **BLOQUEADO POR DEPEND?NCIA/COBERTURA**; D1 = **RISCO ACEITO**, preservado; aprova??o independente da candidata D2/G1 e TTL final de produto = **DECIS?O HUMANA NECESS?RIA**.

Confer?ncia de estado: 13 arquivos preexistentes alterados e 78 arquivos novos deste trabalho, todos no pacote autorizado. agents.md surgiu na raiz ap?s o baseline; foi lido e preservado, sem cria??o/altera??o por esta tarefa. Evid?ncias anteriores e arquivos protegidos permaneceram id?nticos.

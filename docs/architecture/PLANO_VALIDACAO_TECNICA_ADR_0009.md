# Plano de Validação Técnica — ADR-0009

## Estado vigente — fechamento G1 e preservação documental

Registro atualizado em **31/10/2026**, exclusivamente para consistência documental, por autorização expressa do responsável. **G1 foi tecnicamente aprovado por aceite humano em 30/10/2026**, no escopo definido pela matriz de cobertura do `tests/TechnicalValidation/ADR0009.G1/EXECUTION_REPORT.md`. Baseline: `9ba11b81e986`; execução complementar: `ece79aa930df`. O pacote de evidências permanece separado desta integração documental; a referência textual não presume que já esteja publicado no GitHub.

- **D2 aprovada definitivamente:** cinco grupos de owners mantidos e candidata revisada com **178 privilégios de coluna justificados**, conforme inventário, comparação, grafo e revisão semântica documentados.
- **D3/D5 ratificadas:** SQLSTATE nativo com diagnóstico específico e códigos internos explícitos; ambiente/probes limitados ao harness descartável existente, sem bypass ou ampliação de privilégios.
- **G2 não autorizado.** G3, incluindo V11 estrutural, e G4, incluindo Application/API/OIDC e recuperação, permanecem fora do escopo aprovado de G1.
- O aceite não conclui integralmente os **54 protocolos**, não aprova globalmente os ADRs, não libera Gate UX ou Sprint 1 e não autoriza operações Git, produção ou deploy por inferência.

**Divergência preservada nos documentos congelados:** [ADR-0008](decisions/ADR-0008-organization-membership-and-context.md), [ADR-0009](decisions/ADR-0009-persistence-isolation-and-concurrency.md) e [C01–C10](SPRINT_1_CONTRATOS_FLUXOS.md) conservam integralmente seus conteúdos e hashes utilizados em G1. Em particular, o registro do ADR-0009 que apresenta G1 bloqueado, D2 condicionada à comprovação e D3/D5 pendentes descreve o momento anterior ao aceite; não é o status atual. A manutenção de status global Proposto dos ADRs não contradiz o aceite limitado de G1. Nenhuma regra técnica congelada é alterada por esta nota.

**Leitura temporal do restante do plano:** as seções 1–11 abaixo preservam a especificação e os registros da fase G0 anterior ao fechamento G1. Expressões como G1 bloqueado, D3/D5 pendentes, zero execuções e checklist não marcado são registros históricos daquela fase, não revogação do aceite acima nem contagem atual das evidências. As obrigações, IDs, gates e oracles dos 54 protocolos são preservados; resultados atuais pertencem às evidências de execução, sem reclassificação automática de protocolos multigate.

## 1. Objetivo, escopo e estado

**G0 documental APROVADO COM CONDICIONANTES por decisão humana expressa nesta rodada. G1 BLOQUEADO. Todos os cenários: NÃO EXECUTADOS.**
Este plano especifica provas futuras; a decisão humana de G0 não aprova ADRs, infraestrutura, implementação de testes, Gate UX ou Sprint 1. Esta rodada registra somente as decisões humanas D1/D2 aprovadas e suas condicionantes nos documentos oficiais. Não reabre análise ou altera outras decisões. Não contém SQL, migrations, funções, triggers ou testes executáveis. Não depende de plano externo.

Objetivo: transformar o desenho candidato de persistência em condições observáveis de segurança, integridade, concorrência e recuperação, incluindo limites que testes não podem eliminar. Quantidade derivada da cobertura: **54 cenários principais**, com variantes obrigatórias explicitadas em cada linha; não é meta de volume nem equivale a 54 testes aprovados. Variantes parametrizadas receberão subidentificadores estáveis no harness futuro e contagem própria de execução. Nenhum resultado histórico é reaproveitado como prova destes cenários.

Fora do escopo: catálogo, billing, produtos de negócio, integração efetiva GIV/ERP/CRM, implementação de UX, auditoria jurídica conclusiva, benchmarks ou alterações Foundation. A referência a contratos C03/C07 cobre segurança da persistência/retomada, não certificação de acessibilidade nem Gate UX.

### 1.1 Referências reais e hierarquia

1. [Plano Diretor](decisions/PLANO_DIRETOR_BUSINESS_PLATFORM_CORE.md): seções 2, 5, 11, 17 e 20, visão/limites/governança.
2. [ADR-0008](decisions/ADR-0008-organization-membership-and-context.md): regra de produto permanente, modelo candidato, aceite/bootstrap e desligamento.
3. [ADR-0009](decisions/ADR-0009-persistence-isolation-and-concurrency.md): recomendações, três P0 e critérios futuros.
4. [Modelo/RLS/concorrência](ADR_0009_MODELO_RLS_CONCORRENCIA.md): M1–M3, R1–R4, C1–C3/K01–K10, H, T/P, Q1–Q3, V1–V3 e F1–F4.
5. [C01–C10](SPRINT_1_CONTRATOS_FLUXOS.md): comportamento/HTTP, sem endpoints presumidos implementados.
6. [I01–I12](IDENTIDADE_CENARIOS_E_DECISOES.md): H01–H04, resultados esperados e decisões.
7. [Planejamento Sprint 1](SPRINT_1_PLANEJAMENTO.md): D01–D12, backlog S1-00–S1-11 e evidências históricas.
8. [Rastreabilidade UX](SPRINT_1_RASTREABILIDADE_UX.md): RF/UAT e G01–G08; [fechamento UX](SPRINT_1_FECHAMENTO_UX.md): pendências, sem gate aprovado.
9. [Regras de engenharia](../engineering/agents/CLAUDE_SYSTEM_PROMPT.md), [workflow](../engineering/agents/CLAUDE_WORKFLOW.md) e [guia](../engineering/agents/README.md): preservar contratos, mudanças autorizadas, não declarar teste não executado.

Hierarquia aplicada: regras de produto aprovadas registradas no ADR-0008 prevalecem sobre descrições históricas; C01–C10 fixam comportamento candidato; ADR-0009/matrizes propõem mecanismos. Incompatibilidade vira achado, não alteração silenciosa do contrato. Os ADRs 0007–0012 permanecem propostos; aprovação da regra de produto não aprova mecanismo. Este plano não usa o antigo alvo externo de 61 casos.

### 1.2 Registro histórico de decisões humanas — fase G0

Fonte: instrução expressa do responsável nesta solicitação, não resultado de teste nem inferência de G0.
Registro canônico no [ADR-0009, decisões humanas registradas](decisions/ADR-0009-persistence-isolation-and-concurrency.md).

| Decisão | Status e alcance | Condicionante / rastreabilidade |
|---|---|---|
| D1 | **APROVADA**: Application é fronteira confiável de autenticação, autorização e atestação do contexto empresarial; PostgreSQL aplica RLS ao contexto transacional recebido | Credencial SQL comprometida é risco residual explícito; não se declara isolamento comprovado. Q10/3.7, Q1 do complementar; evidência futura continua necessária |
| D2 | **APROVADA**: manter os cinco grupos exec_identity, exec_tenant, exec_bootstrap, exec_context e exec_integrity; desenho aprovado | Grants efetivos e grafo precisam de comprovação antes de liberar G1. F10/3.9 e F1/F2 do complementar |
| D3/D5 | **PENDENTES** de preparação e validação técnica | Oracles/constraints/erros e probes/failpoints/ambiente não aprovados por esta decisão. V08/V14, seção 8 |
| G0/G1 | G0 aprovado com condicionantes; **G1 BLOQUEADO** | Sem autorização para testes, banco, infraestrutura, Gate UX ou Sprint 1 |

## 2. Premissas e invariantes rastreáveis

Aprovado: Organization é empresa isolada; identidade OIDC possui no máximo uma conta empresarial durante sua vida, vínculo imutável mesmo revogado; mudar empresa exige nova identidade/convite; e-mail não autentica vínculo; sem seletor; PlatformOperator não recebe Membership/acesso Tenant automaticamente.

Proposto, ainda a aprovar: modelo físico, PostgreSQL/EF/provedor/versões, papéis/grants, SECURITY DEFINER, RLS, triggers, READ COMMITTED+C1, ETag, NotLinked 200, idempotência e tempos. Hipóteses de 48 h/MaxOffset/sessão/continuation existentes não são fixadas pelo plano.

| ID local | Invariante / fonte |
|---|---|
| N1 | Autenticação OIDC, autorização Application e transporte SQL distintos — C01; Q1/ADR-0009 |
| N2 | Contexto próprio válido, isolamento e ausência de autoridade do cliente — R/Q; C08/C09 |
| N3 | Consumo permanente e imutabilidade, sem transferência/fusão por e-mail — ADR-0008; M/H |
| N4 | Account Active ↔ exatamente um Membership Active coerente; revogação atômica candidata — M3/V |
| N5 | Organization Active tem Owner Active válido; C08 não permite alterar Owner — M3/V; C08 |
| N6 | Mutação, estado do convite, histórico, evento e recibo aplicável atômicos — C04/C06/C09; M3 |
| N7 | C1, revalidação pós-espera e serialização dos escritores — C1/V1/V3 |
| N8 | Menor privilégio efetivo por owner, RLS e grafo controlado — R/F |
| N9 | Versão por recurso, ETag/precondição, sem overwrite — C05 |
| N10 | Replay autorizado, um efeito, sem segredo recuperável — C06 |
| N11 | Auditoria append-only e sem sucesso sem evento transacional — C06/C08; M-AUDIT |
| N12 | Recuperação/indisponibilidade/regressão não inventam autorização — H; C03/C07/C10 |

N4 inclui relação de estados candidata do ADR-0009, não nova regra de produto aprovada. Provas não autorizam reativação/transferência/remoção de Owner nem acesso Platform amplo.

## 3. Estratégia e arquitetura do harness futuro

### 3.1 Camadas propostas, sem artefatos executáveis

Harness .NET 10 a implementar somente após autorização: orquestrador de casos/variantes; clientes reais de Application/API; driver PostgreSQL aprovado; coordenador de barreiras; observer somente de ensaio; coletores de evidência; fixtures e limpeza no destino isolado. Usar organização de testes já existente onde adequada; projeto adicional/pacotes e referências dependem de revisão, não são criados agora.

Provas de banco exigem PostgreSQL real e papéis reais: InMemory/SQLite/mocks não provam RLS, FK diferida, locks, commit ou pooling. IdP de teste/mocks podem cobrir adapters determinísticos; conclusão sobre OIDC/política de sujeito/e-mail exige homologação com o IdP aprovado. Não criar provedor/protocolo próprio. Nenhum teste conecta a produção nem dispara migração automática pela aplicação.

Modalidades: **A** automatizável; **H** revisão humana; **O** ensaio operacional. Modalidade A não significa implementado. Casos com H/O exigem seus registros mesmo que a parte A passe.

### 3.2 Fixtures sintéticas e papéis

- Organizações A/B isoladas, nomes-canário diferentes, DemoRecords e auditoria sintéticos distinguíveis.
- IA = Owner A; IB = Owner B; AdminA/MemberA e equivalentes B com identidades independentes; IN/IN2 nunca consumidas e Active; IR consumida/revogada em A; IP Platform sem Membership.
- IX = consumo sem conta somente fixture excepcional de teste de inconsistência, nunca estado criado por operação normal.
- Convites Pending válidos, Pending vencidos ainda persistidos e Accepted/Cancelled/Replaced; referências/códigos sintéticos conhecidos só pelo harness; instantes T0 e limites referenciados a parâmetros aprovados.
- Cada caso recebe namespace de IDs próprio, baseline de quantidades/versões/consumos, chave C06 por ator/scope/operação e hash de payload canônico aprovado. Não compartilhar IN consumível entre casos.
- CT/CI/CP = logins core_tenant/core_identity/core_platform; exec_* são owners NOLOGIN observados pela cadeia, não credenciais de cliente.
- **D**, **D1/D2** = conexões de execução; **O** = conexão observer separada, com credencial exclusiva do harness para evidência, nunca usada como prova de negativa runtime. Capturar session_user/current_user por papel/aresta.
- **P/P1/P2** = probe de integridade candidato restrito ao ensaio, para alcançar estado intermediário inválido impossível pelos comandos públicos. Depende de D5: desenho/grants/failpoints aprovados; preservar triggers/policies sob teste, sem alterações no runtime ou exceção na Foundation. Não confundir sucesso do probe com privilégio de aplicação.

Probes de DELETE/alteração de Owner são estruturais. Se guard anterior impede alcançar trigger diferida, demonstrar guard no runtime e catalogar alcance da trigger por revisão/probe especificamente aprovado; não desabilitar a proteção em teste e declarar cobertura dessa mesma proteção. Caso inalcançável recebe decisão humana documentada, não PASS automático. Sem probe aprovado, variante fica BLOQUEADA.

### 3.3 Sequência comum obrigatória S0–S7

Estas etapas e os critérios de 3.6 fazem parte de TODOS os casos da seção 4.

S0: verificar gate/dependências aprovados; registrar revisão do plano/ADRs, commit+hash de arquivos não rastreados, manifesto do ambiente, versões reais, configuração de isolamento/pool/retry/timeouts aprovada e variante.
S1: criar fixture por mecanismo aprovado no ambiente descartável; capturar baseline via O, sem reutilizar credencial elevada na ação runtime.
S2: identificar papéis/conexões/PIDs e iniciar unidade de trabalho/contexto definido no cenário; leituras API passam pelo adapter real quando indicado.
S3: executar passos da linha e variantes, com barreiras explícitas; coletar sequência/eventos, status/código HTTP e erro interno sanitizado.
S4: aguardar término do COMMIT/ROLLBACK, incluindo triggers diferidas. Em timeout de transporte, não presumir resultado.
S5: observar estado persistido em O, fora da transação testada e com leitura nova; comparar quantidades/IDs/versões/histórico/eventos/recibos, inclusive ausência de dados B. Leituras internas antes de commit não provam persistência.
S6: comparar esperado/observado por assertion, anexar evidências e classificar PASS/FAIL/BLOQUEADO/INCONCLUSIVO somente na execução futura (Q10.b pode receber CARACTERIZADO conforme 3.7); limpar fixture pelo mecanismo operacional autorizado.
S7: verificar conexão/baseline limpos e finalizar relatório sanitizado. Falha de cleanup/instrumentação torna execução inconclusiva/bloqueante para a próxima, nunca sucesso silencioso.

### 3.4 Barreiras e conexões físicas

- **B-L**: D1 obtém lock e sinaliza ponto nomeado; D2 chega ao ponto de aquisição; O/coordenador comprova a espera real (relação de bloqueio/sinais do servidor e log da instrução). Só então liberar D1 para commit/rollback; D2 relê e conclui. Reexecutar trocando vencedor.
- **B-V**: ambos clientes leem a mesma versão e sinalizam prontidão; disputar mutação; observar ETag/conteúdo final.
- **B-T**: transação segura lock; clock da Application avança para após expiresAt de forma aprovada; verificar relógio efetivamente usado, liberar espera e observar validação de prazo.
- **B-C**: sinal de commit confirmado ou fronteira incerta do commit, seguido de supressão da resposta/interrupção de transporte por failpoint aprovado.
- **B-A**: falha determinística na escrita de evento antes do commit, sem desabilitar o trigger sob teste.
- **B-D**: ciclo de espera controlado exclusivamente no probe de ensaio; banco escolhe vítima. Não introduzir inversão C1 no código de produto.

Registrar pg_backend_pid() de D1 e D2, distintos nas disputas; pooling exige mesmo PID entre leases na variante de reuso e novo PID na de descarte. Conexão lógica nova não prova conexão física nova/reutilizada. Pooling multiplexado pode invalidar a prova; aprovar configuração do harness em D4. Um teste não depende de sleeps para ordenar transações: deadline do harness serve para abortar com diagnóstico e deve ser aprovado, nunca inferir sucesso por elapsed time. pg_locks sozinho não prova todos os locks de linha; combinar cronologia de instruções, relação de espera, inspeção de bodies e estado final.

### 3.5 Evidências e oracles

| Evidência | Conteúdo mínimo verificável |
|---|---|
| E-DB | Baseline/pós-commit em O: IDs/estados/versões/quantidades sintéticas; checks de relações, consumo e ausência de efeitos; erro/constraint/SQLSTATE interno quando existente |
| E-API | Método/rota/correlationId, scheme identificado sem credencial, payload redigido, status/código/DTO autorizado/ETag; ausência explícita de campos secretos/alheios |
| E-ACL | Manifesto de roles/attributes/membership, owners, ACLs efetivas por coluna/tabela/schema/função, defaults, policies ENABLE/FORCE, search_path, volatilidade e grafo de chamadas; revisão assinada |
| E-LOCK | PID/ator/transação, pontos de barreira, ordem de instruções, espera/bloqueador, commit/rollback e revalidação posterior; distinguir lock explícito e interno |
| E-POOL | PID por lease, estado transacional e contexto baseline, cleanup/reset/descarte observado; não apenas connection string |
| E-TRACE | Cronologia correlacionada do caso, função/owner e instrução, failpoint e confirmação de commit ou incerteza; sem segredos |
| E-OPS | Ensaio de restore/expurgo/migração: autorização, origem/destino sintéticos, marcos, reconciliação, resultado e responsável |
| E-REVIEW | Decisão/revisão humana identificada, escopo, justificativa, risco aceito/rejeitado, data e hashes dos artefatos |

Arquivos futuros de evidência recebem runId/caseId/variantId e integridade verificável; armazenamento, prazo/acesso e redaction dependem de D6. Nunca registrar JWT, cookie, código, recipientReference, senha, string de conexão ou payload sensível. Corpo de função/policy é artefato de inspeção, não autorização para alterar grants.

SQLSTATE só é esperado se a camada realmente alcançar aquela condição: 23505 unique; 23503 FK; 23502 NOT NULL; 23514 CHECK; 42501 privilégio; 22P02 conversão UUID quando realizada no banco; 40P01 deadlock; 40001 serialização quando variante aplicável. Erros de trigger customizada dependem de D3; não inventar código nem aceitar qualquer exceção como sucesso. Erros de autorização/ETag na Application podem não produzir SQLSTATE. Mapear constraint conhecida antes de traduzir; interna não conhecida é falha, não 409 genérico. Mensagem SQL nunca chega ao cliente.

### 3.6 Critério comum de aprovação/reprovação e dependências

Cada linha da seção 4 especifica o esperado; **aprovação exige TODAS as assertions dessa linha + S0–S7, em todas as variantes aplicáveis**. FAIL se houver um dado/contagem/segredo cruzado pela Application legítima ou fora do escopo concedido e aprovado, efeito parcial, grant extra não justificado, versão sobrescrita, sucesso antes do commit, duplicação, prova de espera faltante ou resultado divergente do oracle. Exceção de interpretação, não de segurança: Q10.b caracteriza o limite de credencial SQL comprometida com contexto integralmente forjado; acesso nesse adversário não reprova automaticamente RLS segundo o modelo declarado nem comprova isolamento; D1 aprovou a fronteira confiável da Application com risco residual explícito. Não aplicar a Q10.b a regra genérica de zero acesso que rege Q10.a e os caminhos legítimos. Se faltarem contrato aprovado/versão/clock/timeout/grant/probe, registrar BLOQUEADO. Falha de instrumentação/PID/barreira é INCONCLUSIVO e não atende o gate.

Contagens de PASS/FAIL futuras serão por variante, com agregação do cenário principal; caso principal só PASS com todas variantes obrigatórias PASS. Q10 tem a exceção explícita de 3.7: b recebe CARACTERIZADO, com decisão D1 já registrada; a execução/evidência continuam necessárias, sem ser contabilizada como segurança comprovada. N/A exige decisão humana por versão/superfície e justificativa, nunca para ocultar falha de segurança. Sobreposição Q11/K10, V11/K09 etc permite evidência compartilhada com links e variante exata, sem dupla contagem de execução.

Dependências comuns: D1–D6 da seção 8 conforme campo final de cada caso; fixture/predecessores válidos; G1 aprovado e autorização distinta para código e para ambiente; manifestos iguais ao alvo pretendido. Riscos comuns: owner/migrator usado indevidamente, grants de probe vazando para produção, snapshot do observer antigo, relógio/sleep mascarando corrida, reset automático mascarando ausência de contexto, política customizada diferente do manifesto.

### 3.7 Q10 — Dois oracles, sem aceite implícito de risco

Q10.a representa acesso pela Application legítima: IA autenticada não pode escolher IB/orgB por dados externos. Qualquer leitura/contagem/efeito B é FAIL P0 e não pode ser aceito como consequência normal de RLS. Q10.b representa uso direto de credencial SQL comprometida com todos os valores de IB coerentes: é caracterização do limite de confiança declarado em Q1 da especificação, não prova de que PostgreSQL autentica OIDC.

O resultado de b registra alcance observado, inclusive eventual COMMIT nos limites da credencial, com evidência redigida e sem dados reais. Não exigir negação que o modelo não promete; não presumir acesso antes da prova. Se houver acesso além dos privilégios concedidos/policies/grafo aprovados (por exemplo bootstrap sob CT), continua sendo FAIL de F. A aceitação D1 abrange o limite de processo/credencial comprometido; não aceita vazamento no fluxo legítimo nem grant excessivo.

Estado específico da variante b: CARACTERIZADO é registro factual, não PASS de segurança ou prova de isolamento. D1 está APROVADA: a Application é a fronteira confiável, com comprometimento da credencial SQL explicitamente residual. Isso não executa Q10 nem fecha o cenário; faltam as evidências e dependências técnicas. Não reabrir D1 nem introduzir autenticação SQL/token próprio. Nenhum resultado real foi obtido aqui.

### 3.8 V08 — Contrato do contexto técnico dos validadores

Este detalhamento concretiza V3 da especificação complementar; não muda C1, owner, mecanismo de trigger diferida ou comportamento dos contratos. Um comando por transação e uma organização empresarial afetada, como V3 já exige. Não cria tabela, sessão privilegiada ou armazenamento adicional.

| Elemento conceitual do contexto | Origem / responsabilidade / verificação |
|---|---|
| operation_id e tipo de comando | Porta restrita deriva de operação autorizada, não aceita flag de autorização externa. Correlaciona evento/recibo; não funciona como segredo ou permissão |
| Identificador da transação atual | Obtido do PostgreSQL pelo adapter/porta dentro da transação; conferido no disparo para evitar contexto de outra transação. Nome de campo/representação física a congelar em D3, sem criar SQL |
| organization_id afetada | Resolvida do vínculo ou convite provado; no bootstrap, UUID da organização nova validada/criada na mesma transação. Nunca org escolhido por payload como autoridade |
| IDs afetados e âncoras esperadas | Porta já conhece IDs de conta/membro/consumo criados ou alterados; trigger usa sua tabela e OLD/NEW para confirmar que os eventos pertencem ao escopo. Não substituir conjunto consultado por lista externa de membros |
| Ciclo de vida | Configuração local à transação pela porta, depois de provas e locks C1 necessários e antes do primeiro DML relevante; preservada até terminar COMMIT/ROLLBACK, inclusive após retorno da função de comando. Não limpar no retorno da função antes do commit |
| Transporte / limite | Valores técnicos em contexto local conforme R4, separados semanticamente de T/SELF/REC/BOOT. Nenhum valor ou GUC protocol_ok atesta que lock foi adquirido; todos permanecem dentro da fronteira de confiança da aplicação Q1/D1 |

O contexto de integridade não contém autorização de usuário nem exige ator com Membership ativo. A autenticação e a autorização ocorreram na porta de comando; o validator só verifica consistência estrutural. Troca/ausência de contexto, tx diferente ou OLD/NEW de org não correspondente abortam a transação; não ignorar evento como se fosse outra operação.

**Visibilidade técnica sob exec_integrity:** SELECT exclusivamente em IDs/referências/status/role de Organization, Account, Membership e Consumption da organização afetada, com policies-base cujo predicado é igualdade de org ao escopo técnico válido. Essas policies não usam role/status do ator, não chamam helper T, SELF, REC, BOOT ou caso de uso, não filtram somente linhas Active nem lista de IDs de membros recebida da Application. Para consumo, relação pelo OrganizationId histórico; para contas/membros, org da linha. A autorização empresarial do ator não participa desse grafo. Assim, a consulta abrange todos os fatos técnicos da org necessária para contar/verificar Owner; não revela esses fatos ao chamador.

**Checks positivos obrigatórios no estado final:**
1. Contexto válido e transação/OLD/NEW coerentes; Organization esperada existe exatamente uma vez. Uma org runtime não pode sumir silenciosamente: DELETE é guardado; manutenção excepcional permanece fora desta prova.
2. Cada Account referenciada pelo evento/porta deve ser encontrada no escopo esperado; presença do consumo e FK completa é conferida conforme M2. Conta ausente não é prova de inatividade nem de NotLinked.
3. Account Active deve ter contagem exatamente 1 de Membership Active coerente. Membership Active exige Account existente Active da mesma org; revogação deve refletir ambos estados. Account Revoked/Archived pode não ter membro Active; zero Active nesse caso é condição verificada, não aprovação por ignorar o Account.
4. Organization Active deve ter contagem pelo menos 1 de Membership Owner Active ligada a Account Active, no conjunto técnico completo. Contagem zero invalida. Org não Active não exige Owner Active por essa invariante, mas exige âncora/contexto válidos e as demais relações.
5. Resultado de consulta vazia inesperada, erro de permissão/RLS ou escopo incompleto é falha de validação, não condição TRUE por vacuidade. Comparar conjunto de fatos com O no ensaio e conferir policies/corpos: cardinalidade sozinha não prova ausência de filtro indevido.

Funções sob exec_integrity são NOLOGIN, sem bypass/ownership, sem DML, sem UPDATE para locks e sem EXECUTE externo. Retornam apenas sucesso interno/erro sanitizado; sem DTO/lista/perfil/segredo. O conjunto técnico de uma org é maior que o visível ao ator; isso é necessário para integridade, não um privilégio empresarial concedido ao usuário. Não oferecer função de diagnóstico público com esses dados.

**Concorrência:** locks de Identity/Organization/Accounts/Members são adquiridos na porta em C1 antes dos DMLs relevantes e mantidos até finalizar commit. Bootstrap protege org nova pela própria inserção e unicidade. Validator VOLATILE faz leituras finais sem lock explícito novo, sem DML reparador e sem chamadas gravadoras. Organização serializa escritores runtime conforme V1; não confundir isso com ausência de locks internos FK/UNIQUE/SELECT normais do PostgreSQL. Não acrescentar aquisição tardia para consertar contexto perdido. Savepoint que reverta contexto/locks obriga abortar unidade; R4 não autoriza continuar com dados parciais.

V08 preserva suas variantes e acrescenta observação explícita de: contexto persistindo depois do retorno da porta e antes do COMMIT; validador vendo membro revogado e Owner completo sem T; missing org/Account/Member/Owner causando erro apropriado; conjunto técnico igual ao de O; adulteração de contexto e rollback a savepoint falhando fechado. Não são cenários novos, e nenhuma dessas provas foi executada.

### 3.9 F10/D2 — Matriz da união efetiva proposta por owner SQL

**Não há catálogo executado/inspecionado nesta rodada.** A coluna Grants efetivos representa a união documental candidata de R/F, a confirmar em G1b, com ACL por tabela/coluna, grants herdados, policies aplicáveis e EXECUTE transitivo. SELECT/UPDATE necessários a locks também entram na união. Lista fixa de operações no corpo da função não retira privilégios SQL do owner; separação de nomes/funções não segrega ACL.

Abreviações desta matriz: S=SELECT nas colunas mínimas; I=INSERT nas colunas do comando; U=UPDATE nas colunas indicadas/necessárias a locks; sem DELETE runtime. Referências M-ID/LINK/ORG/ACCOUNT/MEMBER/HIST/SLOT/INV/DEMO/AUDIT/IDEM são M2 do complementar. Nomes/tipos de colunas finais seguem D2; nenhum grant é executado/aprovado por esta tabela.

| Owner SQL | Funções / grupo existente | Grants efetivos propostos (união, não ACL por função) | Operações necessárias | Capacidade excedente relativa a uma função | Risco | Recomendação |
|---|---|---|---|---|---|---|
| exec_identity | resolve_own_context, prepare_own_identity, own_recipient_reference, preview_own_invitation, accept_own_invitation; helpers internos de evento do aceite | S técnico próprio ID/LINK/HIST/ACCOUNT/MEMBER/ORG, referência e convite exato; I ID/LINK/referência e, no aceite, HIST/ACCOUNT/MEMBER Member/AUDIT; I/S SLOT; U colunas técnicas necessárias aos locks em ID/ORG/ACCOUNT/MEMBER/SLOT e status/accepted_at/account/version de INV; sem S DEMO/auditoria Tenant/lista de membros | Mapear identidade, resolver estado, preparar referência, provar convite e criar vínculo Member atômico | Resolver/preview compartilham capacidade de inserir conta/membro/consumo e alterar convite necessária a accept; não são SQL read-only por terem corpo de consulta | Defeito no corpo/prova ou policy ampliada pode usar escrita do owner fora do caminho previsto; ausência de Membership não prova elegibilidade | **Manter compartilhado**, com projeções fixas, prova própria REC antes de revelar alvo, policies de escopo sem bypass, guards/constraints, grafo e testes F08/F10/V12. Nenhuma evidência documental demonstra necessidade de split. Separar resolver/prepare de accept somente se escopo de policy ou cadeia real não contiver risco concreto |
| exec_tenant | Conferência Tenant, emitir/cancelar/substituir convite, alterar/revogar Membership, criar/alterar DemoRecord, helpers de auditoria/recibo | S vínculo próprio/atores/alvo provado, referência/HIST, INV/SLOT/DEMO/recibo; I INV/SLOT/DEMO/AUDIT/IDEM; U ACCOUNT.status/version, MEMBER.role/status/version, INV transições/version/sucessor, DEMO.title/version, campos técnicos mínimos para locks e recibo; sem I ORG/ACCOUNT/MEMBER/HIST, sem DELETE ou mutação AUDIT | Gestão limitada C08, operações demonstrativas, estado de convites e replay atômico | Função de conferência/convite partilha capacidade de mudar papel/revogar e escrever DEMO; funções de DEMO partilham acesso técnico a prova/hash de convite requerido por outra função | Autoelevação via bug, retorno indevido de hash, OR de policies ou aresta fora da matriz; nenhum split por nome contém isso | **Manter compartilhado** no escopo Tenant atual: nenhum corpo arbitrário, autorizar alvo/ator C08, projetar retorno sem hash, grants mínimos sem org mutável, RLS/guards, inspeção do grafo e V12. Remover grant/aresta desnecessários primeiro. Split só se surgir acesso excedente não contido com esses controles |
| exec_bootstrap | Conferência Platform, bootstrap_organization_owner, helpers próprios AUDIT/IDEM | S identidade/referência/HIST do proprietário provado e recibo Platform; I ORG/HIST/ACCOUNT/MEMBER Owner/AUDIT/IDEM; U técnico mínimo na identidade para lock e recibo quando exigido; S mínimo de âncoras da operação para replay/validação, sem SELECT empresarial geral | Criar org/proprietário atomicamente e recuperar comprovante exato | Conferência sob mesmo owner tem capacidade SQL de criação de empresa/Owner; não pode ser descrita como owner somente leitura | Bug de prova/grafo pode antecipar criação ou permitir owner inelegível; scheme/MFA depende da Application D1 | **Manter compartilhado**, isolado dos owners Tenant/Identity como já previsto. EXECUTE só CP, prova C02 e elegibilidade rechecadas, vínculo permanente/commit/recibo. Não devolver dados Tenant. Separar conferência de bootstrap apenas se implementação evidenciar aresta/policy incontível |
| exec_context | own_tenant_allowed e helpers booleanos privados de mesmo alcance | S somente IDs/status e role necessários ao join próprio ID/ACCOUNT/MEMBER/ORG sob policies-base SELF/contexto; nenhum I/U/DELETE/lock | Responder booleano para policies sem recursão | A função pode internamente ler fatos próprios além do booleano exposto; nenhum DML agregado | Mudança de projeção/policy pode revelar fato ou recursão; owner não deve absorver consultas integrais de integridade | **Manter grupo de leitura próprio existente**, sem juntar a writers/exec_integrity. F05/Q09 verificam grafo sem ciclo, escopo e retorno booleano |
| exec_integrity | validate_account_membership, validate_org_owner e guards conforme V2/F2 | S de IDs/referências/status/role em ORG/ACCOUNT/MEMBER/HIST de toda org afetada via 3.8; guard usa OLD/NEW; nenhum I/U/DELETE/lock/SELECT de perfil/DEMO/segredos, sem EXECUTE externo | Verificar fatos finais completos independentes de papel/estado do ator | Cada validator pode ler fatos técnicos de integridade da org também usados pelo outro, mais amplos que T do ator | Retorno/call graph indevido transformaria leitura interna em exfiltração; scope incorreto ocultaria fatos | **Manter validators compartilhados neste owner separado**, como desenho atual. Policies técnicas não dependem de T, invocação só por trigger, retorno interno sem dataset e escopo/evento verificado. Não fundir com helper empresarial |
| ddl_owner/core_migrator (manutenção, não owner runtime a reutilizar) | DDL/manutenção autorizada, fora de funções web | Ownership/DDL e demais capacidades operacionais a inventariar fora de runtime; podem alterar RLS/guards | Evolução do schema e recuperação sob autorização específica | Amplo poder além de qualquer comando de produto | Pode contornar garantias; não contido por função/RLS do runtime | **Preservar separação existente**, sem credencial/SET ROLE/aresta runtime; não ampliar operação nesta rodada |

Os grants de leitura técnica acima não incluem automaticamente displayName C02: P1-A continua exigindo definir fonte/projeção mínima antes de G4, sem SELECT de perfil global. Armazenamento de sessão/continuation/draft fora de F2 não é atribuído implicitamente a esses owners. Auditoria/recibo são helpers do mesmo owner/alcance do comando, nunca novo owner universal ou novo EXECUTE genérico.

**Solução de menor complexidade recomendada:** conservar os grupos atuais. União justificada por operações necessárias é aceitável como recomendação documental quando todos os corpos são fixos/revisáveis, gates de entrada são completos, runtime não recebe DML/SET ROLE/ownership, RLS não amplia escopo de funções por policy permissiva, e nenhum retorno/aresta expõe capacidades extras. Isso é controle verificável de software+ACL+policy, não isolamento SQL entre funções do mesmo owner. D2 APROVADA quanto ao desenho dos cinco grupos; grants efetivos e grafo de chamadas continuam sem comprovação, bloqueando a liberação de G1.

Escalonar para split mínimo somente com desvio concreto: policy necessária a uma função força alcance não autorizado em outra que corpo/entrada não conseguem restringir; aresta necessária leva a executor maior fora do boundary; função aceita código/identificador livre; necessidade de conceder a chamador externo leitura/DML extra. Primeiro remover entrada/grant/aresta desnecessária ou corrigir policy; se insuficiente, registrar função/capacidade/ataque e recomendar separação apenas desse boundary. Nada no estado documental atual demonstra essa inevitabilidade. Simplesmente haver grants de escrita no owner de resolver/preview é risco conhecido a justificar/testar, não prova isolada de necessidade de mais owners.

D2 já aprovou manter exec_identity, exec_tenant, exec_bootstrap, exec_context e exec_integrity. A aprovação não equivale a grants aplicados ou grafo comprovado: manifesto efetivo, catálogo e corpos ainda precisam de comprovação antes da liberação de G1. G0 permanece aprovado com condicionantes; F10 não executado e G1 bloqueado.


## 4. Matriz completa de cenários propostos

Prioridade indica severidade da propriedade protegida, não teste executado. Todos possuem estado inicial **NÃO EXECUTADO**. Referências M/R/C/Q/V/F/H e K são seções do documento complementar; C01–C10 são contratos; I são os cenários de identidade. ADR-0009 sustenta o desenho de todos; ADR-0008 rege N3/N4/N5/N6. O cruzamento por caso nas linhas abaixo forma a matriz cenário→ADR→contrato→I/K→invariante→evidência→gate.

### 4.1 Q — Contexto, autenticação e isolamento (12)

#### Q01 — Autenticação e schemes

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N1/N2; C01/C08/C09; I01/I02 |
| Gate, modo, fixture, papéis/conexões | G4; A+H; IA/IP/IN; CT/CI/CP |
| Sequência, além de S0–S7 | Autenticar IA validamente e variar separadamente assinatura, issuer, audience, expiração, ID token como bearer e cookie isolado na API; tentar scheme Tenant em Platform; executar POST web sem/errado antiforgery e chamar caso de uso via adapter não HTTP. |
| Resultado DB/API e transação | Válido resolve A; variantes inválidas 401 API sem HTML; scheme/policy administrativo nega sem bootstrap. POST web sem prova antiforgery retorna 400 antes do comando. Nenhum DML empresarial ou consumo novo nas negativas. SQLSTATE não aplicável. |
| Evidência, dependência e risco específico | E-API/E-DB/E-TRACE; decisão D1 e adapters aprovados. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### Q02 — Autoridade do escopo

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N1/N2; C01/C08/C09; I02 |
| Gate, modo, fixture, papéis/conexões | G4; A; IA/IB; CT |
| Sequência, além de S0–S7 | Com IA, adulterar org em rota, payload, header e claim empresarial; repetir para item/lista/comando e comparar com pedido legítimo A. |
| Resultado DB/API e transação | Nada de B, inclusive contagem/IDs; rota/recurso inacessível 404 genérico, ação proibida 403 conforme C09. Nenhum commit cruzado; não trocar contexto para B. SQLSTATE N/A na API. |
| Evidência, dependência e risco específico | E-API/E-DB/E-TRACE; endpoint/DTO candidato aprovado. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### Q03 — SELF e resolução somente própria

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N1/N3; M-ID/LINK/HIST; Q1/Q2; C02/C09; I01/I08 |
| Gate, modo, fixture, papéis/conexões | G2+G4; A; IA/IN; CI |
| Sequência, além de S0–S7 | Resolver SELF IA e IN; tentar parâmetro de outra identidade e recuperar cadastro através de e-mail; repetir consulta e comparar estado persistido. |
| Resultado DB/API e transação | IA Linked A; IN 200 NotLinked proposto sem org/conta/papel; outra identidade não é selecionável. GET não cria identidade/conta/membro/evento de mutação. SQLSTATE N/A. |
| Evidência, dependência e risco específico | E-DB/E-API; mapeamento UserId/IdentityId e NotLinked dependem D1. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### Q04 — NotLinked e acesso perdido

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N2/N3; Q2; C04/C07/C09; I04/I08/I10 |
| Gate, modo, fixture, papéis/conexões | G2+G4; A; IN/IR/IX; CI/CT |
| Sequência, além de S0–S7 | Resolver IN sem histórico, IR revogada e IX com consumo sem conta; tentar DemoRecord e geração de referência com IR; simular dependência indisponível. |
| Resultado DB/API e transação | Só IN recebe NotLinked/preparação; IR 403 sem novo vínculo; IX 500 sanitizado, dependência conhecida 503, nunca vazio/NotLinked. Nenhuma mutação de sucesso. SQLSTATE interno registrado sem vazamento. |
| Evidência, dependência e risco específico | E-DB/E-API/E-TRACE; IX via fixture excepcional aprovada, não estado normal. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### Q05 — REC sem Membership

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N2/N3/N6; Q2/F2; C02/C03/C04; I03/I04/I05 |
| Gate, modo, fixture, papéis/conexões | G2+G4; A; IN e convite A; CI |
| Sequência, além de S0–S7 | Com IN provar código/destinatário; repetir com código errado, outro destinatário e identidade consumida; preview válido antes de aceite; tentar usar REC para ler dados de A. |
| Resultado DB/API e transação | Preview autorizado mínimo, sem commit de aceite; provas inválidas 404 indistinguível; consumida após prova 403 conforme C04. REC não cria T nem libera membros/DemoRecord. SQLSTATE N/A. |
| Evidência, dependência e risco específico | E-API/E-DB e ausência de segredo em E-TRACE; código só fixture sintética. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### Q06 — BOOT sem acesso Tenant

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N2/N5/N6; Q2/F2; C02/C08/C09; I09 |
| Gate, modo, fixture, papéis/conexões | G2+G4; A; IP+IN2; CP |
| Sequência, além de S0–S7 | Conferir proprietário com referência; criar org C pelo comando autorizado; tentar ler membros, DemoRecord e auditoria C como IP; variar MFA/scheme e referência. |
| Resultado DB/API e transação | Commit atômico autorizado com Owner IN2; zero Membership IP; leituras Tenant negadas e provas inválidas sem efeito. Retorno só comprovante C09; SQLSTATE N/A para negativas de API. |
| Evidência, dependência e risco específico | E-API/E-DB/E-ACL; decisão de atestação Platform D1. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### Q07 — Contexto ausente e inválido

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N2; R2/R4/Q3; C01/C08; I02 |
| Gate, modo, fixture, papéis/conexões | G2; A; CT e IA; D |
| Sequência, além de S0–S7 | Abrir transação limpa sem contexto; omitir um ID por variante, usar vazio, UUID malformado e IDs IA/AccountB/orgA; tentar leituras sem filtro e chamada gravadora. |
| Resultado DB/API e transação | Zero linha ou negação conforme oracle G1 por superfície; conversão inválida aborta unidade, sem retorno ao contexto anterior. Sem commit empresarial. SQLSTATE 22P02 só se conversão UUID chegar ao PostgreSQL; ACL negada 42501; nenhum erro arbitrário conta como aprovação. |
| Evidência, dependência e risco específico | E-DB/E-TRACE; D2 fixa negação por operação antes de executar. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### Q08 — Isolamento SQL e predicados

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N2/N8; R3; C08/C09; I02 |
| Gate, modo, fixture, papéis/conexões | G2; A; todos papéis runtime; IA/IB; D |
| Sequência, além de S0–S7 | Com T A, ler coleção/item e contagem sem filtro; tentar DML direto proibido; nas portas, tentar inserir/referenciar org B e mover org de recurso; repetir SELECT/INSERT/UPDATE por tabela concedida e Owner/Admin/Member, incluindo negativa de leitura de membros/auditoria para Member. |
| Resultado DB/API e transação | Leituras só A; DML sem grant 42501; violações de escopo pela porta rejeitadas sem B. Contraste USING/WITH CHECK inspecionado; zero dado/commit cruzado. FK 23503 somente quando alcançada pelo probe autorizado. |
| Evidência, dependência e risco específico | E-DB/E-ACL; inventário completo de tabelas/colunas e D2. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### Q09 — Helper e grafo sem recursão

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N2/N8; R2/Q3/F2; C08; I02/I10 |
| Gate, modo, fixture, papéis/conexões | G1+G2; A+H; CT, exec_context por chamada real |
| Sequência, além de S0–S7 | Inspecionar policy Tenant→helper→policies-base; executar leituras próprias e cruzadas, alterar papel/revogar em outra transação e consultar novamente; tentar chamar helper para alvo arbitrário. |
| Resultado DB/API e transação | Somente booleano próprio, sem recursão/DTO/lista; nova instrução observa direitos atuais. Sem cache global de T; helper não substitui locks. Nenhuma alteração por consulta; SQLSTATE de erro de recursão é reprovação. |
| Evidência, dependência e risco específico | E-ACL/E-TRACE/E-DB; não testar como owner da tabela. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### Q10 — Application legítima versus contexto SQL integralmente forjado

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | Q10.a: P0 de isolamento; Q10.b: caracterização de limite com D1 APROVADA e comprometimento da credencial SQL como risco residual explícito, sem isolamento comprovado. NÃO EXECUTADO em ambas |
| Proteção / rastreabilidade | ADR-0009; N1/N8; Q1/F3; C01/C08; I02; exceção interpretativa expressa em 3.6 |
| Gate, modo, fixture, papéis/conexões | Q10.a G4 A: adapter legítimo IA, CT, alvo B; Q10.b G1+G2 A+H: credencial CT comprometida simulada em ambiente isolado, contexto coerente completo IB/AccountB/orgB, D+O |
| Sequência, além de S0–S7 | a: autenticar IA pelo middleware real, adulterar identificadores/claims empresariais de B e observar entrada/contexto derivado no servidor e acesso final. b: fora da Application legítima, usar credencial CT de ensaio e forjar todo contexto de IB; executar só superfície concedida CT, registrar SQLSTATE quando houver, dados/efeitos alcançáveis e comparar com matriz 3.9. Não chamar b de autenticação OIDC |
| Resultado DB/API e transação | a: negar B conforme C09, zero leitura/contagem/efeito B; vazamento = FAIL P0, nunca aceitação D1. b: registrar se CT alcança B dentro dos privilégios/policies do papel; modelo não garante negar identidade consistentemente forjada. Se uma mutação concedida confirmar, registrar e comparar estado final, sem promover legitimidade da ação. Excesso sobre a superfície CT aprovada ou acesso a bootstrap/migrator continua falha de privilégios F. SET LOCAL não é prova criptográfica |
| Evidência, dependência e risco específico | E-DB/E-API/E-ACL/E-REVIEW; manifesto e ambiente D5. D1 APROVADA formalmente pelo responsável quanto à fronteira Application e risco residual; aprovação não inferida de G0, não dispensa prova de Q10.a/b |
| Aprovação/reprovação | a: PASS só com isolamento integral demonstrado; qualquer acesso B é FAIL P0. b: registrar CARACTERIZADO quando alcance/limites forem reproduzidos com evidência; CARACTERIZADO não é PASS de segurança nem prova de isolamento. D1 aprovada encerra a pendência decisória da fronteira, não a execução deste cenário. G1 permanece BLOQUEADO pelas comprovações de grants/grafo e D3/D5. Instrumentação/evidência insuficiente = INCONCLUSIVO; não reabrir decisão aprovada sem contradição P0 concreta |

#### Q11 — Commit/rollback e pool físico

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N2; R4/Q3; C07/C09; I02; K10 |
| Gate, modo, fixture, papéis/conexões | G2; A; CT; IA/IB; D+O |
| Sequência, além de S0–S7 | Fixar lease física e registrar pg_backend_pid(); configurar A e terminar com commit, rollback e exceção em variantes; checkout seguinte sem contexto e depois B; observar baseline e conjunto de linhas. |
| Resultado DB/API e transação | Sem contexto não vê A; B só B; mesmo PID exigido nas variantes de reuso. Onde descarte é requerido, novo PID e evidência de descarte. Sem transação aberta no retorno ao pool; SQLSTATE capturado se existir, não fixado para falha de rede. |
| Evidência, dependência e risco específico | E-POOL/E-DB/E-TRACE; pool real do provedor aprovado, não conexão nova disfarçada. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### Q12 — Savepoint, cancelamento e contaminação

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N2; R4/Q3/V3; C07; I02/I10; K10 |
| Gate, modo, fixture, papéis/conexões | G2+G3; A; CT; IA/IB; D+O |
| Sequência, além de S0–S7 | Criar savepoint antes de contexto/locks, reverter até ele e tentar continuar; injetar cancelamento e falha de rollback/reset; repetir com baseline de sessão indevido sintético. |
| Resultado DB/API e transação | Abortar unidade por padrão; nenhuma continuação com prova/locks perdidos. Baseline indevido rejeitado/limpo antes de dados; reset incerto descarta backend. Consulta posterior B nunca mostra A; estado pendente não é devolvido ao pool. |
| Evidência, dependência e risco específico | E-POOL/E-TRACE/E-DB; falhas somente por instrumentação isolada aprovada D5. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

### 4.2 V — Integridade e protocolo de escrita (14)

#### V01 — Active sem Membership

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N4; M3/V2; C04/C09; I05/I09 |
| Gate, modo, fixture, papéis/conexões | G2; A; IN; probe autorizado P; D+O |
| Sequência, além de S0–S7 | No probe com triggers/RLS preservadas, construir Account Active/consumo coerente sem Membership; observar que estado intermediário não é confirmação; tentar COMMIT. |
| Resultado DB/API e transação | COMMIT recusado e zero Account/consumo/evento/recibo novos visíveis em O; erro de trigger conforme D3, não necessariamente 23503. Controle completo Account+Member confirma. API não deve ter rota que crie conta parcial. |
| Evidência, dependência e risco específico | E-DB/E-TRACE; D3 e D5, sem ampliar grants runtime. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### V02 — Exatamente um membro

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N4; M2/M3/V2; C04; I06 |
| Gate, modo, fixture, papéis/conexões | G2; A; conta A; P |
| Sequência, além de S0–S7 | Tentar segundo Membership para mesmo AccountId; testar campos obrigatórios nulos e um membro único válido. |
| Resultado DB/API e transação | Duplicação 23505 e nulidade 23502 quando essas constraints são alcançadas; unidade aborta sem segundo membro. Controle válido mantém cardinalidade 1; API não aceita adição arbitrária. |
| Evidência, dependência e risco específico | E-DB/E-ACL; identificar constraint específica, não aceitar qualquer 23505. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### V03 — Referências cruzadas

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N3/N4; M2/V2; C08; I02 |
| Gate, modo, fixture, papéis/conexões | G2; A; contas A/B; P |
| Sequência, além de S0–S7 | Tentar Membership orgB→AccountA, Account→consumo de outra identidade/org/AccountId e convite Accepted referindo conta de outra empresa; variar estado/papel inválido, versão não positiva e campos obrigatórios; inspecionar nullability e FKs. |
| Resultado DB/API e transação | FK conhecida 23503 ou CHECK 23514 identificado; nulidade 23502 quando alcançada; nunca aceitar NULL que burle par composto. ROLLBACK deixa todas referências originais; erro API sanitizado. |
| Evidência, dependência e risco específico | E-DB/E-ACL; lista de constraints congelada em G1. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### V04 — Estados Account/Membership

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N4/N6; M3/V2/V3; C07/C08; I10 |
| Gate, modo, fixture, papéis/conexões | G2; A; Member A; P/CT |
| Sequência, além de S0–S7 | Revogar só Membership e, em variante, só Account no probe; testar Membership Active com Account Revoked/Archived; executar revogação completa pela porta. |
| Resultado DB/API e transação | Parciais falham no COMMIT com erro D3; comando autorizado revoga os dois no mesmo commit e mantém consumo. Sem reativação. Auditoria/recibo conforme operação; próximas requisições negam. |
| Evidência, dependência e risco específico | E-DB/E-API; não permitir status compensatório fora de contrato. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### V05 — Owner válido por organização

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N5; M3/V2; C08/C09; I09; K09 |
| Gate, modo, fixture, papéis/conexões | G2; A; org nova e org A; P/CP |
| Sequência, além de S0–S7 | Criar Organization Active sem Owner, com Owner revogado ou Account Owner revogada; repetir organização com Owner/Account Active completos. |
| Resultado DB/API e transação | Invalidos falham no COMMIT, nenhum órfão; completo confirma. Alterar Owner pela API sempre nega C08, mesmo existindo outro; probe estrutural não cria função de transferência. |
| Evidência, dependência e risco específico | E-DB/E-API; estados de organização/erro D3. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### V06 — Cobertura de eventos

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N4/N5; V2; C04/C08/C09; I09/I10 |
| Gate, modo, fixture, papéis/conexões | G1+G2; A+H; P; fixtures isoladas |
| Sequência, além de S0–S7 | Inventariar triggers por tabela/evento; variar INSERT/UPDATE/DELETE, role/status, OLD/NEW e múltiplas mudanças na mesma linha; antecipar constraints em estado intermediário; repetir estado final válido. |
| Resultado DB/API e transação | Toda mudança relevante enfileira validação; estado final relido, não NEW antigo/WHEN que suprima checagem. Antecipação inválida aborta; não capturar erro para prosseguir. DELETE runtime segue guard; probe alternativo exige D5. |
| Evidência, dependência e risco específico | E-ACL/E-TRACE/E-DB; não desabilitar trigger para depois declarar que foi testada. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### V07 — Imutabilidade e guard de exclusão

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N3; M2/V2; C04/C08; I03/I04 |
| Gate, modo, fixture, papéis/conexões | G2; A; A e consumo/link; CT/CI/CP/P |
| Sequência, além de S0–S7 | Tentar mudar org/identity/account IDs, par OIDC e reassociação de link; tentar apagar consumo/Account/Membership/Organization por runtime e por probe com guard ativo. |
| Resultado DB/API e transação | Runtime sem grant 42501 ou guard específico D3 no probe; nenhum ID/histórico modificado. UPDATE de estado permitido não libera consumo; SQLSTATE inesperado reprova oracle. |
| Evidência, dependência e risco específico | E-DB/E-ACL; manutenção excepcional não é caminho de produto. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### V08 — Contexto técnico de integridade e visibilidade final

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N4/N5/N7; V3 e 3.8 deste plano; C08/C09; I09/I10 |
| Gate, modo, fixture, papéis/conexões | G2+G3; A+H; CP bootstrap e CT revogação; funções de trigger sob exec_integrity NOLOGIN; D+O e probe D5 |
| Sequência, além de S0–S7 | Instalar contexto técnico 3.8 pela porta autorizada, manter até COMMIT e confrontar escopo com OLD/NEW. Variantes: bootstrap sem Membership IP; revogação completa Account/Member; ator sem T no disparo via probe; org/operation/identificação de transação ausente ou divergente; Account/membro/Owner inexistentes; troca de contexto/savepoint; tentativa de filtrar fatos pelo ator. Comparar conjunto técnico completo visto pelo validator com O, inclusive linhas Revoked, e revisar grafo/policies |
| Resultado DB/API e transação | Validação usa organização afetada e fatos finais, não autorização do ator. Policies técnicas não chamam T/SELF/REC/BOOT/helper nem escondem estados/Owner. Org e contas esperadas devem existir; ausência de contexto/âncora/conjunto obrigatório invalida COMMIT, não retorna integridade válida. Account Active tem exatamente um Member Active coerente; Member Active exige Account Active; org Active tem Owner válido. Inválido desfaz mutação/evento/recibo, sem resposta de sucesso antes do commit; erro D3 sanitizado. Sem novos locks explícitos, escrita ou reparo no validator |
| Evidência, dependência e risco específico | E-TRACE/E-DB/E-ACL/E-LOCK/E-REVIEW; conjunto esperado versus observado, escopo técnico até COMMIT e instruções sem locks tardios. D3/D5; contexto local não é segredo/prova criptográfica nem marcador de lock adquirido |
| Aprovação/reprovação | Todas variantes obrigatórias obedecem 3.8; ocultar uma linha relevante, aprovar conjunto vazio, depender de T do ator, perder contexto antes do commit ou adquirir lock explícito tardio = FAIL P0. Resto conforme 3.6; nenhuma prova executada nesta rodada |

#### V09 — Sem locks explícitos tardios

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N7; C1/V1/V3; C05/C08; K01–K09 |
| Gate, modo, fixture, papéis/conexões | G1+G3; A+H; portas gravadoras e trigger bodies; D1/D2/O |
| Sequência, além de S0–S7 | Revisar corpo/grafo de triggers; executar cada mutação instrumentada até pré-commit e commit; correlacionar ordem de chamadas e instruções com bloqueios observados. |
| Resultado DB/API e transação | Triggers só consultam, sem FOR UPDATE/SHARE/advisory lock/reparo/comando aninhado. Locks internos FK/UNIQUE não são classificados como violação dessa regra. Não inferir locks de linha só de pg_locks. |
| Evidência, dependência e risco específico | E-TRACE/E-ACL/E-LOCK; inspeção humana obrigatória, nenhuma prova só por ausência de espera. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### V10 — Snapshot depois da espera

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N7; C1/V1; C05/C08; I10; K03 |
| Gate, modo, fixture, papéis/conexões | G3; A; CT; Member A; D1/D2/O; B-L |
| Sequência, além de S0–S7 | D1 adquire org e muda papel/estado; D2 inicia comando e bloqueia; provar espera; D1 confirma; D2 adquire lock e executa instrução nova de revalidação; inverter ordem. |
| Resultado DB/API e transação | D2 enxerga commit anterior à nova leitura e nega comando se perdeu direito. Sem reuso de snapshot STABLE pré-espera; não escreve com decisão velha. Ganho legítimo anterior mantém semântica C05. |
| Evidência, dependência e risco específico | E-LOCK/E-TRACE/E-DB/E-API; D4, duas sessões físicas diferentes. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### V11 — Write skew e Owner concorrente

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N5/N7; V1/V2; C08; I10; K09 |
| Gate, modo, fixture, papéis/conexões | G3; A; org com dois Owners apenas fixture; P1/P2/O; B-L |
| Sequência, além de S0–S7 | API tenta alterar Owner e deve negar; no probe estrutural aprovado, duas transações tentam remover/rebaixar Owners diferentes; serializar por org; testar ambas as ordens. |
| Resultado DB/API e transação | API nunca habilita operação proibida; no probe, não confirmam ambas deixando zero Owner. Segunda valida estado atual e aborta; trigger não tenta lock tardio. Sem novo endpoint Owner. |
| Evidência, dependência e risco específico | E-LOCK/E-DB/E-TRACE; D5 explícita, teste estrutural distinto de C08. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### V12 — Todos os escritores e C1

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N6/N7/N11; F2/V1; C02–C09; I03–I10 |
| Gate, modo, fixture, papéis/conexões | G1+G3; A+H; CT/CI/CP; todas portas |
| Sequência, além de S0–S7 | Inventariar funções/casos de uso/handlers/rotas, triggers e jobs efetivamente existentes no futuro; ligar cada escrita a lock/revalidação/evento/commit; exercitar cada caminho e tentar DML direto. |
| Resultado DB/API e transação | 100% dos escritores enumerados cobertos; nenhuma rota lateral, grant direto ou chamado aninhado inverte C1. Ausência de código hoje = pendência, não aprovação. Writes observados correspondem ao inventário. |
| Evidência, dependência e risco específico | E-ACL/E-TRACE/E-REVIEW; novo escritor reabre G1/G3. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### V13 — Deadlock, aborto e retry

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N6/N7/N10; C3/V3; C06/C09; K07/K08 |
| Gate, modo, fixture, papéis/conexões | G3; A; P1/P2/O e porta com recibo; B-D |
| Sequência, além de S0–S7 | Produzir deadlock controlado em fixture/probe, observar vítima; testar falha 40001 somente se caminho/isolamento aprovado puder produzi-la; tentar reutilizar transação abortada; refazer comando permitido. |
| Resultado DB/API e transação | 40P01 (ou 40001 na variante própria) causa rollback integral; nenhum retry só de COMMIT ou efeito duplicado. Retry completo limitado por D4, com nova autorização; sem política definida, cenário bloqueado. |
| Evidência, dependência e risco específico | E-LOCK/E-TRACE/E-DB; não inverter ordem em código de produto para gerar falha. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### V14 — Falha diferida no COMMIT

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N4/N6/N11; V2/V3; C06/C09; I09 |
| Gate, modo, fixture, papéis/conexões | G2+G3; A; operação composta; P/D+O |
| Sequência, além de S0–S7 | Injetar estado inválido que passe etapa intermediária e só falhe na validação diferida; coletar retorno do COMMIT; observar Account/Member/org/evento/recibo de outra conexão. |
| Resultado DB/API e transação | Erro D3 aborta todos os efeitos; resposta de sucesso não sai após SaveChanges antes do commit. Se transporte do commit for cortado, classificar desconhecido e usar K07, sem afirmar rollback. |
| Evidência, dependência e risco específico | E-DB/E-TRACE/E-API; failpoint D5 sem liberar DML ao runtime. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

### 4.3 F — Privilégios e SECURITY DEFINER (12)

#### F01 — Grants efetivos por papel

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N8; R1/R3/F1/F2; C08; I02/I09 |
| Gate, modo, fixture, papéis/conexões | G1+G2; A+H; todos logins/owners; O e D |
| Sequência, além de S0–S7 | Extrair owners, ACL por tabela/coluna/schema, privilégios indiretos e atributos de papéis; tentar cada operação permitida/negada com login real. |
| Resultado DB/API e transação | ACL efetiva corresponde à allowlist aprovada; nenhuma leitura/escrita empresarial ampla em Identity/Platform; negativas de privilégio 42501. Não provar isolamento usando migrator. Sem DML nas inspeções. |
| Evidência, dependência e risco específico | E-ACL/E-DB; D2 e inventário de assinaturas. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### F02 — Ownership e bypass

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N8; R1/F1; C08; I02/I09 |
| Gate, modo, fixture, papéis/conexões | G1+G2; A+H; todos runtime/owners |
| Sequência, além de S0–S7 | Inspecionar superuser, BYPASSRLS, ownership de tabela, login e opções de role; verificar migrator fora das credenciais/processo runtime e FORCE/ENABLE por tabela. |
| Resultado DB/API e transação | Runtime/owners de função sem ownership/bypass; NOLOGIN nos executores; migrator segregado. Tentativas de DDL/TRUNCATE e desligar RLS/trigger negadas, sem alteração. SQLSTATE 42501 quando aplicável. |
| Evidência, dependência e risco específico | E-ACL/E-REVIEW; não usar FORCE como defesa contra superuser. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### F03 — EXECUTE e PUBLIC

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N8; F1/F2; C01/C08; I02/I09 |
| Gate, modo, fixture, papéis/conexões | G1+G2; A; usuário sem grant e CT/CI/CP |
| Sequência, além de S0–S7 | Enumerar todas assinaturas/overloads e default privileges; tentar EXECUTE não concedido e chamada manual de helper privado/validator; repetir após atualização candidata. |
| Resultado DB/API e transação | PUBLIC sem EXECUTE nas privilegiadas; apenas cadeia nominal permitida; validators sem chamada externa. Negativas 42501 ou inacessibilidade de schema conforme D2. Sem efeito persistido. |
| Evidência, dependência e risco específico | E-ACL/E-DB; incluir privilégios novos por default, não só funções conhecidas. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### F04 — Herança e SET ROLE

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N8; R1/F1; C08; I09 |
| Gate, modo, fixture, papéis/conexões | G1+G2; A; todos runtime; D |
| Sequência, além de S0–S7 | Inspecionar grafo de membership, INHERIT/SET e privilégios de administração conforme versão aprovada; tentar SET ROLE para executores, owner e migrator e concessão a si próprio. |
| Resultado DB/API e transação | Nenhum caminho permitido para elevação; NOINHERIT sozinho não é prova. Negativa 42501; effective/current/session user registrados; sem mudança persistida. |
| Evidência, dependência e risco específico | E-ACL/E-DB; D2 e versão de catálogo aprovada. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### F05 — RLS por executor e composição

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N2/N8; R2/R3/Q3; C08; I02 |
| Gate, modo, fixture, papéis/conexões | G1+G2; A+H; owners via chamadas reais |
| Sequência, além de S0–S7 | Extrair policies, roles, USING/WITH CHECK e combinação permissiva/restritiva; exercitar Tenant, SELF, REC, BOOT e integridade; comparar grafo com saída real. |
| Resultado DB/API e transação | SELF/BOOT não tornam leitura Tenant permissiva; helper/validator sem recursão; owners sem bypass; INSERT/UPDATE checam linha nova. Cenários negados sem dado/commit; SQLSTATE conforme superfície D2. |
| Evidência, dependência e risco específico | E-ACL/E-TRACE/E-DB; não aceitar policy textual como evidência suficiente. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### F06 — search_path e shadowing

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N8; F1; C01/C08; I02 |
| Gate, modo, fixture, papéis/conexões | G1+G2; A+H; runtime em DB efêmero aprovado |
| Sequência, além de S0–S7 | Inspecionar search_path/config de funções, schemas graváveis e nomes qualificados; tentar objeto sintético homônimo em namespace/temp ao qual atacante de teste tenha acesso; chamar porta. |
| Resultado DB/API e transação | Nenhum objeto atacante é usado, nenhum código fora do grafo executa. CREATE negado onde proibido; teste de shadow não exige conceder CREATE no schema confiável. Resultado legítimo inalterado ou acesso negado; 42501 quando pertinente. |
| Evidência, dependência e risco específico | E-ACL/E-TRACE/E-DB; D5 para objetos temporários de ensaio, nenhum criado nesta rodada. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### F07 — Grafo de chamadas privilegiadas

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N8; F1/F2; C08; I02/I09 |
| Gate, modo, fixture, papéis/conexões | G1+G2; A+H; funções e owners |
| Sequência, além de S0–S7 | Construir grafo de chamadas estáticas e dependências e revisar o que catálogo não captura; registrar current_user em ensaio de cadeia; tentar cadeia não autorizada e examinar SQL dinâmico. |
| Resultado DB/API e transação | Só arestas aprovadas; sem salto para executor mais amplo, overload oculto ou SQL dinâmico por dado externo. Sem parâmetro SQL/tabela/filtro livre. Negativa sem efeito; SQLSTATE D2. |
| Evidência, dependência e risco específico | E-ACL/E-TRACE/E-REVIEW; inspeção humana de bodies obrigatória. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### F08 — Provas por função

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N1/N2/N8; F2; C02/C04/C08/C09; I03/I04/I08/I09 |
| Gate, modo, fixture, papéis/conexões | G2+G4; A; CT/CI/CP; fixtures de referência/convite |
| Sequência, além de S0–S7 | Para cada linha F2, omitir e adulterar uma prova: SELF, referência, código, destinatário, estado, autorização de papel, scheme/MFA no adapter, alvo e versão; chamar também função diretamente com login permitido. |
| Resultado DB/API e transação | Função não trata flags do cliente como prova; SQL não autentica MFA, conforme Q1. Negativas C/ACL sem dados alheios/efeito; provas válidas produzem só projeção autorizada. Cada assinatura vira variante identificada. |
| Evidência, dependência e risco específico | E-API/E-DB/E-TRACE; bloqueia se assinatura não tiver prova mapeada. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### F09 — Auditoria e recibo privados

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N8/N10/N11; F2/M-AUDIT/M-IDEM; C06/C08; I02/I09 |
| Gate, modo, fixture, papéis/conexões | G2; A; CT/CI/CP; recibos A/B |
| Sequência, além de S0–S7 | Tentar chamar helpers privados e alterar/excluir eventos; ler recibo de outro ator/scope/operação, e replay depois de revogação; verificar owners permitidos de append. |
| Resultado DB/API e transação | Sem engine público ou acesso global; eventos append-only; recibo protegido por autorização atual; negativa 42501 ou contrato 403/404 sem segredo. Nenhuma alteração/replay de efeito. |
| Evidência, dependência e risco específico | E-ACL/E-DB/E-API; resultado de operação não inclui código reconstruível. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### F10 — União efetiva de privilégios e owners compartilhados

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 para freeze/deriva de privilégios / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N8; R1/F1/F2; C08; I02/I09; matriz objetiva 3.9 |
| Gate, modo, fixture, papéis/conexões | G1+G2; A+H; exec_identity/exec_tenant/exec_bootstrap/exec_context/exec_integrity; CT/CI/CP por chamadas reais; observer segregado |
| Sequência, além de S0–S7 | Calcular por owner união de ACL de tabela/coluna/schema/função, memberships/SET ROLE, policies aplicáveis e arestas EXECUTE; comparar 3.9 com bodies e catálogo futuro. Revisar se cada capacidade excedente relativa a uma função é necessária a outra do mesmo owner e contida por entrada/prova/policy/grafo. Registrar desvio concreto antes de propor separação |
| Resultado DB/API e transação | D2 APROVADA: manter exec_identity/exec_tenant/exec_bootstrap compartilhados nos grupos atuais e exec_context/exec_integrity distintos como já desenhados, sem owner por função. Mesmo owner tem mesma união SQL; função somente de consulta pode possuir capacidade SQL de escrita do owner, que seu corpo aprovado não usa. Não alegar negação SQL por função. D2 congela união justificada e controles, não implementa grants |
| Evidência, dependência e risco específico | E-ACL/E-REVIEW/E-TRACE; 3.9; matriz candidata, ainda não catálogo observado. D2 aprovada no desenho; comprovação de grants efetivos/grafo pendente antes da liberação de G1. Se controle insuficiente demonstrar capacidade excessiva concreta, propor redução de grant/policy/aresta e só depois separação mínima necessária |
| Aprovação/reprovação | Em G1a aprovar documentalmente apenas união necessária com controles verificáveis e decisão D2; em G1b/G2 exigir igualdade ao catálogo e provas F/Q/V. Owner compartilhado por si só não é FAIL; grant sem necessidade/controle, bypass, aresta lateral ou promessa de segregação por função = FAIL P0. D2 aprovada não comprova ACL/grafo: G1 continua BLOQUEADO até evidência exigida e D3/D5; resto conforme 3.6 |

#### F11 — Locks sem grants amplos

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N7/N8; R3/C1/F2; C05; K03 |
| Gate, modo, fixture, papéis/conexões | G1+G2; A; executores através das portas |
| Sequência, além de S0–S7 | Comparar colunas UPDATE necessárias aos locks e DML permitido por owner; realizar lock autorizado e tentar alterar campos imutáveis/segredos/role Owner pelos caminhos disponíveis. |
| Resultado DB/API e transação | Locks funcionam com menor privilégio; sem grant UPDATE irrestrito só para lock. Guard mantém IDs; controles de função C08 permanecem. Falta de grant no caminho válido reprova, não justificar BYPASSRLS. |
| Evidência, dependência e risco específico | E-ACL/E-TRACE/E-DB; D2, considerar união F10. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### F12 — Regressão arquitetural e superfície

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P1 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N8/N12; ADR-0009 Repository/Foundation; C01/C10; I02 |
| Gate, modo, fixture, papéis/conexões | G1+G4; A+H; solution/assemblies e catálogo futuro |
| Sequência, além de S0–S7 | Comparar assinaturas/dependências Foundation/ADRs aceitos com baseline; verificar ausência de motor genérico novo e migração automática na inicialização; repetir inventário de funções após atualização. |
| Resultado DB/API e transação | Sem mudança Foundation não aprovada, IQueryable exposto ou dependência indevida; guards existentes preservados. Gates MaxOffset/M1–M5 somente se Repository 2.x consumido, sem fallback; ausência de prova aplicável bloqueia. |
| Evidência, dependência e risco específico | E-REVIEW/E-ACL e relatório de regressão; testes executáveis futuros somente sob autorização. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

### 4.4 K — Disputas e recuperação de comandos (10)

#### K01 — Aceites A/B (K01)

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N3/N6/N7; C1/C2; C04/C05/C06; I06; K01 |
| Gate, modo, fixture, papéis/conexões | G3; A; IN+convites A/B válidos; CI em D1/D2/O; B-L |
| Sequência, além de S0–S7 | D1 aceita A e pausa após lock Identity; D2 tenta B e deve esperar; confirmar D1 e liberar D2; inverter A/B; observar histórico e registros. |
| Resultado DB/API e transação | Um consumo/Account/Member e um aceite com sucesso; perdedor não transfere nem cria órfão e segue C04 (403 após prova de inelegibilidade). Se UNIQUE 23505 for alcançada internamente, traduzir só constraint conhecida. COMMIT só vencedor. |
| Evidência, dependência e risco específico | E-LOCK/E-DB/E-API/E-TRACE; D4 e cronologia completa. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### K02 — Bootstrap × aceite (K02)

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N3/N5/N6; C1/C2; C04/C06/C09; I06/I09; K02 |
| Gate, modo, fixture, papéis/conexões | G3; A; IN+convite A e BOOT novo B; CI/CP em D1/D2; B-L |
| Sequência, além de S0–S7 | Disputar mesma identidade com bootstrap e accept; pausar vencedor após lock Identity, comprovar espera e executar ambas ordens; repetir recibo autorizado do vencedor. |
| Resultado DB/API e transação | Um consome; bootstrap perdedor não deixa org/Owner/auditoria de sucesso/recibo; accept perdedor sem Accepted. Retry bootstrap confirmado só retorna mesmo comprovante, sem novo consumo. |
| Evidência, dependência e risco específico | E-LOCK/E-DB/E-API; sem lock de org prévio à identidade. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### K03 — Revogação × comando (K03)

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N4/N6/N7; C2/V3; C05/C07/C08; I10; K03 |
| Gate, modo, fixture, papéis/conexões | G3; A; Admin ator de Demo e Owner revogador; CT em D1/D2/O; B-L |
| Sequência, além de S0–S7 | Pausar comando após locks e disputar revogação; depois inverter, com revogação confirmando primeiro; executar nova requisição em ambos casos. |
| Resultado DB/API e transação | Comando primeiro pode confirmar antes de revogação; revogação primeiro bloqueia comando depois de revalidar. Próxima requisição negada; Account+Membership revogados; sem promessa de invalidar JWT de produtos. |
| Evidência, dependência e risco específico | E-LOCK/E-DB/E-API/E-TRACE; observar versão/eventos uma vez por efeito. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### K04 — Versão concorrente (K04)

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N9; C05/C08; I02/I10; K04 |
| Gate, modo, fixture, papéis/conexões | G3+G4; A; DemoRecord e Membership; CT; D1/D2; B-V |
| Sequência, além de S0–S7 | Ler mesma versão por dois clientes; competir PATCH autorizado para cada tipo; variar If-Match ausente, malformado, wildcard e outro recurso; repetir após perda de acesso. |
| Resultado DB/API e transação | Uma gravação incrementa versão; outra 412. Ausente 428, malformado/wildcard 400; sem direito precede revelação de versão (403/404). Sem overwrite ou retry automático; SQLSTATE de optimistic check pode não existir. |
| Evidência, dependência e risco específico | E-API/E-DB/E-TRACE; versão e hash do conteúdo antes/depois. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### K05 — Replace concorrente (K05)

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N6/N9/N10; C04/C05/C06; I06; K05 |
| Gate, modo, fixture, papéis/conexões | G3; A; convite Pending, duas chaves; CT; D1/D2; B-L |
| Sequência, além de S0–S7 | Disputar replace com mesmo ETag e chaves diferentes; repetir com mesma chave/payload; inspecionar cadeia old/new e material de segredo. |
| Resultado DB/API e transação | Uma substituição; outra 412 se ainda autorizada. Mesma chave retorna replay C06 sem código; um Pending/sucessor, predecessor Replaced, segredo antigo inválido. Constraints 23505 só salvaguarda interna conhecida. |
| Evidência, dependência e risco específico | E-LOCK/E-DB/E-API; sem reparo por job. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### K06 — Pending vencido (K06)

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N6/N9; C04/C05; I03/I06; K06 |
| Gate, modo, fixture, papéis/conexões | G3; A; convite Pending que expira durante espera; CI/CT; B-T |
| Sequência, além de S0–S7 | Ler preview/ETag antes do prazo; manter lock em outra sessão até relógio controlado ultrapassar expiresAt; soltar; aceitar e depois emitir/substituir no slot conforme operação permitida. |
| Resultado DB/API e transação | Aceite 409 Expired mesmo ETag igual; GET não grava/incrementa; mutação materializa Expired auditado e libera Pending único. Sem validade por timestamp pré-espera ou índice com now(). |
| Evidência, dependência e risco específico | E-LOCK/E-DB/E-API/E-TRACE; D4 relógio e ponto de transição aprovados. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### K07 — Resposta perdida/commit incerto (K07)

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N6/N10; C06/C09; I09; K07 |
| Gate, modo, fixture, papéis/conexões | G3+G4; A; create org/convite/demo, replace e accept; D+O; B-C |
| Sequência, além de S0–S7 | Variante 1 confirmar commit e suprimir resposta; variante 2 cortar transporte durante commit sem saber resultado; consultar via nova requisição e observer; replay exato autorizado; revogar ator e repetir. |
| Resultado DB/API e transação | Um efeito no máximo; caso incerto não é chamado rollback. Recibo/Accepted reconcilia estado; replay create 200 vs primeiro 201, sem segredo; accept só direito atual. Nova chave não é mecanismo automático de retry. |
| Evidência, dependência e risco específico | E-DB/E-API/E-TRACE; D5 failpoint e observação independente do commit. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### K08 — Falha da auditoria (K08)

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N6/N11; C06/C09; I09/I10; K08 |
| Gate, modo, fixture, papéis/conexões | G3; A; cada mutação de F2; papéis reais; D+O; B-A |
| Sequência, além de S0–S7 | Provocar falha determinística no append antes do commit, incluindo hipótese de falha diferida; observar alterações/eventos/recibos; executar controle válido e falha de rede no commit separada. |
| Resultado DB/API e transação | Rollback confirmado remove todos efeitos de sucesso; nenhum sucesso parcial. Rede incerta segue K07; não classificar toda falha como 409/412. SQLSTATE é o da injeção D5, sanitizado; não criar evento de sucesso em outra transação. |
| Evidência, dependência e risco específico | E-DB/E-TRACE/E-API; revisão de cobertura de escritores V12. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### K09 — Último Owner (K09)

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N5/N7; C08/V2; I09/I10; K09 |
| Gate, modo, fixture, papéis/conexões | G3+G4; A; um/dois Owners por fixture; CT/P1/P2/O |
| Sequência, além de S0–S7 | Tentar revogar/rebaixar Owner e si próprio via API; exercitar proteção final de zero Owner no probe V11, sem usar esse probe como função de produto. |
| Resultado DB/API e transação | API nega todas alterações de Owner, mesmo com dois. Probe não confirma zero Owner; rollback preserva composição anterior. Nenhuma licença/permissão nova; erro de trigger D3 separado de autorização HTTP. |
| Evidência, dependência e risco específico | E-API/E-DB/E-LOCK; compartilha prova V11, execução não contabilizada duas vezes. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### K10 — Reuso após falha (K10)

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N2/N6; R4/Q3; C07/C09; I02; K10 |
| Gate, modo, fixture, papéis/conexões | G3; A; CT; IA/IB; D+O |
| Sequência, além de S0–S7 | Executar sequência Q11/Q12 depois de abortos reais K03/K06/K08/V14; identificar PID e operação anterior; tentar consulta sem contexto e nova transação B. |
| Resultado DB/API e transação | Backend reutilizável só após estado limpo; sem A em B nem transação abortada retornada ao pool. Se reset incerto, descarte comprovado. Falha de obtenção de mesmo PID não prova reuso e deixa variante inconclusiva. |
| Evidência, dependência e risco específico | E-POOL/E-TRACE/E-DB; reutiliza cenários, mantém referência e evidência vinculada. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

### 4.5 H — Histórico, recuperação e operação (6)

#### H01 — Consumo permanente e novo emprego

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N3; M-HIST/H; ADR-0008; C02/C04/C09; I03/I04/I05 |
| Gate, modo, fixture, papéis/conexões | G2+G4; A; IR consumida A e IN novo login; CI/CT |
| Sequência, além de S0–S7 | Revogar/arquivar A; tentar referência/emissão/aceite B para IR; usar nova identidade IN e convite B; comparar histórico antes/depois. |
| Resultado DB/API e transação | IR nunca elegível/NotLinked, nem após arquivamento; novo login IN cria conta B sem tocar consumo A. Nenhuma fusão por e-mail; tentativa antiga não confirma vínculo. SQLSTATE de UNIQUE só quando alcançada pelo probe. |
| Evidência, dependência e risco específico | E-DB/E-API; política de estado e IdP D1/D6. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### H02 — Recuperação, e-mail e subject

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P1 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N1/N3; H/Q1; ADR-0008; C01/C02/C07; I07/I11 |
| Gate, modo, fixture, papéis/conexões | G4; A+H; IdP de homologação aprovado; IR/IN |
| Sequência, além de S0–S7 | Recuperar senha/MFA da identidade A; mudar e-mail; testar mesma pessoa com identidades distintas e e-mail repetido se suportado; simular política IdP que proíbe repetição e subject reciclado. |
| Resultado DB/API e transação | Recuperação preserva sujeito/empresa e não reativa; e-mail não faz linking. Proibição do IdP é informada sem bypass. Subject reciclado requer rejeição/quarentena conforme procedimento aprovado, não acesso à conta antiga. |
| Evidência, dependência e risco específico | E-API/E-DB/E-REVIEW; não simular IdP e declarar política real comprovada. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### H03 — Perfil expurgado e ledger

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P1 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N3/N11; H-A–H-D/V2; C09/C10; I04/I08 |
| Gate, modo, fixture, papéis/conexões | G4; A+H+O; cópia descartável; IR; operador segregado |
| Sequência, além de S0–S7 | Revisar alternativa de retenção aprovada; executar expurgo sintético só de campos autorizados, preservar prova mínima/alias; tentar reutilização; testar perda da prova como ensaio negativo. |
| Resultado DB/API e transação | Ledger impede reciclagem; prova perdida não é NotLinked e impede retorno ao tráfego. Sem afirmar anonimização/LGPD. Descarte e restauração do teste seguem autorização; SQLSTATE por ação aprovada, não predeterminado. |
| Evidência, dependência e risco específico | E-DB/E-OPS/E-REVIEW; D6 privacidade/retenção/keys antes do ensaio. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### H04 — Restore anterior ao consumo

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P1 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N3/N12; H; C09/C10; I04/I08; K07 |
| Gate, modo, fixture, papéis/conexões | G4; O+H; backup sintético antes de consumo e fonte posterior aprovada |
| Sequência, além de S0–S7 | Criar marco backup; consumir identidade depois; restaurar em destino separado; iniciar modo sem tráfego; reconciliar consumo posterior; testar fonte ausente/corrompida e repetir login. |
| Resultado DB/API e transação | Sem fonte confiável, destino não volta ao tráfego nem libera identidade. Com reconciliação validada, marca/AccountId/org originais preservados. Medir RPO/RTO, não inventar metas; nenhuma operação no banco de origem. |
| Evidência, dependência e risco específico | E-OPS/E-DB/E-REVIEW; D6 RPO/RTO/fonte/reconciliação e autorização operacional específica. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### H05 — Migração IdP e aliases

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P1 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N1/N3; H/M-LINK; ADR-0008; C01/C09; I07/I11 |
| Gate, modo, fixture, papéis/conexões | G4; O+H+A; dois emissores sintéticos/aprovados; IR |
| Sequência, além de S0–S7 | Inventariar mapeamento antigo→novo com prova; migrar alias no ensaio mantendo IdentityId/consumo; tentar vincular pelo e-mail, reatribuir sujeito e importar org/AccountId conflitantes; cobrir pairwise. |
| Resultado DB/API e transação | Só mapeamento aprovado preserva conta original; aliases antigos não são reciclados; negativos rejeitados sem novo consumo. Novo emprego é nova identidade, não alias. Sem contrato pairwise, variante fica bloqueada. |
| Evidência, dependência e risco específico | E-OPS/E-DB/E-REVIEW; D1/D6 prova e rollback de migração. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

#### H06 — Indisponibilidade, sessão e retomada

| Campo | Especificação futura |
|---|---|
| Prioridade / estado | P0 / NÃO EXECUTADO |
| Proteção / rastreabilidade | ADR-0009; N1/N2/N6/N12; Q2/H; C01/C03/C07/C09; I10/I12 |
| Gate, modo, fixture, papéis/conexões | G4; A+H; IdP/session store/DB de homologação; dois fluxos/abas |
| Sequência, além de S0–S7 | Interromper IdP em login/refresh/retomada; interromper store/DB; expirar continuation e sessão conforme parâmetros aprovados; relogar outra identidade, atrasar resposta A e tentar replay/rascunho. |
| Resultado DB/API e transação | Sem fallback, nova autorização ou POST automático; 503 conhecido/estado desconhecido C06, não falso vazio. Fluxo/token/código não cruza usuário/aba/org; logout local não promete global. Sem dados protegidos em URL/log/recibo. |
| Evidência, dependência e risco específico | E-API/E-TRACE/E-OPS; D1/D4/D6 e metas UX separadas; ensaio não valida todos critérios WCAG. |
| Aprovação/reprovação | Aplicar 3.6: todas as assertions acima verificadas e evidências completas; divergência = FAIL; dependência/instrumentação ausente = BLOQUEADO/INCONCLUSIVO, sem aprovação presumida. |

## 5. Rastreabilidade consolidada e cobertura

As linhas de 4 são a rastreabilidade individual completa; este índice permite conferir cobertura inversa sem repetir regras.

| Fonte | Cenários principais do plano | Evidência / gate |
|---|---|---|
| ADR-0008, vínculo permanente e ausência de transferência | Q03–Q06; V01–V07; K01/K02; H01–H05 | E-DB/E-API/E-OPS; G2–G4 |
| ADR-0009 Q/R, P0-01 | Q01–Q12; F05/F08; K10 | E-API/E-ACL/E-POOL; G1–G4 |
| ADR-0009 V/C1, P0-02 | V01–V14; K01–K10 | E-DB/E-LOCK/E-TRACE; G1–G4 |
| ADR-0009 F, P0-03 | F01–F12; Q06/Q09/Q10; V08/V12 | E-ACL/E-REVIEW/E-TRACE; G1–G4 |
| C01 | Q01/Q02/Q03/Q10; F03/F08/F12; H02/H06 | E-API/E-TRACE; G4, ACL em G1/G2 |
| C02 | Q03–Q06; F08; H01/H02 | E-API/E-DB; G2/G4 |
| C03 | Q05; H06 | E-API/E-TRACE/E-OPS; G4 |
| C04 | Q04/Q05; V01/V02/V06; F08; K01/K02/K05/K06; H01 | E-DB/E-API/E-LOCK; G2–G4 |
| C05 | V09/V10; F11; K04/K05/K06 | E-API/E-LOCK/E-DB; G3/G4 |
| C06 | F09; K01/K02/K05/K07/K08; V13/V14 | E-DB/E-API/E-TRACE; G2–G4 |
| C07 | Q04/Q11/Q12; V04; K03/K10; H02/H06 | E-POOL/E-API/E-OPS; G2–G4 |
| C08 | Q01/Q02/Q06/Q08; V04/V05/V11/V12; F01–F11; K03/K09 | E-ACL/E-API/E-DB; G1–G4 |
| C09 | Q01–Q04/Q06; V01/V05/V14; K02/K07; H03/H04 | E-API/E-DB; G2–G4 |
| C10 | F12; H03/H04; seções 7–9 | E-REVIEW; G0/G1/G4 |
| I01 | Q01/Q03 | E-API/E-DB; G4 |
| I02 | Q02/Q07–Q12; V03; F01/F05/F08/F10 | E-API/E-DB/E-ACL/E-POOL; G1–G4 |
| I03/I04 | Q04/Q05; V07; H01 | E-API/E-DB; G2/G4 |
| I05 | Q05; V01; H01 | E-API/E-DB; G2/G4 |
| I06 | K01/K02/K05; V02 | E-LOCK/E-DB; G3 |
| I07/I11 | H02/H05 | E-API/E-DB/E-REVIEW; G4 |
| I08 | Q03/Q04; H03/H04 | E-API/E-DB/E-OPS; G2/G4 |
| I09 | Q06; V05/V14; K02/K07/K08 | E-API/E-DB/E-LOCK; G2–G4 |
| I10 | Q04; V04/V08/V10; K03/K04/K09; H06 | E-LOCK/E-DB/E-API; G2–G4 |
| I12 | H06 | E-API/E-OPS; G4 |
| K01–K10 originais | K01–K10 deste plano, um a um; V09–V14 detalham mecanismos; Q11/Q12 detalham pooling | E-LOCK/E-POOL/E-DB/E-TRACE; G3/G4 |

Áreas: Q=12, V=14, F=12, K=10, H=6. Total 54. Cobertura documental não é prova executada. F10 é a prova explícita de owner compartilhado, independentemente da numeração F-14 usada numa solicitação anterior; não existe plano externo nem case F14 a conferir. Não renumerar silenciosamente artefatos oficiais já existentes.

Cobertura de todas as portas F2: resolve/prepare/reference→Q03/Q04/F08; check/issue→Q05/F08/H01; preview/accept→Q05/K01; cancel/replace→K05/F08; bootstrap→Q06/K02; change/revoke→V04/K03/K04/K09; demo create/update→Q08/K04/K07; audit/receipt→F09/K07/K08; helper→Q09/F05; validators→V01–V14. V12 exige inventário futuro de assinaturas reais: família conceitual não prova que toda implementação foi exercitada. Testar cada transição permitida/negada C04 por variante de F08, inclusive cancel repetido/Accepted replay autorizado sem ETag antiga, sem confundir com nova transição. C02 TTL/rate limit, C03/C07 janela/sessão dependem dos parâmetros aprovados: F08/H06 incluem borda antes/no/depois do prazo e 429 com Retry-After quando o limite aprovado for atingido. Não usar valores hipotéticos como oracle final.

## 6. Achados, três P0 e critérios de bloqueio

| Achado / gravidade | Evidência documental | Risco | Menor correção recomendada, sem alterar fonte nesta rodada |
|---|---|---|---|
| P0-A: comprovação dos grants efetivos e grafo pendente após D2 aprovada | Complementar R1 e F2: exec_identity é owner de resolver, preparar e aceitar; exec_tenant cobre várias escritas. F1 descreve campos fixos por função, mas grants SQL pertencem ao owner | Confundir limitação do corpo com restrição de privilégio por função; aceitar grant/policy amplo pensando que outra função não o possui | Matriz 3.9 agora explicita união, capacidades excedentes e controles; recomendação é manter grupos atuais e só separar diante de risco concreto não contido. D2 aprovou os cinco grupos e o desenho; F10 e conferência do manifesto/grants/grafo ainda a comprovar antes da liberação de G1. Não há falha provada só por compartilhar owner; G1 permanece bloqueado |
| P0-B: oracles e probe de integridade ainda não aprovados | Complementar V2/V3 e C3 não fixam SQLSTATE de erro de trigger, assinaturas/probe ou failpoints | Declarar cobertura por qualquer erro, testar como owner ou inserir exceção insegura para alcançar estado inválido | Congelar erro/constraint/momento por variante em D3 e desenho de probe D5; nenhum runtime adicional. V01–V08/V14 só executam após isso |
| P0 de isolamento no fluxo legítimo / fronteira D1 aprovada com risco residual | Complementar Q1/F3 e 3.7/Q10 distinguem os adversários | Acesso B via Application IA é falha P0; contexto SQL integralmente forjado com credencial comprometida pode alcançar capacidades concedidas, limite conhecido registrado como risco residual explícito em D1 aprovada | Q10.a exige negação absoluta no fluxo legítimo; Q10.b caracteriza alcance sem PASS de segurança/isolamento. D1 aprovou a Application como fronteira confiável, não vazamento no fluxo legítimo. Não reabrir a decisão sem contradição P0 concreta |
| P1-A: projeção de nome mínimo do destinatário | Contrato C02 exige displayName na conferência; complementar F1 lista leitura técnica de identidade sem nome e F2 check_recipient_reference não explicita sua fonte | Implementador ampliar SELECT de perfil ou omitir campo sem decisão | Especificar fonte/projeção/coluna mínima autorizada em revisão posterior, sem diretório; congelar em D2 antes de F08/G4 |
| P1-B: consumo/histórico e recuperação dependem de operação | Complementar H e P; I04/I08; não há RPO/retenção/procedimento aprovado | Restore perde marca, expurgo libera identidade, IdP recicla sujeito | D6 aprova procedimento/fonte/quarentena e limites; H03–H05 não passam sem ensaio e revisão |
| P1-C: snapshot e cobertura de escritores exigem implementação inspecionável | Complementar C1/V1/V3; V2 triggers apenas de leitura | Palavra VOLATILE/READ COMMITTED sem instrução nova/grant correto pode não assegurar revalidação esperada | V09/V10/V12 testam cronologia e grafo real, bloqueando G2/G3 se não observável |
| P1-D: parâmetros pendentes e gates de regressão | Planejamento D01–D12; C07/C10; ADR-0009 Repository | Congelar timeout/retry/TTL/MaxOffset/versão por conveniência do harness | D1/D4 e decisões Repository aplicáveis; sem mudança de Foundation/guards |
| P2: desempenho do lock org e consultas repetidas | Complementar C1/V2/F3 | Contenção ou custo operacional | Medir depois de baseline correto; critérios/volume aprovados, sem otimização antecipada |
| P2: documentos históricos com descrições anteriores | Plano Diretor 5.1/20.5 e Planejamento D02 versus consolidação permanente do ADR-0008 | Ler restrição como somente MVP ou tratar regra já aprovada como indecisa | Registrar hierarquia 1.1; futura reconciliação documental autorizada, sem reabrir regra aprovada |

A grafia retenção substitui apenas descrição deste plano; nenhum documento-fonte é corrigido aqui.

Parecer individual dos P0:
- **P0-01: D1 APROVADA no desenho; validação técnica pendente.** Application atesta contexto, PostgreSQL aplica RLS; credencial SQL comprometida é risco residual explícito. Q01–Q12 ainda não executados; SET LOCAL não autentica OIDC.
- **P0-02 CONDICIONADO:** V1–V3 evitam lock explícito tardio; falta congelar D3/D5 e comprovar V/K reais. Locks internos PostgreSQL continuam possíveis.
- **P0-03 CONDICIONADO na documentação, G1 ainda BLOQUEADO:** matriz 3.9/F10 recomenda manter owners compartilhados e contém os critérios de exceção. D2 APROVADA para os cinco grupos; falta comprovar grants efetivos e grafo de chamadas antes de liberar G1. G0 já aprovado com condicionantes não implica decisão sobre grants.

Bloqueios de execução/progressão: vazamento de qualquer dado/contagem/segredo pela Application legítima ou fora da superfície aprovada; acesso indevido ou grant lateral; consumo duplicado/transferido; Active sem membro/Owner; escrita sem evento/recibo aplicável; autorização antiga após espera; DML não inventariado; trigger que adquire lock explícito tardio; evidência de contexto residual; FAIL, BLOQUEADO ou INCONCLUSIVO em variante P0 obrigatória; decisão de confiança/owner não registrada. Q10.b caracterizado dentro do limite declarado não é FAIL automático nem PASS; D1 está aprovada, mas execução/evidência continuam pendentes. Nenhum percentual agregado compensa uma negativa de segurança falha.

## 7. Gates e sequência futura de execução

G0/G1/G2/G3/G4 abaixo são gates técnicos LOCAIS a este plano, distintos de G01–G08 de UX. Aprovar um não altera os demais.

| Gate | Entrada e sequência | Saída e evidência / responsável |
|---|---|---|
| G0 — revisão documental atual | Decisão humana expressa: APROVADO COM CONDICIONANTES; esta rodada corrige Q10/V08/F10 sem ampliar escopo | Aprovação G0 registrada por instrução humana, não pelo autor; correções atuais aguardam revisão. Não encerra condicionantes, ADRs nem G1 |
| G1 — decisões e superfície congeladas | G0 aprovado com condicionantes; D1/D2 aprovadas no desenho; G1 permanece BLOQUEADO até comprovar grants efetivos/grafo e preparar/validar D3/D5. Etapa G1a: matriz final de funções/owners/ACL, grafo, transações e variantes antes de autorizar código. Após autorização separada de implementação/ambiente, G1b compara catálogo REAL com matriz; nenhuma conexão de banco nesta rodada | F01–F07/F10/F11/V06/V09/V12 e F12 documental, nas etapas cabíveis; E-REVIEW/E-ACL com versão e drift. Revisor independente+segurança. Divergência bloqueia G2 |
| G2 — banco, papéis e invariantes | G1a aprovado e G1b conferido no alvo isolado autorizado; schema/funções implementados sob autorização própria, probes aprovados, fixtures prontas | Q SQL, V básicos, F dinâmicos, H01 em banco. Sem API disponível não declarar Q/H de G4 concluídos. Constraints/commit/RLS/pool reais demonstrados; revisão de E-DB/E-ACL/E-POOL |
| G3 — concorrência e falhas | G2 sem P0 pendente; D4/D5 aprovados; duas conexões físicas e observer, barreiras/instrumentação verificadas | V09–V14 e K01–K10, com ambos os ordenamentos, failpoints e oracles C; E-LOCK/E-TRACE/E-DB e ausência de efeito parcial; segurança+arquitetura revisam |
| G4 — Application/API e recuperação operacional | G3 aprovado; host/adapters/IdP/session store implementados e homologação autorizada; D1/D6 e ensaios de operação aprovados | Q/F/K com API+H completos, regressão aplicável e relatório consolidado. H/O não são substituídos por mocks/CI. Aprovação técnica não aprova UX/Sprint nem autoriza deploy |

G1b não é autorização para criar DDL durante inspeção; compara alvo previamente implementado/autorizado. É possível implementar primeiro o mínimo de persistência para provas sem liberar produto, mas isso exige escopo de código/infra/testes explicitamente aprovado pelo gestor. Este documento não fornece essa autorização.

Execução futura sugerida, ainda não autorizada: registrar D1/D2 já aprovadas → preparar/validar D3/D5 e comprovação de grants/grafo → freeze G1a/variantes → autorizações de implementação e ambiente → preparar alvo/probes/fixtures → inspecionar G1b → provas determinísticas G2 → corridas/falhas G3 → adapters/IdP/ensaios G4 → relatório humano. Casos de gates múltiplos têm resultado parcial por variante; não avançam como completos antes do último gate indicado.

CI/CD futuro: job segregado de banco real com credenciais de teste por papel, recursos isolados por run, plano de evidência/redaction e autorização de provisionamento/limpeza; sem segredo de produção/migrator no processo runtime; publicar resultados/manifesto/evidências sanitizadas conforme D6. Falha P0 impede promoção, sem retry indiscriminado de job até ficar verde; repetir corrida por política aprovada e reportar todas tentativas. Nenhum pipeline será criado/editado nesta rodada.

Reexecução por impacto: mudança em owner/grant/policy/function/search_path reabre F e Q correspondentes + G1b; trigger/lock/escritor novo reabre V e K envolvidos + V12; pool/provedor altera Q11/Q12/K10; recibo/auditoria altera F09/K07/K08; ledger/IdP/backup altera H e Q03/Q04. Mudança transversal requer conjunto completo P0. Preservar FAIL anterior, linkar correção e novo runId; não apagar evidência. Critério de estabilidade/repetições depende de D4, não de quantidade arbitrária.

## 8. Dependências, riscos e decisões humanas

| ID | Pergunta objetiva a decidir | Dependência e efeito da falta de resposta |
|---|---|---|
| D1 | **APROVADA formalmente pelo responsável:** Application é fronteira confiável de autenticação, autorização e atestação do contexto empresarial; PostgreSQL aplica RLS conforme contexto transacional recebido | Comprometimento da credencial SQL permanece risco residual explícito; sem isolamento comprovado. Q10 continua não executado. Aprovação não escolhe IdP/versões nem altera outras decisões |
| D2 | **APROVADA formalmente pelo responsável:** manter exec_identity, exec_tenant, exec_bootstrap, exec_context e exec_integrity; desenho aprovado | Grants efetivos e grafo de chamadas precisam de comprovação antes da liberação de G1. Não equivale a ACL implementada/validada ou teste executado; projeções técnicas pendentes permanecem |
| D3 | Quais constraints/estados, eventos diferidos, códigos internos/SQLSTATE de triggers e mapeamentos de falhas serão adotados? | Sem nomes/erro/momento congelados, V/K de integridade não executáveis de forma auditável; não assumir P0001 nem todo 23505 como negócio |
| D4 | Quais versões .NET/provedor/PostgreSQL, isolamento, clock, política de pool, deadlines, lock timeout, retry, repetições e TTLs serão aprovados? Quais gates MaxOffset/M1–M5 se Repository for consumido? | SDK descrito no repositório não aprova dependências novas. Sem valores, casos parametrizados ficam bloqueados; plano não fixa parâmetros por conveniência |
| D5 | Qual ambiente descartável, probe/failpoint e observabilidade permitirão estados inválidos/falhas sem conceder bypass runtime nem mudar a proteção ensaiada? Quem autoriza instalação/limpeza e acessa observer? | Sem aprovação, variantes estruturais/commit incerto/falhas bloqueadas. Probes não migram para pacote/credencial de produção; exigem prova de ausência no alvo de produção |
| D6 | Qual registro mínimo/retenção/descarte/base de privacidade, fonte de restore/RPO/RTO, migração IdP, chaves e armazenamento de evidências/segredos será aprovado e por quais responsáveis? | H03–H05 e relatório de evidência operacional bloqueados; não prometer retenção indefinida, anonimização ou LGPD |
| D7 | Quais autorizações separadas abrangem implementação dos testes, implementação mínima sob teste, ambiente/banco/IdP, ensaios destrutivos no destino isolado e ações CI? | Sem autorização específica nenhuma etapa executável começa; aprovação de G0 não substitui D12 do planejamento Sprint 1 |

Decisões D1–D7 são locais deste plano e referenciam, sem substituir, D01–D12 do planejamento. Responsáveis necessários: gestor, arquitetura, segurança, operação, privacidade/jurídico onde aplicável e revisor independente. Nomear pessoas/prazos/capacidade exige gestão, não inferência do autor do plano.

Riscos de teste: probe com grants excessivos; isolamento desativado durante setup e não restaurado; observer confundido com login runtime; clocks diferentes; fake IdP tratado como real; transação de teste envolvendo tudo e escondendo commit; pool que troca backend sem medir; lock interno interpretado como ordem explícita; grants de owner compartilhado ignorados; erro genérico contado como assert. Controles: manifestos, S0–S7, F10 e evidência independente, além da revisão humana indicada.

## 9. Checklist de prontidão para autorizar implementação dos testes

Esta lista registra PRÉ-REQUISITOS, não aprovações concedidas.

- [x] Decisão humana G0 documental aprovado com condicionantes registrada; esta correção única aguardando revisão, sem liberação G1.
- [x] D2 humana aprovada: manter os cinco grupos de owners existentes; sem alteração do desenho.
- [ ] Grants efetivos/manifesto e grafo comprovados para liberar G1; aprovação D2 não satisfaz esta prova.
- [x] D1 humana aprovada: Application como fronteira confiável, risco residual de credencial SQL comprometida explícito; sem isolamento comprovado.
- [ ] Modelo/constraints/triggers/ordem C1 e leitura pós-espera aprovados, sem locks explícitos tardios.
- [ ] Assinaturas, projeções, erros/SQLSTATE, DTOs/HTTP e variantes congelados para implementação.
- [ ] Todos os escritores planejados e futuros incluem prova, locks, auditoria/recibo e testes mapeados.
- [ ] D4 tem parâmetros e dependências aprovados sem hipóteses transformadas em valores vigentes.
- [ ] D5 tem ambiente/probes/failpoints/observer e escopo de limpeza aprovados, sem exceções de produção.
- [ ] D6 tem responsável e política para ledger/evidências/keys, ensaios de restore e migração.
- [ ] Estrutura do harness e limites de pacotes/projetos aprovados, Foundation preservada.
- [ ] Critério de FAIL/BLOQUEADO/INCONCLUSIVO e reexecução por impacto aceitos.
- [ ] D7 contém autorização de código/testes distinta da autorização de ambiente/operação.
- [ ] Gate UX, aprovação ADR e Sprint 1 continuam separados; não há commit/push/deploy implícito.

## 10. Parecer G0 e registro desta rodada

**Status humano histórico da fase G0: G0 DOCUMENTAL APROVADO COM CONDICIONANTES; G1 então BLOQUEADO.** Aquela rodada registrou as aprovações formais D1/D2 e suas condicionantes anteriores ao fechamento. O estado vigente está no início deste plano; os 54 casos e a rastreabilidade permanecem preservados, sem aprovação global do ADR-0009, Gate UX ou Sprint 1 e sem autorização da próxima etapa.

Atividades desta rodada: leitura de referências reais e governança, inspeção de git status, captura SHA-256 do estado inicial e comparação final, pesquisa técnica somente de documentação oficial e validação estática do Markdown. Nenhum build/teste executável, SQL, PostgreSQL, Docker, serviço, migration, deploy, commit/push/merge, monday.com ou alteração de gate executado.

Fontes técnicas auxiliares de semântica (não escolhem versão nem aprovam desenho): [owners e SECURITY DEFINER](https://www.postgresql.org/docs/current/sql-createfunction.html), [triggers](https://www.postgresql.org/docs/current/sql-createtrigger.html), [RLS](https://www.postgresql.org/docs/current/ddl-rowsecurity.html), [isolamento](https://www.postgresql.org/docs/current/transaction-iso.html) e [locks](https://www.postgresql.org/docs/current/explicit-locking.html). O plano foi elaborado a partir dos documentos do repositório, sem exigir arquivo externo.

**Estado de execução: zero cenários executados; zero resultados de teste aprovados. Aguardar autorização humana.**

## 11. Histórico da rodada anterior de fechamento G0

| Alteração identificada | Local / efeito |
|---|---|
| R-G0-01 | Seções 1, 6–10: registrar decisão humana de G0 aprovado com condicionantes e G1 bloqueado; apenas o item de checklist referente a G0 é marcado |
| R-G0-02 | Q10, 3.6/3.7 e D1: separar FAIL P0 na Application legítima de caracterização de credencial SQL comprometida; nenhuma aceitação humana de risco presumida |
| R-G0-03 | V08 e 3.8: contexto técnico transacional, conjunto completo da org, verificações positivas, independência de T e ausência de locks explícitos tardios |
| R-G0-04 | F10, 3.9, P0-A e D2: matriz da união por owner e recomendação de manter compartilhados, sem segregação fictícia por função; separação somente por risco concreto não contido |
| R-G0-05 | Complementar V3 e R1/F1/F2: esclarecimentos indispensáveis do contrato técnico e distinção entre grant de owner e controles do corpo; remissão à matriz sem duplicá-la |

Nenhuma alteração das ADR-0008/0009, contratos C01–C10, I01–I12, K01–K10 ou contagem/IDs dos 54 casos. Revisão não adota novo sistema de autenticação SQL, owner por função ou framework de contexto/validação.

### Registro histórico G0 para conferência no monday.com — não substitui o aceite G1 acima

- Atividade existente: ADR-0009 — fechamento documental G0 / decisões D1-D2. Usar o item já existente; não abrir itens duplicados para D1, D2 ou nova rodada de análise.
- Atividade documental desta solicitação: concluída — aprovações humanas registradas nos documentos oficiais.
- D1: aprovada; fronteira Application e risco residual explícito de credencial SQL comprometida.
- D2: aprovada no desenho dos cinco grupos; comprovação dos grants efetivos/grafo pendente.
- D3/D5: pendentes de preparação e validação técnica.
- G0: aprovado com condicionantes. G1: bloqueado. Próximo gate: G1, sem início autorizado.
- Testes: 54 cenários preservados, zero executados. Gate UX e Sprint 1: inalterados.
- monday.com: nenhuma atualização externa realizada; este único registro substitui os status anteriores para conferência humana.

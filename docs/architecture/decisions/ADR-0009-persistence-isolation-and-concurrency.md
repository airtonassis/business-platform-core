# ADR-0009 — Persistência, isolamento e concorrência

## Status

**Proposto — pendente de aprovação humana. Não autoriza implementação ou migração.**

A regra de produto do [ADR-0008](ADR-0008-organization-membership-and-context.md) está aprovada: uma identidade OIDC pode consumir no máximo uma conta empresarial durante todo seu ciclo de vida, inclusive após revogação/arquivamento. Novo ingresso em outra empresa exige nova identidade e novo convite. Esta revisão propõe mecanismos para preservá-la, sem aprovar schema, fornecedor ou parâmetros.

Referências: [Plano Diretor](PLANO_DIRETOR_BUSINESS_PLATFORM_CORE.md), ADRs [0004](ADR-0004-repository-2x-paged-query-contract.md), [0005](ADR-0005-sort-specification-and-total-order.md), [0006](ADR-0006-translation-policy-and-failure-classification.md), [0008](ADR-0008-organization-membership-and-context.md), [C01–C10](../SPRINT_1_CONTRATOS_FLUXOS.md) e [I01–I12](../IDENTIDADE_CENARIOS_E_DECISOES.md).

## Decisões humanas registradas — D1 e D2

Fonte: aprovação formal comunicada pelo responsável nesta solicitação. Registro documental, sem execução ou comprovação técnica.

| Decisão | Status / conteúdo aprovado | Condicionantes preservadas |
|---|---|---|
| D1 | **APROVADA:** Application é a fronteira confiável de autenticação, autorização e atestação do contexto empresarial; PostgreSQL aplica RLS conforme o contexto transacional recebido | Comprometimento da credencial SQL permanece risco residual explícito; não há alegação de isolamento comprovado. Middleware padrão integra essa fronteira, sem autenticação OIDC própria no banco. Q10 e demais provas não executados |
| D2 | **APROVADA:** manter os cinco grupos exec_identity, exec_tenant, exec_bootstrap, exec_context e exec_integrity; desenho aprovado | Grants efetivos e grafo de chamadas ainda precisam de comprovação antes da liberação de G1. Aprovação do desenho não é evidência de ACL aplicada/validada |
| D3/D5 | **PENDENTES:** preparação e validação técnica | Oracles/constraints/erros, probes/failpoints/ambiente e suas provas continuam pendentes |

G0 documental aprovado com condicionantes; **G1 continua BLOQUEADO**. O status global deste ADR permanece Proposto: aprovações parciais D1/D2 não aprovam automaticamente os demais mecanismos, Gate UX ou Sprint 1. Não há autorização de implementação/operação.

Rastreabilidade: [Plano, registro 1.2 e D1/D2 da seção 8](../PLANO_VALIDACAO_TECNICA_ADR_0009.md); [complementar Q1/F1–F4](../ADR_0009_MODELO_RLS_CONCORRENCIA.md). Os 54 cenários permanecem não executados.

## Especificação complementar e autoridade

[Modelo relacional, RLS, concorrência e histórico](../ADR_0009_MODELO_RLS_CONCORRENCIA.md) concentra as definições técnicas:
- M: entidades, PKs/FKs, índices, constraints, estados, versões e separação banco/aplicação.
- R: matriz SQL, contexto transacional, funções restritas e pooling.
- C: ordem global de locks, K01–K10 e classificação de resultados.
- H: alternativas de histórico, privacidade, restauração e migração IdP.
- T/P: rastreabilidade, evidência futura e aprovações.
- P0-01/Q: contrato de confiança e matriz de contextos; P0-02/V: triggers e protocolo sem locks tardios; P0-03/F: superfície privilegiada por função, provas e riscos.

O detalhamento substitui as antigas tabelas resumidas deste ADR; não constitui autorização de execução. Contratos HTTP permanecem em C01–C10, regras de produto no ADR-0008. Divergência deve ser resolvida documentalmente antes de implementar, sem escolher silenciosamente uma versão.

## Alternativas

| Opção | Vantagens | Trade-offs e riscos |
|---|---|---|
| Banco compartilhado com organization_id, filtros e RLS | Menor operação inicial, defesa adicional | Contexto, grants e pooling rigorosos; recuperação por empresa exige desenho |
| Banco por organização | Separação física | Mais conexões, atualização, backup e custo |
| Schema por organização | Separação por namespace | Não substitui autorização; manutenção maior |
| EF Core + provedor PostgreSQL | Mapeamento e concorrência integrados | Compatibilidade .NET 10 e tradução precisam de prova |
| SQL explícito/Dapper | Controle de SQL | Mais mapeamento e disciplina manual |

## Recomendação proposta

PostgreSQL próprio do Core, separado do IdP e produtos, schemas por módulo, banco compartilhado entre empresas com filtros explícitos e RLS. EF Core/provedor compatíveis com .NET 10, dependências e versões a aprovar. GIV/ERP/CRM não compartilham DbContext, transação, tabelas operacionais ou joins interbancos com o Core.

### Modelo e garantias

Organization é a empresa/fronteira de isolamento. IdentityLink associa issuer/subject validado a IdentityId; EnterpriseUserAccount candidata tem identidade única e OrganizationId imutável; Membership tem AccountId único e FK composta de empresa/conta. Sem chave/autorização por e-mail. Os nomes anteriores users/external_identities são consolidados como identities/identity_links, não entidades duplicadas.

UNIQUE da conta inclui revogadas; registro mínimo de consumo preserva IdentityId, empresa e AccountId originais. Revogar/arquivar não libera identidade. Exclusão de dados pessoais não pode ser confundida com ausência de histórico. Forma física e retenção exigem revisão humana.

**FK Membership→Account não garante que Account tenha Membership.** Recomenda-se constraint trigger diferida no commit, apenas de leitura do estado final, com locks de organização adquiridos previamente pelas portas e mantidos até o commit, validando existência e estados coerentes de conta/membro e Owner de organização ativa. Revogar Membership implica revogar Account na mesma transação como mecanismo candidato; não autoriza reativação. Alternativas e limites estão em M3. Imutabilidade também requer grants por coluna/triggers, não apenas RLS.

Bootstrap confirma Organization+consumo+Account+Owner+auditoria+recibo no mesmo commit; aceite confirma consumo+Account+Member+Invitation Accepted+auditoria. Falha implica rollback completo. CHECK/FK não substituem essas transações nem provam completude de auditoria.

### RLS e SQL

Recomendam-se logins separados Tenant, identidade/convites e Platform, com executores NOLOGIN específicos por porta. Runtime sem ownership, superusuário, BYPASSRLS, DDL, TRUNCATE ou papel administrativo herdável. Nenhum papel de leitura irrestrita empresarial; migrator separado e fora do processo web.

Mutação por funções estreitas SECURITY DEFINER é proposta para limitar grants, com owners sem login/bypass, search_path seguro, sem SQL dinâmico e EXECUTE revogado de PUBLIC. Leitura Tenant sob RLS; funções de identidade e bootstrap retornam apenas dados necessários de alvo provado, sem diretório. Matriz USING/WITH CHECK em R3 inclui operações sem empresa e armazenamento técnico C03/C07. ENABLE/FORCE RLS não eliminam riscos de ownership, integridade referencial e policies permissivas combinadas.

Autenticação OIDC/cookie/bearer pertence ao middleware padrão; Application resolve IdentityLink e vínculo corrente e autoriza a ação. Contexto SQL transporta esses valores validados, não autentica usuário. A aplicação/credencial é fronteira confiável; SET LOCAL não é prova criptográfica. Rotas/payload/headers/claims empresariais não decidem empresa ou papel. Q1–Q3 detalham essa fronteira, operações NotLinked/REC/BOOT e helper sem recursão.

Contexto por set_config com escopo local de transação e parâmetros do servidor. Sem contexto, negar. BEGIN antes de qualquer consulta protegida; baseline vazio; nunca SET de sessão para tenant. Rollback/exceção exige encerrar transação e descartar conexão física se limpeza não for confirmada. Savepoints e mesmo backend físico A/B exigem testes. Contexto customizado não protege contra comprometimento completo do processo que consegue forjá-lo.

### Concorrência, idempotência e auditoria

READ COMMITTED + locks explícitos é hipótese inicial. A ordem anterior resumida organização→membros→alvo é substituída explicitamente pelo protocolo completo C1: chave idempotente aplicável → identidades ordenadas → organizações ordenadas → contas/membros → slot → convites → recurso → inserções finais. Accounts precedem Memberships, cada conjunto ordenado. Descobrir/revalidar IDs e abortar se precisar adquirir lock em ordem inversa. Triggers diferidas não adquirem novos locks de linha, não reparam dados e não chamam comandos. Depois de esperar locks, reler estado em instrução nova sob READ COMMITTED; validators VOLATILE sob exec_integrity enxergam fatos finais sem helper de autorização que ocultaria linhas revogadas. V1–V3 fixam eventos e provas. Organização FOR UPDATE em mutações é opção conservadora com risco de contenção.

K01–K10 cobrem aceite A/B, bootstrap concorrente, revogação, versão, replace, Pending vencido, resposta perdida, auditoria, Owner e pooling. UNIQUE é salvaguarda, não substitui verificação histórica. Prazo de convite é reavaliado após locks; GET calcula estado sem gravar; mutação materializa expiração sob slot.

ETag forte individual/versão explícita seguem C05, não xmin público: autorizar antes de comparar; 412 obsoleta, 428 ausente, 400 malformada. C06 mantém escopo ator/empresa ou Platform/operação/chave, hash canônico, recibo no mesmo commit e autorização atual no replay. Retenção candidata 48 h não aprovada. Convite não guarda segredo recuperável no recibo; resposta perdida oferece consulta/substituição explícita, sem novo efeito automático.

Revogação disputa mesmos locks. Comando primeiro pode concluir; revogação primeiro impede próximo comando autorizado. Sem cancelamento retroativo nem promessa de invalidar todos os produtos imediatamente. Falha de auditoria reverte mutação quando rollback confirmado; falha no commit pode deixar resultado desconhecido, a reconciliar. SQLSTATE conhecido pode ter tradução específica; UNIQUE, deadlock, falha de versão e infraestrutura não são o mesmo conflito.

### Histórico e operação

Recomenda-se avaliar ledger mínimo separado do perfil, preservando o bloqueio de identidade consumida. Alternativas H-A–H-D tratam dados brutos/protegidos, soft delete e impossibilidade de garantir permanência após apagar toda prova. Não há declaração de anonimização, LGPD ou retenção indefinida.

Restore deve reconciliar consumos posteriores ao backup antes de retomar tráfego; se não houver fonte de recuperação, não há garantia de permanência. Migração IdP preserva aliases sob prova, sem fusão por e-mail. Subject reatribuído pode causar acesso à conta antiga, risco que ledger sozinho não resolve.

### Repository 2.x, Foundation e armazenamento técnico

Foundation CLOSED e ADRs 0001–0006 permanecem intactos. Unidade transacional em Application/Infrastructure, sem IRepository genérico novo, IQueryable exposto ou abstração transversal de idempotência. Portas específicas do módulo reutilizam contratos existentes.

MaxOffset=10.000 continua hipótese pendente, sem evidência de desempenho; nenhuma implementação ISpecificationRepository antes dos gates ADR-0004. PageSize preservado, ordenação permitida pelo domínio e ordem total ADR-0005; M1–M5 ADR-0006, C-B desabilitada e fallback client-side proibido. Não converter qualquer exceção em Result.

RecipientReference mantém hash; WebSession revogável; Continuation e Draft exigem criptografia/expiração e vínculo a usuário/fluxo/empresa conforme C03/C07. Armazenamento, chaves e retenção continuam pendentes. Nenhum deles amplia Foundation ou gera infraestrutura por este ADR. Catálogo, billing, licenças e templates permanecem fora da sprint.

## Critérios de aceite futuros

- Testar PK/FK/UNIQUE e triggers diferidas no commit, incluindo Account Active sem Membership e vínculo cruzado.
- Provar cada grant/policy com papéis reais, SQL sem filtro, contexto ausente/forjado e negativas sem vazamento.
- Demonstrar K01–K10 com conexões reais, barreiras e ambos os ordenamentos.
- Injetar falha de auditoria/commit/rede e provar rollback ou recuperação de estado desconhecido.
- Testar pooling com mesma conexão física após commit/rollback/exceção/cancelamento/savepoint.
- Validar restauração e migração sem liberar identidade histórica, além dos gates Repository aplicáveis.
- Execução desses testes exige autorização posterior; análise documental/build/health check não são comprovação.

## Fechamento dos três P0 — proposta para revisão

P0-01: Application é atestadora confiável de identidade/scheme; banco revalida vínculo/estado, mas não autentica OIDC nem impede falsificação de SELF por processo totalmente comprometido. D1 foi formalmente aprovada quanto a essa fronteira; comprometimento da credencial SQL permanece risco residual explícito, sem isolamento comprovado. Não reabrir a decisão sem contradição P0 concreta.

P0-02: aquisição antecipada de locks em C1 e validação diferida somente de leitura, sob owner técnico com visibilidade restrita e independente do estado ativo do ator. A menor correção evita inversão tardia na trigger; não promete eliminar deadlocks internos de FK/UNIQUE. Garantia depende de todos os escritores passarem pelas portas autorizadas e de testes reais.

P0-03: matriz F2 define executor/owner, campos, provas, RLS e resultado por função. EXECUTE restrito, search_path seguro, sem bypass/owner genérico, sem protocolo SQL de autenticação próprio. Helper booleano e validator de integridade são papéis distintos, sem acesso empresarial externo genérico. D2 aprovou os cinco grupos de owners; grants efetivos e grafo ainda precisam de comprovação antes de liberar G1.

Status: G0 aprovado com condicionantes; D1/D2 aprovadas no desenho. G1 bloqueado por comprovação de grants/grafo e preparação/validação D3/D5; não pronto para alegar segurança física implementada. Q/V/F são especificação e plano de prova, não testes executados. C01–C10, I01–I12, K01–K10, Foundation e ADRs 0001–0008 permanecem preservados.

## Aprovações e riscos residuais

A lista objetiva está em P da especificação complementar. Arquitetura, segurança, operação, privacidade/jurídico e responsáveis pelos contratos devem revisar modelo, grants, triggers, locks, ledger, RPO/RTO e políticas. Contenção, comprometimento administrativo, perda de histórico, migração IdP e revogação entre produtos continuam riscos.

**Nenhum teste PostgreSQL/RLS/concorrência foi executado nesta revisão. D1/D2 foram aprovadas pelo responsável; aprovação integral do ADR, Gate UX e Sprint 1 não foi concedida.**

Fontes dos mecanismos, consultadas sem escolher versão: [constraints](https://www.postgresql.org/docs/current/ddl-constraints.html), [RLS](https://www.postgresql.org/docs/current/ddl-rowsecurity.html), [locks](https://www.postgresql.org/docs/current/explicit-locking.html), [SET LOCAL](https://www.postgresql.org/docs/current/sql-set.html) e [funções](https://www.postgresql.org/docs/current/sql-createfunction.html).

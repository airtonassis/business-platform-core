# ADR-0009 — preparação técnica G1

**G1 APROVADO por aceite humano definitivo em 2026-10-30, exclusivamente no escopo técnico documentado.** D2 aprovada com 178 privilégios de coluna; D3/D5 ratificadas nos limites existentes.

Estado vigente e referência à autorização expressa do responsável: [EXECUTION_REPORT.md](EXECUTION_REPORT.md), baseline `9ba11b81e986` e complemento `ece79aa930df`. O aceite não conclui os 54 protocolos nem reclassifica resultados históricos. V11 estrutural permanece G3; Application/API/OIDC e recuperação permanecem G4. Próximo gate do plano: G2, sujeito à autorização de execução e às dependências registradas no relatório. Não há liberação automática da Sprint 1, Gate UX, merge, deploy ou produção.
Este diretório é isolado: não é migration de produção, módulo de produção, projeto adicionado à solução ou pipeline. Instalação suportada somente pelo harness com scripts 001–004. D1 preservada; D2 definitivamente aceita e D3/D5 ratificadas. A preparação original abaixo é histórica: referências a G1 bloqueado, decisões pendentes ou cenários não executados descrevem aquele momento e não substituem o aceite atual. Nenhum documento aprovado fora deste pacote foi reescrito.

## Autoridade e inventário

Fonte: [Plano de validação](../../../docs/architecture/PLANO_VALIDACAO_TECNICA_ADR_0009.md), [ADR-0009](../../../docs/architecture/decisions/ADR-0009-persistence-isolation-and-concurrency.md), [complementar](../../../docs/architecture/ADR_0009_MODELO_RLS_CONCORRENCIA.md), [ADR-0008](../../../docs/architecture/decisions/ADR-0008-organization-membership-and-context.md), [ADR-0007](../../../docs/architecture/decisions/ADR-0007-oidc-identity-and-web-session.md) e [contratos C01–C10](../../../docs/architecture/SPRINT_1_CONTRATOS_FLUXOS.md).

Inspeção de arquivos: solution contém Domain/Application/UnitTests/ArchitectureTests; projetos API/Infrastructure/Contracts não constam da solution e seus csproj estão vazios. Sem SQL, migration, Docker/compose ou provedor PostgreSQL declarado encontrados sob src/tests antes desta preparação. Referência a Npgsql em guards de arquitetura não é dependência instalada. Não infere objetos existentes em banco nem estado de ambientes externos. Nenhum executável/SDK/psql foi invocado para inventariar instalação.

Reuso: target net10.0, global.json SDK 10.0.401/latestPatch; xUnit 2.9.3, Test SDK 17.14.1, Shouldly 4.3.0 existentes. Nada adicionado aos csproj, solution, pacotes ou guards. PowerShell é apenas wrapper isolado de inspeção e triagem; futuro harness de integração xUnit/provedor depende das aprovações e implementações abaixo. PostgreSQL/psql/Npgsql/EF, versões, timeouts e retry não foram escolhidos.

## Artefatos, finalidade e dependências

| Artefato | Finalidade/cenários | Pré-requisitos e estado |
|---|---|---|
| [security.expected.json](manifests/security.expected.json) | Cinco owners, união de grants proposta, funções candidatas, tabelas/RLS, arestas permitidas/censuradas; F01–F11 | Não contém GRANT/REVOKE executável. D2 no desenho aprovada; assinatura/schema/coluna/ACL concreta requer freeze e evidência. Arrays vazios de grants/signatures NÃO significam ausência comprovada |
| [dependencies.json](manifests/dependencies.json) | Dependências observadas e pendentes; F12/G1 | Versões de cliente/servidor/provedor e ambiente a aprovar; não restaura pacote |
| [environment.example.json](environment.example.json) | Configuração exclusivamente do destino de inspeção | Desativada por padrão, valores críticos null; sem credenciais; não provisiona |
| [inspect-catalog.sql](sql/inspect-catalog.sql) | Catálogo, roles/membership, ACL/defaults, privilégios efetivos, ownership, EXECUTE, search_path, RLS/policies, triggers/constraints/índices, arestas pg_depend | Futuro PostgreSQL isolado e versão aprovada, cliente compatível, observer mínimo e autorização distinta; script NÃO EXECUTADO |
| [Collect-Catalog.ps1](harness/Collect-Catalog.ps1) | Wrapper somente para inspeção acima; nenhuma mutação/prova de negócio | Switch e referência de autorização explícitos, configuração habilitada, cliente hash-pinned, loopback+nome+marker+observer+versão conferidos; sem execução nesta rodada |
| [Review-Catalog.ps1](harness/Review-Catalog.ps1) | Triagem OFFLINE de catálogo coletado, não aprovação | Catálogo real íntegro/redigido; falta de objeto, signature ou review = BLOCKED, nunca PASS |
| [synthetic.json](fixtures/synthetic.json) | Fixture simbólica A/B, IA/IB/IN/IR/IP/IX, convites e baseline | Nenhum INSERT. Setup/cleanup e identidade real de homologação/probe dependem D3/D5 |
| [scenarios.json](proofs/scenarios.json) | 54 cenários do plano com campos originais, hash da fonte, evidências e bindings | Todos NOT_EXECUTED/executionReady=false; sem duplicar outro plano ou redefinir contracts; runnerBinding=null até implementação aprovada |
| [negative-access.json](proofs/negative-access.json) | Receitas negativas F/Q: PUBLIC, SET ROLE, bypass, DDL, contexto, escalada e owner compartilhado | SQL/caller bindings inexistentes permanecem null; negativa por objeto inexistente NÃO comprova autorização |
| [concurrency.protocols.json](proofs/concurrency.protocols.json) | K01–K10 com sessões, sequência, barreiras, oracle e cleanup | Duas conexões físicas+observer, comandos reais, clock/failpoints/barrier hooks aprovados; tudo null/NOT_EXECUTED |
| [error-oracles.proposed.json](proofs/error-oracles.proposed.json) | Categorias integridade, autorização, versão, negócio e operação | SQLSTATE padrão condicionado à camada; erro de trigger não definido, D3 pendente; não vira contrato novo |
| [result.template.json](evidence/result.template.json) | Evidência auditável de execução futura por variante | Modelo sem resultado real; zero PASS e commit NOT_ATTEMPTED; política de redaction/acesso/retenção D6 |

## Reprodução futura da inspeção — não executar agora

1. Obter autorização específica de execução e política do ambiente isolado. Preparar destino por ação operacional separadamente autorizada, nunca por este wrapper. Manifestar versão/clientes e compatibilidade dos catálogos, inclusive prokind, aclexplode, pg_policy e opções de membership; current dos documentos PostgreSQL não escolhe versão.
2. Operação registra destino descartável, nome bpc_adr0009_g1_ seguido de identificador, marker de isolamento no comentário do database e observer bpc_g1_observer_ seguido de identificador. Nenhum CREATE DATABASE/COMMENT/CREATE ROLE é fornecido/executado aqui. Observer não deve ser superuser/BYPASSRLS e precisa apenas de catálogo/inspeção; validar acesso aos metadados antes de interpretar ausência como inexistência.
3. Criar cópia local aprovada de environment.example.json e preencher os null com parâmetros aprovados; executionAuthorized deve ser true e authorizationReference exata. Nome, marker e loopback são proteções contra acidente, não prova criptográfica de isolamento ou substituto de revisão operacional. Não usar túnel/port-forward para servidor compartilhado/produção.
4. Credenciais em PGPASSFILE externo ao repositório, ACL de arquivo restrita. Separar observer, CT/CI/CP e P/probe; não usar credencial migrator como executor de prova. Runner limpa overrides PG*, usa --no-psqlrc/--no-password e restaura ambiente; não solicita senha nem imprime connection string.
5. Em autorização futura, invocar Collect-Catalog.ps1 com Configuration, AuthorizationReference e AuthorizeInspection. Nenhuma linha de comando é executada neste preparo. O runner aceita só seu arquivo SQL fixo, não SQL arbitrário. Usa default_transaction_read_only e BEGIN READ ONLY; guarda valida database/marker/sessão/versão/endereço antes de coletar. Termina com ROLLBACK. Não executa scenários/fixtures.
6. Resultado catalog-runId.jsonl é criado sem sobrescrever arquivo em diretório externo já existente/aprovado. Guardar SHA-256 do catálogo, script/config e hash do cliente. Erro de permissão/versão/coleta aborta, não é evidência de segurança.
7. Review-Catalog.ps1 lê evidência local sem conectar, produz triagem BLOCKED_PENDING_BINDINGS_AND_HUMAN_REVIEW. É proposital: não avalia completamente ACL de coluna, herança/policy, signature ou grafo. Não chamar esse status de FAIL de implementação ou PASS.
8. Confrontar matriz 3.9/F10 do plano com ACLs/owners reais, todos overloads, colunas e objetos de TODOS schemas não-sistema, inclusive public e objetos de owner em schema inesperado. ACL de coluna pode conceder mesmo quando ACL de tabela não concede; explicitar união por owner. Review humano deve comparar também role memberships/SET, CREATE em schemas, defaults, triggers e grants técnicos para lock.
9. pg_depend fornece somente arestas conhecidas pelo catálogo, não grafo completo de PL/pgSQL/SQL dinâmico. Revisão humana dos corpos reais é bloqueante; collector registra fingerprint de definição, não corpo completo nem aprovação. Obter corpos por procedimento de inspeção autorizado, verificar em canal restrito/redigido e anexar revisão ligada ao fingerprint. Se texto/catálogo não estiver acessível ou origem do corpo não for demonstrada, grafo fica BLOCKED.
10. Só após corrigir bindings e obter autorizações correspondentes planejar execução das provas de mutação. G1 não é liberado pelo collector, pelo triage ou por esta preparação.

O SQL é uma consulta de catálogo real preparada, não DDL empresarial. Só em execução futura revelará quais objetos existem. Lista vazia é ausência de evidência, não aprovação do desenho. Fingerprint MD5 do corpo é apenas comparação de versão, não assinatura/controle de segurança; integridade do arquivo de evidência usa SHA-256.

## Probes D3/D5 e harness de integração: scaffold explícito

scenarios.json preserva os 54 IDs e os sete campos do plano; as receitas técnicas são bindings, não nova ADR/plano. Nenhum contrato público/caso de uso foi inventado. Assinaturas em security.expected são nomes candidatos extraídos de F2, com decomposição cancel/replace e create/update ainda a resolver; não alegar que existem. Manter private helpers no owner/alcance do comando, sem executor universal.

V01–V07/V14 precisam alcançar estado intermediário inválido para observar trigger no COMMIT. Sem esquema e probe aprovado, não há teste executável honesto a oferecer: setupBinding, runnerBinding e cleanupBinding ficam null, executionReady=false. NÃO criar tabelas/funções empresariais definitivas nem desabilitar trigger/guard para produzir resultado artificial. Probe P é separado de runtime, autorizado para o ensaio e nunca embarcado em produção; se guard bloqueia antes de trigger, provar guard e revisar alcance sem declarar trigger exercitada.

V08 exige contexto técnico de integridade derivado da porta (operation/transação/org/âncoras), preservado até COMMIT depois do retorno da função; exec_integrity lê conjunto técnico completo da org sem T/SELF do ator, não filtra Active e exige âncoras positivas. Ausência/filtro indevido deve abortar, não aprovar por conjunto vazio. Nenhum lock explícito tardio ou DML reparador no validator. Provas futuras incluem IP sem Membership no bootstrap e estado final após revogação.

Ordem C1: recibo/chave quando aplicável → identidades ordenadas, modo mais forte antecipado → orgs ordenadas → Accounts → Memberships → slot → Invitations → alvo. Locks/revalidação em instruções separadas sob READ COMMITTED candidato aprovado antes da execução. Organização nova protegida pela própria inserção, não lock em linha inexistente. Triggers diferidas somente leitura; locks internos FK/UNIQUE continuam possíveis. Catálogo/volatilidade por si não provam comportamento.

K01–K10: arquivos definem dados/papéis/passos/oracle; preencher scripts/handlers de D1/D2/O e hooks de barreira somente quando comandos/probes reais forem aprovados. Dois PIDs distintos para disputa, mesmo PID entre leases na prova de pooling, PID diferente quando descarte exigido. Espera precisa ser observada antes de soltar vencedor; sleeps não provam ordenação. Registro do resultado depois do COMMIT por O, sem transação de teste envolvendo tudo e mascarando efeitos persistidos.

Para .NET futuro, reutilizar convenções xUnit existentes e portas reais; versão de driver/ORM, projeto e integração com pool carecem aprovação. Nenhum Testcontainers, Docker ou banco é iniciado implicitamente por fixture/constructor/hook. Não adicionar teste fake que só verifica string do scaffold e chamar isso de prova PostgreSQL.

Erros: separar 23505/23503/23502/23514 conhecidos de erro customizado de trigger (proposto/pendente), 42501, versão 412/428/400 em Application, negócio 409/403/404 e falha operacional/commit desconhecido. Nunca considerar qualquer exception como teste aprovado. Q10.a cruzado pela Application = FAIL P0; Q10.b caracteriza o limite D1 aprovado, sem PASS de segurança nem alegação de autenticação SQL/OIDC.

## Evidência e cleanup

Cada variante requer resultado template com autorização/manifests/hashes, papel efetivo/PID, cronologia/barreiras, baseline/pós-estado de O, categoria/SQLSTATE/constraint real, status API quando aplicável e commitOutcome. Não registrar segredo, hash de segredo de convite, JWT/cookie, dados civis ou connection strings. Catálogo pode conter definições/expressões operacionais: canal restrito e revisão de redaction antes de compartilhar; fingerprints não substituem revisão de corpo.

Cleanup é etapa obrigatória por prova: parar agendamento, cancelar/encerrar sessões de teste, confirmar ROLLBACK de transações abertas, reconciliar COMMIT incerto via C06+observer e só depois aplicar cleanupBinding previamente aprovado por runId no destino isolado. Commit confirmado não é desfeito por ROLLBACK posterior. Nenhum DELETE/DROP genérico, recursivo ou automático é incluído. Limpeza após falha de reset exige descarte físico, não retorno de sessão contaminada ao pool.

Dados já commitados exigem cleanup explícito/per-run ou descarte do recurso isolado por operação autorizada, nunca produção/DB compartilhado. Sem cleanupBinding aprovado, provas mutantes continuam BLOCKED. Inspeção deixa somente saída local; não altera dados/grants e faz ROLLBACK. Retenção/expurgo de evidência D6 permanecem pendentes; não declarar conformidade LGPD.

## P0, prontidão e autorização posterior

- Preparação dos artefatos para REVISÃO: concluída como coleta/triagem + scaffolds identificados.
- Validação: não executada. G1: BLOQUEADO. Os 54 casos estão NOT_EXECUTED; nenhum resultado PASS.
- P0: esquema/casos de uso/assinaturas/policies/triggers não implementados ou não comprovados no alvo; grants/grafo ainda sem evidência; D3 sem oracle customizado congelado; D5 sem probe/failpoint/clock/ambiente/cleanup aprovados.
- Sem prova de corpo/arestas dinâmicas, pg_depend vazio não aprova grafo. Sem objetos, inspector só registra ausência; não gera DDL para suprir falta.
- Nada desta preparação aceita fornecedor/versão/timeout/retenção novos ou libera UX/Sprint 1.

Checklist antes de qualquer execução:
- [ ] Autorizar separadamente inspeção de banco e provas mutantes, alvo e cleanup.
- [ ] Aprovar versão de servidor/cliente/driver e manifestos/assinaturas/colunas/arestas.
- [ ] Preencher config exata e criar marker/observer fora deste pacote sob autorização própria.
- [ ] Segregar pgpass/credenciais fora do repo, nenhum migrator no processo runtime.
- [ ] D3: congelar constraints, erros e momento de falha, sem códigos customizados presumidos.
- [ ] D5: aprovar probes/hooks/clock/barreiras/limites e comprovar que não alteram a proteção ensaiada.
- [ ] Implementar somente bindings/casos de uso mínimos especificamente autorizados; não migration de produção por conveniência.
- [ ] Validar isolamento/PIDs/observer, política de evidências e redaction.
- [ ] Revisar grants/grafo reais, tratar missing/ambiguidade/drift como bloqueio.
- [ ] Aprovar cleanupBindings e estratégia de commit desconhecido.
- [ ] Manter G1/Gate UX/Sprint 1 sem liberação automática.

Registro monday.com para conferência humana: atualizar o MESMO item ADR-0009/G1, sem abrir duplicatas. Atividade preparação técnica G1 = concluída para revisão; bindings/provas futuras = bloqueados por dependências; D1/D2 aprovadas; D3/D5 pendentes; grants/grafo não comprovados; 54 cenários não executados; G1 bloqueado. Nenhuma ferramenta externa atualizada.

Fontes oficiais dos mecanismos de inspeção, sem escolha de versão: [pg_proc](https://www.postgresql.org/docs/current/catalog-pg-proc.html), [privilege inquiry](https://www.postgresql.org/docs/current/functions-info.html), [pg_policy](https://www.postgresql.org/docs/current/catalog-pg-policy.html) e [psql](https://www.postgresql.org/docs/current/app-psql.html). Compatibilidade do cliente/servidor ainda precisa de validação futura.

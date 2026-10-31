# ADR-0009 / G2 — Aprovação humana e regularização documental

## Decisão vigente e datas

**G2 APROVADO COM RESSALVAS pelo responsável humano**, exclusivamente no recorte abaixo. Fonte: solicitação expressa do responsável nesta conversa, intitulada “regularização documental e de rastreabilidade do pacote G2 aprovado com ressalvas”, que comunica a aprovação formal e autoriza este registro. O executor registra a decisão; não emite aprovação automática.

**Data da decisão humana:** não informada expressamente na comunicação. Não inferida da execução nem retroativamente atribuída à data deste arquivo.

**Data deste registro documental:** 31/10/2026 (UTC). O instante de preparação da matriz consta em `recordedAtUtc` do complemento JSON.

> G2 da ADR-0009 aprovado com ressalvas, exclusivamente no recorte das 31 variantes SQL/driver evidenciadas por f31a6617ed85 e 6909108391b4. As quatro ressalvas P2 foram aceitas. A aprovação não conclui os 54 protocolos, não amplia D1/D2/D3/D5 e não autoriza G3, G4, Sprint 1, publicação, merge, deploy ou produção.

Este registro atualiza somente a leitura do estado decisório do pacote G2. As expressões `PENDING_HUMAN_REVIEW`, `NOT_HUMAN_APPROVED` e equivalentes nos arquivos anteriores descrevem o momento em que foram produzidos. Permanecem intactas, assim como os resultados brutos, os FAILs e os estados dos protocolos multigate. Os ADRs globais não são aprovados por este aceite parcial.

## Integridade e autoridade dos arquivos

- Os 177 arquivos cobertos por [evidence.sha256.json](evidence.sha256.json), inclusive [coverage.json](coverage.json), [EXECUTION_REPORT.md](EXECUTION_REPORT.md), fontes e verificadores, são preservados byte a byte. O próprio manifesto anterior também permanece inalterado.
- Os nove diretórios em `evidence/runs/` são históricos imutáveis: resultados, assertions, logs, catálogos, expectativas congeladas, comparações, hashes, snapshots e cleanup não são editados.
- Cobertura e relatório foram derivados, mas já estão protegidos pelo inventário histórico. Por isso a regularização usa complementos; não recalcula os hashes antigos para legitimar uma alteração.
- `bin/`, `obj/` e caches ignorados são artefatos locais de geração, não novos candidatos ao versionamento desta regularização.
- [RASTREABILIDADE_COMPLEMENTAR.json](RASTREABILIDADE_COMPLEMENTAR.json) complementa a matriz e contém a errata de V09 e o adendo da lista de tabelas. Não é uma nova execução, uma nova expectativa de privilégios ou uma reclassificação de resultados.
- [.gitattributes](.gitattributes) impede conversão automática de finais de linha somente neste pacote, preservando os bytes dos arquivos históricos em futuro add/checkout. Não converte arquivos existentes nem altera políticas globais do repositório.
- [regularizacao.sha256.json](regularizacao.sha256.json) fixa os dois complementos e a política local de preservação de bytes e referencia, pelo SHA-256, o manifesto histórico e as fontes utilizadas. Não substitui `evidence.sha256.json` nem inclui a si próprio em seu hash.

## Tratamento das quatro ressalvas P2

| Ressalva aceita | Tratamento documental | Limite preservado |
|---|---|---|
| Rastreabilidade nula nas 31 entradas antigas | Saneada no complemento: cada `coveragePointer` identifica a entrada original e liga requisito existente, seção do plano, protocolo-fonte, variante, execução, PASS, resultado e assertions por JSON Pointer. | Os campos nulos do snapshot não são preenchidos retroativamente. Cada vínculo vale para a subprova SQL, não para o protocolo completo. |
| Justificativa genérica de G3 para V09 | Saneada por `V09Erratum`: a variante específica `V09/G1-validators-no-explicit-locks-or-repair` já tem PASS aprovado em `ece79aa930df`; não foi reexecutada em G2. | As variantes estruturais restantes de V09 continuam em G3. A contagem histórica de 7 NOT RUN e 13 DEFERRED permanece como registrada; a errata distingue a reutilização de G1 da postergação estrutural. |
| Omissão de `identity.identity_links` | Saneada no adendo documental `tableInventoryAddendum`, com 12 nomes esperados consolidados e referências ao modelo IdentityLink, SQL aprovado e catálogos G1/G2. | A lista congelada com 11 nomes e seus comparadores históricos não são alterados. Não há novo schema, grant ou allowlist SQL. |
| Diagnóstico não capturado no FAIL `3ed87d94f5a1` | Limitação histórica explicitada neste registro e já reconhecida no relatório anterior. | Permanece como limitação aceita; não é uma lacuna apagada ou um diagnóstico reconstruído. |

Os vínculos adicionais de requisito não executam outros gates: a negativa estática de dois Owners foi registrada pelo runner como V07, reutilizando a variante originalmente identificada por K09. O complemento preserva V07 e acrescenta as referências existentes V05/K09, sem alegar execução do protocolo concorrente K09 ou de V11. As negativas de convites registradas em F09 também apontam para os requisitos existentes Q05/F08; não aprovam suas jornadas completas.

## Lista esperada e `identity.identity_links`

A tabela IdentityLink consta do modelo dos ADRs 0008/0009 e do SQL congelado. Sua expectativa documental deriva dessas fontes, não da cópia dos privilégios observados para uma nova allowlist.

O complemento registra a linha exata de `identity.identity_links` nos catálogos [G1 aprovado](../ADR0009.G1/evidence/runs/ece79aa930df/catalog.jsonl) e [G2 observado](evidence/runs/f31a6617ed85/catalog.jsonl). Ambos apresentam `owner=ddl_owner`, `rls=true` e `forceRls=true`; o conjunto de 12 tabelas é igual. A ausência anterior afetava a lista esperada de nomes, não a existência da tabela nem essas proteções observadas. A correspondência não é alegação de nova prova comportamental de todos os fluxos IdentityLink.

## FAILs históricos preservados

- **`3ed87d94f5a1` / V02:** o diagnóstico nativo anterior à assertion não foi capturado. A causa — preparação com identificador da nova organização fora do contexto do probe — foi inferida pela análise do SQL/policy; não está comprovada por um SQLSTATE histórico que não foi retido. As execuções posteriores demonstram a preparação corrigida e a rejeição específica por constraint. Não reclassificar o FAIL anterior nem reconstruir sua mensagem.
- **`e76986da515a` / V08:** preservado o FAIL do probe que retirava o contexto antes do UPDATE e não confirmava linha afetada. A correção exige `RETURNING id` antes de retirar o componente técnico, e a nova execução registra `P1001` no COMMIT e ausência de efeito persistido.
- **`5c56afedf9e5` / Q12:** preservado o FAIL do oracle dependente de título produzido por outra fixture. A correção verifica a identidade exata do registro A restaurado pelo savepoint. Os resultados posteriores permanecem separados.

As correções e PASS posteriores estão descritos no [relatório histórico](EXECUTION_REPORT.md) e nos próprios runs. Este registro não acrescenta prova experimental à causa inferida nem converte flags conservadoras de parada em uma vulnerabilidade demonstrada.

## Resultados e operações autorizadas

Permanecem **30 PASS em `f31a6617ed85` e 1 PASS em `6909108391b4`**, com 31 pares distintos de ID/variante. Nenhum resultado, execução, parâmetro do harness, evidência histórica ou decisão D1/D2/D3/D5 foi modificado.

Esta regularização utiliza somente leitura, cálculo estático de hashes, conferência de referências e escrita dos complementos documentais. Não executa harness, banco, Docker, migração ou testes G2. G1 e os documentos congelados permanecem preservados.

O pacote pode ser preparado para futuro commit/PR após conferência estática. Esta autorização documental **não autoriza commit, publicação, PR, merge, deploy, produção ou início de outro gate**. A data civil da aprovação original continua não informada; isso é registrado com transparência e não impede a identificação da decisão expressa nesta conversa.

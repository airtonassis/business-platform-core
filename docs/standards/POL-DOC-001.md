# POL-DOC-001 — Governança documental de produtos AraIa

**Estado:** Aprovado como diretriz. **Versão da especificação:** 1.0. **Implementação/publicação:** aplicada documentalmente nos repositórios autorizados locais; não publicada por este registro. **Origem:** diretriz corporativa aprovada pelo responsável, 2026-11-06.

## Objetivo e ownership

Cada produto em desenvolvimento mantém suas fontes oficiais e versionadas no próprio repositório, respeitando sua estrutura. O repositório do produto é responsável por regras de negócio, validação, fluxos, perfis/permissões, contratos, segurança, auditoria, suporte, critérios de aceite e evidências. O Core mantém esta política transversal e o catálogo de padrões compartilhados em [AraIa Development Standards](DEVELOPMENT_STANDARDS.md). O Framework mantém convenções de código em `business-platform-framework/DEVELOPMENT_STANDARDS.md`. O repositório de UI mantém implementação visual identificável; snapshots locais não são releases compartilhados.

Uma regra possui uma única fonte oficial. Documentos de planejamento, homologação, manuais e conhecimento de IA referenciam a regra e sua versão; não duplicam seu corpo normativo. Contratos transversais vivem no Core ou em ADR/arquitetura aprovada; regras próprias permanecem no produto.

## Registro mínimo por regra

Cada regra material usa um registro com:

| Campo | Conteúdo obrigatório |
| --- | --- |
| Identificador | ID estável, único e nunca reutilizado |
| Descrição | Comportamento exigido e limite de escopo |
| Estado | Um estado do ciclo abaixo |
| Versão | Versão da especificação; separar implementação e publicação se diferentes |
| Origem | Decisão humana, requisito, ADR ou evidência identificável |
| Histórico | Data, delta, justificativa e decisão/origem da alteração |

Documentos consolidados podem agrupar regras em tabela, desde que os seis elementos sejam consultáveis por ID. Mudanças compatíveis elevam versão documental; mudanças semânticas incompatíveis exigem nova versão/contrato e decisão apropriada.

## Estados — sem promoção automática

1. **Proposto:** aguardando decisão. Não define comportamento vigente.
2. **Aprovado como diretriz:** decisão normativa autorizada; não implica código.
3. **Implementado:** código/configuração local ou publicada existe; identificar versão e ambiente.
4. **Validado tecnicamente:** evidências reproduzíveis cobrem os critérios declarados.
5. **Homologado:** aceite humano obtido quando exigido, com quem/quando/escopo.
6. **Publicado:** disponível no ambiente nomeado, com release/versão/evidência.

Um documento aprovado pode conter regras com estados diferentes. Uma prova parcial não promove todo o fluxo. Um artefato local não é publicado nem homologado por inferência.

## Conteúdo, integridade e segurança

- Preservar rastreabilidade decisão → requisito/regra → implementação → teste → aceite/publicação quando aplicável.
- Atualizar o documento oficial existente após mudança relevante e registrar delta/motivo; preferir atualização a duplicação.
- Preservar decisões aprovadas. Divergência demonstrável é registrada e dirigida ao responsável; não reabrir por preferência.
- Não incluir credenciais, tokens, dados pessoais ou conteúdo de produção em documentos, evidências e índices de conhecimento. Sanitizar fixtures e anexos.
- Documentar tenant, unidade, permissões, auditoria, integridade, retenção e responsabilidade antes de instruir operação.
- Contratos de integração devem ter versão, produtor/consumidor e política de compatibilidade. Não afirmar serviço externo disponível sem comprovação.

## Conhecimento para IA e suporte

Documentos oficiais poderão ser consultados por produto, módulo, ID de regra e versão. Respostas operacionais devem citar a fonte e seu estado; conhecimento proposto não é instrução executável. Usar apenas informação validada para a versão/ambiente correspondente e respeitar permissões e privacidade do usuário. Ao mudar regra ou comportamento, atualizar a fonte e, futuramente, qualquer índice aprovado. Esta política não autoriza serviços IA, ingestão automática, indexador, treinamento ou exposição de documentos.

## Validação documental e evidências

Cada entrega registra verificações como **executada nesta atividade**, **evidência anterior reutilizada**, **não executada**, **bloqueada**, ou **dependente de homologação humana**. Reuso informa artefato, baseline, ambiente e escopo. Testes repetidos ou sobrepostos não são somados como testes únicos. Lacunas permanecem explícitas. Publicação, aceite humano e gates não mudam por resultado técnico apenas.

O catálogo e matriz reutilizável de UI estão em [AraIa Development Standards](DEVELOPMENT_STANDARDS.md). Cada produto registra sua aplicação e evidência local sem declarar conformidade além do que foi testado.

## Aplicação e histórico

Esta política se aplica aos produtos AraIa/Business Platform Core em desenvolvimento. Cada repositório mantém um `AGENTS.md` curto que aponta para fontes oficiais e exige consulta/reuso, atualização documental proporcional, evidência por impacto, limites entre Core/produtos e cumprimento de autorização. `AGENTS.md` não replica a política.

| Versão | Data | Alteração | Origem |
| --- | --- | --- | --- |
| 1.0 | 2026-11-06 | Publicação documental local inicial de POL-DOC-001 | Diretriz corporativa aprovada pelo responsável |

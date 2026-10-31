# ADR-0008 — Vínculo empresarial único e permanente

## Status

**Proposto — pendente de aprovação humana. Não autoriza implementação.**

Regra de produto aprovada: uma identidade técnica pode ter no máximo uma EnterpriseUserAccount durante todo seu ciclo de vida, vinculada à mesma Organization, inclusive após revogação. Entidade, constraints e mecanismos abaixo continuam propostas técnicas. Referências: [ADR-0007](ADR-0007-oidc-identity-and-web-session.md), [M01–M10](../CONTA_EMPRESARIAL_UNICA.md), [C01–C10](../SPRINT_1_CONTRATOS_FLUXOS.md), [I01–I12](../IDENTIDADE_CENARIOS_E_DECISOES.md).

## Contexto e alternativas

Organization representa empresa e fronteira de isolamento. A empresa tem vários usuários e pode contratar aplicações diferentes. Não duplicar Organization com tabela Tenant/BusinessAccount apenas para renomear ambiente. E-mail não identifica empresa, pessoa civil ou autorização.

| Alternativa | Benefício | Avaliação |
|---|---|---|
| Uma identidade → uma conta → uma Organization permanentemente | Entrada simples e invariantes claras | Produto aprovado; mecanismo técnico proposto |
| Uma identidade → várias empresas por domínio/seletor | Reutiliza credencial | Incompatível com regra atual, não alternativa ativa do MVP |
| Reutilizar identidade após revogação | Conveniência aparente no desligamento | Proibido; novo login/convite são necessários |
| Novo login independente para B | Preserva histórico/separação de A | Regra aprovada, operação depende da política do IdP |

## Modelo candidato e invariantes

IdentityLink mapeia issuer/subject validados para IdentityId estável; mudar e-mail/senha/MFA não libera vínculo. EnterpriseUserAccount candidata tem AccountId escalar, IdentityId consumido uma única vez, OrganizationId obrigatório/imutável, estado e versão. Membership candidata tem AccountId único, OrganizationId coerente, papel/estado/versão. Papel não é licença ou assinatura.

Revogação/bloqueio/desligamento não remove a marca histórica de identidade vinculada. Mesmo após arquivamento, não há segunda conta nem transferência. Identidade nunca vinculada, revogada e inconsistência histórica são estados distintos; não inferir elegibilidade por ausência de linha ativa.

Constraints propostas, sem schema/migration: UNIQUE(issuer,subject), UNIQUE(identity_id) não filtrada por Active, UNIQUE(account_id) no Membership e FK composta (organization_id,account_id). OrganizationId não editável. Unicidade de linha atual sozinha não basta se houver exclusão física: preservar registro mínimo de vínculo/identidade que impeça reciclagem. Forma física, expurgo/anonimização e retenção exigem revisão de privacidade/operação, sem prometer PII integral guardada para sempre.

Mudança de issuer/IdP e restauração de backup preservam histórico por procedimento próprio, nunca tornam identidade antiga livre. Nenhum mecanismo amplia Foundation ou foi implementado.

## Entrada direta e consulta

Login → resolver conta única → validar empresa/membership → início direto. Sem lista/seletor/troca padrão. OrgId na rota é escopo solicitado, não autoridade; divergência nega. Nova requisição e replay revalidam direitos.

C09 propõe GET /me/context com 200 state=Linked se ativo, 200 state=NotLinked somente para identidade nunca consumida, 403 genérico para acesso negado e erro sanitizado para inconsistência. Consulta não cria conta ou seleciona empresa. NotLinked permite apenas preparação/convite próprio, não acesso empresarial.

## Convites e bootstrap atômicos

Emissão/conferência exigem identidade elegível, conhecida, nunca vinculada e prova C02. Identidade de A, inclusive revogada, não recebe convite B; ao emissor não revelar empresa de origem. Convite emitido antes de outra vinculação não garante direito futuro: preview e aceite revalidam.

Aceite confirma conta+Membership Member+marca histórica de vínculo+auditoria e estado do convite no mesmo commit. Convites A/B concorrentes: uma transação vence; a outra falha sem conta parcial, transferência ou aceite fictício. Verificar segredo/destinatário/estado/validade/ETag/elegibilidade atomicamente. GET/callback não aceitam. Replay autorizado de aceite confirmado apenas recupera mesmo resultado, sem nova conta/evento ou reativação.

Bootstrap cria Organization+conta proprietária+Membership Owner+marca histórica+auditoria atomicamente. Proprietário já vinculado provoca rollback completo, sem empresa órfã. PlatformOperator tem conta administrativa separada e não recebe membership.

## Desligamento e ingresso em B

Revogação em A mantém vínculo permanente da identidade A. Para B: novo login com novo issuer/subject segundo IdP, novo convite B e nova conta empresarial. E-mail igual só se permitido; endereço diferente não converte identidade A em nova. Recuperação de senha/MFA não autoriza B nem reativa A.

Transferência, reativação implícita, desvinculação para reutilizar identidade e união por e-mail são proibidas. Eventual reativação explícita na mesma empresa exigiria decisão futura e não libera segundo vínculo; fora da Sprint 1.

## Isolamento, trade-offs e impactos

RLS/filtros/constraints/grants, locks de conta/membership/organização e autorização por recurso continuam. Sem seletor ainda é obrigatório negar rota/payload/claims/cache/pooling cruzados. Identidade sem empresa não usa bypass para listar clientes.

GIV/ERP/CRM preservam interfaces, bancos e regras locais; nenhuma migração agora. Assinaturas/licenças/templates são futuras e por empresa. Restringir reutilização aumenta custo de novos logins e recuperação; não aliviar essa regra aprovada via UX. Viabilidade de novo login/e-mail deve ser demonstrada pelo IdP antes de implementação.

## Aceite e aprovação

I01–I12/RF/UAT devem provar histórico permanente após revogação/arquivamento, concorrência, recuperação A, novo login+convite B, e-mail sem fusão, entrada direta, NotLinked sem dados e bootstrap atômico. Testes são propostos, não executados.

Não reapresentar regra de produto como pendente. Exigem aprovação técnica: entidade/constraints/registro histórico, IdP/e-mail, HTTP proposto, retenção, grants, transações, sessões e propagação. ADR e Gate UX seguem pendentes; nenhum código/banco foi alterado.

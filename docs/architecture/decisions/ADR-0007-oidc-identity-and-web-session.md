# ADR-0007 — Identidade OIDC, conta empresarial e sessão

## Status

**Proposto — pendente de aprovação humana. Não autoriza implementação.**

Regras de produto abaixo foram aprovadas explicitamente pelo gestor; ADR, fornecedor, bibliotecas, parâmetros de sessão e storage continuam propostos. Referências: [conta única](../CONTA_EMPRESARIAL_UNICA.md), [ADR-0008](ADR-0008-organization-membership-and-context.md), [C01–C10](../SPRINT_1_CONTRATOS_FLUXOS.md), [12 cenários](../IDENTIDADE_CENARIOS_E_DECISOES.md), [ADR-0001](ADR-0001-technology-stack.md).

## Conceitos e regra aprovada

Cada Organization representa uma empresa isolada, com vários usuários e aplicações futuras. Cada identidade OIDC pode ter no máximo uma EnterpriseUserAccount durante todo seu ciclo de vida; a conta permanece na mesma Organization, inclusive após revogação. Não é apenas unicidade de contas ativas ou simplificação temporária do MVP.

Identidade técnica é o par (issuer, subject) validado, mapeado a IdentityId interno estável; não é e-mail ou pessoa civil. EnterpriseUserAccount é entidade candidata para conta empresarial de usuário. Organization é ambiente da empresa, não login. E-mail é atributo, nunca chave de vínculo/autorização.

Funcionário que sai de A e ingressa em B cria novo login/identidade, recebe novo convite e cria nova conta B. Identidade A não é reciclada por revogação, senha nova, mudança de e-mail ou exclusão lógica. Mesmo e-mail em identidades diferentes depende da capacidade/política do IdP, sem fusão de contas.

## Alternativas e critérios de IdP

| Alternativa | Vantagem | Risco/custo |
|---|---|---|
| OIDC self-hosted, como Keycloak | Controle e federação | Operação, atualização, chaves, disponibilidade e recuperação |
| OIDC gerenciado | Menor operação direta | Contrato, custo, residência/exportação e dependência |
| Provedor corporativo existente | Aproveita operação disponível | Precisa provar usuários externos e identidades independentes |

**Nenhum fornecedor foi escolhido.** A referência anterior a Keycloak candidato não é escolha. Recomenda-se provedor maduro selecionado por decisão humana, sem construir protocolo, emissor de token, senha ou recuperação próprios.

Critérios: Authorization Code/PKCE, discovery/JWKS, issuer/audience/assinatura/expiração, MFA administrativo, clientes separados, rotação, logout/refresh, privacidade e disponibilidade. Exigir também prova de:

- Novo login independente para mesma pessoa e política de e-mail repetido/único; se não suportado, registrar limitação antes da seleção.
- Subject estável e não reatribuído no issuer; alterar e-mail/senha/MFA não muda sujeito nem libera vínculo.
- Public/pairwise subjects e mapeamento confiável entre clientes de produtos, sem unir por e-mail/nome. SSO entre produtos da mesma empresa requer contrato próprio.
- Migração de issuer/IdP, exportação e restauração sem apagar histórico de vínculo ou reciclar identidade.
- Conta administrativa Platform separada, sem concessão empresarial automática.

## Web, API e Platform

Web cookie e API bearer são esquemas explícitos separados; Platform tem cliente/scheme próprio. Razor→Application e Minimal API→Application usam os mesmos casos de uso/políticas. Cookies Secure/HttpOnly, antiforgery nos comandos web, callbacks correlacionados e redirects permitidos. Access token não é ID token nem prova de membership. Não criar token próprio de contexto ou transportar token ao browser como contrato de sessão.

Após login: identidade validada → histórico único de conta → empresa/membership atuais. Ativa/coerente: início direto, sem seletor. Nunca vinculada: preparação/convite, sem dados empresariais. Revogada/bloqueada/historicamente consumida: negar funções empresariais, não liberar novo convite. Ausência de conta com histórico existente é inconsistência, não primeiro acesso.

C09 propõe 200 state=NotLinked como resultado esperado de GET /me/context, substituindo explicitamente a hipótese anterior de 409, pendente de aprovação. GET não cria identidade/conta/membership; preparação técnica de identidade é etapa explícita/idempotente no processamento de autenticação, não efeito oculto da consulta.

## Recuperação, sessão e revogação

Senha/MFA/recuperação são do IdP. Recuperar A permite autenticar como A; não reativa membership/conta, muda OrganizationId ou cria B. Novo login B exige novo par técnico e novo convite, não reset ou renomeação da identidade A.

Recomenda-se avaliar ticket/session store padrão revogável e rascunho permitido por identidade/conta/empresa. Logout local revoga sessão antes da tentativa OIDC remota; se o store falhar, expirar cookie e informar revogação não confirmada, sem alegar invalidação de cópias. Abas/histórico revalidam; A não restaura rascunho em B.

Tempos em C07 (token, absoluto/inatividade, aviso/extensão, retomada e rascunho) são hipóteses para comparação, não parâmetros aprovados. Revisar risco, acessibilidade e IdP; aviso não comprova WCAG.

## Limites nos produtos independentes e falha do IdP

Revogar Membership no Core impede novas operações do Core após commit conforme política transacional. Não promete cancelar operação concluída ou invalidar imediatamente sessões/tokens de GIV/ERP/CRM. Logout local, logout IdP, refresh/revogação de token e autorização empresarial são controles distintos.

Produtos validam tokens/audiences e direitos locais. Propagação de revogação, cache/TTL/manifest, reconciliação e janela máxima de risco exigem contrato aprovado antes da integração. Considerar tokens curtos e eventos/invalidação sem exigir chamada síncrona ao Core por operação. Token pode permanecer criptograficamente válido até expirar; isso não garante autorização empresarial. Sem contrato, não alegar revogação global imediata.

IdP indisponível em login/refresh/retomada não cria sessão nem aceita convite. Validação de token já emitido em produto independente com material confiável tem política própria a aprovar; não estender validade nem habilitar conta por indisponibilidade.

## Critérios, dependências e aprovações

Provar I01–I12 e RF/UAT em IdP/PostgreSQL reais quando autorizados: identidade estável, novo login+convite B, conta revogada permanente, concorrência, recuperação sem mudança de vínculo, não enumeração, credenciais inválidas, CSRF, replay, falha IdP e Platform segregado.

Pendências humanas: fornecedor/responsável, política de e-mail/novo login, pairwise/migração, retenção mínima sem reciclagem, sessão e propagação nos produtos. Regra de produto já aprovada não é reaberta por essas pendências. Não alterar Foundation ou implementar para produzir a evidência nesta etapa.

## Referência técnica

[OIDC: estabilidade de claims](https://openid.net/specs/openid-connect-core-1_0.html#ClaimStability). Uma conta por identidade é regra de produto do Core, não imposição do protocolo.

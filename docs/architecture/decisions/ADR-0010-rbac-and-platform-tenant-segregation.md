# ADR-0010 — RBAC e segregação Platform/Tenant

## Status

**Proposto — pendente de aprovação humana. Não autoriza implementação.**

Referências: [Plano Diretor](PLANO_DIRETOR_BUSINESS_PLATFORM_CORE.md), [ADR-0007](ADR-0007-oidc-identity-and-web-session.md), [ADR-0008](ADR-0008-organization-membership-and-context.md), [ADR-0009](ADR-0009-persistence-isolation-and-concurrency.md), [Sprint 1](../SPRINT_1_PLANEJAMENTO.md).

## Alternativas

| Modelo | Vantagens | Trade-offs e riscos |
|---|---|---|
| Papéis fixos + políticas ASP.NET Core | Escopo pequeno e revisão simples | Mudanças de permissões exigem evolução controlada |
| Papéis configuráveis por tenant | Flexibilidade | Editor, validação, auditoria e riscos de autoelevação |
| Motor externo ABAC/políticas | Expressividade | Infraestrutura, latência e operação adicionais |

## Recomendação proposta

Diretriz vigente: [empresa única](../CONTA_EMPRESARIAL_UNICA.md). Uma conta empresarial de usuário tem organização imutável; todas as políticas revalidam esse vínculo e o Membership atual. Não há papel de administrador multiempresa, seletor ou concessão automática pelo e-mail/IdP. Conta sem vínculo só prepara convite. Aceite em outra empresa não reutiliza conta já vinculada. Recuperação não reativa revogado.

Permissões pertencem ao ambiente empresarial; Membership não é assinatura, licença ou assento pago. Gestão de licenças por produto é futura e não é concedida por Owner/Admin nesta sprint. PlatformOperator segue conta/esquema separados, sem acesso empresarial automático.

Papéis fixos com handlers/policies padrão ASP.NET Core na borda; regras empresariais e autorização dos casos de uso em Application. Não criar motor genérico de autorização. Chamadas não HTTP também precisam passar pela política do caso de uso.

| Papel | Permissões iniciais |
|---|---|
| TenantMember | Consultar organização acessível e recursos demonstrativos |
| TenantAdmin | Além de Member, criar/alterar recurso e convidar/revogar Members; consultar membros e auditoria |
| TenantOwner | Além de Admin, promover/rebaixar Admins e consultar governança local; não remover último Owner |
| PlatformOperator | Criar organização e proprietário inicial por endpoint específico; sem acesso automático aos dados empresariais |

Member não recebe listagem de pessoas ou auditoria. Admin não altera Admin/Owner, nem o próprio papel. Owner não transfere propriedade nesta sprint. Atualização de Membership distingue ação/estado/papel, não permite patch arbitrário de usuário/organização.

Identidade de PlatformOperator: mapeamento explícito de claim/grupo emitido por cliente administrativo permitido do IdP para política do Core, sem aceitar roles do cliente. Não criar tabela genérica de papéis configuráveis. Conta operacional separada, MFA e audience/cliente administrativo próprios. Aprovar o mapeamento exato e procedimento de concessão/revogação antes de implementar.

Toda rota nega por padrão; cada operação possui política, recurso e escopo. Acesso empresarial valida membership atual e status da organização. Headers/payloads não concedem papel. Platform e Tenant não usam bypass compartilhado. Break-glass, PlatformSupport e Console completo ficam fora da sprint.

Semântica HTTP: 401 sem autenticação válida; 403 por ação proibida em organização acessível; 404 para organização inacessível ou recurso fora do contexto, sem confirmar sua existência. Endpoints web/API mantêm tratamentos de autenticação explícitos. Negação é auditável conforme ADR-0011.

## Impacto nos produtos

GIV: permissões do domínio continuam locais; TenantAdmin não vira administrador irrestrito de viagens/financeiro. ERP Imobiliário: escopos de imóveis, contratos e clientes exigem políticas próprias. Não compartilhar cookies entre aplicações como contrato de SSO nem inserir chamada síncrona ao Core em toda operação dos produtos.

## Critérios de aceite propostos

- Matriz de testes por operação × papel × organização, com casos permitidos e negados.
- TenantAdmin/Member rejeitados em toda rota Platform; PlatformOperator sem membership rejeitado nas rotas de tenant.
- Claims de organização/papel forjadas e token de audience errada não concedem acesso.
- Membership revogado ou papel rebaixado passa a valer em novas requisições conforme ADR-0009.
- Tentativas de autoelevação, edição de outro Admin e remoção do último Owner negadas e auditadas.
- Nenhum usuário comum enumera usuários globais, convites alheios ou organizações inacessíveis.

## Aprovações

Gestor e arquitetura aprovam matriz, mapeamento administrativo e limites de delegação; segurança revisa MFA, bootstrap e revogação. Papéis configuráveis e ABAC exigirão decisão futura, sem alteração automática da Foundation.

## Matriz detalhada proposta após UX-16

A [matriz C08](../SPRINT_1_CONTRATOS_FLUXOS.md) especifica ator × alvo × estado × ação e é referência das propostas de UI/API/caso de uso. [RF-03/09–15 e UAT-03/07/08/16/17](../SPRINT_1_RASTREABILIDADE_UX.md) cobrem positivos e negativos. Não houve concessão de aprovação ou mudança dos papéis existentes implementados, pois esses papéis SaaS ainda são propostos.

Owner pode promover/rebaixar/revogar outro Admin/Member ativo. Admin só revoga outro Member ativo. Nenhum ator altera si próprio, UserId ou OrganizationId do vínculo. Owner não é revogável nem tem papel alterável nesta sprint, independentemente de quantidade; transferência permanece fora do escopo. Membership revogado não é reativado; convite ao mesmo usuário não contorna essa regra. Convite inicial é sempre Member e preview exige referência voluntária do destinatário. PlatformOperator confere proprietário por referência e cria org/Owner, mas não ganha acesso Tenant.

UI recebe capacidades permitidas derivadas da mesma política para apresentação, nunca autoridade para executar; caso de uso revalida estado/alvo/transação, inclusive no replay idempotente. Negar operações não listadas; GET individual de Membership tem a mesma restrição Owner/Admin da coleção.

Alternativa de permitir auto-rebaixamento ou reativação foi adiada: aumenta estados de perda de acesso e risco de contornar convite/revogação. Trade-off é limitação administrativa explícita, comunicada na UI, não botão que falha sem explicação. Dependências: C02/C05/C07, RLS/grants e revisão humana da matriz. Aceite: UAT-16 cobre cada linha/complemento negado, UAT-08 revogação concorrente e UAT-17 separação Platform/Tenant, sem duplicar regras entre adapters.

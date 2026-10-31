# Identidade e conta empresarial — consolidação final e cenários

## Autoridade e estado

**Regras de produto aprovadas; ADRs 0007/0008 e contratos técnicos permanecem Propostos.**

Autorização desta etapa: somente documentação relacionada à identidade/conta empresarial. Nenhum endpoint, schema, migration, frontend, teste executável ou infraestrutura criado. Foundation e ADRs 0001–0006 não são alterados. Referências: [ADR-0007](decisions/ADR-0007-oidc-identity-and-web-session.md), [ADR-0008](decisions/ADR-0008-organization-membership-and-context.md), [C01–C10](SPRINT_1_CONTRATOS_FLUXOS.md), [modelo M01–M10](CONTA_EMPRESARIAL_UNICA.md), [RF/UAT](SPRINT_1_RASTREABILIDADE_UX.md).

Produto aprovado: identidade técnica OIDC tem no máximo uma EnterpriseUserAccount durante todo seu ciclo de vida. Vínculo a Organization é imutável inclusive após revogação. Funcionário que muda A→B usa novo login/identidade e novo convite/conta. Mesma pessoa e e-mail não unem identidades. Mesmo e-mail só pode ser reutilizado como atributo entre identidades distintas se o IdP/política permitir. Não há seletor ou transferência, e PlatformOperator permanece separado.

## H01 — Decisão HTTP proposta para Account.NotLinked

### Alternativas e recomendação

| Alternativa | Vantagem | Limite |
|---|---|---|
| 200 com state=NotLinked | Consulta resolve estado esperado da própria identidade | Consumidor deve verificar state; não presumir que 200 libera dados empresariais |
| 409 Account.NotLinked | Facilita clientes que esperam somente contexto empresarial ativo | Trata preparação normal como conflito numa consulta sem mutação; mistura onboarding e conflito de comando |
| 404 contexto inexistente | Expressa ausência de representação se a rota for definida como recurso ativo | Nesta proposta a rota resolve estado da própria identidade; não apenas Organization existente |

**Recomendação: 200 com discriminante explícito.** A consulta foi resolvida; ausência de primeiro vínculo é estado esperado. Isso é escolha de contrato da plataforma, não imposição da RFC. Em comandos, conflito de vínculo/estado continua podendo ser 409. Referência de semântica: [RFC 9110](https://www.rfc-editor.org/rfc/rfc9110.html#name-status-codes).

Mudança explícita: substitui somente a hipótese anterior de 409 Account.NotLinked em GET /api/v1/me/context. Não altera endpoint implementado, pois esse contrato ainda é proposto. Após aprovação, clientes/OpenAPI/testes devem adotar a mesma versão do DTO; não interpretar simultaneamente as duas hipóteses como comportamento vigente.

### Contrato candidato de consulta

| Condição validada no servidor | Resultado HTTP proposto | Representação mínima / próxima ação |
|---|---|---|
| Identidade autenticada e nunca vinculada historicamente | 200 | state=NotLinked, código Account.NotLinked, ações permitidas de preparação; nenhum OrganizationId/AccountId/papel empresarial |
| Única conta/empresa/membership ativos e coerentes | 200 | state=Linked, AccountId/OrganizationId, nome/papel/permissões atuais autorizados |
| Autenticação inválida/expirada, issuer/audience/assinatura inadequados | 401 | Problem Details sanitizado, sem redirect HTML em API bearer |
| Conta/membership/empresa revogados, bloqueados ou indisponíveis por política | 403 | Access.Unavailable genérico, sem empresa alternativa ou declaração de elegibilidade |
| Registro incoerente/múltiplos vínculos/ausência de conta com marca histórica existente | 500 sanitizado + alerta interno | Não converter em NotLinked, não escolher vínculo ou exibir dados cruzados |
| Dependência conhecida indisponível | 503 | Não converter erro em primeiro acesso/NotLinked; repetir consulta quando seguro |

Não usar 404 de rota/recurso cruzado para inferir se outra empresa existe. Consultar contexto não cria identidade, conta, membership ou seleção. Identity bootstrap idempotente no processamento autenticado é operação técnica distinta e precisa ser demonstrado, não efeito de GET. DTO pode ter formas Linked/NotLinked, usando Result existente sem modificar Foundation; a forma NotLinked não contém permissões empresariais. Response protegida no-store.

Elegibilidade usa histórico, não apenas COUNT de contas Active. Conta revogada/arquivada com vínculo consumido nunca retorna NotLinked. Identidade sem registro interno por falha de persistência também não é classificada como nova silenciosamente.

## H02 — Matriz dos 12 cenários obrigatórios

Todos os resultados são esperados/propostos, **não executados**. Fixture: identidade IA/conta A, identidade IB/conta B, identidade IN ainda não vinculada e operador IP separado. A mesma pessoa pode controlar IA/IB, mas são logins/sujeitos distintos, sem vínculo automático entre eles.

| ID | Cenário e ação | Resultado esperado, controle e evidência futura | Rastreabilidade |
|---|---|---|---|
| I01 | Usuário da empresa A faz login como IA | Resolver IA→Account A→Organization A/membership; 200 Linked e início direto, sem seletor | RF-01/04/05; UAT-01/04/07; C01/C09; P01/P02/P03 |
| I02 | IA acessa recurso B por rota, payload, claims, cache ou conexão reutilizada | Negar sem dado/contagem B; 404 genérico em recurso cruzado, filtros/RLS WITH CHECK e contexto transacional coerente | RF-07/08/20; UAT-07/19; C01/C08; P10 |
| I03 | IA recebe convite de B | Emissão/conferência para identidade já vinculada são bloqueadas sem revelar A ao emissor. Se convite B foi emitido antes do vínculo A, preview/aceite revalidam e negam, sem transferência | RF-09/10/11; UAT-03/05/06; C02/C04; P05/P06 |
| I04 | IA revogada em A tenta convite B | Mesma negativa: identidade consumida permanentemente, nenhuma nova conta ou reativação; contexto não é NotLinked. Mostrar orientação de novo login sem dados de B não autorizados | RF-11/14; UAT-06/08/16; C04/C07; P06/P10 |
| I05 | Funcionário desligado de A cria novo login IN e recebe convite B | IdP deve provar novo issuer/subject; convite válido para IN cria nova conta B/membership/auditoria atomicamente. IA permanece revogada e vinculada a A. Não basta resetar senha ou editar e-mail de IA | RF-02/09/11; UAT-03/06/17 ampliados; C02–C06; P04–P06 |
| I06 | Convites A e B concorrem por IN elegível | As duas podem existir antes da vinculação; aceite e/ou bootstrap disputam unicidade histórica na transação. Um commit vencedor; outro falha sem conta/empresa órfã, transferência ou falso Accepted. Replay só do resultado autorizado | RF-11/13; UAT-06/09/10/17; C04/C05/C06 |
| I07 | Recuperação de senha/MFA de IA | Processo do IdP preserva sujeito e vínculo A; recuperação não reativa papel/conta, aceita convite B ou cria novo usuário empresarial | RF-01/14/16; UAT-01/08/13 ampliados; C01/C07; P01/P10 |
| I08 | IN autentica sem histórico empresarial | 200 NotLinked sem dados de empresa; permite apenas identidade própria/preparação/convite. Não criar empresa ou usuário empresarial no GET | RF-01/02/04; UAT-03/04/14; C02/C09; P02 preparação/P04 |
| I09 | IP cria empresa e proprietário IN | Autorizar Platform/MFA, conferir IN elegível e confirmar Organization+Account+Membership Owner+marca histórica+auditoria no mesmo commit. IP sem membership; falha desfaz tudo; retry autorizado retorna mesma operação | RF-03; UAT-10/11/17; C06/C08/C09; P09 |
| I10 | Revogar conta/membership A com sessão ativa | Novas operações Core após commit negadas; comando já em andamento segue ordenamento de locks. Sessão pode autenticar mas não autorizar empresa. Revogação nos produtos requer propagação/TTL acordados, sem promessa global imediata | RF-13/14/17; UAT-08/12/16; C05/C07/C08; P07/P10 |
| I11 | Mesmo e-mail em IA e IN distintas, se permitido | Lookup por issuer/subject/IdentityId, não e-mail; contas independentes em A/B. Testar também IdP que recusa repetição e oferecer orientação sem contornar política. Nenhuma fusão ou login compartilhado | RF-01/02/20; UAT-01/03/19 ampliados; C01/C02; ADR-0007 |
| I12 | IdP indisponível em login/retomada | Nenhuma autenticação/aceitação fictícia, fallback ou renovação de direitos. Informar indisponibilidade; continuation só persiste dentro da janela; recomeçar se expirada. Não repetir comando após retorno. Política de tokens já emitidos nos produtos é decisão separada | RF-10/16/18; UAT-01/05/13/15; C03/C07/C09; P01/P06/P10 |

Para I03/I04: ID/código inválido ou conta destinatária errada continuam resposta mínima não enumerável. Só após prova de segredo+destinatário reconhecer indisponibilidade da própria conta, sem revelar organização de origem ao emissor ou detalhes não autorizados do convite. Assinatura/licença não resolve inelegibilidade de identidade.

## H03 — Invariantes, provas e riscos

- Uma constraint UNIQUE filtrada por Active é insuficiente: após revogação permitiria reutilização, proibida pelo produto. Propor unicidade permanente e registro mínimo de identidade/vínculo consumido; técnica/retenção a aprovar.
- Exclusão física, mudança de issuer, migração ou restauração não podem apagar a restrição. Definir expurgo/anonimização/retenção de identificadores mínimos sem prometer armazenar dados pessoais completos indefinidamente. Tratamento jurídico/privacidade precisa de responsável.
- Subject pode variar por cliente em configurações pairwise; não unir por e-mail para criar SSO ou alegar correlação global automática. Exigir evidência do IdP e contratos de produto.
- Recuperar credencial não prova elegibilidade para outra empresa. Novo login exige nova identidade técnica; evitar atalhos de suporte que transfiram a antiga.
- Idempotência e consulta de status exigem autorização atual. Condição de nunca vinculado é reavaliada em novo comando, mas replay de operação que criou a própria conta não cria outra nem reaplica bootstrap: só recupera mesmo resultado se autorizado.
- Membership não é licença/assinatura. Novos usuários e novo vínculo não compram produto, não habilitam módulo e não antecipam catálogo/billing/templates.
- Revogar Core não equivale a revogar todo token em produto independente. Definir janela de risco/cache/eventos/reconciliação antes de integração; evitar dependência síncrona por requisição sem justificativa.

## H04 — Decisões técnicas pendentes

| Decisão | Alternativas / recomendação para revisão | Aprovação necessária |
|---|---|---|
| IdP e operação | Gerenciado/self-hosted/corporativo por critérios; nenhum fornecedor escolhido | Gestor, arquitetura, segurança e operação |
| Nova identidade/e-mail | Comprovar política de novo login para mesma pessoa; e-mail repetido só se permitido | Segurança, privacidade e responsável IdP |
| Registro permanente | Constraint + histórico mínimo que não libera identidade por revogação/arquivamento; forma física/expurgo a definir | Arquitetura, privacidade e operação |
| Account/Membership/transações | Entidades/constraints candidatas; vínculo imutável e unicidade já são regras aprovadas | Arquitetura e revisão de dados |
| NotLinked | 200 com estado explícito nesta consulta, em vez de 409; proposta de mudança registrada | Arquitetura e consumidores futuros dos contratos |
| Sessões e retomada | Tempos C07 são hipóteses; ticket store/TTL/rascunho e AA precisam de evidência | Segurança, UX, privacidade e operação |
| Produtos independentes | Token local/eventos/cache/TTL e janela de revogação; sem garantia global imediata | Arquitetura Core + responsáveis dos produtos |
| Gate e execução | Revisar I01–I12, RF/UAT, ADRs e restrições antes de implementar | Revisão humana; autorizações separadas |

Critério documental: todos os cenários têm resultado positivo/negativo e rastreabilidade; nenhuma regra aprovada é apresentada como opção de produto ainda em aberto. Critério técnico futuro: testes reais IdP/PostgreSQL/concorrência/runtime somente sob autorização. Nenhum resultado aprovado é inventado nesta etapa.

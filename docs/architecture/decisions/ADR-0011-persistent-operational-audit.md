# ADR-0011 — Auditoria operacional persistente

## Status

**Proposto — pendente de aprovação humana. Não autoriza implementação.**

Referências: [Plano Diretor](PLANO_DIRETOR_BUSINESS_PLATFORM_CORE.md), [Auditing Foundation](../../foundation/Auditing/README.md), [TimeProvider](../../foundation/TimeProvider/README.md), [ADR-0009](ADR-0009-persistence-isolation-and-concurrency.md) e [Sprint 1](../SPRINT_1_PLANEJAMENTO.md).

## Contexto e alternativas

AuditMetadata preserva criação e última atualização; não oferece histórico operacional. IDomainEvent não implementa entrega de eventos. Nenhum dos dois será alterado ou duplicado para adicionar infraestrutura.

| Alternativa | Vantagens | Trade-offs e riscos |
|---|---|---|
| Eventos append-only no PostgreSQL do Core | Atomicidade com comandos locais | Retenção, volume e administração privilegiada |
| Somente logs estruturados | Operação simples | Sem atomicidade empresarial e consultas consistentes garantidas |
| Serviço externo de auditoria | Separação operacional | Entrega confiável, dependência e custo adicionais |

## Recomendação proposta

Eventos append-only persistidos no Core e logs operacionais complementares. Não criar serviço distribuído nesta sprint.

Contrato candidato: AuditEventId escalar, schemaVersion, scope Platform/Tenant/Identity, OrganizationId somente quando validado, AuditActor existente, action, targetType/targetId, outcome, reasonCode permitido, occurredAt UTC e correlationId. Identidade do ator vem do servidor. Ação humana ou de sistema não exige novo enum Agent na Foundation.

- Application obtém UTC pelo System.TimeProvider; AuditMetadata continua aplicável a criação/última atualização, sem substituir eventos.
- Eventos obrigatórios: criação de organização/proprietário, emissão/cancelamento/substituição/expiração materializada de convite, aceitação, mudança de papel, revogação e criação/alteração do recurso demonstrativo. Registrar criação/vinculação da conta empresarial e autenticação/sessão conforme política; não há seleção/troca de organização no MVP. Leitura GET de contexto não cria evento de seleção; negativas têm controle de volume.
- Não copiar senhas, tokens, segredo/hash de convite ou payload completo. Antes/depois de papel/estado apenas por whitelist; IDs alegados não são tratados como organização validada.
- Mutação confirmada e evento no mesmo commit local; falha de auditoria impede sucesso e reverte mutação. Leitura de contexto e negativas podem usar transação separada, sem modificar decisão de acesso.
- Falha ao registrar negação mantém negação e gera sinal operacional; não prometer persistência quando o banco está indisponível. Requisições sem autenticação válida têm telemetria sanitizada/rate limit, sem actor inventado nem obrigatoriedade de gravar toda tentativa no banco.
- Escritor de auditoria recebe INSERT, sem UPDATE/DELETE. Leitor separado e limitado por tenant/papel. Expurgo por papel operacional aprovado, jamais por endpoint comum.
- Append-only por grants não comprova imutabilidade contra DBA. Exportação assinada/WORM e cadeia criptográfica ficam para decisão futura conforme risco.
- Não excluir usuários/organizações em cascata no histórico. Logs do IdP são fonte dos eventos de autenticação do provedor; o Core registra sua sessão/resolução local sem alegar observação de todos os logins externos.

Retenção, campos pessoais, acesso operacional, expurgo e requisitos LGPD permanecem pendentes: não assumir armazenamento ilimitado ou prazo fictício. Definir parâmetros antes de dados reais. Auditoria empresarial só é consultada por Owner/Admin da organização; PlatformOperator não recebe leitura ampla por este ADR.

## Impacto nos produtos

GIV/ERP Imobiliário registram suas ações localmente e propagam correlação por contratos futuros. Não bloquear operações dos produtos esperando escrita síncrona no Core. Ownership do histórico e políticas de retenção por produto precisam de aprovação própria.

## Critérios de aceite propostos

- Cada mutação bem-sucedida tem evento correspondente e ambos desaparecem num rollback.
- Falha induzida de INSERT em auditoria impede mutação; falha em log complementar não mascara o resultado real.
- Runtime não atualiza/exclui evento; leitor de A não lê B; Member não consulta histórico.
- Testes verificam ator/instante/correlação, whitelist e ausência de credenciais/segredos.
- Negativas de acesso não criam eventos falsamente atribuídos a tenant não validado.
- Política de retenção aprovada antes de uso de dados reais e evidência de monitoramento quando a escrita falha.

## Aprovações

Gestor, arquitetura, segurança/privacidade e operação aprovam catálogo de ações, atomicidade, acesso, retenção e tratamento de falhas. Esta proposta não encerra QA da Auditing Foundation.

## Detalhamento proposto UX-07/12/14

[C06/C09](../SPRINT_1_CONTRATOS_FLUXOS.md) distinguem intenção, leitura e resultado incerto. A proposta anterior de POST de seleção/OrganizationSelected foi retirada, conforme [empresa única](../CONTA_EMPRESARIAL_UNICA.md). GET /me/context resolve o vínculo existente sem gravar seleção. Login/sessão e alterações reais de conta/vínculo têm eventos próprios; requests/reloads/poll podem ter telemetria sanitizada, não falsa troca de empresa. Não criar seleção apenas porque há orgId na rota.

Replay autorizado não duplica evento de mutação confirmada. Prévia do convite não aceita/materializa expiração; a transação mutável posterior registra transição efetivamente aplicada. Resultado de timeout não é anunciado como rollback; UI só informa falha sem efeito quando comprovada pelo backend. Nenhum evento inclui código/referência/continuation ou payload bruto. Erro de store/log complementar não pode ser confundido com commit empresarial.

Impacto: mais tipos de ação e controles de deduplicação, sem criar event bus ou mudar AuditActor/AuditMetadata. Dependências: versões/idempotência e políticas; retenção/ações e forma de registrar previews sensíveis como telemetria ou evento permanecem sujeitos à privacidade. Aceite [UAT-10/11/14/15](../SPRINT_1_RASTREABILIDADE_UX.md): um evento por efeito, nenhum evento de seleção por GET de contexto, rollback atômico e ausência de segredos. Aprovação de catálogo e volume é humana.

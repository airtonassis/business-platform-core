# Etapa 1 — tratamento documental UX-01 a UX-16

## Status

**Pacote documental preparado para revisão humana. ADRs propostos; Gate UX e execução não aprovados.**

Origem: diagnóstico UX/Documentação anterior e autorização exclusiva para editar Markdown. [Contratos C01–C10](SPRINT_1_CONTRATOS_FLUXOS.md) e [rastreabilidade RF/UAT](SPRINT_1_RASTREABILIDADE_UX.md) são propostas. "Tratado documentalmente" significa especificação verificável para revisão, não implementação, teste aprovado ou decisão técnica aceita.

Revisão de conta empresarial única: [M01–M10](CONTA_EMPRESARIAL_UNICA.md). Esta matriz conserva IDs da auditoria, mas o tratamento atual elimina seletor e vínculos múltiplos; não aprova o Gate anterior. Login leva à única empresa da conta. Identidade sem vínculo só prepara convite. A prototipação estática autorizada foi produzida e agora revisada; nenhuma interface executável existe por estes documentos.

## Matriz dos achados

| Achado / criticidade | Situação anterior | Ação realizada nesta etapa | Evidência documental | Situação resultante |
|---|---|---|---|---|
| UX-01 P1 | Caminho cookie/bearer até casos de uso indefinido | Comparadas alternativas; recomendado Razor→Application, esquemas/CSRF e política única | C01; RF-01; UAT-01/02; ADR-0012 | Especificado; recomendação pendente de aprovação |
| UX-02 P1 | Link, segredo e retorno OIDC incompletos | Link sem segredo+código separado; continuation protegida e fluxo conta/login/retomada/aceite | C03; RF-10; UAT-05; ADR-0007/0008 | Especificado; UX/armazenamento transitório pendentes |
| UX-03 P1 | Aceite sem leitura segura de condições | POST preview autenticado com prova; GET sem aceite; estados/erros mínimos | C04; RF-10/11; UAT-05/06 | Contrato proposto, não implementado |
| UX-04 P1 | Destinatário conhecido sem jornada de conferência | Próprio ID e referência voluntária; preview restrito sem diretório | C02; RF-02/03/09; UAT-03 | Proposta específica; privacidade/TTL pendentes |
| UX-05 P1 | If-Match sem versão individual obtível | Item ETag/lista resourceVersion, GET membership e conflitos/revogação/rascunho | C05/C07; RF-08/13; UAT-08/09 | Especificado; sem alteração da Foundation |
| UX-06 P1 | Estados sem operações/recuperação completos | Lista/item/cancel/replace, transições e adiamentos explícitos | C04; RF-12; UAT-06 | Escopo proposto de ciclo mínimo, aprovação pendente |
| UX-07 P1 | Resposta perdida e hash sem recuperação segura | Replay sem código; substituir explicitamente; autorização atual antes de replay | C06; RF-18; UAT-06/10 | Especificado; retenção/idempotência pendentes |
| UX-08 P1 | Oito horas e aviso genérico sem tratamento AA | Alternativas de duração, hipótese 24 h/idle extensível, sessão revogável e rascunho | C07; RF-16; UAT-13; ADR-0007 | Revisão proposta, não conformidade certificada |
| UX-09 P2 | API Platform sem UI mínima no backlog | Proposto formulário restrito de owner/org, revisão e comprovante | C09; RF-03; UAT-17; S1-10 | Ajuste proposto para aprovação; console completo adiado |
| UX-10 P2 | Revogação protege backend, não estados renderizados | Revalidar foco/restauração/ação, descartar respostas antigas e limpar ao detectar | C07; RF-14; UAT-08/12 | Corrigido documentalmente; não promete apagar dados já vistos |
| UX-11 P2 | Logout/histórico/abas incompletos | Revogação local, remoto separado, falha do store e cache/bfcache | C07; RF-17; UAT-02/12/15 | Corrigido documentalmente; infraestrutura pendente |
| UX-12 P2 | Falhas e desconhecido genéricos | Catálogo de status e feedback; distinguir timeout de rollback | C06/C09; RF-18; UAT-10/15 | Corrigido documentalmente com recuperação por operação |
| UX-13 P2 | AA sem critérios verificáveis | Meta WCAG 2.2 AA e matriz de evidências/processo completo | Rastreabilidade: matriz WCAG/G06; RF-19; UAT-13/18 | Critérios documentados; teste de IdP/UI adiado até artefato autorizado |
| UX-14 P2 | GET contexto podia gerar seleção de auditoria | Resolução do vínculo único no servidor; comando de seleção retirado; GET/poll não criam vínculo ou falsa troca | C09; RF-05/06/15; UAT-14; ADR-0011 | Revisado para empresa única; sem comando de seleção |
| UX-15 P2 | UX concentrada no fim e sem rastreabilidade | RF/UAT e revisão preliminar S1-10 antes do congelamento; entregas com demonstração incremental | Planejamento revisado; RF-01–20; UAT-01–19 | Dependências revisadas; estimativa anterior não é compromisso |
| UX-16 P2 | Papéis sem todas transições por alvo | C08 nega padrão, protege Owner, proíbe autoalteração e reativação | C08; RF-13; UAT-16; ADR-0010 | Matriz proposta para aprovação |

Nenhum P1 foi encerrado como funcionalidade ou teste aprovado. Todos receberam tratamento específico; aceitar tecnicamente essas propostas depende do revisor humano. P2 de conformidade runtime/design system completo e console completo permanece explicitamente posterior, sem ser requisito executado nesta fase.

## Decisões adicionais pendentes de aprovação

| Decisão | Recomendação documentada | Responsável por aprovar / risco |
|---|---|---|
| H01 Transporte web | Razor→Application; bearer separado | Arquitetura/segurança; duplicação de adapters exige testes |
| H02 Identificação | ID+referência voluntária 24 h, prévia mínima | Gestor/privacidade/segurança; exposição voluntária limitada |
| H03 Convite | Link sem código; colagem e continuation criptografada, padrão 15 min ajustável até 150 min antes de capturar | Gestor/segurança/UX; passo adicional e tratamento de tempo |
| H04 Ciclo e segredo | Cancel/replace; apenas hash persistente; replay sem código | Gestor/arquitetura; nova substituição se resposta se perder |
| H05 Concorrência/idempotência | ETag individual, chave por comando/48 h, replay reautorizado | Arquitetura/operação; retenção e espaço de armazenamento |
| H06 Sessão | 24 h absoluto, idle 30/15 min, aviso 2 min e extensões | Segurança/UX/gestor; janela maior, validar IdP e AA |
| H07 Sessão técnica/rascunho | Store revogável, rascunho seguro por usuário/org/24 h | Privacidade/operação; retenção, criptografia e indisponibilidade |
| H08 Escopo UI e RBAC | Platform mínimo e matriz C08; Owner não revogável | Gestor/arquitetura; ações excluídas não podem surgir na UI |
| H09 Auditoria de vínculo/resolução | Vínculo/resolução única, sem seleção; GET/poll não mudam empresa | Arquitetura; evitar duplicação e excesso de volume |
| H10 Gate/execução | G01–G08, RF/UAT e backlog revisado | Revisores humanos; aprovação documental ≠ ADR/UX/protótipo/código |

H01–H10 detalham D01–D11 do planejamento, sem conceder aprovação. Continuam pendentes provedor/responsável IdP, grants RLS/bootstrap, pacotes/versões, MaxOffset, retenção de auditoria e gates Foundation/Repository 2.x. Código/infraestrutura/migração/deploy/commit/push não estão autorizados.

## Riscos residuais e limites

- Link+código separado adiciona esforço; a prototipação deve validar compreensão e colagem acessível, sem reduzir prova de destinatário.
- Referências, continuation, sessão e rascunho adicionam armazenamento técnico e privacidade. A recomendação não aprova sua operação nem aumenta capacidade confirmada.
- Rascunho tem TTL; além dele há perda possível explicitamente informada. Convite pode exigir recomeçar captura sem perder o convite de negócio, desde que válido.
- Extensão de sessão não é renovação de membership; comprometimento do servidor/DB e dados já vistos não são apagáveis pela UX.
- Revogação concorrente tem semântica de ordenamento, não cancelamento retroativo. Reautenticação não reenvia comandos automaticamente.
- Granularidade SQL/grants e prova de RLS dependem da revisão arquitetural e testes reais futuros, não de wireframe.
- Testes do IdP, AA, mobile, rede e bfcache precisam de artefato executável autorizado; não alegar conformidade a partir de texto.
- Nenhuma alteração nos produtos GIV/ERP Imobiliário ou implementação do ERP horizontal/CRM nesta etapa.

## Parecer documental

O pacote possui tratamento específico dos 16 achados, contratos e estados críticos e rastreabilidade proposta. Está preparado para revisão humana e, se autorizada, prototipação não executável das hipóteses. A prototipação estática foi autorizada em etapa posterior e revisada para conta única; não está aprovada como solução técnica, Gate UX, implementação ou conformidade. G01–G08 são critérios de aceite propostos, não gates aprovados por este parecer.

O relatório de integridade será apresentado no chat com comparação de hashes, git status e diff. Não criar commits para produzir evidência desta etapa.

## Reavaliação pelo modelo de empresa única

UX-01: adapters mantêm autorização única e resolução da empresa por conta. UX-02–07: identificação/segredo/preview e ciclo permanecem, com criação atômica de conta/membership e unicidade contra outro convite de B. UX-08/10/11: sessão não permite trocar empresa; relogin revalida todas as abas e rascunhos. UX-09: operador não reutiliza identidade já vinculada como Owner de B. UX-12/13: erros e AA permanecem. UX-14: comando/evento de seleção foi retirado, substituído por resolução validada e auditoria de mudanças reais de vínculo. UX-15/16: backlog e matriz C08 incorporam conta única, não papel multiempresa. RF/UAT revisados mantêm IDs sem afirmar evidências executadas.

Os seis temas humanos estão tratados em M08: identificação simples, entrega controlada do convite, validade informada, resultado incerto, aplicações futuras e demo só homologação. O texto completo de uma revisão anterior além desses temas não foi fornecido; não atribuir aprovações a opções/durações. H01–H10 continuam pendentes quanto aos mecanismos. A vinculação permanente identidade→uma conta empresarial é regra de produto aprovada; política IdP para identidades independentes, constraints/FKs, registro histórico e proposta HTTP ainda requerem aprovação. Ver [consolidação e cenários I01–I12](IDENTIDADE_CENARIOS_E_DECISOES.md). Assinatura/licença/template permanecem futuros e por empresa.

# Avaliação dos protótipos — alternativas, permissões e aceite

## Status e escopo da avaliação

**Material exploratório não executável. Revisão humana pendente.**

Referências: [mapa/fluxos](README.md), [wireframes P01–P12](WIREFRAMES.md), [C01–C10](../../architecture/SPRINT_1_CONTRATOS_FLUXOS.md), [RF/UAT](../../architecture/SPRINT_1_RASTREABILIDADE_UX.md) e [planejamento](../../architecture/SPRINT_1_PLANEJAMENTO.md). Os seis ADRs técnicos permanecem Propostos. Nenhuma alternativa, tempo de sessão, fluxo técnico ou controle visual é aprovado por existir nesta maquete.

Revisão vigente: [conta empresarial única M01–M10](../../architecture/CONTA_EMPRESARIAL_UNICA.md), diretriz aprovada. Desenho de identidade/conta, contratos e protótipos continuam propostas. Todas as telas entram diretamente na empresa vinculada; múltiplas empresas por conta e seletor estão fora do MVP. A matriz abaixo usa fixtures distintas em A/B.

## A01 — Comparativo de convites

| Alternativa | Jornada e clareza | Controles necessários / risco | Situação exploratória |
|---|---|---|---|
| A: URL sem segredo + código separado | Abrir → colar código → login → prévia → aceitar; mais esforço de cópia | Código não entra em URL/state/referrer; conta destinatária obrigatória, expiração e uso único; copiar pode expor clipboard | Baseline desenhada P05/P06, coerente com C03; revisão pendente |
| B: convite na caixa de entrada do usuário autenticado, sem código entregue externamente | Entrar → convites recebidos → prévia → aceitar; menos transcrição | Destinatário vinculado no servidor, não enumeração, sessão/MFA e confirmação; exige contrato/listagem e revisão do modelo de ameaça | Alternativa futura, sem substituir prova por código já proposta ou criar endpoint nesta etapa |
| C: URL sem segredo + fragmento com código e remoção imediata no browser | Menos passos aparentes | Fragmento continua acessível a scripts/histórico/cópia; script, CSP, limpeza e retomada exigem análise; não é automaticamente seguro | Não recomendada como baseline Razor de baixa complexidade; não implementada |
| D: convite por contato/e-mail verificado | Familiar, pode facilitar destinatário ainda desconhecido | Entrega confiável, verificação de contato, associação de identidade e privacidade; não associar conta só pelo e-mail | Fora da Sprint 1; dependências comerciais/operacionais não autorizadas |
| E: URL com segredo em query/path | Conveniente para um clique | Pode expor em logs/referrer/retornos; confirmação e identidade não resolvem essa exposição | Excluída da baseline, sem protótipo de fluxo inseguro |

"Link e código separados" não impede que o destinatário repasse ambos. A proteção depende também de identidade destinatária, validade, autorização, rate limit e confirmação. Não reduzir esses controles ao avaliar conveniência.

Tarefa de revisão humana: pedir ao participante para identificar o que enviar, colar e conferir, usando valores fictícios. Observar necessidade de voltar, erro de cópia, confusão entre senha/referência/código e compreensão da exibição única. Não registrar segredos reais ou executar envios externos. A hipótese B só pode prosseguir com revisão de C03/C04 e aprovação humana, não por preferência isolada de layout.

## A02 — UserId/referência versus linguagem simples

| Variante de linguagem | Benefício | Risco | Hipótese a avaliar |
|---|---|---|---|
| "UserId / recipientReference" | Correspondência técnica direta | Termos opacos e percepção de configuração técnica | Manter nomenclatura em ajuda/detalhes, não como título principal |
| "Compartilhar meu acesso" | Curta e familiar | Pode sugerir compartilhar senha/sessão ou conceder autorização | Não usar sem qualificação; testar interpretação antes de adotar |
| "Receber um convite" → "Gerar identificação para convite" | Foco na intenção, sem prometer acesso concedido | Precisa explicar dois valores e validade | Baseline P04 recomendada para avaliação |
| "Copiar pacote de identificação" | Uma ação pode reduzir trocas | Formato composto/adapters ainda não especificados; não inventar parser nem esconder campos necessários | Não desenhar como fluxo aprovado; decisão futura se houver evidência de necessidade |

Texto de apoio proposto: "Estes dados permitem que um administrador confira sua identificação para enviar um convite. Eles não dão acesso à sua conta." Acrescentar que a referência permite conferência mínima até expirar e deve ir somente a administrador conhecido. Não confundir essa referência com código do convite ou token de login.

A linguagem simples muda apresentação, não contrato. Dois campos do baseline continuam presentes/copiáveis; ID completo deve ser acessível para conferência, ainda que visualmente recolhido. Nome de exibição não é identidade civil verificada.

## A03 — Navegação administrativa versus usuário comum

| Opção | Vantagem | Custo/risco | Recomendação exploratória |
|---|---|---|---|
| Mesmo menu completo, ações bloqueadas | Ajuda a revelar o produto | Ruído, pedidos de elevação e divulgação de capacidades irrelevantes | Não usar como baseline para Member |
| Menu por capacidades atuais | Caminho curto e menos ação impossível | Precisa refletir atualização/revogação; esconder não autoriza | Baseline P03/P07, com política única no backend futuro |
| Portais totalmente distintos para todo papel Tenant | Forte especialização | Duplicação e desorientação quando papel muda | Não necessário para esta sprint |
| Área Platform separada da empresarial | Clareza sobre privilégio e escopo | Conta/login separados aumentam passos | Manter conforme contratos; nunca atalhar por impersonação |

Empresa vinculada deve permanecer visível em desktop/mobile, como texto sem seletor. Priorizar itens administrativos só a quem pode usá-los, com termos iguais em ambas as larguras. Não incluir menus de viagens, imóveis, financeiro ou funil no portal Core. Usuário comum não precisa conhecer RBAC/tenant para entender "Organização A / Membro".

## A04 — Expiração e recuperação de formulário

| Alternativa | Clareza / vantagem | Risco e limite | Situação |
|---|---|---|---|
| Encerrar sessão sem aviso ou retomada | Menos estados visuais | Perda de trabalho, surpresa e barreira de acessibilidade | Não adotada nos desenhos |
| Aviso acessível + continuar quando permitido | Explica tempo e preserva tarefa | Não garante AA; não pode prolongar limite absoluto ou membership por conta própria | P01-R como baseline exploratória |
| Reautenticar + revisão de rascunho autorizado | Recuperação clara, sem gravação automática | Depende de store, whitelist, privacidade e mesmo usuário/org | Proposta C07 ilustrada, não capacidade implementada |
| Salvar automaticamente tudo no navegador | Parece simples | Risco de segredo/dado de tenant em cache/localStorage | Não utilizar no baseline |

Os tempos C07 (incluindo hipótese de sessão absoluta/idle e janela de retomada de convite) não são escolhas aprovadas. Wireframes usam placeholders, não cronômetro funcional. A revisão deve avaliar texto/foco/decisão; comprovar limites e conformidade requer testes do processo real, inclusive IdP. Se não houver rascunho preservado ou se expirou, informar a perda em vez de prometer recuperação.

Não há troca de empresa. Logout/relogin com outra identidade exige decisão sobre edição não confirmada e revalidação; salvar não pode ocorrer em outra conta/empresa. Logout local, provedor e outras aplicações têm alcances diferentes. Acesso perdido não é sessão expirada: usuário recebe recuperação controlada ou saída, sem seletor, novo vínculo ou reativação automática.

## Matriz tela × ação × permissão × estado

Esta matriz descreve o desenho; C08 continua referência proposta de política. IDs Pxx são telas, RF/UAT são requisitos/cenários ainda não executados. Novas apresentações não aprovam contratos nem implementam endpoints.

| Tela | Ação representada | Permissão / caso de uso relacionado | Estados e recuperação | Referência |
|---|---|---|---|---|
| P01 | Entrar, recuperar sessão, sair | Identidade ativa; esquema correto; OIDC/session C01/C07 | IdP indisponível, expiração, remoto incerto | RF-01/16/17; UAT-01/02/12/13/15 |
| P02 | Entrar no início da empresa | Conta única ativa; ResolveEnterpriseContext | Carregando, sem vínculo/preparação, negado ou indisponível | RF-04; UAT-04/07/15 |
| P03 | Navegar na empresa vinculada | Conta/empresa/membership atuais; GET /me/context | Resolução, acesso perdido, relogin e histórico | RF-05/06/14; UAT-07/08/12/14 |
| P04 | Gerar/copiar própria identificação | Próprio usuário; GenerateRecipientReference | Única exibição, expirada, clipboard indisponível, 429 | RF-02; UAT-03/16/18 |
| P05 | Conferir destinatário e emitir | Owner/Admin atual; PreviewRecipient/CreateInvitation | Referência inválida, pendente existente, resultado desconhecido | RF-09/18; UAT-03/04/10/16 |
| P05 | Ver lista/item, cancelar/substituir | Owner/Admin da organização; C04, versão/reautorização | Pending/Expired/Accepted/Cancelled/Replaced | RF-12; UAT-06/09/10 |
| P06 | Capturar código, autenticar e pré-visualizar | Prova de código e destinatário; C03/C04 | Conta errada, continuation expirada, código inválido | RF-10; UAT-05/13/15 |
| P06 | Aceitar ou não aceitar agora | Destinatário autorizado, estado/validade/versão atuais | Aceito, expirado, cancelado, substituído, replay | RF-11; UAT-05/06/09/10 |
| P07 | Promover/rebaixar/revogar outro membro | Owner altera Admin/Member; Admin só revoga outro Member; C08 | Próprio/Owner sem ação, revogado sem reativação, 412 | RF-13/14; UAT-08/09/16 |
| P08 | Consultar auditoria sanitizada | Owner/Admin do mesmo tenant; QueryAudit | Vazio por página, erro, falta de direito | RF-15; UAT-11/14/15/17 |
| P09 | Conferir proprietário e criar organização | PlatformOperator/MFA; C02/C09 | Revisão, criado, referência vencida, repetição | RF-03; UAT-03/10/17 |
| P10 | Recuperar de erro/estado | Direito atual é revalidado; C05–C09 | 400/401/403/404/409/412/428/429/500/503/sem resultado confiável | RF-18; UAT-09/10/15 |
| P11 | Ver espaço e hipotético launch | Sprint 1: informação estática; futuro: contrato e autorização a definir | Sem integração nesta sprint; variante futura fictícia | RF-20 como limite; não inventar API de catálogo |
| P12 | Somente homologação: ler/criar/alterar registro demonstrativo | Leitura Member+; escrita Admin/Owner; C05/C06/C08 | Vazio, validação, conflito, perda de acesso | RF-07/08/20; UAT-07–10/19 |

Regras negativas comuns: nenhum botão eleva papel; ID/código não autoriza sozinho; PlatformOperator não recebe acesso empresarial; dados de B não aparecem para quem só acessa A; replay depois de revogação revalida o direito. Resposta antiga de A não pode ser renderizada como B. Observar esses requisitos no walkthrough; comprovação técnica pertence à implementação futura.

## Roteiro de avaliação humana

Fixtures fictícias: Ana_A Owner A; Bruno_B com identidade independente em B; Carla conhecida sem conta empresarial, depois Member/Admin A; uma conta Platform separada. Uma identidade não possui conta/membership em A e B, inclusive após revogação. Não usar clientes/contas reais. Outras combinações de papel são variações de cenário, não alterações reais de usuário.

| Exercício | Material | Pergunta/observação verificável |
|---|---|---|
| V01 Entrar diretamente na empresa | P01/P02/P03 | Pessoa reconhece sua empresa vinculada sem lista/seletor e não interpreta o cabeçalho como ação de troca? |
| V02 Preparar convite sem empresa | P02 preparação/P04 | Entende que identidade ainda sem conta não recebe dados empresariais e que identificação não concede acesso? |
| V03 Emitir convite | P05 | Consegue distinguir destinatário, organização, papel Member, URL e código sem instrução técnica adicional? |
| V04 Conta errada e prévia | P06 | Entende como trocar conta sem receber identidade/organização não autorizada? Sabe que login ainda não aceitou? |
| V05 Alterar papel/revogar | P07 | Reconhece efeito e alvo antes de confirmar? Admin percebe a ausência de promoção sem pedir bypass? |
| V06 Contas distintas e abas | P03/P10/P12 | Reconhece empresa única por sessão, revalidação ao relogar e impossibilidade de levar resposta/rascunho A à conta B? |
| V07 Sessão e conflito | P01-R/P10 | Distingue continuar sessão, reautenticar, revisar versão e reenviar manualmente? |
| V08 Operação Platform | P09 | Operador entende que criar organização não o torna proprietário nem libera leitura empresarial? |
| V09 Auditoria e indisponibilidade | P08/P10 | Distingue vazio, erro, resultado desconhecido e efeito confirmado? |
| V10 Aplicativos e limites | P11/P12 | Entende que produtos têm interfaces próprias e que atalhos não contratam/habilitam nada? |

Para cada exercício registrar caminho escolhido, passos, dúvidas, erros de compreensão e sugestão, sem alegar resultados antes da sessão. Usar comparação A/B de linguagem e fluxo como hipótese qualitativa; não inventar taxa de sucesso, tempo medido ou amostra. Confusão sobre autorização, destinatário, organização ou resultado é bloqueadora de aceite até tratamento revisado.

## Checklist WCAG 2.2 AA — validação futura

Todos os itens estão **pendentes**. Monoespaço/Mermaid não demonstram semântica de formulário, contraste, mobile real ou leitor de tela. Avaliar texto/ordem/intenção agora; validar critérios A/AA aplicáveis no processo executável futuro, incluindo IdP.

- [ ] Teclado, ordem de foco, retorno de foco e ausência de armadilhas.
- [ ] Rótulos, títulos, idioma, nomes acessíveis e relações de tabela/campo.
- [ ] Erros associados a campos e status anunciados sem mover foco indevidamente.
- [ ] Foco visível, não inteiramente encoberto, navegação consistente.
- [ ] Contraste textual aplicável 4,5:1 (grande 3:1), controles 3:1; cor não é única pista.
- [ ] Texto 200%, reflow 320 CSS px, orientação e espaçamento sem perda de função, observadas exceções.
- [ ] Limites de tempo avaliados por fluxo; alerta isolado não comprova conformidade.
- [ ] Alvos de ponteiro ≥24×24 CSS px ou exceções válidas; preferência de conforto maior não é requisito AA adicional.
- [ ] Login/MFA e colagem compatíveis com mecanismos assistivos; não exigir memorização/transcrição sem alternativa/mecanismo adequado.
- [ ] Confirmações e recuperação compreensíveis; não depender de hover, gesto ou ícone sem nome.

Fontes normativas e apoio: [WCAG 2.2](https://www.w3.org/TR/WCAG22/), [alvos mínimos](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html), [autenticação acessível](https://www.w3.org/WAI/WCAG22/Understanding/accessible-authentication-minimum.html). Checklist é recorte prioritário; não substitui avaliação completa nem certifica AA.

## Componentes candidatos, sem biblioteca implementada

Cabeçalho de contexto e papel; navegação adaptada à capacidade; cartão de organização; campo com instrução/erro; controle de cópia com feedback; resumo de revisão; lista/tabela responsiva; paginação; aviso de sessão; estado vazio/erro; comparação de versões e evento de auditoria sanitizado. Reuso visual não implica pacote compartilhado, framework de plugins ou entidade de domínio comum.

## Riscos e decisões exploratórias pendentes

| ID | Questão para aprovação | Baseline representada / risco |
|---|---|---|
| E01 | Link+código separados versus outra alternativa segura | P05/P06 mantêm C03; avaliar esforço sem retirar prova/identidade |
| E02 | Nome da ação de identificação | P04 evita promessa de compartilhar acesso; termos técnicos em ajuda |
| E03 | Navegação por papel e área Platform | P03/P07/P09 reduzem ruído; testar compreensão quando papel muda |
| E04 | Sessão e recuperação | P01/P10 ilustram C07; storage/tempo/IdP/rascunho continuam decisões técnicas pendentes |
| E05 | Descoberta do acesso às aplicações | P11 tem estado Sprint 1 e variante futura; contrato/launch/entitlements adiados, sem criar catálogo |
| E06 | Demonstração visível no portal | P12 explicitamente de avaliação; decidir sua localização e retirada futura, sem virar produto |
| E07 | Detalhes de auditoria e ajuda | P08 usa campos autorizados; não pressupor endpoint de detalhe global ou integração de suporte |
| E08 | Necessidade de desenho adicional | Wireframes precisam de revisão humana; uma hipótese preferida só muda contrato/ADR mediante revisão própria |

Riscos residuais: copying/clipboard compartilhado, segredo de uso único perdido, dados já vistos não apagáveis, timeout sem certeza de resultado, mudança de acesso concorrente e indisponibilidade dos serviços. Não resolver por autoelevação, reativação, fallback de autorização, armazenamento de token no browser ou repetição cega. Itens de H01–H10/D01–D12 do planejamento não ganham aprovação por estes wireframes.

## Critérios de aceite da prototipação — revisão pendente

| Critério | Evidência documental entregue | O que ainda exige validação humana |
|---|---|---|
| PX-01 Cobertura | P01–P12 com desktop/mobile e variantes | Suficiência e clareza das telas obrigatórias |
| PX-02 Navegação | Mapa e quatro fluxos Mermaid; empresa única por conta/sessão | Entendimento por Owner/Admin/Member/PlatformOperator |
| PX-03 Segurança compreensível | Confirmação de alvo/org/papel, conta errada e não enumeração | Ausência de ambiguidade que induza concessão/acesso errado |
| PX-04 Alternativas | A01–A04 com vantagens/custos e hipóteses | Preferências justificadas, sem reduzir controles |
| PX-05 Estados | P10 e recuperação em cada jornada | Vazio/erro/expiração/conflito/desconhecido distinguíveis |
| PX-06 Acessibilidade | Checklist e intenção de foco/colagem/mobile | Revisão textual agora; testes técnicos futuros separados |
| PX-07 Expansão controlada | P11 futuro e limites operacionais dos produtos | Entender acesso a aplicação sem catálogo/contratação nesta sprint |
| PX-08 Rastreabilidade | Matriz Pxx→ação/política/estado→RF/UAT/Cxx | Nenhuma ação aparente com permissão inexistente ou contrato novo implícito |
| PX-09 Governança | ADRs anteriores intactos e Propostos; artefatos somente Markdown | Aceite explícito da prototipação separado de Gate UX/arquitetura/execução |

Critério de encerramento proposto: percorrer V01–V10, registrar ajustes e decisões E01–E08, e não deixar ambiguidade crítica de organização, destinatário, ação ou resultado sem tratamento. Não inventar aprovação/feedback de usuários; nenhum desses exercícios foi executado nesta criação documental.

## Relatório do escopo produzido

Protótipos [README.md](README.md), [WIREFRAMES.md](WIREFRAMES.md) e AVALIACAO.md foram revisados para a nova diretriz junto aos documentos arquiteturais/propostas afetados. Foundation, código, testes, projetos, infraestrutura e ADRs aceitos permaneceram preservados. A verificação de hashes, links, status e contagem de variantes será apresentada no chat. Não houve frontend/backend, endpoints, testes executáveis, migrations, commit ou push.

## Revisão dos seis temas e aceite de conta única

| Tema humano | Tratamento verificável | Decisão ainda pendente |
|---|---|---|
| Identificação simples | P04 prepara convite com linguagem de intenção; ID/referência acessíveis, sem diretório ou pacote implícito | Validar usabilidade sem retirar prova de destinatário |
| Entrega de convite | P05/P06 endereço sem segredo+código separado, cópia controlada e prévia autenticada | Canal automatizado é futuro; não criar envio real |
| Prazo de segurança | Mostrar validade do servidor e recuperação; tempos de sessão/convite/retomada não são aprovação | Rever parâmetros e acessibilidade, sem prometer AA só pelo aviso |
| Resultado incerto | Verificar estado/replay reautorizado, substituir explicitamente segredo perdido | Retenção/idempotência/operação a aprovar |
| Aplicações futuras | P11 catálogo/assinatura/licenças futuras por empresa, sem habilitação comercial | Fonte de direitos/launch é contrato futuro |
| Demonstração técnica | P12 fora do menu normal e só avaliação/homologação | Ambiente e testes só quando autorizados |

Novo critério PX-10: conta empresarial comum chega diretamente à empresa; P02/P03 não oferecem lista/seletor/troca; conta A não adere automaticamente a B por convite/e-mail/recuperação. Matriz RF/UAT cobre identidade sem conta, identidade já vinculada (incluindo revogada), convites A/B concorrentes e Platform separado. Outra empresa pela mesma pessoa não reutiliza credencial automaticamente: política do IdP/conta independente é proposta para aprovação. Confirmar assinatura/configuração futura independentes, produtos operacionais fora do Core e P12 somente homologação.

As alternativas A01–A04 não revogam a diretriz de empresa única. Os seis temas foram informados no pedido, sem texto adicional da revisão humana anterior; não inventar comentários/aceite. Nenhum walkthrough ou teste real foi executado.

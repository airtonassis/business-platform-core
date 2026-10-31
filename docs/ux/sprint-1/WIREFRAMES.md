# Wireframes P01–P12 — desktop e mobile

## Como revisar

Revisão vigente: [conta empresarial única](../../architecture/CONTA_EMPRESARIAL_UNICA.md), diretriz de produto aprovada; contratos técnicos/protótipos continuam propostos. Empresa é Organization; cada conta de usuário pertence a uma empresa, sem seletor. Revisão anterior de múltiplos vínculos foi substituída antes de implementação.

**Baixa fidelidade, estático, dados fictícios, sem ações executáveis.** Consulte [escopo e navegação](README.md) e [permissões/avaliação](AVALIACAO.md). Os contratos/ADRs permanecem propostos. Caixas não definem dimensões, cores, componentes ou tecnologia final. Leia o texto fora das caixas como alternativa acessível aos desenhos monoespaçados.

Cada tela tem variante desktop e mobile. P10 é biblioteca de estados; suas variantes aplicam-se às demais telas. Datas, durações e códigos são placeholders ou hipóteses dos contratos; não indicam credenciais, prazo de entrega ou configuração aprovada.

## P01 — Entrada, autenticação e recuperação de sessão

Desktop:

```text
+----------------------------------------------------------------+
| Business Platform Core                      Ajuda                |
| Portal de administração                                         |
|                                                                |
| Organizações, acessos e auditoria em um só lugar.                 |
| [Entrar no portal]                                              |
| Sua autenticação ocorre no provedor de identidade.               |
|                                                                |
| Administração da plataforma: acesso administrativo separado      |
| [Entrar como operador]                                          |
+----------------------------------------------------------------+
```

Mobile:

```text
+--------------------------------+
| Business Platform Core          |
| Portal de administração         |
|                                |
| [Entrar no portal]              |
| Autenticação no provedor.       |
| [Ajuda]                         |
|                                |
| Acesso administrativo separado  |
| [Entrar como operador]          |
+--------------------------------+
```

Não desenhar formulário próprio de senha. No IdP, avaliar login, MFA e recuperação de credenciais em processo separado; essas páginas não são implementadas nem controladas por este protótipo. O botão administrativo visível não concede papel.

Variante P01-R — aviso e recuperação (desktop/mobile):

```text
Desktop
| Sua sessão está prestes a terminar.                             |
| [Continuar nesta sessão] [Sair]                                 |
| O limite é validado no servidor. Nenhuma operação será reenviada.|

Mobile
| Sua sessão vai terminar.       |
| [Continuar nesta sessão]       |
| [Sair]                        |

Após expiração
| Entre novamente para continuar.                                |
| Rascunho permitido: disponível até [validade], se preservado.    |
| [Entrar novamente] [Voltar à entrada]                           |
```

Retorno ao mesmo usuário e contexto autorizado mostra revisão de rascunho, nunca gravação automática. Outra conta não recupera conteúdo alheio. Se rascunho não foi salvo ou expirou, informar isso sem prometer recuperação. Aviso e extensão não comprovam AA por si só; tempos continuam hipóteses de C07.

Logout: uma ação direta quando não houver edição aberta. Com edição, oferecer continuar editando ou sair descartando campos não confirmados. Resultado local e remoto são distintos: "Você saiu deste portal. O encerramento no provedor não foi confirmado" quando aplicável. Se o store não confirmar revogação, não afirmar que cópias de sessão foram invalidadas.

Rastreabilidade: RF-01/16/17; C01/C07; UAT-01/02/12/13/15.

## P02 — Início da empresa vinculada

Desktop:

```text
+----------------------------------------------------------------+
| Core / Empresa A | Proprietária             Ana [Conta] [Sair]  |
+----------------------+-----------------------------------------+
| Início               | Bem-vinda à Empresa A                   |
| Usuários e convites  | Sua conta está vinculada a esta empresa.|
| Auditoria            | [Gerir usuários] [Consultar auditoria]  |
| Minha conta          | Ações conforme suas permissões atuais. |
+----------------------+-----------------------------------------+
```

Mobile:

```text
+--------------------------------+
| Core                  [Menu]   |
| Empresa A / Proprietária       |
| Bem-vinda à sua empresa.       |
| [Gerir usuários]               |
| [Consultar auditoria]          |
| [Minha conta] [Sair]           |
+--------------------------------+
```

Member vê apenas início/conta e recursos permitidos, sem botões de administração. Não há cartões de outras empresas, seletor, filtro empresarial, criar empresa para usuário comum ou métricas comerciais. Assinatura/aplicações não são carregadas como capacidades da Sprint 1.

Sem conta vinculada: "Sua conta ainda não está vinculada a uma empresa." [Preparar meu convite] [Sair]. Isso não é uma lista empresarial vazia. Conta revogada/inconsistente: estado P10 com recuperação controlada, não convite para escolher B. Sem resposta: indisponível, não apresentar ambiente errado.

RF-01/04; C01/C09; UAT-01/04/07/15.

## P03 — Navegação empresarial direta, sem seleção ou troca

Desktop:

```text
+----------------------------------------------------------------+
| Core | Empresa A | Administradora           [Minha conta] [Sair]|
+----------------------+-----------------------------------------+
| Início               | Administração da Empresa A              |
| Usuários e convites  | Empresa resolvida pelo vínculo da conta.|
| Auditoria            | Sem troca de empresa neste ambiente.    |
+----------------------+-----------------------------------------+
```

Mobile:

```text
+--------------------------------+
| Core                  [Menu]   |
| Empresa A / Administradora     |
| Início                         |
| [Usuários e convites]          |
| [Auditoria]                    |
| [Minha conta] [Sair]           |
+--------------------------------+
```

Nome/papel são informativos, não dropdown. Contexto é derivado no servidor por conta/membership ativo. Todas as abas dessa sessão têm a mesma empresa. Reautenticar outra identidade não restaura rascunhos/respostas de A em B; ao detectar troca de login, revalidar antes de exibir/salvar.

Pedido de outra empresa pela mesma pessoa exige ambiente/conta/identidade independentes conforme política a aprovar. Não oferecer "Adicionar outra empresa" ou "Mudar ambiente" no MVP. Administração consolidada é estudo futuro. P12 não aparece nesse menu normal.

RF-05/06/14; C07/C09; UAT-07/08/12/14.

## P04 — Preparar meu convite para uma empresa

Desktop:

```text
+----------------------------------------------------------------+
| Minha conta > Preparar meu convite                         [Voltar]|
|                                                                |
| Compartilhe sua identificação com um administrador conhecido.    |
| Isso não concede acesso à sua conta nem à organização.           |
| [Gerar identificação para convite]                              |
|                                                                |
| Após gerar — exibida uma vez                                    |
| Identificador: [ID fictício do próprio usuário] [Copiar ID]       |
| Referência:    [referência fictícia]            [Copiar referência]|
| Válida até: [validade fornecida pelo servidor]                    |
| Nome de exibição: Bruno — confira com o administrador.           |
| Não compartilhe senha ou token de autenticação.                  |
+----------------------------------------------------------------+
```

Mobile:

```text
+--------------------------------+
| Receber um convite     [Voltar] |
| Envie sua identificação a um    |
| administrador conhecido.       |
| Não concede acesso à conta.    |
| [Gerar identificação]          |
|                                |
| Seu identificador: [ID]        |
| [Copiar identificador]         |
| Referência: [valor fictício]    |
| [Copiar referência]            |
| Validade: [instante local]     |
| [Como usar estes dados]        |
+--------------------------------+
```

Linguagem recomendada para avaliação: "Receber um convite" e "Compartilhar minha identificação"; evitar afirmar "Compartilhar meu acesso" sem explicar que não há concessão. Não esconder do usuário os dois campos exigidos pelo contrato. Detalhes técnicos podem explicar UserId/referência sem obrigar memorizar/copiar manualmente cada caractere.

Feedback: "Identificador copiado" / "Referência copiada", anunciado sem mover foco. Falha do clipboard oferece seleção/cópia manual acessível. Após sair da página, referência não é recuperada pelo GET; gerar outra é ação explícita sujeita ao limite, não recuperar segredo antigo. Não enviar mensagem ou criar convite por essa ação. Compartilhar permite conferência mínima por emissor autorizado até a validade; não alegar que é dado público sem risco.

Preparação é para identidade ainda sem vínculo; a conta ativa entra diretamente na empresa e não usa isso para aderir a B. Campos técnicos ficam em detalhes acessíveis; copiar ainda entrega ID+referência, sem pacote/parser novo ou envio automático. Conta já vinculada não é transferida por convite.

Rastreabilidade: RF-02; C02; UAT-03/16/18.

## P05 — Emissão, visualização e gestão de convite

Desktop:

```text
+----------------------------------------------------------------+
| Empresa A / Membros e convites               [Voltar]        |
| Convidar pessoa                                                 |
| Identificador recebido [________________________________]       |
| Referência recebida    [________________________________]       |
| [Conferir destinatário]                                         |
|                                                                |
| Após conferência autorizada                                     |
| Bruno — [ID completo acessível]                                 |
| Papel inicial: Membro (fixo)                                    |
| Organização: A                                                 |
| Nome de exibição não comprova identidade civil.                  |
| [Voltar e corrigir] [Confirmar convite]                          |
+----------------------------------------------------------------+
```

Mobile:

```text
+--------------------------------+
| Empresa A                   |
| Convidar pessoa                 |
| Identificador [_____________]   |
| Referência    [_____________]   |
| [Conferir destinatário]         |
|                                |
| Bruno / [ID em detalhes]       |
| Empresa A — Membro          |
| [Voltar e corrigir]            |
| [Confirmar convite]            |
+--------------------------------+
```

Sucesso de emissão (ambas as larguras, coluna única no mobile):

```text
| Convite criado para Bruno na Empresa A.                    |
| 1. Endereço: [URL fictícia sem código] [Copiar endereço]         |
| 2. Código: [código fictício, não utilizável] [Copiar código]     |
| Compartilhe os dois com o destinatário por canal controlado.    |
| O código só é mostrado agora. Não é senha da sua conta.        |
| [Concluir e voltar aos convites]                               |
```

Não oferecer "copiar link único com código" ou botão Enviar. Emissor nunca digita papel Admin/Owner. Código não entra em histórico/auditoria. Antes de sair do resultado de emissão, aviso claro de exibição única; não declarar que a saída cancela o convite.

Visualização administrativa: linha/cartão com destinatário autorizado, papel Membro, validade, estado e ação permitida. Pendente: [Ver detalhes], [Cancelar convite], [Substituir convite]. Expirado: [Substituir convite]. Aceito/Cancelado/Substituído: somente detalhes, sem restaurar ou recuperar código. Paginação e filtros de estado são propostos no contrato existente; nada de diretório global.

Revisão de substituição: "O código anterior deixará de funcionar. Será criado outro convite para a mesma pessoa como Membro." [Voltar] [Substituir]. Se resposta anterior se perdeu: "O convite foi registrado, mas o código não pode ser exibido novamente" somente após consulta confirmar registro; oferecer substituição, nunca reconstrução do hash.

Erros de par inválido: "Não foi possível conferir a identificação. Confira os dados recebidos." Sem confirmar existência ou informar outro destinatário. Pendente existente não é ignorado automaticamente; orientar consultar o convite autorizado.

Rastreabilidade: RF-09/12/18; C02/C04/C05/C06; UAT-03/04/06/09/10/16.

## P06 — Recebimento, prévia e aceitação do convite

Desktop, antes de validar:

```text
+----------------------------------------------------------------+
| Core / Convite                                                  |
| Informe o código recebido junto com o endereço.                  |
| Código [________________________________________________]       |
| Tempo para concluir o login: [padrão proposto / ajustar]          |
| [Continuar] [Voltar à entrada]                                  |
| A organização será mostrada após conferir código e conta.        |
+----------------------------------------------------------------+
```

Mobile:

```text
+--------------------------------+
| Core / Convite                  |
| Cole o código recebido.         |
| Código [____________________]  |
| Tempo para login [Ajustar]     |
| [Continuar]                    |
| [Voltar à entrada]             |
+--------------------------------+
```

Janela de retomada: permitir ajuste anterior à captura conforme hipótese C03, sem usar countdown decorativo como prova de acessibilidade. Código pode ser colado; não exigir memorização/transcrição ou segmentação que impeça colagem. Se não autenticado, continuar inicia IdP e depois retoma; não aceita automaticamente.

Prévia após prova válida (desktop/mobile):

```text
Desktop
| Você está conectado como Bruno. [Trocar conta]                 |
| Convite para Empresa A                                    |
| Papel: Membro — consultar recursos permitidos                  |
| Validade: [instante local identificado]                        |
| [Não aceitar agora] [Aceitar convite]                          |

Mobile
| Conectado como Bruno          |
| [Trocar conta]                |
| Empresa A                  |
| Papel: Membro                 |
| Validade: [instante]           |
| [Aceitar convite]             |
| [Não aceitar agora]           |
```

Se a conta já pertence a outra empresa, não há transferência ou segundo vínculo. Depois de autenticar, orientar uso de conta independente sem revelar a empresa de origem ao emissor. Antes da prova válida, conta errada/convite inválido não mostra nome da organização nem destinatário: "Não foi possível acessar este convite com a conta atual." [Trocar conta] [Conferir código]. Não indicar "convite para Ana" à pessoa errada. Depois da prova, permitir informar estado expirado/cancelado/substituído, sem oferecer código sucessor.

Confirmação bem-sucedida: "Convite aceito. Sua conta está vinculada à Empresa A." [Ir para minha empresa]. O servidor resolve o vínculo após o commit; sem lista/seletor ou escolha manual de outra empresa. Não aceitar agora encerra jornada, não cancela o convite do emissor. Convite expirado não inicia pedido externo automaticamente.

Rastreabilidade: RF-10/11/18; C03/C04/C06/C07; UAT-05/06/09/10/13/15.

## P07 — Usuários, papéis e permissões da empresa

Desktop:

```text
+----------------------------------------------------------------+
| Empresa A | Proprietária                    [Sair]   |
| Membros e convites                [Convidar pessoa]              |
| Membros [selecionado]  Convites                                  |
|                                                                |
| Pessoa          Papel          Estado       Ações permitidas     |
| Ana (você)      Proprietária   Ativo        sem ação             |
| Bruno           Membro         Ativo        [Promover] [Revogar] |
| [Anterior] [Próxima]                                            |
+----------------------------------------------------------------+
```

Mobile:

```text
+--------------------------------+
| Empresa A / Proprietária    |
| Membros e convites              |
| [Convidar pessoa]              |
| Membros | Convites             |
|                                |
| Ana (você) — Proprietária      |
| Ativo — sem alteração própria  |
|                                |
| Bruno — Membro / Ativo         |
| [Promover] [Revogar]            |
| [Anterior] [Próxima]            |
+--------------------------------+
```

Variante Owner olhando outro Admin: [Rebaixar para Membro] [Revogar]. Variante Admin olhando outro Member: somente [Revogar]. Admin olhando Admin/Owner e linha do próprio usuário não oferecem ações. Member não acessa a página. Revogado tem estado explícito, sem "Reativar" nesta sprint.

Revisão de ação: nome/ID do alvo, organização A, papel anterior/novo ou efeito da revogação. Texto: "Bruno perderá acesso à Empresa A. Operações já concluídas não serão desfeitas." [Voltar] [Confirmar revogação]. Em promoção: explicar capacidade administrativa que será concedida. Não usar confirmação vaga "Tem certeza?" sem contexto.

412: versão foi alterada; carregar estado atual e exigir nova revisão. Permissão perdida tem prioridade sobre comparação/exibição de versão. Nenhum campo livre de permissão, troca de UserId ou organização.

Usuários/memberships são locais à empresa. A quantidade exibida não é licença paga ou assento contratado. Gestão futura de licenças por produto é separada; promover papel não contrata produto e criar usuário não compra assento. Conta empresarial de usuário não pode mover para outra empresa.

Rastreabilidade: RF-13/14; C05/C08; UAT-08/09/16/17.

## P08 — Auditoria autorizada

Desktop:

```text
+----------------------------------------------------------------+
| Empresa A | Administradora                           |
| Auditoria                                                       |
| Horários apresentados em [fuso identificado].                   |
|                                                                |
| Instante       Ator autorizado   Ação             Resultado      |
| [instante]     Ana               Papel alterado   Confirmado     |
| [instante]     Ana               Convite criado   Confirmado     |
| [Anterior] [Próxima]                                            |
| [Ver detalhe permitido do evento selecionado]                    |
+----------------------------------------------------------------+
```

Mobile:

```text
+--------------------------------+
| Empresa A / Auditoria       |
| Fuso: [identificado]           |
|                                |
| [Instante]                    |
| Ana — Papel alterado          |
| Resultado: Confirmado         |
| [Ver detalhe]                 |
|                                |
| [Anterior] [Próxima]            |
+--------------------------------+
```

Detalhe é a representação sanitizada dos campos já autorizados da listagem/caso de uso; nenhum novo endpoint global ou payload bruto é pressuposto. Mostrar correlação somente quando permitida e útil ao suporte. Não exibir tokens, referência, código, SQL ou stack. Sem exportação ou edição nesta proposta. Member e operador sem membership não têm esta navegação. Vazio: "Não há eventos disponíveis nesta página"; não afirmar ausência universal com contagem best-effort.

Rastreabilidade: RF-15/18; C08/C09; UAT-11/14/15/17.

## P09 — Criação de organização pelo PlatformOperator

Desktop:

```text
+----------------------------------------------------------------+
| Core / ADMINISTRAÇÃO DA PLATAFORMA           Operador [Sair]     |
| Criar organização                                               |
| Identificação do proprietário                                  |
| Identificador [________________] Referência [________________]  |
| [Conferir proprietário]                                        |
|                                                                |
| Conferido: Ana — [ID completo acessível]                         |
| Nome da organização [____________________________________]      |
| [Revisar criação]                                              |
+----------------------------------------------------------------+
```

Mobile:

```text
+--------------------------------+
| ADMINISTRAÇÃO DA PLATAFORMA      |
| Criar organização               |
| ID proprietário [___________]  |
| Referência      [___________]  |
| [Conferir proprietário]        |
| Ana — [ID em detalhes]         |
| Organização [_______________]  |
| [Revisar criação] [Sair]       |
+--------------------------------+
```

Proprietário precisa ser identidade sem conta empresarial vinculada. Se já está em A, criar B não reutiliza a conta e falha atomicamente, sem ambiente órfão. Revisão de criação mostra nome e proprietário, com [Voltar e corrigir] [Confirmar criação]. Comprovante: organização/proprietário criados, identificadores autorizados e "Sua conta de operador não recebeu acesso empresarial". [Criar outra organização] [Sair]. Não mostrar [Entrar como proprietário], seletor de empresas ou lista global.

Resposta perdida: mesma chave permite consultar resultado seguro sem criar outra empresa. Se referência expirou entre conferência e criação, pedir dados atualizados sem confirmar criação. Não tratar nome de exibição como validação civil.

Rastreabilidade: RF-03; C02/C06/C08/C09; UAT-03/10/15/17.

## P10 — Estados de erro, carregamento e ausência de dados

Desktop, área de conteúdo:

```text
+----------------------------------------------------------------+
| [contexto já validado, quando ainda permitido]                   |
| Título do estado                                                |
| Explicação curta: o que ocorreu e o que é seguro fazer agora.    |
| [Próxima ação permitida] [Voltar]                               |
| Referência do atendimento: [correlação sanitizada, se disponível]|
+----------------------------------------------------------------+
```

Mobile:

```text
+--------------------------------+
| Título do estado               |
| Explicação curta e compreensível|
| [Próxima ação]                 |
| [Voltar]                      |
| Referência: [se disponível]    |
+--------------------------------+
```

| Variante | Mensagem/ação proposta | Limite |
|---|---|---|
| P10-L Carregamento | "Carregando organizações..."; anúncio não repetitivo | Não mostrar lista vazia durante espera |
| P10-V Vazio | "Nenhum membro/registro nesta página" ou orientação P02 | Mensagem específica ao recurso e permissão |
| P10-A Acesso perdido | "O acesso a esta organização não está disponível." [Orientações de acesso] [Sair] | Retirar dados ao detectar; não revelar B nem forçar logout global |
| P10-C Conflito | "O registro mudou desde sua leitura." [Ver versão atual] [Voltar ao rascunho] | Revalidar direito antes de mostrar comparação |
| P10-S Sessão expirada | "Entre novamente para continuar." [Entrar novamente] | Não repetir gravação após login |
| P10-I Indisponível | "Não foi possível consultar agora." [Tentar consulta novamente] | Retry de leitura não é replay automático de mutação |
| P10-U Resultado desconhecido | "Não foi possível confirmar o resultado." [Verificar resultado] | Não declarar rollback ou novo sucesso sem prova |
| P10-R Limite de tentativas | "Aguarde [tempo do servidor] para tentar novamente." | Respeitar Retry-After, sem loop |
| P10-F Validação | Resumo de erros e erro associado ao campo | Não expor referência/código em mensagem |

Conflito mobile empilha "Seu rascunho" e "Versão atual", sem exigir tabela lado a lado. Comparação de acesso/papel sempre expõe organização e alvo válidos; código e referência nunca entram no rascunho. Logout/histórico em outra aba podem exigir nova validação, não prometer limpeza remota instantânea.

Rastreabilidade: RF-14/16/17/18; C05/C06/C07/C09; UAT-08–UAT-15/18.

## P11 — Ponto de acesso às aplicações: exploração futura

**Duas variantes estáticas distintas. Nenhuma lista de produtos contratados é consultada ou inferida na Sprint 1.**

Desktop, variante compatível com a Sprint 1:

```text
+----------------------------------------------------------------+
| Empresa A / Aplicações — conceito para revisão              |
|                                                                |
| O acesso a aplicações aparecerá aqui quando a integração         |
| correspondente estiver disponível e autorizada.                 |
| Nenhuma aplicação é habilitada por esta demonstração.            |
| [Voltar à organização]                                          |
+----------------------------------------------------------------+
```

Mobile, mesma variante:

```text
+--------------------------------+
| Empresa A                   |
| Aplicações: conceito            |
| Integrações e autorização       |
| serão definidas em outra etapa. |
| Sem habilitação nesta demo.    |
| [Voltar à organização]         |
+--------------------------------+
```

Variante futura, exclusivamente ilustrativa de aplicativos disponíveis para um usuário autorizado:

```text
Desktop — DADOS FICTÍCIOS / FUTURO
| Aplicações disponíveis para Empresa A                      |
| GIV — operação em interface própria           [Abrir produto*] |
| CRM Configurável — aplicação independente     [Abrir produto*] |
| *Controle desenhado, sem destino ou execução neste protótipo.   |

Mobile — DADOS FICTÍCIOS / FUTURO
| Empresa A                  |
| GIV                            |
| Interface própria              |
| [Abrir produto*]               |
| CRM Configurável               |
| Aplicação independente         |
| [Abrir produto*]               |
```

Seleção dos nomes nessa fixture não indica contratação, disponibilidade real ou prioridade aprovada. ERP Imobiliário e ERP Multissetorial podem aparecer com o mesmo padrão em fixtures alternativas, sem assumir direitos de A/B. Não há preços, planos, comprar, ativar, upgrade ou pedido comercial. Clicar não cria sessão, assinatura, entitlement ou provisionamento.

Contrato futuro de origem dos aplicativos contratados e autorização de launch **ainda não existe nesta sprint**. Esta é exceção exploratória registrada: não criar endpoint para servir a maquete. Revisão futura precisa definir somente itens autorizados, destino confiável, contexto compatível, validação no produto e recuperação de indisponibilidade. Navegar para aplicativo não transporta referência/código de convite e não compartilha cookie administrativo. GIV mobile/web e demais produtos mantêm UX operacional própria.

Rastreabilidade: RF-20 como limite de independência; não alegar RF de catálogo implementado. Decisão exploratória E05 em Avaliação.

## P12 — Demonstração somente em avaliação/homologação

Desktop:

```text
+----------------------------------------------------------------+
| Empresa A | Administradora                  [Sair]   |
| Demonstração de isolamento — ambiente de avaliação               |
| Este recurso não pertence ao GIV, ERP ou CRM.                    |
| [Novo registro demonstrativo]                                   |
| Título                                  Ação                    |
| Exemplo A1                              [Ver] [Editar]          |
| [Anterior] [Próxima]                                            |
+----------------------------------------------------------------+
```

Mobile:

```text
+--------------------------------+
| Empresa A / Administradora  |
| Demonstração de isolamento      |
| Ambiente de avaliação           |
| [Novo registro]                |
| Exemplo A1                     |
| [Ver] [Editar]                 |
| [Anterior] [Próxima]            |
+--------------------------------+
```

Member vê somente [Ver]. Formulário: "Título do registro demonstrativo", campo com orientação de limite já proposto no contrato e [Salvar] [Cancelar]; organização não editável. Papel atual não é enviado como prova de acesso. P10 trata conflito, rede e perda de acesso. Nada de viagens, imóveis, negociação ou financeiro no formulário.

P12 é acessada somente em ambiente explicitamente autorizado de avaliação/homologação, fora da navegação empresarial normal. Não há atalho na P02/P03, produto demonstrativo contratado ou tela operacional de GIV/ERP/CRM. Conta de A não pode informar B como contexto para teste positivo; B exige fixture com identidade distinta.

Rastreabilidade: RF-07/08/20; C05/C06/C08; UAT-07/08/09/10/19.

## Pontos de revisão das variantes

Comparar entendimento do contexto, facilidade de convidar, descoberta de "minha identificação", carga de copiar dois valores, confirmação de acesso e mensagens de sessão. Não aprovar detalhes visuais pela aparência dos caracteres; conferir [matriz, alternativas e critérios](AVALIACAO.md) antes de decidir. Todos os estados são desenhos, não evidências de autorização, responsividade ou conformidade executadas.

# AGENTS.md — Business Platform Core

## 1. Missão e prioridade

Desenvolver o Business Platform Core como plataforma SaaS modular, segura, testável e evolutiva. Priorizar **software funcional, evidências reproduzíveis e entregas incrementais**. Reduzir retrabalho, documentos duplicados e ciclos de revisão sem decisão.

Este arquivo orienta agentes de programação (incluindo Codex). Não substitui requisitos, ADRs aprovadas, critérios de aceite nem instruções explícitas do responsável pelo projeto. Em conflito, sinalizar a divergência antes de alterar decisões aprovadas.

Para operar o ambiente local Core + ERP Imobiliário, use a Skill Codex `araia-local-environment` e o comando `business-platform-core-s2-1-ui/scripts/araia_local_environment.ps1`; o script é a fonte do procedimento operacional, sem migrations automáticas.

## 2. Fontes de verdade

Antes de mudar comportamento ou modelo de dados, consultar **somente os documentos relevantes para a tarefa**, especialmente:

- `docs/architecture/decisions/` — ADRs e decisões aprovadas.
- `docs/architecture/` — contratos e desenho técnico aplicável.
- `docs/standards/DEVELOPMENT_STANDARDS.md` e `docs/standards/POL-DOC-001.md` — padrões transversais e governança documental; use-os sem reproduzir seus catálogos nos documentos do produto.
- `tests/TechnicalValidation/ADR0009.G1/` — ensaios e evidências da ADR-0009, quando pertinente.
- Código, testes e configuração existentes no módulo afetado.

Não presumir que arquivos locais estão publicados no GitHub. Não inventar caminhos, decisões, evidências, resultados de testes ou funcionalidades inexistentes.

## 3. Fluxo de execução por tarefa

1. **Entender:** identificar objetivo, critérios de aceite, restrições e arquivos afetados. Fazer perguntas somente quando uma decisão indispensável estiver ausente.
2. **Verificar:** reproduzir defeitos e identificar causa raiz. Para alterações de comportamento, preferir teste que falhe antes da correção.
3. **Implementar:** produzir a menor mudança coesa que resolva a causa raiz, respeitando padrões existentes.
4. **Validar:** executar testes relevantes e regressões afetadas; registrar comando, ambiente, resultado e limitações.
5. **Entregar:** apresentar resumo curto com alterações, evidências, pendências e próxima ação objetiva.

Não criar uma nova rodada de planejamento se a tarefa já estiver especificada. Não transformar uma correção localizada em refatoração ampla sem justificativa e autorização.

Priorizar Vertical Slices funcionais, dos contratos à UI/API e persistência, com autorização e isolamento demonstráveis. Planejar uma vez, executar com qualidade, validar de forma inteligente e avançar.

## 4. Superpowers — uso seletivo

Se as skills do Superpowers estiverem disponíveis **nesta sessão do agente**, usá-las conforme a necessidade, sem impor cerimônias adicionais:

- **Test-driven development:** preferencial para novas regras de negócio, correções e invariantes verificáveis.
- **Systematic debugging:** quando a causa de uma falha não for evidente ou houver recorrência/intermitência.
- **Verification before completion:** antes de declarar que uma implementação ou correção foi concluída.
- **Planning / brainstorming:** somente para mudanças grandes, decisões ainda abertas ou requisitos ambíguos.
- **Code review:** quando houver risco elevado, mudança transversal ou solicitação expressa.

Não afirmar que uma skill foi executada se não estiver instalada/disponível. Se indisponível, aplicar os princípios de teste, depuração e verificação com as ferramentas existentes. Não interromper uma tarefa simples apenas para instalar ferramentas.

## 5. Revisão e papéis

- **Codex:** agente principal de implementação, testes, correções e evidências.
- **ChatGPT / responsável técnico:** apoio às decisões arquiteturais, avaliação de riscos e critérios de aceite.
- **Claude Code:** recurso excepcional; não é revisão obrigatória nem deve receber todos os documentos. Solicitar segunda opinião apenas quando houver bloqueio técnico relevante, risco crítico, divergência persistente ou pedido explícito.
- **CI / GitHub Actions:** executar verificações automatizadas quando configuradas; não substituir resultados ausentes por afirmações narrativas.

Não exigir aprovação de outro agente para cada arquivo ou correção. Revisões devem se concentrar no diff, no risco e nas provas, não repetir a análise completa sem motivo.

## 6. Regras de arquitetura e segurança

- Respeitar arquitetura modular, limites de módulos, contratos e ADRs aprovadas.
- Manter isolamento multi-tenant, autorização por contexto e mínimo privilégio como invariantes.
- Em PostgreSQL, considerar RLS, FORCE RLS, owners de SECURITY DEFINER, privilégios efetivos, dependências implícitas e concorrência; não confiar apenas em GRANTs textuais.
- Separar risco aceito, falha demonstrada, hipótese e cobertura ainda não executada.
- Não classificar subprova PASS como cenário completo PASS.
- Não produzir allowlist de segurança copiando o snapshot observado; expectativas devem derivar de requisitos e decisões aprovadas.
- Para a ADR-0009, manter D1 como risco residual aceito conforme decisão existente; não reabrir a decisão sem evidência nova. D2/G1 só mudam de estado mediante critérios e evidências aplicáveis.

## 7. Testes e ambientes

- Preferir testes determinísticos, negativos e de regressão para invariantes críticas.
- Distinguir fixture privilegiada, `SET ROLE`, porta SECURITY DEFINER e login real da aplicação.
- Para ensaios destrutivos ou de concorrência do G1, usar **somente banco PostgreSQL isolado e descartável**, com limpeza verificável.
- Nunca executar testes destrutivos em produção ou ambientes compartilhados.
- Registrar o que **não** foi executado, além do que passou ou falhou.
- Interromper alterações potencialmente perigosas diante de P0 demonstrado e informar evidência e impacto.

## 8. Documentação e produtividade

- Atualizar documentos existentes em vez de criar documentos paralelos.
- Criar ADR somente para decisão arquitetural nova e relevante, com autorização apropriada.
- Evitar relatórios extensos quando um resumo de mudanças e evidências basta.
- Não reabrir achado corrigido sem nova prova de regressão.
- Classificar pendências como: **CORRIGIDO**, **BLOQUEADO POR DEPENDÊNCIA**, **RISCO ACEITO** ou **DECISÃO HUMANA NECESSÁRIA**.
- Não criar tarefas duplicadas em gerenciadores externos.
- Dimensionar testes pelo impacto e risco; reutilizar evidências somente quando baseline, fingerprint, ambiente e escopo forem aplicáveis, registrando execução nova, reuso e cobertura ausente separadamente.
- Não repetir auditorias, releituras integrais ou testes sem alteração material, dúvida concreta, conflito, risco ou exigência de aceite. Preservar verificações obrigatórias dos gates e decisões humanas.
- Consolidar alterações, evidências e pendências em um único relatório objetivo por incremento.
- Manter cada regra relevante ligada à sua fonte oficial e versão; separar proposta, implementação, validação, homologação e publicação conforme POL-DOC-001.

## 9. Limites de autonomia

- Não criar tarefas duplicadas nem alterar escopo, responsáveis, prioridades ou estados de aprovação sem autorização aplicável.
- Atualizar autonomamente os itens existentes no Monday quando a operação estiver previamente autorizada pelo responsável, registrando progresso, evidências e bloqueios reais.
- Não interpretar autorização para registrar progresso como autorização para aprovar gates, homologações ou publicações.
- Informar objetivamente as atualizações realizadas, sem exigir confirmação repetida para operações já autorizadas.

## 10. Formato padrão de resposta do agente

Ao concluir uma tarefa, informar de forma concisa:

1. **Alterações:** arquivos e comportamento alterados.
2. **Validação:** comandos/testes executados, resultados e ambiente.
3. **Pendências:** somente riscos, bloqueios e decisões reais.
4. **Próximo passo:** uma ação objetiva, sem abrir planejamento novo desnecessário.

Se a tarefa for apenas de leitura ou diagnóstico, não alterar arquivos nem executar ações de escrita.

## 11. Eficiência de execução

Priorizar a conclusão verificável de cada incremento. Reutilizar diagnósticos, decisões e testes válidos. Evitar ciclos repetitivos de análise, implementação e validação. Escalar a capacidade do modelo apenas diante de necessidade técnica identificada, preservando qualidade, segurança e rastreabilidade.

1. **Continuidade:** aproveitar decisões, implementações e evidências existentes.
2. **Proporcionalidade:** testar conforme o impacto real da alteração.
3. **Conclusão:** priorizar entregas funcionais verificáveis, não volume de atividades ou relatórios.

## 12. Reutilização arquitetural entre produtos

O Business Platform Core é a referência para capacidades transversais dos produtos SaaS integrados. Uma decisão funcional ou arquitetural aprovada deve ser reutilizada nos produtos compatíveis, sem nova discussão desnecessária.

### 12.1. Descoberta antes da implementação

Antes de propor uma solução, verificar se o requisito já está contemplado por decisão, contrato, componente ou padrão existente.

Consultar apenas as fontes relevantes e preservar suas versões, estados de aprovação e evidências.

Antes de implementar uma tela ou fluxo de tela novo, identificar o contrato funcional, o dono/origem dos dados, os componentes reais compatíveis e a referência visual aprovada aplicável, seguindo ARS-FUNC-001, ARS-TPL-001 e ARS-GATE-001 em docs/standards/DEVELOPMENT_STANDARDS.md. Se faltar referência aprovada, registrar a lacuna e solicitar decisão humana; não inventar um modelo definitivo. Para correções pequenas, verificar proporcionalmente somente os critérios afetados e preservar evidências válidas.

### 12.2. Classificação obrigatória

Classificar a capacidade encontrada como:

- **DEFINIDA:** regra aprovada e documentada, ainda não necessariamente implementada.
- **IMPLEMENTADA:** código existente, sem presumir validação suficiente.
- **VALIDADA:** comportamento comprovado por evidência aplicável.
- **REUTILIZÁVEL:** contrato ou componente validado e adequado ao novo produto.
- **PENDENTE:** capacidade necessária ainda não disponível.

Não confundir aprovação funcional com implementação, validação, homologação ou publicação.

### 12.3. Reutilização sem rediscussão

Quando uma decisão aprovada contemplar o requisito:

1. Aplicar a decisão existente.
2. Reutilizar o contrato e o componente compartilhado, quando disponíveis.
3. Não criar soluções paralelas.
4. Discutir somente particularidades reais do domínio do produto.
5. Solicitar decisão humana apenas diante de lacuna, incompatibilidade ou conflito comprovado.

A DCSE-01 constitui referência para sessão e contexto empresarial, incluindo matriz, filiais, unidade ativa, alternância e escopos autorizados, respeitando seu estado técnico e as restrições de BASE-03.

### 12.4. Evolução dos padrões

Quando uma implementação revelar melhoria transversal comprovada:

- Verificar se já existe padrão correspondente.
- Atualizar a fonte oficial apropriada, conforme POL-DOC-001.
- Preservar compatibilidade e contratos aprovados.
- Registrar a evidência que fundamenta a melhoria.
- Não generalizar regras específicas de um produto sem necessidade demonstrada.

### 12.5. Eficiência

Evitar redescobrir decisões, repetir homologações, reconstruir componentes ou executar novamente testes válidos sem motivo técnico.

Aproveitar evidências anteriores quando continuarem aplicáveis.

**Princípio permanente: definir uma vez, implementar no responsável correto, validar adequadamente e reutilizar nos produtos compatíveis.**

### 12.6. Experiência e simplicidade permanentes AraIa

Aplicar, em novas funcionalidades e melhorias, a diretriz aprovada **Simplicidade Inteligente AraIa**: oferecer sistemas leves, simples, eficazes, intuitivos e completos. A tecnologia deve facilitar o trabalho, resolver a complexidade de plataforma e produzir informação na origem para organizá-la e reutilizá-la onde houver valor.

- Reduzir cliques, etapas, configurações, confirmações e textos que não ajudem o usuário; automatizar operações repetitivas quando isso trouxer benefício real. Priorizar desempenho, responsividade, clareza e baixo acoplamento, sem abstrações prematuras ou soluções paralelas.
- Manter a experiência integrada e o acesso autenticado único definidos em [DCSE-01](docs/architecture/IDENTIDADE_CENARIOS_E_DECISOES.md#dcse-01), [ARS-NAV-04](docs/standards/DEVELOPMENT_STANDARDS.md#araia-smart-navigation--ars-nav) e [Consolidação Estratégica](docs/architecture/CONSOLIDACAO_ESTRATEGICA.md#acesso-integrado-coreprodutos). O Core governa identidade, organizações, filiais, usuários/vínculos, licenciamento, perfis e permissões; produtos continuam donos dos dados e regras de domínio. Acesso funcional exige cumulativamente vínculo/membership ativo, entitlement/licença válida, perfil ativo e permissão efetiva; operações por filial também exigem contexto ativo autorizado. Direitos administrativos no Core não concedem acesso irrestrito aos produtos. Não exigir logins/portais separados como experiência normal nem compartilhar cookies ou duplicar fontes de identidade. Validar contexto e autorização no backend.
- Cada microserviço continua responsável pelos dados e regras de seu domínio. Reutilizar informação por contratos/interfaces definidos, sem duplicar cadastros, fontes de verdade, integrações ou regras de negócio no Core.
- **Dashboard Inteligente AraIa é diretriz transversal para evolução futura; este registro não autoriza sua implementação.** A tela Início é o espaço permanente e personalizável de cada usuário autenticado. O próprio usuário pode adicionar, remover, reorganizar e configurar componentes, indicadores, gráficos, informações e filtros sem autorização adicional. Oferecer uma configuração inicial útil sem exigir configuração obrigatória; aplicar automaticamente a filial ativa da sessão e os acessos existentes.
- A personalização poderá ser assistida pelo agente AraIa em linguagem natural para criar, organizar, melhorar e analisar componentes e indicadores, incluindo comparações, gráficos e filtros. O agente também poderá recomendar indicadores a partir de padrões de uso. Consumir fontes e contratos controlados, sem consultas livres aos bancos dos produtos. Usar dados reais, cálculos verificáveis e informar origem e período. Pedir confirmação antes de alterar permanentemente a organização escolhida; manter alterações reversíveis e simples de desfazer, sem exigir conhecimento técnico.
- Persistir entre acessos e dispositivos os componentes, posições, indicadores, filtros e preferências individuais; restaurar automaticamente a configuração anterior. Atualizar os dados conforme a filial ativa, sem confundir preferências com valores históricos. Preservar componentes personalizados com assistência de IA e, quando houver dados confiáveis, apresentar discretamente mudanças relevantes desde a última visita. Não exigir nova configuração nem confirmação para recuperar o espaço de trabalho.
- Implementar futuramente esta capacidade como funcionalidade compartilhada da plataforma, sem criar dashboards ou mecanismos redundantes. Cada produto permanece responsável por seus dados, regras e indicadores; o Core não concentra dados operacionais. Priorizar contratos reutilizáveis, baixo acoplamento, desempenho e simplicidade.
- Projetar e validar jornadas reais quanto a simplicidade, eficácia, desempenho, responsividade e compreensão. Relacionar as decisões de experiência aos pilares institucionais: **Origem** (visão humana), **Fluidez** (tecnologia inteligente) e **Conexão** (ecossistema integrado sem processos duplicados).

A fonte transversal detalhada é [`DEVELOPMENT_STANDARDS.md`](docs/standards/DEVELOPMENT_STANDARDS.md), especialmente os padrões de navegação/experiência compartilhada. A governança documental segue [`POL-DOC-001.md`](docs/standards/POL-DOC-001.md). Este resumo orienta agentes sem copiar o catálogo de padrões ou transformar a diretriz de dashboard em requisito de implementação imediato.

# AGENTS.md — Business Platform Core

## 1. Missão e prioridade

Desenvolver o Business Platform Core como plataforma SaaS modular, segura, testável e evolutiva. Priorizar **software funcional, evidências reproduzíveis e entregas incrementais**. Reduzir retrabalho, documentos duplicados e ciclos de revisão sem decisão.

Este arquivo orienta agentes de programação (incluindo Codex). Não substitui requisitos, ADRs aprovadas, critérios de aceite nem instruções explícitas do responsável pelo projeto. Em conflito, sinalizar a divergência antes de alterar decisões aprovadas.

## 2. Fontes de verdade

Antes de mudar comportamento ou modelo de dados, consultar **somente os documentos relevantes para a tarefa**, especialmente:

- `docs/architecture/decisions/` — ADRs e decisões aprovadas.
- `docs/architecture/` — contratos e desenho técnico aplicável.
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

## 9. Limites de autonomia

Sem autorização explícita aplicável à operação, **não**:

- Fazer commit, push, merge, publicar releases ou deploy.
- Modificar produção, dados reais, infraestrutura compartilhada ou credenciais.
- Alterar Foundation, contratos aprovados, ADRs aprovadas ou escopo de sprint.
- Aprovar gates, mudar status de aceite ou liberar UX/Sprint 1 por inferência.
- Criar ou modificar tarefas em ferramentas externas.

Uma autorização para editar e testar localmente **não** autoriza automaticamente publicar alterações.

## 10. Formato padrão de resposta do agente

Ao concluir uma tarefa, informar de forma concisa:

1. **Alterações:** arquivos e comportamento alterados.
2. **Validação:** comandos/testes executados, resultados e ambiente.
3. **Pendências:** somente riscos, bloqueios e decisões reais.
4. **Próximo passo:** uma ação objetiva, sem abrir planejamento novo desnecessário.

Se a tarefa for apenas de leitura ou diagnóstico, não alterar arquivos nem executar ações de escrita.

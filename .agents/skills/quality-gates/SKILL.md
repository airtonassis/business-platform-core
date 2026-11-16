---
name: quality-gates
description: Selecionar testes por impacto e reutilizar evidências verificáveis no Business Platform Core, distinguindo execução nova, histórico, cobertura ausente e bloqueios. Usar antes de validar um diff ou aceitar um incremento E2.
---

# Validação por impacto

Executar da raiz do repositório. `AGENTS.md` governa autorização e segurança;
`docs/architecture/SPRINT_1_PLANEJAMENTO.md` mantém baseline, pendências e plano E2.
Não disparar suites históricas nem consultar a VPS só para reconstruir relatório.

Antes de cada execução:

1. Verificar a integridade e o escopo da última evidência. Hash confere integridade,
   não autenticidade, atualidade da VPS ou aprovação. Evidência só com contagens não
   prova associação aos fontes. Não inventar um manifesto de fontes retroativo.
2. Comparar os arquivos atuais com a última baseline válida, incluindo arquivos
   não rastreados: neste checkout `git diff HEAD` sozinho omite parte do produto.
3. Mapear contrato, componentes e consumidores afetados. Registrar riscos
   transversais antes de escolher comandos. O helper sugere seleção conservadora;
   não descobre dependências semânticas nem substitui análise do diff.
4. Executar somente os comandos necessários e autorizados. Mudanças em identidade,
   autorização, contexto, persistência, dependências compartilhadas ou Foundation
   justificam regressão completa de produto e provas de segurança afetadas.
   Gate de aceite E2 exige regressão completa, integração PostgreSQL e E2E reais;
   se faltam ambiente/contrato/autorização, marcar a parte bloqueada, não aprovar gate.
5. Registrar comando exato, ambiente, resultado, escopo, justificativa e artefatos.
   Avançar baseline só depois de resolver falhas e anexar a nova evidência de validação;
   não “zerar” o diff por recaptura para dispensar testes.

Estados exclusivos por linha de evidência:

| Estado | Significado |
|---|---|
| PASS_EXECUTED | Comando/prova realmente executado nesta rodada e aprovado |
| REUSED_VALID_EVIDENCE | Integridade e aplicabilidade verificadas; execução histórica identificada |
| NOT_RUN | Não executado, com motivo e cobertura que falta |
| BLOCKED | Não executável por dependência/decisão/autorização ausente |
| FAIL | Execução ou verificação falhou; não equivale a defeito de produto sem análise |

Helper local, sem instalação, rede, execução automática de testes ou leitura de segredos:

```powershell
python -B scripts/quality_gate.py verify --summary <resumo.json> --sha256 <hash-confiavel>
python -B scripts/quality_gate.py capture --summary <resumo.json> --sha256 <hash-confiavel> --output TestResults/<rodada>/baseline.json
python -B scripts/quality_gate.py plan --baseline TestResults/<rodada>/baseline.json --output TestResults/<rodada>/selection.json
python -B scripts/quality_gate.py plan --baseline TestResults/<rodada>/baseline.json --acceptance --output TestResults/<rodada>/acceptance-selection.json
```

`capture` estabelece uma fotografia **agora**, não reconstrói proveniência histórica.
`plan` não executa os comandos sugeridos nem declara PASS. Mudança não reconhecida
exige análise manual e seleção conservadora. Não elevar PASS de fixture privilegiada
ou SQL isolado a prova de login real/fluxo completo. Timeout/commit desconhecido,
falha de auditoria ou ausência de coleta não são sucesso. Segredos não entram no recibo.

Exemplo localizado: Platform Application → testes PlatformOperations + HTTP/MFA e
arquiteturais; SQL/auth → regressão de produto + login restrito, RLS, pooling,
rollback e negativos reais autorizados. Documentação/skills/helper → testes do
helper e formato das skills, sem repetir os 860 resultados históricos de produto.
G3 offline só entra quando seus artefatos/dependências mudam; G3 real permanece proibido.

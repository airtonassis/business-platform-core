# ADR-0009 G3 — Etapa 1 offline

Suporte local de planejamento e validação de contratos. Não contém runner SQL,
conexão de banco, subprocessos, acesso de rede ou comando para executar o gate.
Nenhuma seleção autoriza execução e nenhum resultado G3 foi produzido.

## Arquivos e comandos

Executar a partir da raiz do repositório, com Python 3.11+ e somente biblioteca padrão:

```text
python -B -m unittest discover -s tests/TechnicalValidation/ADR0009.G3 -p test_offline.py -v
python -B tests/TechnicalValidation/ADR0009.G3/offline.py validate
python -B tests/TechnicalValidation/ADR0009.G3/offline.py list
python -B tests/TechnicalValidation/ADR0009.G3/offline.py select K01/accept-a-first
python -B tests/TechnicalValidation/ADR0009.G3/offline.py select K03/revoke-first K10/pool-after-revoke
python -B tests/TechnicalValidation/ADR0009.G3/offline.py evidence CAMINHO/bundle.json
```

- `variants.json`: 33 variantes planejadas sobre 18 protocolos oficiais. Os IDs com
  sufixo são identificadores locais de planejamento, não novos requisitos aprovados.
  Inclui hashes das fontes, rastreabilidade original, contratos e exclusões para G4.
- `offline.py`: leitor JSON/JSONL estrito, validação de manifesto, dependências,
  seleção explícita e contrato de evidências futuras. Só lê arquivos e escreve stdout/stderr.
- `test_offline.py`: fixtures sintéticas criadas em diretórios temporários e removidas
  ao final. Seus PASS são resultados de testes de software, nunca provas SQL/G3.

`PLANNED` significa especificada para seleção offline; não significa pronta ou
autorizada para executar. `BLOCKED` impede seleção. Dependências entre variantes
devem constar explicitamente do conjunto solicitado; não há expansão silenciosa.
A ordem de saída é lexical e estável, não uma ordem de execução concorrente.
Decisões externas pendentes são reportadas em `pendingDecisions`; nunca satisfeitas
por inferência. Códigos de saída: 0 contrato/seleção válido; 1 violação estática;
2 uso incorreto da CLI. Validação de contrato não aprova oracle nem gate.

## Contrato futuro de evidências

O modelo executável está em `validate_evidence`, sem schema paralelo divergente.
O bundle JSON deve declarar `kind=execution`, `formatVersion=1`, `runId`,
`variantId`, `sources` com hashes idênticos ao manifesto validado, `environment`,
`configuration`, `actors`, `sessions`, `transactions`, `diagnostics`, `assertions`,
`result`, `reason`, `cleanup`, `evidence` e `hashes`.

- Actors: `id`, `mode` (fixture, set-role, security-definer ou runtime-login),
  `sessionUser`, `currentUser`. Diferenciar fixture privilegiada e login real.
- Sessions: `id`, `actor`, `backendPid`; transactions: `id`, `session`, `outcome`
  (COMMITTED, ROLLED_BACK, UNKNOWN ou NOT_STARTED).
- Diagnostics: `transaction`, `sqlstate`, `message`, `constraint` (pode ser null).
  SQLSTATE isolado não demonstra o oracle; assertions devem apontar provas.
- `evidence`: tipos E-TRACE/E-DB e demais tipos exigidos pela variante, cada qual
  com lista de caminhos relativos ao diretório do bundle. Nesta versão, conteúdos
  são JSON ou JSONL; logs textuais deverão ser encapsulados em JSON sem perder
  o original nem modificar históricos. E-TRACE é JSONL ordenado com `sequence`
  contígua desde 1, `session`, `transaction`, `barrier`, `event`.
- E-DB contém `before`, `after`, `observer`; observer referencia sessão independente
  das sessões escritoras. Os valores são snapshots sanitizados, não credenciais.
- Assertions: `id`, booleano `passed`, lista `evidence` não vazia com referências
  existentes. O conteúdo semântico específico continua sujeito à revisão humana.
- `hashes`: mapa exato e completo dos arquivos declarados em evidence, SHA-256
  sobre bytes originais. O bundle não inclui seu próprio hash (evita ciclo);
  o arquivamento futuro deve fixá-lo em inventário externo conforme convenção G1/G2.
- Cleanup: `confirmed` booleano e `details`. PASS exige assertions verdadeiras,
  cleanup confirmado e nenhum resultado transacional UNKNOWN/NOT_STARTED.
  FAIL exige assertion falsa. BLOCKED não é aprovação e requer motivo explícito.

Fixtures `kind=fixture` são aceitas apenas por opção interna explícita dos testes;
a CLI de evidências as rejeita. Não existe bundle PASS persistido neste pacote.
Essas fixtures devem declarar `environment.synthetic=true`. Alterar apenas `kind`
para `execution` não as converte em evidências reais: esse tipo exige explicitamente
`environment.synthetic=false`, `configuration.synthetic=false` e nenhum ator com
`mode=fixture`. Setup privilegiado real deve ser registrado separadamente das ações
sob teste, sem ser apresentado como execução runtime.

Para `kind=execution`, `provenance` deve conter `origin=observed-run`, `runId` e
`variantId` coincidentes com o bundle, `environmentId` igual a `environment.id`
não vazio, além de `runner` e `runnerVersion` não vazios. Todos os
`executionRequirements` da variante precisam estar APPROVED no manifesto fornecido;
o validador não altera nem concede essas aprovações. O manifesto atual mantém D4,
STAGE2 e as decisões D5 específicas pendentes: não admite evidência de execução.

Essas verificações estabelecem coerência estrutural e declaratória, não autenticam
a proveniência nem comprovam que uma execução ocorreu. Metadados e hashes podem ser
fabricados em conjunto; comprovação real exige registros confiáveis da execução e
revisão independente. Sucesso do validador não aprova D4/D5/STAGE2 ou G3. Os testes
temporários que simulam metadados coerentes validam somente esse contrato.

Rejeitam-se caminhos absolutos, drives, traversal, escape por symlink, aliases de
hash, arquivos ausentes, inventários incompletos, JSON duplicado/não finito,
dependências ausentes/cíclicas e estados de execução no manifesto de planejamento.
Separadores Windows são normalizados apenas na leitura, nunca nos históricos.

## Limites e bloqueios

V11/two-owner-write-skew permanece BLOCKED: os guards existentes impedem remover
ou rebaixar Owner. Duas negativas concorrentes não demonstram write skew. K09
estrutural referencia a mesma prova, sem dupla contagem. É necessária decisão
humana de aplicabilidade/oracle ou experimento alcançável compatível com as proteções;
nenhum bypass, fluxo de sucessão ou alteração de grants está proposto aqui.

Também bloqueadas: corte de transporte com COMMIT incerto, cancelamento/reset
incerto, falha diferida de auditoria e deadlock por inversão controlada, até aprovação
dos mecanismos D5 específicos. D5 aprovado em G1 não se estende automaticamente.
Todas as demais variantes ainda dependem de autorização da Etapa 2 e D4: versões,
pool, clock, barreiras, deadlines, retries e repetições. Nenhum timeout por conveniência
foi congelado. Variantes agregadas devem ser desdobradas por ordem/escritor/fixture
no protocolo futuro antes da execução; o manifesto não comprova cobertura integral.

V14 preserva o limite do probe atual: não possui grants para auditoria/recibo;
essa parte não pode ser declarada comprovada sem prova compatível e autorização.
K06 distingue rollback do aceite vencido de materialização na substituição; seu
oracle exato deve ser congelado contra o comportamento aprovado antes de executar.

HTTP, OIDC, sessões web, recuperação Application e portas inexistentes ficam em G4,
enumerados em `exclusions`. Não há alteração de D1/D2/D3/D5, G1/G2, ADRs,
Foundation, contratos ou liberação de Sprint 1.

Para Etapa 2: revisão humana deste suporte, decisões D4/D5 aplicáveis e autorização
expressa de ambiente descartável e ensaios. Só então vincular variantes a protocolos
executáveis, com diagnóstico específico, estado final observado independentemente,
COMMIT/ROLLBACK e cleanup verificáveis. Esta etapa não cria runner nem infraestrutura.

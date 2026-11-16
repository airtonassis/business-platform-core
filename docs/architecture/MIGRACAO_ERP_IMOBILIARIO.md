# ERP Imobiliário — cobertura e impedimento da migração

Rodada local de 05/11/2026. **Verificação encerrada — DECISÃO HUMANA NECESSÁRIA.**
O responsável confirmou que o repositório conhecido é
`airtonassis/business-platform-real-estate`, checkout `erp-imobiliario-platform`,
e que não há evidência confirmada de outra implementação executável. Corrigida
a premissa da solicitação inicial: especificações e arquivos-base não constituem
uma aplicação pronta para migração. Nenhum módulo foi migrado ou validado.

Não realizar novas buscas extensivas nem iniciar construção integral do ERP sem
autorização específica. O histórico abaixo preserva as verificações já realizadas,
sem constituir novo plano ou pressupor uma implementação ausente.

## Fontes verificadas

- `../erp-imobiliario-platform`: origin
  `https://github.com/airtonassis/business-platform-real-estate.git`; HEAD local
  `2b572d77053ab5d430aff0d20c967639cb81a8f4`. `git ls-remote --heads origin`
  retornou somente main nesse mesmo SHA. Não houve fetch, checkout ou escrita
  nesse repositório. Suas alterações locais em `.ai/framework/` foram preservadas.
- Os 13 arquivos C# sob `backend/src/ERP.Framework/ERP.Framework.Domain/` estão
  vazios (zero bytes); nomes de classes não comprovam implementação reutilizável.
- Nenhum `.csproj`, `.sln`, `package.json`, `.tsx` ou `.sql` encontrado nesse
  checkout. Diretórios de módulos, frontend, mobile e testes estão vazios.
  `database/DATABASE_MODEL.md` e README raiz também estão vazios.
- `../erp-imobiliario-enterprise-architecture`: origin
  `https://github.com/airtonassis/business-platform-architecture.git`; documentação
  de domínios e jornadas. Declarações como `POST /properties` em
  `modules/properties/README.md` não são endpoints implementados.
- `C:/Temp/backup_airton/temp/Projetos/ia/imobiliaria`: um DOCX de documentação e
  um TXT vazio; nenhum projeto ou implementação. Consultados também os nomes
  de arquivos em `source`, Downloads e no diretório de projetos `ia`.
- GitHub: buscas por repositórios do proprietário com `real estate`, `imobiliario`
  e `ERP`; a primeira encontrou `business-platform-real-estate`, as outras não
  retornaram resultados. Isso não prova inexistência de fonte em outra conta,
  branch não publicada, arquivo compactado ou localização não consultada.
- Monday: itens existentes ERP Imobiliário `13428486009` e M6 `13428486582`;
  sem atualizações retornadas na consulta inicial. Não foram criados itens.
  Impedimento registrado posteriormente nas atualizações `5699836643` e
  `5699836548`, respectivamente, sem alterar status de aceite.

## Mapa único de cobertura

Universo abaixo: todos os 24 diretórios de `modules/` do repositório de arquitetura.
É cobertura documental identificada, não catálogo comercial nem inventário de
funcionalidades executáveis. Cada linha está **pendente**, pois não foi encontrada
implementação de negócio correspondente. Não houve descarte de funcionalidades.

| Módulo documental | Classificação | Justificativa / destino quando houver fonte |
|---|---|---|
| ai | Pendente | Código e capacidades reais ausentes; não publicar recursos presumidos. |
| analytics | Pendente | Relatórios e cálculos executáveis não localizados. |
| authentication | Pendente | Adaptar autenticação original ao AraIa/Keycloak existente. |
| authorization | Pendente | Adaptar regras originais à interseção de direitos do Core. |
| brokers | Pendente | Cadastro e regras de corretores não localizados. |
| communication | Pendente | Serviços e integrações operacionais não localizados. |
| contracts | Pendente | Contratos imobiliários, documentos e regras não localizados. |
| core | Pendente | Separar domínio imobiliário das responsabilidades já existentes no Core. |
| crm | Pendente | Jornadas, entidades e validações não localizadas. |
| customers | Pendente | Cadastro e regras de clientes não localizados. |
| dashboard | Pendente | Telas e consultas reais não localizadas. |
| documents | Pendente | Geração, armazenamento e acesso não localizados. |
| financial | Pendente | Regras e rotinas financeiras não localizadas. |
| integrations | Pendente | Contratos executáveis e consumidores não localizados. |
| marketing | Pendente | Fluxos implementados não localizados. |
| notifications | Pendente | Serviços e canais implementados não localizados. |
| owners | Pendente | Cadastro e regras de proprietários não localizados. |
| payments | Pendente | Rotinas e integrações de pagamento não localizadas. |
| portal | Pendente | Interface e implementação não localizadas; README vazio. |
| properties | Pendente | Imóveis e APIs apenas documentados; candidato à primeira jornada após localizar código. |
| rentals | Pendente | Locações e regras não localizadas. |
| reports | Pendente | Relatórios executáveis não localizados. |
| sales | Pendente | Vendas e regras não localizadas. |
| workflow | Pendente | Rotinas e transições executáveis não localizadas. |

| Elemento transversal | Classificação | Evidência / limite |
|---|---|---|
| Framework ERP (13 arquivos C#) | Pendente | Arquivos vazios; nenhuma regra a migrar demonstrada. |
| Telas e jornadas desktop/mobile | Pendente | Frontend vazio; não criar telas fictícias. |
| Entidades, validações e persistência | Pendente | Modelo vazio e ausência de projetos/migrations. |
| APIs, relatórios e rotinas | Pendente | Descrições arquiteturais não demonstram execução. |
| Testes de equivalência | Pendente | Nenhum teste original localizado. |
| Licenciamento/perfis do Core | Existente; adaptação pendente | Localizado no worktree S2.1 UI, não neste checkout raiz. |
| Identidade visual AraIa v2 | Existente; adaptação pendente | Protótipo e componentes locais identificados; nenhuma tela ERP disponível para adaptar. |

## Integrações existentes preservadas

`../business-platform-core-s2-1-ui/src/Business.Platform.Core.Application/Tenancy/BusinessAccess.cs`
contém `BusinessAccess`, `IBusinessAccessStore`, `LicensedProduct`, perfis,
atribuições e consulta de grants efetivos. A implementação PostgreSQL está em
`Infrastructure/Persistence/PostgresBusinessAccess.cs`, com migrations 013 e 014.
Esses arquivos foram localizados/lidos parcialmente, não copiados, modificados,
aplicados a banco ou certificados nesta rodada.

O protótipo em `../business-platform-core-visual-prototype/dist/` disponibiliza
tokens e estilos; seu README mantém uma indicação histórica de aceite pendente.
A solicitação atual declara AraIa v2 homologado em 05/11/2026 e é a diretriz desta
execução. Não foi necessário reabrir a decisão visual ou modificar o protótipo.

Sem implementação original, não há recursos de ERP comprovados para publicar
em catálogo. Nenhum produto, licença, contratação, permissão ou dado fictício foi
criado. Fonte comercial autorizada e situação contratada não foram verificadas;
isso não equivale a afirmar que inexistem.

## Validação e pendências

- **PASS_EXECUTED (inventário somente):** listagem integral dos arquivos do ERP,
  tamanhos dos 13 C#, busca de manifestos/telas/SQL, `git ls-tree -r --name-only HEAD`,
  `git rev-parse HEAD`, `git ls-remote --heads origin`, consulta de origens,
  diretórios documentais, GitHub e atualizações dos dois itens existentes no Monday.
  Ambiente: Windows/PowerShell local; consultas remotas somente leitura.
- **NOT_RUN:** regressões .NET/PostgreSQL/Keycloak, provas de licenciamento,
  revogação e isolamento, equivalência funcional e capturas desktop/mobile.
  Nenhum comportamento foi alterado e nenhuma aplicação ERP executável foi localizada.
- **NOT_RUN:** gates bloqueados ADR-0009; produção/VPS, migrações e deploy não acessados.
- **DECISÃO HUMANA NECESSÁRIA:** definir e autorizar especificamente eventual
  desenvolvimento futuro do ERP. O esclarecimento do responsável encerra a busca
  por uma implementação não comprovada e não autoriza construção integral.

Coleta executada: `python -B scripts/inventory_erp_migration.py`, saída em
`TestResults/erp-migration-inventory/inventory.json`, em
`2026-11-05T21:17:28.130524+00:00`; duração medida somente dessa coleta: 0,2527 s.
Resultado: 14 arquivos nos cinco diretórios de código/dados consultados, todos
vazios (13 C# e um Markdown); 24 módulos documentais; nenhum manifesto/tela/SQL
dos tipos pesquisados. O JSON contém SHA-256 por arquivo. `git diff --check`
retornou código 0 para alterações rastreadas; arquivos novos foram conferidos
separadamente. Nenhuma evidência histórica de teste foi promovida a execução nova.

Componentes realmente aproveitáveis: documentação como insumo de requisitos,
sujeita à confirmação de aplicabilidade; licenciamento, catálogo, perfis,
atribuições e autorização efetiva existentes no Core; tokens, estilos e componentes
AraIa identificados. Os arquivos C# vazios não oferecem comportamento reutilizável.
Não foi demonstrada funcionalidade imobiliária executável. Código e documentação
foram preservados integralmente; o padrão AraIa v2 aprovado permanece obrigatório.

Próximo passo objetivo: aguardar decisão e autorização específica do responsável
sobre eventual desenvolvimento. Nenhum novo ciclo de planejamento, busca ou
implementação foi iniciado após o esclarecimento.

Tempo: não houve cronômetro contínuo desde o recebimento; não se atribui duração
precisa retrospectiva. A coleta reproduzível em `TestResults/erp-migration-inventory/`
registra seu próprio instante UTC e duração, que não equivalem à duração da tarefa.

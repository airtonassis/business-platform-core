# TimeProvider Foundation — Testes

## Matriz

| ID | Teste | Resultado esperado |
|---|---|---|
| TIME-UT-01 | `Create_WithInstantFromFakeClock_ShouldSucceedWithExactUtcInstant` | Sucesso, instante exato, offset zero |
| TIME-UT-02 | `WithUpdate_AfterClockAdvance_ShouldSucceedAndPreserveCreatedAt` | Sucesso, `CreatedAt` preservado |
| TIME-UT-03 | `Create_WithNonConformingFakeOffset_ShouldFailWithNonUtcOffset` | `Audit.CreatedAt.NonUtcOffset` |
| TIME-UT-04 | `Create_WithDefaultInstantFromFake_ShouldFailWithNotSet` | `Audit.CreatedAt.NotSet` |
| TIME-UT-05 | `WithUpdate_AfterClockRewindBeforeCreation_ShouldFailWithBeforeCreation` e `WithUpdate_AfterClockRewindBeforeLastUpdate_ShouldFailWithBeforeLastUpdate` | `Audit.UpdatedAt.BeforeCreation` / `Audit.UpdatedAt.BeforeLastUpdate` |
| TIME-UT-06 | `FakeClock_RepeatedReads_ShouldBeDeterministicAndYieldEqualMetadata` | Leituras iguais; metadata equivalente igual |
| TIME-ARCH-01 | `DomainAssembly_ShouldNotExposeTimeProviderInAnySignature` e `DomainAssembly_ShouldNotDeclareTypesDerivedFromTimeProvider` | Nenhuma violação |

TIME-ARCH-02: sem snapshot global; contratos anteriores seguem cobertos pelos testes arquiteturais existentes. A ausência de novos tipos de produção é comprovada pelo manifesto fechado e pelo diff Git.

## Resultados reais das validações (execução local)

| Comando | Exit code | Resultado |
|---|---|---|
| `dotnet restore` | 0 | Projetos atualizados |
| `dotnet build -c Release -warnaserror` | 0 | 0 avisos, 0 erros |
| `dotnet test` UnitTests | 0 | 492 aprovados, 0 falhas |
| `dotnet test` ArchitectureTests | 0 | 200 aprovados, 0 falhas |
| `dotnet format --verify-no-changes` | 0 | Sem alterações necessárias |

Total: 692 testes (683 anteriores + 9 novos), todos aprovados.

## Limitações de reflexão

TIME-ARCH-01 inspeciona campos, propriedades, construtores, métodos (retorno e parâmetros), incluindo tipos não públicos e aninhados, arrays e genéricos. **Não detecta chamadas ao relógio dentro de corpos de métodos** nem uso em variáveis locais.

## Mutation testing

N/A — No New Production Code (Stryker não executado).

## Remediação A-01, A-02, A-03

Auditoria independente original: PASS WITH NON-BLOCKING FINDINGS. **Reauditoria independente necessária.**

- **A-01 corrigido:** composition root/DI futuros documentados (não implementados) em API.md e USE_CASES.md.
- **A-02 corrigido:** TIME-ARCH-01 passou a inspecionar também BaseType, interfaces implementadas, restrições de parâmetros genéricos (de tipos e métodos), arrays/tipos com elemento, argumentos genéricos e tipos aninhados, com controle de tipos visitados contra recursão. Os dois testes arquiteturais foram preservados.
- **A-03 corrigido:** TIME-UT-02 agora verifica `CreatedBy` e `CreatedAt` preservados, nova instância retornada e original sem `UpdatedBy`/`UpdatedAt`.

Limitações residuais: reflexão de metadados não detecta todas as chamadas ao relógio dentro dos corpos de métodos; a restrição em parâmetros genéricos é verificada por tipo declarado, sem análise de IL.

Validações executadas após a remediação (todas com exit code 0): restore OK; build Release `-warnaserror` com 0 avisos e 0 erros; UnitTests 492 aprovados, 0 falhas; ArchitectureTests 200 aprovados, 0 falhas; `dotnet format --verify-no-changes` sem alterações. Final QA, commit e push permanecem pendentes.

# TimeProvider Foundation — Implementação

## Manifesto (9 arquivos, todos novos)

Testes:
1. `tests/UnitTests/Domain/Shared/Time/TimeProviderCompatibilityTests.cs`
2. `tests/ArchitectureTests/Domain/Shared/Time/TimeProviderArchitectureTests.cs`

Documentação (`docs/foundation/TimeProvider/`):
3. `README.md`
4. `API.md`
5. `BUSINESS_RULES.md`
6. `USE_CASES.md`
7. `IMPLEMENTATION.md`
8. `TESTS.md`
9. `CHECKLIST.md`

## Ausência de código de produção

A convenção adota API nativa (D-TIME-01/02/03); não há necessidade de abstração. Nenhum arquivo existente, `.csproj` ou arquivo de propriedades foi alterado.

## Estratégia de testes

- Unitários: `FakeTimeProvider` privado e aninhado, sobrescrevendo `GetUtcNow()`, com avanço/retrocesso manual; sem `Thread.Sleep`, timers, delays nem leitura do relógio real.
- Arquiteturais: reflexão sobre o assembly Domain para garantir ausência de `TimeProvider` em assinaturas.

## Dependências preservadas

Nenhum pacote NuGet adicionado (D-TIME-09); Auditing e DomainEvent intocados.

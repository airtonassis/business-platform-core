# TimeProvider Foundation — Casos de Uso

> Os casos 1–3 descrevem uso futuro da Application e estão **NÃO IMPLEMENTADOS** (Application ainda não funcional). O caso 4 está implementado nos testes.

1. **Obtenção de instante UTC pela futura Application** — injetar `TimeProvider` e chamar `GetUtcNow()`. *Não implementado.*
2. **Criação de metadata** — `AuditMetadata.Create(actor, timeProvider.GetUtcNow())`. *Não implementado (exercitado em teste com relógio falso).*
3. **Atualização de metadata** — `metadata.WithUpdate(actor, timeProvider.GetUtcNow())`. *Não implementado (exercitado em teste).*
4. **Cenários de teste com relógio controlado** — `TimeProviderCompatibilityTests`: criação, avanço, offset inválido, instante não definido, regressão temporal, igualdade.

## Fluxo futuro com composition root (NÃO IMPLEMENTADO)

Quando Application e composition root forem funcionais:

1. A composition root disponibiliza `TimeProvider.System` em produção.
2. A Application recebe o `TimeProvider` por injeção de dependência.
3. A Application obtém o instante via `GetUtcNow()`.
4. O instante é passado ao Domain como `DateTimeOffset` (ex.: `AuditMetadata.Create`/`WithUpdate`).
5. Testes substituem o provedor por uma implementação controlada.

Nenhuma DI, Application ou Infrastructure foi implementada nesta feature.

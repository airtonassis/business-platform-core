# TimeProvider Foundation — Regras de Negócio

1. **UTC obrigatório para Auditing** (D-TIME-06): instantes com offset diferente de zero são rejeitados por `AuditMetadata`.
2. **Determinismo**: em testes, o instante vem de um relógio falso próprio, sem consultar o relógio real (D-TIME-10).
3. **Responsabilidade do chamador**: o chamador (futura Application) obtém o instante e o informa ao Domain; o Domain não lê o relógio.
4. **Regressão temporal rejeitada** (D-TIME-08): `Audit.UpdatedAt.BeforeCreation` e `Audit.UpdatedAt.BeforeLastUpdate`.
5. **Auditing preservado** (D-TIME-05): nenhuma alteração em `AuditMetadata`/`AuditActor`.
6. **Exclusões** (D-TIME-07): timezone, timers e delays. Sem pacotes NuGet novos (D-TIME-09).
7. Mudanças em Foundations encerradas são tratadas separadamente (D-TIME-12).

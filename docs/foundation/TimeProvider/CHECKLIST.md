# TimeProvider Foundation — Checklist

## Decisões

| ID | Decisão | Evidência |
|---|---|---|
| D-TIME-01 | Usar `System.TimeProvider` | Testes usam subclasse de `TimeProvider` |
| D-TIME-02 | Sem `IClock`/`ITimeProvider` | Nenhum tipo novo; manifesto fechado |
| D-TIME-03 | Sem contratos públicos no Domain | TIME-ARCH-01; diff Git |
| D-TIME-04 | Documentar uso na futura Application | API.md, USE_CASES.md |
| D-TIME-05 | Preservar Auditing | Diff Git sem alterações |
| D-TIME-06 | UTC | TIME-UT-01, TIME-UT-03 |
| D-TIME-07 | Excluir timezone/timers/delays | Testes sem timers; BUSINESS_RULES.md |
| D-TIME-08 | Rejeitar regressão temporal | TIME-UT-05 |
| D-TIME-09 | Sem pacotes NuGet | Nenhum `.csproj` alterado |
| D-TIME-10 | Relógio falso próprio | `FakeTimeProvider` aninhado |
| D-TIME-11 | Documentação e testes | 9 arquivos |
| D-TIME-12 | Foundations encerradas tratadas à parte | Nenhuma alterada |

## Critérios de aceitação

- [x] 9 arquivos criados, nenhum outro modificado (verificado por `git status`/`git diff`)
- [x] Restore, build Release `-warnaserror`, testes (692) e `dotnet format` aprovados localmente
- [x] Mutation testing N/A — No New Production Code

## Pendências

- [ ] Auditoria independente
- [ ] Final QA
- [ ] Commit
- [ ] Push

## Estado real

Implementação local concluída; **não** auditada, **não** commitada, **não** encerrada.

# TimeProvider Foundation (FEATURE-0011)

## Objetivo

Estabelecer a convenção arquitetural para obtenção determinística de instantes UTC usando o `System.TimeProvider` nativo do .NET 10. A entrega é composta **somente por documentação e testes**; não há código de produção novo.

## Justificativa

- `System.TimeProvider` já oferece abstração testável do relógio (`GetUtcNow()`), sem pacotes adicionais.
- Criar `IClock`/`ITimeProvider` próprio duplicaria a plataforma e criaria um contrato a manter.

## Ausência de contrato próprio

Nenhum `IClock`, `ITimeProvider`, wrapper ou tipo novo é criado. Nenhum contrato público é adicionado ao Domain.

## Limites arquiteturais

- O Domain **não** referencia `TimeProvider` em suas assinaturas; os instantes são informados pelo chamador.
- A obtenção do instante pertence à futura camada Application (ainda não funcional).
- Fora do escopo: timezone, timers, delays, agendamento.

## Relação com Auditing

`AuditMetadata` (FEATURE-0010) permanece inalterado: recebe `DateTimeOffset` em UTC e valida offset zero, instante definido e ausência de regressão temporal. Os testes desta feature demonstram a compatibilidade com um relógio falso.

## Rastreabilidade

D-TIME-01 a D-TIME-12: ver [CHECKLIST.md](CHECKLIST.md).

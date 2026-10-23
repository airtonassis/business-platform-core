# TimeProvider Foundation — API

## API nativa adotada

`System.TimeProvider` (.NET 10). Membro utilizado: `DateTimeOffset GetUtcNow()` (D-TIME-01, D-TIME-06).

## Nenhuma nova API pública

Nenhum tipo, interface ou membro público foi criado no Domain (D-TIME-02, D-TIME-03).

## Exemplo ilustrativo — NÃO IMPLEMENTADO

A Application ainda não está funcional; o trecho abaixo é apenas ilustrativo do consumo futuro (D-TIME-04):

```csharp
// NÃO IMPLEMENTADO — ilustrativo
public sealed class CreateSomethingHandler(TimeProvider timeProvider)
{
    public Result<AuditMetadata> Handle(AuditActor actor)
    {
        var nowUtc = timeProvider.GetUtcNow();
        return AuditMetadata.Create(actor, nowUtc);
    }
}
```

O Domain valida o offset de qualquer instante recebido (`Audit.CreatedAt.NonUtcOffset`).

## Composition root e injeção (futuro — NÃO IMPLEMENTADO)

Não existem hoje Application, composition root nem DI funcionais; nada abaixo está implementado. Quando existirem:

- A composition root será responsável por disponibilizar `TimeProvider.System` em produção.
- A Application receberá o provedor por injeção de dependência.
- A Application obterá o instante via `GetUtcNow()`.
- O instante será passado ao Domain como `DateTimeOffset`.
- Os testes poderão substituir o provedor por uma implementação controlada.

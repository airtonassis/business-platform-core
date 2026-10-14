# Componente: DomainEvent

O `IDomainEvent` é um componente fundamental da Shared Foundation responsável por representar eventos de domínio — registros imutáveis de fatos significativos que ocorreram no domínio.

## Propósito Arquitetural

Em Domain-Driven Design, eventos de domínio capturam **o quê aconteceu** no domínio: "um pedido foi criado", "um pagamento foi recebido", "um cliente se inscreveu". Eventos são:

- **Fatos imutáveis:** Uma vez ocorridos, não podem mudar
- **Identificáveis:** `EventId` identifica a ocorrência do evento e `OccurredOnUtc` registra o instante informado. Timestamps não são únicos (dois eventos podem ocorrer no mesmo instante), e a interface não garante unicidade global de `EventId`, deduplicação nem idempotência
- **Independentes:** Não dependem de infraestrutura, handlers ou publicadores
- **Semanticamente ricos:** Contêm dados específicos do domínio

O componente `IDomainEvent` fornece:

- Um contrato mínimo para eventos de domínio
- Garantia de identidade (EventId) e temporalidade (OccurredOnUtc)
- Flexibilidade para que eventos concretos definam semântica própria
- Independência completa de infraestrutura

## Responsabilidades

`IDomainEvent` deve:

- Representar um evento que já ocorreu no domínio, com contrato somente leitura
- Expor o identificador da ocorrência (`EventId: Guid`); a unicidade depende de quem constrói o evento
- Encapsular momento de ocorrência (`OccurredOnUtc: DateTimeOffset`)
- Permitir que implementações concretas adicionem dados específicos
- Ser compatível com reflexão e testes
- Não impor dependência de infraestrutura, handlers, bus ou persistência

## Estrutura Conceitual

Um evento de domínio é composto por:

- **EventId** (identificador da ocorrência — `Guid`)
- **OccurredOnUtc** (momento de ocorrência em UTC — `DateTimeOffset`)
- **Dados específicos do evento** (conteúdo semântico, incluindo identidade de agregado se necessário)
- **Estabilidade** (a interface expõe apenas getters, o que impede alterar as duas propriedades pelo contrato; isso não é imutabilidade profunda, pois eventos concretos podem expor setters ou dados mutáveis adicionais)

Exemplo conceitual (sem implementação):

```
CustomerCreated : IDomainEvent
├── EventId: Guid (obrigatório, gerado na criação)
├── OccurredOnUtc: DateTimeOffset (obrigatório, sempre UTC)
├── CustomerId: Guid (propriedade específica — não na interface)
├── CustomerName: string (propriedade específica)
├── CustomerEmail: string (propriedade específica)
└── Somente getters no contrato; a mutabilidade dos dados adicionais é decisão do evento concreto
```

**Nota:** `AggregateRootId` não é parte do contrato universal. Eventos concretos podem incluir identificadores de agregado quando semanticamente necessário.

## Separação de Responsabilidades

Domain Events são **independentes** de:

- **Integration Events** — publicação, serialização, schema
- **Handlers** — lógica reativa, MediatR
- **Ciclo de vida** — coleta, limpeza, confirmação de eventos
- **Infraestrutura** — Entity Framework, bus, Outbox, transações
- **AggregateRoot<TId>** — modificações não autorizadas

Esses componentes pertencem a features futuras ou camadas de infraestrutura.

## Namespace

```csharp
Business.Platform.Core.Domain.Shared
```

Localização:

```
src/Business.Platform.Core.Domain/Shared/DomainEvents/IDomainEvent.cs
```

## Decisões Arquiteturais (D01–D08)

Veja `BUSINESS_RULES.md` para análise completa.

- **D01:** Contrato com `EventId` e `OccurredOnUtc` (aprovado)
- **D02:** `EventId` obrigatório, `Guid` (aprovado)
- **D03:** `OccurredOnUtc` obrigatório, `DateTimeOffset`, offset UTC (aprovado)
- **D04:** Eventos como registros estáveis de fatos (aprovado)
- **D05:** AggregateRoot<TId> preservado integralmente (aprovado)
- **D06:** Ciclo de vida de eventos fora do escopo (aprovado)
- **D07:** Independência de infraestrutura (aprovado)
- **D08:** Testes do contrato aprovado (implementados; ver `TESTS.md`)

## Compatibilidade Confirmada

- ✅ Nenhuma modificação a `Entity<TId>`
- ✅ Nenhuma modificação a `AggregateRoot<TId>`
- ✅ Nenhuma modificação a `Error` ou `Result`
- ✅ Independência completa de infraestrutura
- ✅ Compatibilidade com FEATURE-0003, FEATURE-0004

## Fora do Escopo

Esta feature **não inclui**:

- Classe-base de evento ou qualquer implementação além do contrato `IDomainEvent`
- Ciclo de vida (coleta, limpeza, confirmação de eventos)
- Handlers ou dispatchers
- Publicação ou bus de mensagem
- Entity Framework ou persistência
- Serialização
- Outbox pattern
- Integration Events
- AggregateRoot com suporte a eventos
- Saga ou orquestração

Essas responsabilidades pertencem a features futuras ou infraestrutura.

## Relação com Componentes Existentes

- **Entity<TId>:** Independente. Entidades não herdam comportamentos de eventos.
- **AggregateRoot<TId>:** Independente. Preservado integralmente.
- **Error, Result:** Ortogonal. Eventos e retornos de operações são conceitos distintos.
- **Domain Services:** Podem orquestrar agregados que levantam eventos.

## Próximos Passos

1. Auditoria independente do Codex.
2. Versionamento somente após a auditoria.
3. Gerenciamento de eventos pendentes, publicação e demais componentes dependem de decisões e features futuras, ainda sem numeração aprovada.

---

## Notas de Versão

**Versão:** 1.0  
**Status:** Implementado — aguardando auditoria independente do Codex  
**Namespace:** `Business.Platform.Core.Domain.Shared`  
**Compatibilidade:** .NET 10  
**Modificações a Componentes Existentes:** Nenhuma

# Componente: AggregateRoot

O `AggregateRoot<TId>` é um componente fundamental da Shared Foundation responsável por representar a raiz de um agregado em Domain-Driven Design, estabelecendo contratos para consistência, isolamento e ciclo de vida agregado.

## Propósito Arquitetural

Em Domain-Driven Design, um agregado é um cluster de entidades relacionadas tratadas como uma unidade coesa. A raiz do agregado (AggregateRoot) é o ponto de acesso externo ao agregado e responsável por:

- Manter a consistência e as invariantes internas do agregado;
- Controlar o ciclo de vida das entidades filhas;
- Validar transições de estado internas;
- Ser a unidade de atomicidade em persistência.

O componente `AggregateRoot<TId>` fornece:

- Uma abstração que herda a identidade, a igualdade e o hash de `Entity<TId>`;
- Uma base que permite a agregação de entidades sem impor eventos ou persistência;
- Compatibilidade total com testes unitários e arquiteturais;
- Independência de infraestrutura (sem eventos, persistência, auditoria ou multitenancy nesta feature).

## Responsabilidades

O `AggregateRoot<TId>` deve:

- herdar de `Entity<TId>` com toda a semântica de identidade, igualdade e hash;
- ser uma classe abstrata que permite herança por entidades concretas que formam agregados;
- oferecer um construtor protegido que aceita `TId id` e o passa a `base(id)`;
- permitir que subclasses concretas definam e encapsulem suas entidades filhas, sem impor um padrão de encapsulamento;
- não impor eventos de domínio, persistência, auditoria, ou tenant;
- preservar a propriedade `Id` sem setter, herdada de `Entity<TId>`;
- não redefinir operadores de igualdade (herda `==`, `!=`, `Equals`, `GetHashCode` de `Entity<TId>`).

## Estrutura Conceitual

Um agregado é composto por:

- **AggregateRoot:** a raiz, uma entidade especial que é o ponto de acesso ao agregado;
- **Entidades filhas:** entidades que só existem no contexto do agregado (hereditariamente `Entity<TId>`);
- **Value Objects:** objetos de valor que representam conceitos do domínio sem identidade própria;
- **Invariantes:** regras que devem ser sempre verdadeiras dentro do agregado;
- **Comportamento:** métodos que modificam o estado do agregado atomicamente.

```
AggregateRoot (ex: Order)
├── OrderLine[] (entidades filhas)
├── Address (value object)
└── Customer (referência por ID, não agregação)
```

## Tipo Genérico TId

Como `Entity<TId>`, `AggregateRoot<TId>` é genérico sobre o tipo de identificador:

```csharp
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    protected AggregateRoot(TId id) : base(id)
    {
    }
}
```

Exemplos de tipos válidos para `TId`:

- `int`, `long`, `Guid`, `string`
- Value Objects tipados (ex: `OrderId`, `CustomerId`)
- Identificadores que respeitem estabilidade de igualdade durante a vida do agregado

## Identidade e Igualdade Herdadas

`AggregateRoot<TId>` herda completamente:

- A propriedade `Id`, sem setter
- Igualdade baseada em tipo concreto e `Id`
- `GetHashCode()` consistente
- Operadores `==` e `!=`

Nenhuma delas é redefinida ou personalizada.

## Agregação de Entidades

Entidades filhas pertencem ao agregado e:

- herdam de `Entity<TId>` ou possuem identidade própria;
- são definidas e encapsuladas pelo agregado concreto (via propriedades read-only, métodos, ou nenhuma exposição);
- devem respeitar invariantes do agregado;
- são coordenadas por métodos da raiz (ex: `AddLine`, `RemoveLine`).

Exemplo (com exposição):

```csharp
public sealed class Order : AggregateRoot<Guid>
{
    private readonly List<OrderLine> _lines = new();

    public Order(Guid id) : base(id)
    {
    }

    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    public void AddLine(OrderLine line) => _lines.Add(line);
}

public sealed class OrderLine : Entity<Guid>
{
    public OrderLine(Guid id, int quantity) : base(id) { }
}
```

Exemplo (sem exposição):

```csharp
public sealed class Order : AggregateRoot<Guid>
{
    private readonly List<OrderLine> _lines = new();

    public Order(Guid id) : base(id)
    {
    }

    // Lines não é exposto; acesso apenas via métodos
    public int LineCount => _lines.Count;

    public void AddLine(OrderLine line) => _lines.Add(line);
}
```

**Nota:** `AggregateRoot<TId>` não impõe padrão de encapsulamento. Agregados concretos decidem. Um `IReadOnlyList<T>` isolado apenas oculta os métodos de modificação da lista; não torna imutáveis a coleção subjacente nem as entidades contidas.

## Invariantes Mínimos

`AggregateRoot<TId>` não impõe invariantes além dos herdados de `Entity<TId>`:

- `Id` não nulo
- `Id` sem setter
- Igualdade por tipo concreto e `Id`

Invariantes de negócio específicos do agregado (ex: "quantidade mínima de linhas") pertencem à subclasse concreta.

## Sem Eventos, Persistência ou Auditoria

Esta feature **não inclui**:

- Eventos de domínio (`IDomainEvent`, coleta de eventos)
- Persistência (repository pattern, especificação, mapeamento ORM)
- Auditoria (timestamps, user tracking, soft delete)
- Multi-tenancy (`TenantId`, isolamento)
- Serialização específica

Essas responsabilidades pertencem a features futuras ou infraestrutura.

## Validação de Invariantes

- `AggregateRoot<TId>` não valida genericamente;
- Subclasses concretas implementam validações específicas de domínio;
- Métodos públicos da raiz do agregado devem garantir invariantes antes de modificar estado;
- Falhas esperadas podem retornar `Result.Failure(...)` (recomendado) ou lançar exceção;
- Sem `DomainException` nesta feature (responsabilidade de agregados concretos).

## Igualdade de Agregados

Dois agregados (de mesmo tipo concreto) com o mesmo `Id` são iguais, independentemente de seu estado interno.

Isto habilita:

- Agregados em `HashSet`, `Dictionary` baseados em identidade;
- Comparação de agregados sem inspeção de estado;
- Separação entre igualdade e estado.

---

## Próximos Passos

Após aprovação desta FEATURE-0004:

- FEATURE-0005: Domain Events (publicação e manipulação dentro do agregado)
- FEATURE-0006: Value Objects (abstrações sem identidade)
- Implementações concretas de agregados específicos do domínio

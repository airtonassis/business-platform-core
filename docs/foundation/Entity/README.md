# Componente: Entity

O `Entity<TId>` é um componente fundamental da Shared Foundation responsável por representar abstrações de identidade em objetos de domínio, estabelecendo contratos para igualdade, imutabilidade e relacionamentos.

## Propósito Arquitetural

Em uma arquitetura baseada em Domain-Driven Design, uma entidade é um objeto definido primariamente por sua **identidade única** e não por seus atributos. Uma entidade permanece a mesma entidade mesmo quando seus atributos mudam — a identidade é o que a define.

O componente `Entity<TId>` fornece:

- Uma abstração genérica para tipos de identidade de domínio;
- Um contrato claro de igualdade baseado em tipo concreto e identidade;
- Uma base imutável que respeita princípios de DDD sem comprometer flexibilidade de herança;
- Independência de infraestrutura (sem EF Core, persistência ou auditoria).

## Responsabilidades

O `Entity<TId>` deve:

- encapsular um identificador genérico `Id` do tipo `TId`;
- garantir que `Id` é obrigatório no momento da construção;
- impedir substituição ou alteração da identidade após construção;
- definir igualdade com base no tipo concreto e no `Id`;
- fornecer sobrecarga explícita de `Equals`, `GetHashCode`, `==` e `!=`;
- ser uma classe abstrata que permite herança controlada;
- não impor relacionamentos, timestamps, tenant, ou responsabilidades de AggregateRoot;
- ser thread-safe e imutável após construção.

## Estrutura Conceitual

Uma entidade é composta inicialmente por:

- `Id`: identificador genérico (tipo `TId`), obrigatório e imutável;
- Outras propriedades de domínio específicas do tipo concreto;
- Relacionamentos definidos na entidade concreta, não na base;
- Comportamento de domínio específico do tipo concreto.

## Tipo Genérico TId

A abstração é genérica sobre o tipo de identificador (`TId`):

```csharp
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    // Id do tipo TId
}
```

A restrição `where TId : notnull` oferece verificações de nulidade em compilação, garantindo que apenas tipos não-nuláveis possam ser utilizados como identificadores. Validação de nulidade em runtime permanece obrigatória no construtor.

Exemplos de tipos válidos para `TId`:

- `int`, `long`, `Guid`, `string`
- Value Objects que implementem `IEquatable<T>` com semântica apropriada de igualdade e hashing
- Tipos personalizados que respeitem estabilidade de igualdade durante a vida da entidade

## Igualdade

Duas entidades são consideradas iguais **se e somente se**:

1. São do mesmo tipo concreto;
2. Possuem o mesmo `Id`.

Nenhum outro atributo participa da igualdade. Isto implica:

- Duas instâncias diferentes do mesmo tipo com o mesmo `Id` são iguais;
- Dois tipos concretos diferentes nunca são iguais, mesmo com `Id` idênticos;
- Relacionamentos não participam da igualdade;
- Mudanças em outros atributos não alteram a igualdade.

## Validação de Identidade

- Identidade nula (`id == null`) é inválida e deve ser rejeitada na construção;
- Valores como `Guid.Empty`, `0`, `""` são **não-nulos** e semanticamente válidos — sua validade pertence ao domínio ou ao tipo de identificador específico, não à abstração `Entity<TId>`;
- Não existe um `EntityId` global — cada bounded context define seus próprios identificadores.

## Relacionamentos

Relacionamentos **não pertencem** à classe-base `Entity<TId>`:

- `TenantId`, `OrganizationId`, `ParentId`, `OwnerId` são definidos em entidades concretas;
- Propriedades de navegação são definidas em entidades concretas;
- Relacionamentos não participam da igualdade;
- A carga de relacionamentos (lazy, eager) é responsabilidade da infraestrutura.

## Imutabilidade e Thread-Safety

`Entity<TId>` é imutável após construção no nível da propriedade `Id`:

- `Id` não possui setter público ou protegido.
- Nenhuma propriedade da classe-base muda após construção.
- Leitura simultânea é segura (não há estado mutável).

Para tipos `TId` que são referências (classes, Value Objects), a estabilidade interna da igualdade e do hashing é responsabilidade do tipo de identificador, não da Foundation.

## Fora do Escopo

A primeira versão de `Entity<TId>` não será responsável por:

- persistência ou mapeamento ORM;
- controle de concorrência ou versionamento;
- auditoria (Who, When, Why);
- timestamps (CreatedAt, UpdatedAt);
- eventos de domínio;
- Tenant e Organization (contexto específico);
- Repository e Specification;
- responsabilidades de AggregateRoot (invariantes de agregado);
- soft delete;
- qualquer dependência de infraestrutura.

Essas responsabilidades pertencem a outras camadas ou componentes especializados.

## Namespace

```csharp
Business.Platform.Core.Domain.Shared
```

Caminho de implementação futuro:

```
src/Business.Platform.Core.Domain/Shared/Entities/Entity.cs
```

## Relação com Error e Result

- `Entity<TId>` é independente de `Error` e `Result`;
- `Entity<TId>` será utilizado em Domain Services e Use Cases que retornam `Result<T>`;
- O padrão de composição permite que operações retornem `Result<Entity>` ou `Result<IEnumerable<Entity>>`.

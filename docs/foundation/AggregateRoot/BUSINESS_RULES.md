# Regras de Negócio e Invariantes — AggregateRoot

Este documento define as regras obrigatórias e invariantes arquiteturais do componente `AggregateRoot<TId>`.

Todas as implementações devem respeitar estas regras.

---

## RN-ARG-01 — Herança de Entity

`AggregateRoot<TId>` deve herdar de `Entity<TId>` com a mesma restrição genérica `where TId : notnull`.

A herança é simples e direta: `public abstract class AggregateRoot<TId> : Entity<TId>`.

Nenhuma redefinição ou sobrescrita de membros de `Entity<TId>`.

---

## RN-ARG-02 — Construtor Protegido Mínimo

`AggregateRoot<TId>` deve expor apenas um construtor protegido com assinatura:

```csharp
protected AggregateRoot(TId id) : base(id) { }
```

Nenhum construtor público, estático factory no nivel de raiz abstrata, nem parâmetros adicionais.

A passagem de `id` a `base(id)` é mandatória; nenhuma lógica adicional.

---

## RN-ARG-03 — Sem Redefinição de Operadores

`AggregateRoot<TId>` não deve redefinir:

- `operator ==`
- `operator !=`
- `Equals(object?)`
- `Equals(AggregateRoot<TId>?)`
- `GetHashCode()`

`Equals(object?)` e `GetHashCode()` são `sealed override` em `Entity<TId>` e não podem ser sobrescritos. Já a linguagem C# permite declarar novos operadores `==` e `!=` em subclasses; a arquitetura desta feature proíbe essa redefinição, e ela é verificada por testes arquiteturais na própria `AggregateRoot<TId>` (não em subclasses concretas).

A igualdade continua baseada em tipo concreto e `Id`, conforme `Entity<TId>`.

---

## RN-ARG-04 — Sem Propriedades Públicas Adicionais Além de Id

`AggregateRoot<TId>` expõe apenas `Id` (herdado). Nenhuma propriedade pública adicionada no nível abstrato.

Subclasses concretas adicionam propriedades e coleções de entidades filhas conforme necessário.

---

## RN-ARG-05 — Agregação de Entidades Filhas

Um agregado pode conter entidades filhas que:

- herdam de `Entity<TId>` ou possuem identidade própria;
- são encapsuladas pelo agregado concreto, que decide como (ou se) as expõe;
- respeitam invariantes do agregado;
- não são modificadas diretamente de fora do agregado.

**Exemplo:**

```csharp
public sealed class Order : AggregateRoot<Guid>
{
    private readonly List<OrderLine> _lines = new();

    public Order(Guid id) : base(id)
    {
    }

    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    // Falha de negócio esperada: retorna Result (A2); violação de contrato lançaria exceção
    public Result AddLine(OrderLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (_lines.Count >= 100)
            return Result.Failure(OrderErrors.TooManyLines);

        _lines.Add(line);
        return Result.Success();
    }
}
```

`OrderLine` e `OrderErrors` são ilustrativos. O encapsulamento da coleção é decisão do agregado concreto.

---

## RN-ARG-06 — Invariantes de Agregado

Cada agregado concreto define seus próprios invariantes (ex: quantidade mínima/máxima de entidades, referências válidas, estados permitidos).

`AggregateRoot<TId>` não impõe invariantes adicionais além de:

- `Id` não nulo (herdado de `Entity<TId>`)
- `Id` sem setter (herdado de `Entity<TId>`)
- Tipo concreto e `Id` definem igualdade (herdado de `Entity<TId>`)

---

## RN-ARG-07 — Sem Eventos de Domínio Nesta Feature

`AggregateRoot<TId>` não coleta, publica ou gerencia eventos de domínio.

Essa responsabilidade pertence a uma feature futura.

Agregados concretos podem implementar métodos que retornam resultados ou exceções, mas não eventos.

---

## RN-ARG-08 — Sem Persistência, Auditoria ou Imposição de Multitenancy

`AggregateRoot<TId>` não inclui:

- Repository pattern
- Persistência ou mapeamento ORM
- Timestamps (`CreatedAt`, `UpdatedAt`, `DeletedAt`)
- User tracking (`CreatedBy`, `UpdatedBy`)
- Soft delete ou marcas de exclusão
- Imposição de `TenantId` ou padrão de isolamento multi-tenant (agregados concretos podem implementar conforme necessário)
- Versionamento ou concorrência otimista/pessimista

Essas responsabilidades pertencem a infraestrutura ou agregados concretos.

---

## RN-ARG-09 — Independência de Infraestrutura

`AggregateRoot<TId>` não deve depender de:

- ORM (Entity Framework, NHibernate, Dapper)
- banco de dados ou persistência
- serialização
- logging ou observabilidade
- cache
- ASP.NET Core ou camada de apresentação
- eventos de domínio ou bus de mensagem
- qualquer biblioteca de infraestrutura

O componente pertence à Shared Foundation do domínio.

---

## RN-ARG-10 — Ciclo de Vida do Agregado

Agregados concretos definem seu próprio ciclo de vida através de:

- Métodos factory (ex: `Create`) que retornam `Result<T>` (recomendado para falhas de domínio esperadas)
- Métodos de transição (ex: `Confirm`, `Cancel`) que garantem invariantes
- Validações em construtores (públicos devem ser raros; construtores privados são comuns)

`AggregateRoot<TId>` não impõe padrão de ciclo de vida, nem de recarregamento (`Reconstitute`). Persistência e recarregamento de agregados pertencem a infraestrutura (Repository).

---

## RN-ARG-11 — Igualdade de Agregados

Dois agregados são iguais se:

1. São do mesmo tipo concreto
2. Possuem o mesmo `Id`

Seu estado interno (entidades filhas, value objects) **não participa** da igualdade.

Isto implica:

- `new Order(orderId)` com 0 linhas == `new Order(orderId)` com 10 linhas
- Agregados podem ser comparados sem copiar ou inspecionar estado
- Collections (`HashSet`, `Dictionary`) funcionam baseadas em identidade

---

## RN-ARG-12 — Herança Múltipla Não Permitida

`AggregateRoot<TId>` é uma classe. C# não permite herança múltipla de classes.

Uma classe concreta pode herdar de uma única raiz de agregado ou de uma única entidade.

Para combinar comportamentos, use composição.

---

## RN-ARG-13 — Sem Selagem Obrigatória

`AggregateRoot<TId>` não é `sealed`. Subclasses podem ser abertas ou seladas conforme padrão de domínio.

Recomendação: raízes de agregado concretos devem ser `sealed` para evitar herança não-intencional, mas isto é decisão do agregado concreto, não da abstração.

---

## RN-ARG-14 — Validação de Entidades Filhas

Entidades filhas mantêm sua própria validação de `Id` não-nulo (herdada de `Entity<TId>`).

A raiz do agregado é responsável por validar:

- Integridade de referências (ex: `OrderLine.OrderId` deve corresponder a `Order.Id`)
- Invariantes de relacionamento (ex: não duplicar linhas)
- Estado consistente do agregado

---

## RN-ARG-15 — Sem Genéricas Adicionais

`AggregateRoot<TId>` não introduz parâmetros genéricos adicionais além de `TId`.

Conceitos como `TAggregateRoot` ou `TEntity` são complexidade desnecessária neste nível.


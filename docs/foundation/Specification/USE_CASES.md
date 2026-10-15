# Casos de Uso — Specification Foundation

> **Nota:** `Customer` e suas propriedades são hipotéticos e ilustrativos. Apenas `ISpecification<T>`, `Specification<T>`, `And`, `Or`, `Not` e `Specifications` são contratos reais.

## UC01 — Criar uma especificação

```csharp
ISpecification<Customer> active = new Specification<Customer>(c => c.IsActive);
```

`Criteria` devolve a mesma expressão fornecida. Expressão nula lança `ArgumentNullException`.

## UC02 — Compor com And

```csharp
var eligible = active.And(new Specification<Customer>(c => c.Age >= 18));
```

Resultado: `c => c.IsActive && c.Age >= 18`, com curto-circuito.

## UC03 — Compor com Or

```csharp
var priority = vip.Or(premium);
```

## UC04 — Negar com Not

```csharp
var inactive = active.Not();
```

`active.Not().Not()` é logicamente equivalente a `active`.

## UC05 — Composição encadeada

```csharp
var result = active.And(adult).Or(vip.Not());
```

Cada `And`/`Or` cria um novo `ParameterExpression` e reescreve para ele os parâmetros de ambos os operandos; `Not` reutiliza os parâmetros existentes e aplica `Expression.Not` ao corpo. Nenhuma composição usa `Expression.Invoke`. O resultado continua sendo `Expression<Func<T, bool>>`.

## UC06 — Construir filtros dinâmicos a partir das identidades

```csharp
ISpecification<Customer> filter = Specifications.True<Customer>();

if (onlyActive)
{
    filter = filter.And(active);
}

if (onlyVip)
{
    filter = filter.And(vip);
}
```

`True<T>()` é o elemento neutro de `And`; `False<T>()` é o elemento neutro de `Or`.

## UC07 — Avaliar em memória

```csharp
var predicate = eligible.Criteria.Compile();
var ok = predicate(customer);
```

`Compile()` é responsabilidade de quem consome; a composição não compila. Closures são lidas na avaliação.

## UC08 — Implementação personalizada de ISpecification

Uma implementação própria pode ser composta, desde que `Criteria` não seja `null`; caso contrário `And`/`Or`/`Not` lançam `ArgumentException` identificando o argumento.

## UC09 — Erros de programação

```csharp
spec.And(null!);                 // ArgumentNullException (ParamName: "right")
((ISpecification<Customer>)null!).Not();   // ArgumentNullException (ParamName: "specification")
```

Não são convertidos em `Result`.

## UC10 — Consumo por provedores de consulta (fora do escopo)

Passar `Criteria` a um ORM é possível, mas a tradução **não é garantida** e a integração com `IRepository` não faz parte desta feature (Repository 1.0 permanece inalterado).

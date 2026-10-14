# API Pública — ValueObject

**Arquivo:** `src/Business.Platform.Core.Domain/Shared/ValueObjects/ValueObject.cs`
**Namespace:** `Business.Platform.Core.Domain.Shared`

## Declaração

```csharp
public abstract class ValueObject : IEquatable<ValueObject>
{
    public static bool operator ==(ValueObject? left, ValueObject? right);
    public static bool operator !=(ValueObject? left, ValueObject? right);

    public bool Equals(ValueObject? other);
    public sealed override bool Equals(object? obj);
    public sealed override int GetHashCode();

    protected abstract IEnumerable<object?> GetEqualityComponents();
}
```

Não há construtor declarado, campos nem propriedades.

## Membros

### `GetEqualityComponents()` — `protected abstract`

Produz os componentes que definem a igualdade, em ordem determinística. Componentes `null` são aceitos. O resultado deve ser estável enquanto o objeto for usado em coleções hash.

A base consome a sequência uma única vez por chamada de `Equals`/`GetHashCode`, sem materializar listas.

### `Equals(ValueObject?)`

Retorna `true` somente se:

1. `other` não é `null`;
2. `GetType() == other.GetType()` (tipos concretos idênticos; classe base e derivada nunca são iguais);
3. a mesma instância (atalho por referência, sem enumerar componentes) **ou** as duas sequências têm o mesmo comprimento e componentes iguais posição a posição, segundo `EqualityComparer<object?>.Default`.

Tipos diferentes retornam `false` antes de qualquer enumeração.

### `Equals(object?)` — `sealed override`

Delega para `Equals(obj as ValueObject)`. Argumentos que não são `ValueObject` retornam `false`.

### `GetHashCode()` — `sealed override`

`HashCode` incremental: `Add(GetType())` seguido de `Add(componente)` para cada componente, na ordem. Componentes `null` contribuem com hash 0. Sem cache.

Compatível com `Equals`: objetos iguais produzem o mesmo hash dentro do processo, desde que componentes iguais produzam o mesmo hash. Não promete ausência de colisões nem valores estáveis entre processos.

### `operator ==` / `operator !=`

`==`: `null == null` é `true`; `null` contra não-nulo é `false`; caso contrário `left.Equals(right)`. Usa `is null` (não recorre ao próprio operador). `!=` é a negação de `==`.

## Uso esperado

```csharp
public sealed class Money(decimal amount, string currency) : ValueObject
{
    public decimal Amount { get; } = amount;
    public string Currency { get; } = currency;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
```

(Exemplo ilustrativo; a Foundation não define Value Objects de produção.)

Funciona em `HashSet<T>`, `Dictionary<TKey, TValue>` e `Distinct()`.

## Responsabilidades do Value Object concreto

- Ser imutável (a base não impõe).
- Validar na construção (factory/`Result<T>` opcionais).
- Emitir coleções de forma estrutural: elementos individuais para sequências ordenadas; elementos normalizados (ex.: ordenados e sem duplicatas) para coleções não ordenadas; um delimitador estrutural (ex.: contagem) quando a concatenação puder ser ambígua.
- Distinguir três situações ao lidar com listas:
  1. **Lista emitida como um único componente** (`yield return list`): `List<T>` não sobrescreve `Equals`/`GetHashCode`, então igualdade e hash são **por referência**. Duas listas distintas com o mesmo conteúdo são diferentes.
  2. **Elementos emitidos individualmente** (`foreach (var x in list) yield return x`): o conteúdo participa da igualdade e do hash, na ordem. Se a lista for alterada depois, a igualdade e o hash do Value Object passam a refletir o novo conteúdo; por isso a lista não deve ser mutável.
  3. **Cópia defensiva ou exposição somente leitura** (`IReadOnlyList<T>`, `ImmutableArray<T>`): protege contra mutação externa, mas **não** implica igualdade estrutural; um `IReadOnlyList<T>` emitido como componente único continua sendo comparado por referência, a menos que o tipo defina igualdade própria.
- Componentes mutáveis alterados depois de o objeto ser usado em coleções hash-based podem fazer lookups falharem (não necessariamente falharão; o resultado é indefinido pelo contrato).

## Limitações da sequência de componentes

- A sequência deve ser finita e determinística: uma sequência infinita não termina em `Equals` nem em `GetHashCode`.
- Sequências compartilhadas entre instâncias são seguras apenas se puderem ser enumeradas de forma independente e repetida.
- Sequências instáveis (que mudam entre enumerações) violam as leis de igualdade e de hash.
- As exceções lançadas por `GetEnumerator`, `MoveNext`, `Current`, pela comparação dos componentes ou por `Dispose` propagam para o chamador; os enumeradores adquiridos são descartados (verificado por testes).
- Subclasses podem ser não seladas; instâncias de tipos concretos diferentes nunca são iguais.

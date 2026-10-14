# Casos de Uso — ValueObject

Exemplos ilustrativos de como Value Objects concretos devem usar a Foundation. Não são implementações de produção.

## UC01 — Valor com componentes simples

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

`new Money(10, "BRL") == new Money(10, "BRL")` é `true`; moedas diferentes, `false`.

## UC02 — Componente opcional (`null`)

Componentes `null` são aceitos: emita `null` normalmente (ex.: complemento de endereço ausente). `null` é igual a `null` e diferente de qualquer valor.

## UC03 — Sequência ordenada

Emita um componente por elemento; a ordem importa e comprimentos diferentes são diferentes.

```csharp
protected override IEnumerable<object?> GetEqualityComponents() => _steps;   // IReadOnlyList<string> imutável
```

## UC04 — Coleção não ordenada

Normalize antes de emitir (ex.: remover duplicatas e ordenar com comparador explícito), de preferência no construtor, guardando uma cópia.

```csharp
_tags = [.. tags.Distinct().OrderBy(t => t, StringComparer.Ordinal)];
```

## UC05 — Evitar ambiguidade de estrutura

Ao emitir **duas ou mais coleções adjacentes** sem delimitadores, a sequência achatada pode ser ambígua. Exemplo: `(["a"], ["b","c"])` e `(["a","b"], ["c"])` produzem ambas `a, b, c` e seriam iguais. Emita a contagem (ou outro delimitador estrutural) antes de cada coleção:

```csharp
yield return first.Length;
foreach (var item in first) yield return item;
yield return second.Length;
foreach (var item in second) yield return item;
```

Com contagens, as duas estruturas acima passam a produzir `1, a, 2, b, c` e `2, a, b, 1, c`, e são diferentes.

## UC06 — Value Objects aninhados

Emita o Value Object interno como componente; seu `Equals`/`GetHashCode` é usado.

## UC07 — Uso em coleções

`HashSet<T>`, `Dictionary<TKey, TValue>` e `Distinct()` funcionam por valor.

## Armadilhas

- **Lista emitida como único componente:** `List<T>` usa igualdade e hash por referência; listas distintas com o mesmo conteúdo são diferentes. Para comparar por conteúdo, emita os elementos individualmente.
- **Elementos de uma lista mutável emitidos individualmente:** o conteúdo participa da igualdade e do hash; alterar a lista depois muda ambos e pode comprometer lookups em coleções hash-based. Faça cópia defensiva e exponha somente leitura. Isso protege contra mutação, mas não cria igualdade estrutural por si só.
- **Componentes mutáveis:** a base não impede; mutação após uso em `HashSet`/`Dictionary` pode fazer lookups falharem (não necessariamente falharão).
- **Sequências infinitas, instáveis ou compartilhadas sem enumeração independente:** não terminam ou violam as leis de igualdade e hash.
- **Persistir `GetHashCode()`:** não é estável entre processos.
- **Esperar que `IEnumerable` seja comparado estruturalmente:** não é; a Foundation não faz isso.
- **Validação:** feita pelo concreto (construtor ou factory, opcionalmente com `Result<T>`).

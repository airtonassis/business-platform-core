# Regras de Negócio — ValueObject

## Pré-condições das leis de igualdade

Reflexividade, simetria, transitividade e consistência de igualdade e hash dependem, **além da base**, de:

1. **Estabilidade dos componentes** durante o tempo de vida do objeto e do seu uso em coleções hash-based.
2. **Implementações corretas de `Equals` e `GetHashCode` pelos componentes** (consistentes entre si, simétricas e transitivas).
3. **Sequência determinística de componentes** (finita, mesma ordem e mesmo conteúdo a cada enumeração).

A classe-base não consegue garantir essas propriedades para componentes arbitrários com implementações defeituosas ou instáveis. Onde a tabela abaixo diz "Sim", leia "desde que as pré-condições acima sejam satisfeitas".

## Regras de igualdade

| ID | Regra | Garantida pela base? |
|---|---|---|
| R01 | Uma instância é igual a si mesma (atalho por referência, sem enumerar componentes). | Sim |
| R02 | `Equals(null)` retorna `false`. | Sim |
| R03 | `null == null` é `true`; `null` contra não-nulo é `false` (`!=` é a negação). | Sim |
| R04 | Tipos concretos de runtime diferentes nunca são iguais, mesmo com componentes iguais (inclusive base × derivada). | Sim |
| R05 | Componentes são comparados na ordem declarada, com `EqualityComparer<object?>.Default`. | Sim |
| R06 | Componentes `null` são aceitos (`null` igual a `null`; `null` diferente de valor). | Sim |
| R07 | Sequências de comprimentos diferentes são diferentes. | Sim |
| R08 | `Equals` e operadores não recorrem um ao outro. | Sim |
| R09 | Igualdade é simétrica, transitiva e consistente, **para componentes estáveis**. | Sim, condicionada à estabilidade |

## Regras de hash

| ID | Regra |
|---|---|
| H01 | `GetHashCode` usa `HashCode` incremental com `GetType()` e cada componente, preservando a ordem. |
| H02 | Semântica compatível com `Equals`: iguais ⇒ mesmo hash (no processo), se os componentes respeitarem o mesmo contrato. |
| H03 | Sem cache de hash. |
| H04 | Sem promessa de ausência de colisões nem de estabilidade entre processos/execuções; não persistir o hash. |

## Coleções

- A base **não** trata `IEnumerable` como coleção estrutural. Um componente que seja uma coleção é comparado por `object.Equals` (em geral, referência).
- A representação estrutural pertence ao Value Object concreto: sequência ordenada = um componente por elemento; coleção não ordenada = elementos normalizados antes de emitir; coleção vazia = nenhum componente (ou marcador explícito); ambiguidade entre coleções adjacentes = delimitador estrutural (ex.: contagem antes de cada coleção).
- Lista emitida como único componente: igualdade e hash por referência. Elementos emitidos individualmente: conteúdo participa da igualdade e do hash.
- Enumeradores: os enumeradores adquiridos por `Equals` e `GetHashCode` são descartados em todos os caminhos (sucesso, diferença antecipada, comprimentos diferentes, exceções), e exceções de `GetEnumerator`/`MoveNext`/`Current`/comparação/`Dispose` propagam. Sequências infinitas, instáveis ou compartilhadas sem enumeração independente não são suportadas.
- Value Objects aninhados participam naturalmente: seus `Equals`/`GetHashCode` são usados como componentes.

## Limitações contratuais (não garantidas pela base)

1. **Imutabilidade, inclusive profunda.** Componentes mutáveis alterados depois de o objeto entrar em `HashSet`/`Dictionary` mudam igualdade e hash e podem fazer lookups falharem (não necessariamente falharão). Demonstrado por testes (`MutableComponents_AfterMutation_ShouldChangeEquality`, `MutableComponents_AfterInsertionInHashSet_MayCompromiseLookup`).
2. **Igualdade estrutural automática de coleções.**
3. **Estabilidade dos componentes.** É responsabilidade do concreto.
4. **Validação de domínio.**

## Invariantes verificadas por testes arquiteturais

- Classe pública, abstrata, não selada, não genérica, derivada diretamente de `object`, em `Business.Platform.Core.Domain.Shared`.
- Implementa `IEquatable<ValueObject>`.
- `GetEqualityComponents` é `protected abstract`; `Equals(object?)` e `GetHashCode()` são `sealed override`; `Equals(ValueObject?)` não é sobrescrevível; operadores `==`/`!=` públicos.
- Sem campos ou propriedades; apenas os seis métodos do contrato.
- Depende somente de `System.*`; sem dependência de camadas superiores nem de `Error`, `Result`, `Entity`, `AggregateRoot` e `IDomainEvent`; nenhuma outra Foundation depende de `ValueObject`.

# Estratégia de Testes — ValueObject

## Resultado registrado (2026-10-13, `dotnet test -c Release`)

| Suíte | Testes de ValueObject | Total da suíte | Falhas |
|---|---|---|---|
| Unitários | 61 | 264 | 0 |
| Arquiteturais | 22 | 117 | 0 |

Valores da execução mais recente; reexecutar antes de presumir validade futura.

Todos os tipos concretos usados nos testes são definidos exclusivamente nos arquivos de teste (classes privadas aninhadas). Nenhum Value Object de produção foi criado.

## Testes unitários — `ValueObjectTests.cs`

| Área | Cobertura |
|---|---|
| Reflexividade | Mesma instância igual (`Equals` tipado e `object`); sem enumeração de componentes |
| Igualdade/desigualdade | Componentes iguais; cada componente diferente (Theory); argumento que não é `ValueObject` |
| Simetria, transitividade, consistência | Verificadas com componentes estáveis |
| Operadores | `==`/`!=` para iguais, diferentes, mesma instância, referências da base (sem recursão) |
| Null | `Equals(null)` tipado e `object`; `null == null`; um lado nulo nos dois sentidos |
| Tipo concreto | Tipos irmãos e base × derivada com componentes iguais são diferentes; hash inclui o tipo |
| Componentes nulos | Nulo × nulo, nulo × valor, todos nulos |
| Ordem e quantidade | Ordem diferente; comprimentos diferentes (todas as combinações com vazio); sem componentes |
| Enumeração | Cada lado enumerado uma vez em `Equals`; uma vez em `GetHashCode`; tipos diferentes não enumeram |
| Hash (consistência) | Consistente; depende de cada componente e da ordem; igual para valores iguais (inclusive nulos e aninhados) |
| Coleções hash/LINQ | `HashSet`, `HashSet<ValueObject>` com tipos distintos, `Dictionary`, `Distinct` |
| Aninhados | VO dentro de VO, igualdade, hash, componente aninhado nulo |
| Coleções | Sequência ordenada; não ordenada normalizada (ordem de entrada ignorada, entrada não mutada); vazia; duas coleções adjacentes `(["a"],["b","c"])` × `(["a","b"],["c"])` iguais sem delimitadores e diferentes com contagens; lista crua comparada por referência |
| Estabilidade/limitação | Componentes estáveis; componente mutável altera a igualdade; lookup em `HashSet` após mutação pode falhar (sem exigir falha); hash sem cache |
| Hash | Composição incremental verificada contra `HashCode` calculado no teste (tipo + componentes, em ordem); sem assertivas de hashes diferentes para valores diferentes |
| Enumeradores | Descarte após sucesso, diferença antecipada e comprimentos diferentes; descarte do esquerdo se a aquisição do direito falhar; propagação de exceções de `MoveNext`, `Current`, comparação de componente e `Dispose` (ambos descartados); `GetHashCode` descarta e propaga; sequência compartilhada |

## Testes arquiteturais — `ValueObjectArchitectureTests.cs`

Assembly e namespace; filtro de componente seleciona só `ValueObject` (evita regras vacuamente verdadeiras); classe pública, abstrata, não selada, não genérica; `IEquatable<ValueObject>`; base direta `object`; `GetEqualityComponents` protegido e abstrato; `Equals(object?)`/`GetHashCode()` sealed override; `Equals(ValueObject?)` não sobrescrevível; operadores; sem campos/propriedades (sem cache); apenas os seis métodos do contrato; dependências proibidas; apenas `System`; sem dependência de Error/Result/Entity/AggregateRoot/IDomainEvent (Theory); nenhuma outra Foundation depende de `ValueObject`; Foundations anteriores preservadas no namespace.

Não exige que subclasses sejam seladas.

## Mutation testing (Stryker.NET 4.8.1)

Resultados em `CHECKLIST.md`. `ValueObject.cs`: 23 mutantes elegíveis, 22 mortos + 1 timeout, 0 sobreviventes (100%).

## Não coberto por design

- Ausência absoluta de colisões de hash (não prometida).
- Imutabilidade profunda e validação de domínio (responsabilidade do concreto).

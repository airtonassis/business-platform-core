# Detalhes de Implementação — ValueObject

## Status

**Post-Audit Corrections Applied — QA Gate Pending.** Contrato aprovado (Architecture Gate: APPROVED) implementado sem desvios de contrato.

## Arquivos

| Arquivo | Papel |
|---|---|
| `src/Business.Platform.Core.Domain/Shared/ValueObjects/ValueObject.cs` | Implementação |
| `tests/UnitTests/Domain/Shared/ValueObjects/ValueObjectTests.cs` | 61 testes comportamentais |
| `tests/ArchitectureTests/Domain/Shared/ValueObjects/ValueObjectArchitectureTests.cs` | 22 testes arquiteturais |

Nenhum pacote foi adicionado; `Error`, `Result`, `Entity`, `AggregateRoot`, `IDomainEvent` e ADRs não foram modificados.

## Decisões

### D01 — Classe base abstrata, não selada
Subclasses concretas podem ser seladas ou não. Tipos concretos diferentes nunca são iguais.

### D02 — Igualdade por tipo concreto e componentes
`Equals(ValueObject?)` verifica `null`, depois `GetType()`, depois o atalho por referência e, por fim, percorre as duas sequências com dois enumeradores em lockstep, comparando com `EqualityComparer<object?>.Default`. Cada lado é enumerado exatamente uma vez; tipos diferentes e a mesma instância não enumeram.

### D03 — Sem `System.Linq`
A primeira versão usava `SequenceEqual`. Ela foi trocada por comparação manual porque o teste arquitetural preexistente `ErrorArchitectureTests.DomainAssembly_ShouldReferenceOnlyAllowedAssemblies` restringe os assemblies referenciados pelo Domain (não inclui `System.Linq`), e esse teste não pode ser alterado nesta feature. Não foi preciso novo ADR.

### D04 — Hash incremental sem cache
`HashCode.Add(GetType())` e `Add(componente)` por componente, na ordem. Sem campo de cache (verificado por teste arquitetural). Sem promessas de ausência de colisões ou de estabilidade entre processos.

### D05 — Coleções
A base não interpreta `IEnumerable`. A representação estrutural pertence ao concreto (ver `BUSINESS_RULES.md` e os tipos de teste `Sequence`, `TagSet`, `FlatLists`, `CountedLists`, `RawListComponent`).

### D06 — Imutabilidade
Não imposta e não declarada. A limitação é demonstrada por testes com componente mutável.

### D07 — Operadores
`left is null ? right is null : left.Equals(right)`; `!=` é `!(left == right)`. Sem recursão entre `Equals` e operadores.

## Observações

- O Stryker classifica como *Timeout* a mutação `!(left == right)` → `left != right` (recursão infinita); é contada como detectada.
- Nenhum teste exige hashes diferentes para valores diferentes (colisões são permitidas pelo contrato); o hash é validado pela composição incremental esperada (`HashCode` com tipo e componentes na ordem) e pela consistência com `Equals`.

## Fora do escopo

Value Objects de produção (Money, Address, Email…), serialização, ORM, framework genérico de comparação de coleções.

## Enumeradores e limitações da sequência de componentes

`Equals` adquire dois enumeradores com `using`; `GetHashCode` usa `foreach`. Os enumeradores são descartados em todos os caminhos (sucesso, diferença antecipada, comprimentos diferentes, exceções); se a aquisição do enumerador direito falhar, o esquerdo é descartado. Exceções de `GetEnumerator`, `MoveNext`, `Current`, da comparação dos componentes e de `Dispose` propagam. Isto é coberto por testes com enumeradores instrumentados que verificam apenas o comportamento observável (descarte e propagação), não detalhes internos.

Não suportado: sequências infinitas (não terminam), instáveis (violam as leis de igualdade e hash) e compartilhadas sem enumeração independente.

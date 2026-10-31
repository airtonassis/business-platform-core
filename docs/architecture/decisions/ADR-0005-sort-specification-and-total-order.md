# ADR-0005 — `SortSpecification<TAggregate, TId>`, `SortKey`, `TotalOrder`, identificadores fortemente tipados e validações

## Status

**Accepted**

Data: 2026-10-22

Origem: Repository 2.x — decisões D-01, D-02, D-03, D-04, D-05, D-07, D-10 e D-11 do complemento de reconciliação.

---

## Contexto

Paginação por offset só é determinística com ordenação total: sem desempate único, linhas empatadas podem aparecer em duas páginas ou em nenhuma. `PaginationRequest` já registra que a camada de consulta deve garantir "ordenação determinística com desempate único", mas nenhum tipo expressa essa garantia. Ordenação por strings livres (nomes de coluna ou propriedade) acopla o chamador ao esquema persistido e permite injeção de membros arbitrários.

---

## Alternativas consideradas

- **Ordenar por `string` (nome de propriedade/coluna):** acopla ao esquema e não é verificável em compilação. Rejeitada.
- **`Expression<Func<T, object>>` livre:** boxing, ordenação por membros não mapeados e dependência de tradução arbitrária. Rejeitada.
- **Aplicar desempate implícito na Infrastructure:** a garantia ficaria fora do Domain e variaria por provedor. Rejeitada.
- **Aceitar `TotalOrder` construído por Application/Infrastructure:** permitiria contornar a validação. Rejeitada.
- **Chaves de ordenação como enum global:** não escala por agregado. Rejeitada.

---

## Decisão

1. **`SortKey`.** Representa uma chave de ordenação nomeada e tipada de um agregado específico, composta por seletor de membro (expressão), direção (ascendente/descendente) e identificador estável. Chaves são declaradas pelo Domain do agregado; chamadores escolhem entre chaves declaradas e não informam nomes livres.
2. **`SortSpecification<TAggregate, TId>`.** Declarada com `where TAggregate : AggregateRoot<TId>` e `where TId : notnull`. **Não** exige `IComparable<TId>`: a comparação do identificador é feita pelo armazenamento sobre a representação persistida (M1–M5, ADR-0006), nunca em memória. Descreve a ordenação solicitada como sequência não vazia de `SortKey` e **sempre** conclui com desempate pelo identificador do agregado (`TId`), de modo que a ordem resultante seja total.
   - **Chaves solicitadas:** no máximo **quatro**, contando o `Id` quando informado explicitamente.
   - **`TotalOrder`:** no máximo **cinco** chaves, isto é, as solicitadas mais o `Id` anexado pelo Domain quando não informado. Se o `Id` for informado explicitamente, deve ser a última chave solicitada e não é anexado de novo.
3. **`TotalOrder` construído exclusivamente pelo Domain.**
   - `TotalOrder` é o resultado validado da construção de `SortSpecification`; só é obtido por fábrica do Domain (construtor não público, sem construtor público nem fábrica em Application ou Infrastructure).
   - Infrastructure e Application **recebem** `TotalOrder`; não o criam, não o alteram e não removem o desempate.
   - `FindPagedAsync` (ADR-0004) aceita apenas `TotalOrder`/`SortSpecification` já validada.
4. **Identificadores fortemente tipados.** O desempate usa `TId` (parâmetro genérico de `AggregateRoot<TId>`), e não `object`, `string` ou `Guid` cru; chaves de ordenação referenciam membros com tipo estático conhecido.
5. **Validações na construção** (retornam `Result` com `ErrorType.Validation`, sem lançar para entrada inválida do chamador; códigos finais fixados na implementação):
   - sequência de chaves vazia ou nula é rejeitada;
   - chave duplicada (mesmo membro) é rejeitada, inclusive com direções diferentes;
   - chave nula é rejeitada;
   - o desempate por `TId` não pode ser repetido nem removido; o identificador é sempre a última chave de `TotalOrder`;
   - mais de quatro chaves solicitadas (incluindo `Id` explícito) são rejeitadas; `TotalOrder` nunca excede cinco chaves;
   - seletor deve ser acesso direto a membro (propriedade) do agregado: sem chamadas de método, conversões arbitrárias, closures ou expressões compostas.
6. **Comprovação de mapeamento é da Infrastructure.** Domain declara ordenação; **Infrastructure é responsável por traduzir a representação persistida e comprovar** que cada `SortKey` e o identificador correspondem a colunas/campos persistidos e ordenáveis (por exemplo, por teste de mapeamento), **sem alterar** chaves, direções nem ordem de `TotalOrder`. Chave não mapeável é falha de tradução (ADR-0006). As obrigações M1–M5 sobre o identificador estão no ADR-0006. O Domain não conhece o esquema.
7. **Imutabilidade e igualdade.** `SortKey`, `SortSpecification` e `TotalOrder` são imutáveis, com cópia defensiva da coleção e igualdade por valor.

---

## Justificativa

- Fazer o Domain produzir a ordem total elimina paginação não determinística por construção e impede contorno por camadas externas.
- Chaves declaradas por agregado evitam strings livres e permitem verificação em compilação e em teste.
- Separar declaração (Domain) de comprovação (Infrastructure) respeita as fronteiras já adotadas no ADR-0003.

---

## Consequências

### Positivas

- Páginas consistentes entre chamadas para o mesmo estado de dados.
- Superfície de ordenação limitada e auditável.

### Negativas / Custos

- Cada agregado consultável precisa declarar suas `SortKey`.
- Desempate por identificador é sempre aplicado, mesmo quando redundante.
- Restrição a acesso direto de membro limita ordenações calculadas; elas exigirão decisão própria.

### Escopo da autorização

Este ADR **não autoriza implementação** de tipos, testes ou mapeamentos. Namespaces e códigos de erro de validação serão fixados na fase de implementação autorizada.

---

## Rastreabilidade de decisões

| Decisão | Aplicação neste ADR |
|---|---|
| D-01 | Aplicável; formalizada neste ADR |
| D-02 | Aplicável; formalizada neste ADR |
| D-03 | Aplicável; formalizada neste ADR |
| D-10 | Aplicável; formalizada neste ADR |
| D-08, D-09 | Decisões externas a este ADR; não geram cláusula aqui |

---

## Relação com outros ADRs

- ADR-0004: `FindPagedAsync` consome `TotalOrder`.
- ADR-0006: chave não traduzível/mapeada gera `Query.TranslationFailed`.
- ADR-0003: seletores usam árvores de expressão permitidas no Domain.

---

## Rastreabilidade

- `src/Business.Platform.Core.Domain/Shared/Pagination/PaginationRequest.cs` (nota sobre ordenação determinística).
- `src/Business.Platform.Core.Domain/Shared/AggregateRoots/AggregateRoot.cs`.
- `docs/foundation/Pagination/`, `docs/foundation/Specification/`.

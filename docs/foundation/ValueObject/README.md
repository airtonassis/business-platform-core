# Componente: ValueObject

O `ValueObject` é a classe base de objetos de valor da Shared Foundation. Um objeto de valor não possui identidade: é definido pelos componentes que o compõem e é intercambiável com qualquer outro do mesmo tipo concreto cujos componentes sejam iguais.

## Status

**Post-Audit Corrections Applied — QA Gate Pending**

- Implementação, testes, formatação e mutation testing executados e reportados (ver `CHECKLIST.md`).
- Auditoria independente do Codex: **PASS WITH NON-BLOCKING FINDINGS** (corrigidos; ver `CHECKLIST.md`). QA Gate definitivo: **PENDENTE**. Nenhum versionamento (`git add`/`commit`/`push`) foi realizado.

## O que a Foundation fornece

- Igualdade por valor: mesmo tipo concreto de runtime e componentes iguais, na ordem declarada.
- `GetHashCode()` compatível com `Equals`, calculado com `HashCode` incremental (tipo concreto + componentes), sem cache.
- Operadores `==` e `!=` tolerantes a `null`.
- Contrato extensível por um único ponto: `protected abstract IEnumerable<object?> GetEqualityComponents()`.

## O que a Foundation **não** garante

- **Imutabilidade (inclusive profunda).** A base não declara estado nem impede que subclasses o tenham. Subclasses devem ser imutáveis por convenção.
- **Igualdade estrutural automática de coleções.** Um `IEnumerable` emitido como componente é comparado por `object.Equals` (normalmente por referência). A representação estrutural (ordem, normalização, delimitadores) pertence ao objeto de valor concreto.
- **Ausência de colisões de hash ou estabilidade do hash entre processos.** `HashCode` usa semente aleatória por processo; o hash não deve ser persistido.
- **Validação.** Regras de domínio, factories e uso de `Result<T>` são responsabilidade do objeto concreto.

## Relação com outros componentes

`ValueObject` não depende de `Entity`, `AggregateRoot`, `IDomainEvent`, `Error` nem `Result`, e nenhum deles depende dele (verificado por testes arquiteturais). Depende apenas de `System.*`.

## Fora do escopo

Serialização, mapeamento ORM, mensagens localizadas e Value Objects de produção (Money, Address, Email etc.), que pertencem a contextos delimitados específicos.

## Documentos

`API.md` · `BUSINESS_RULES.md` · `IMPLEMENTATION.md` · `TESTS.md` · `USE_CASES.md` · `CHECKLIST.md`

# ADR-0003 — Allow Expression Trees (System.Linq.Expressions) in Domain Assembly

## Status

**Accepted**

Data: 2026-10-14

Origem: implementação da FEATURE-0008 — Specification Foundation.

---

## Contexto

`ISpecification<T>` expõe a regra de seleção como `Expression<Func<T, bool>>`
(`Criteria`). Compor especificações (`And`, `Or`, `Not`) exige construir novas
árvores de expressão (`Expression.AndAlso`, `Expression.OrElse`,
`Expression.Not`, `Expression.Lambda`) e reescrever parâmetros com
`ExpressionVisitor`. Esses tipos pertencem ao assembly `System.Linq.Expressions`,
cuja referência passa a existir no assembly `Business.Platform.Core.Domain`.

A política vigente (ADR-0002), verificada por
`ErrorArchitectureTests.DomainAssembly_ShouldReferenceOnlyAllowedAssemblies`,
permite apenas `System.Runtime` e `System.Collections`.

---

## Alternativas consideradas

- **`Func<T, bool>` como contrato:** não é inspecionável nem composável sem
  `Invoke` de delegates; impede qualquer tradução por um provedor de consulta.
  Rejeitada.
- **Composição com `Expression.Invoke`:** produz `InvocationExpression`, que
  provedores de consulta frequentemente não traduzem (ou traduzem apenas em
  alguns casos). Rejeitada; a composição reescreve parâmetros.
- **`IQueryable<T>` / métodos de consulta no Domain:** acopla o Domain a um
  modelo de consulta. Rejeitada.
- **Interface com métodos default (`And`/`Or`/`Not`):** acopla o contrato à
  composição e dificulta evolução. Rejeitada; a composição fica em métodos de
  extensão.
- **Relaxar a allowlist para `System.*`:** já substituída por lista explícita
  (ver ADR-0002). Rejeitada.
- **Definir a especificação fora do Domain (Application/Infrastructure):**
  tornaria o conceito inacessível às regras de domínio. Rejeitada.

---

## Decisão

1. Autorizar **somente** a dependência adicional `System.Linq.Expressions` no
   assembly Domain, incluindo-a em `AllowedReferencedAssemblies` de
   `ErrorArchitectureTests`, ao lado de `System.Runtime` e `System.Collections`.
2. Manter a verificação do public key token da Microsoft (`b03f5f7f11d50a3a`):
   um assembly só é aceito com nome permitido **e** a chave pública da Microsoft.
3. Compor especificações construindo árvores de expressão com `AndAlso`,
   `OrElse` e `Not`, reescrevendo parâmetros por identidade de referência
   (`ExpressionVisitor`), sem `Expression.Invoke`, sem `Compile()` e sem
   `IQueryable`.

| Assembly permitido | Public key token |
|---|---|
| `System.Runtime` | `b03f5f7f11d50a3a` |
| `System.Collections` | `b03f5f7f11d50a3a` |
| `System.Linq.Expressions` | `b03f5f7f11d50a3a` |

---

## Justificativa

- Árvores de expressão são a representação padrão do .NET para predicados
  inspecionáveis; o Domain continua independente de infraestrutura.
- `System.Linq.Expressions` faz parte da BCL; não há pacote NuGet nem referência
  de projeto novos.
- A reescrita de parâmetros evita `InvocationExpression` e captura indevida de
  variáveis em lambdas aninhadas.
- A allowlist explícita é preservada: apenas um assembly é acrescentado.

---

## Consequências

### Positivas

- Especificações compostas continuam sendo árvores de expressão.
- Validação de assembly, chave pública e demais regras arquiteturais preservadas.
- `IRepository` 1.0 permanece inalterado.

### Negativas / Custos

- O Domain passa a conhecer o modelo de árvores de expressão.
- A lista de assemblies permitidos continua exigindo decisão explícita para
  novas necessidades.
- Variáveis capturadas (closures) são lidas no momento da avaliação, não da
  construção; o contrato não garante imutabilidade profunda.

### Limitações de tradução por ORMs

Este ADR **não garante** que qualquer ORM ou provedor de consulta traduza
`Criteria` ou expressões compostas. A tradução depende do provedor: membros,
métodos, closures e construções usados na expressão podem não ser suportados.
A verificação de tradução pertence a quem consome a especificação
(implementações de repositório em features futuras).

### Escopo da autorização

Este ADR **não autoriza outros assemblies**. Qualquer referência além de
`System.Runtime`, `System.Collections` e `System.Linq.Expressions` requer
decisão arquitetural própria.

---

## Relação com ADR-0002

Complementa o ADR-0002 (`System.Collections`): mantém o mesmo mecanismo
(allowlist explícita com nome + public key token) e amplia a lista em um
assembly. O ADR-0002 permanece válido; a lista efetiva passa a ter três
assemblies.

---

## Rastreabilidade

- FEATURE-0008 — Specification Foundation (`docs/foundation/Specification/`).
- `src/Business.Platform.Core.Domain/Shared/Specifications/`.
- `tests/ArchitectureTests/Domain/Shared/Errors/ErrorArchitectureTests.cs`.
- `tests/ArchitectureTests/Domain/Shared/Specifications/SpecificationArchitectureTests.cs`.
- ADR-0001 — Technology Stack; ADR-0002 — Allow System.Collections.

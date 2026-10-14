# ADR-0002 — Allow System.Collections in Domain Assembly

## Status

**Accepted**

Data: 2026-10-13

Origem: implementação da FEATURE-0003 — Entity Foundation e Architecture/QA
Gate. Identificador histórico: ADR-ENT-006.

---

## Contexto

A implementação de `Entity<TId>` utiliza `EqualityComparer<TId>.Default` para
comparar identificadores e calcular seu hash, conforme o contrato aprovado
(decisões D01–D05 da FEATURE-0003). Esse tipo pertence ao assembly
`System.Collections`, e seu uso introduz uma referência a esse assembly no
assembly `Business.Platform.Core.Domain`.

A política anterior, verificada por
`ErrorArchitectureTests.DomainAssembly_ShouldReferenceOnlyAllowedAssemblies`,
permitia apenas `System.Runtime`. Com `Entity<TId>`, o teste falhou com a
referência `System.Collections, Version=10.0.0.0,
PublicKeyToken=b03f5f7f11d50a3a`.

---

## Decisão

Incluir `System.Collections` na lista de assemblies permitidos
(`AllowedReferencedAssemblies` em `ErrorArchitectureTests`), ao lado de
`System.Runtime`.

A verificação do public key token da Microsoft (`b03f5f7f11d50a3a`) é
preservada: um assembly só é aceito se tiver nome permitido **e** a chave
pública da Microsoft. O teste não foi removido, ignorado nem enfraquecido.

| Assembly permitido | Public key token |
|---|---|
| `System.Runtime` | `b03f5f7f11d50a3a` |
| `System.Collections` | `b03f5f7f11d50a3a` |

---

## Consequências

### Positivas

- Permite o contrato de igualdade genérica aprovado para `Entity<TId>`
  (`EqualityComparer<TId>.Default`).
- Não introduz dependência de infraestrutura: `System.Collections` faz parte
  da BCL.
- Preserva a restrição arquitetural existente: demais assemblies, pacotes,
  referências de projeto e referências de framework continuam proibidos.

### Negativas / Custos

- A lista de assemblies permitidos passa a ter manutenção explícita: novas
  necessidades da BCL exigem nova decisão.

### Escopo da autorização

Este ADR **não autoriza automaticamente outros assemblies**. Qualquer nova
referência além de `System.Runtime` e `System.Collections` requer decisão
arquitetural própria.

---

## Alternativas consideradas

- **Comparar identificadores sem `EqualityComparer<TId>.Default`** (por
  exemplo, `Id.Equals(other.Id)`): contraria o contrato aprovado (D05),
  que exige tratamento uniforme de tipos de valor e de referência sem
  boxing. Não adotada.
- **Relaxar a verificação para qualquer assembly `System.*`:** foi
  explicitamente substituída pela lista explícita na remediação da
  FEATURE-0001 (M01). Não adotada.

---

## Rastreabilidade

- FEATURE-0003 — Entity Foundation (`docs/foundation/Entity/`).
- Decisões D01–D05 da FEATURE-0003.
- Identificador histórico: ADR-ENT-006.
- `docs/foundation/Error/IMPLEMENTATION.md` — política de assemblies
  permitidos.
- `tests/ArchitectureTests/Domain/Shared/Errors/ErrorArchitectureTests.cs`.
- Decisão anterior relacionada: ADR-0001 — Technology Stack.

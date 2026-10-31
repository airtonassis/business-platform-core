# ADR-0006 — Política de tradução, `Query.TranslationFailed` e classificação C-A/C-B/C-C

## Status

**Accepted**

Data: 2026-10-22

Origem: Repository 2.x — decisões D-01, D-02, D-03, D-04, D-05, D-07, D-10 e D-11 do complemento de reconciliação.

---

## Contexto

O ADR-0003 estabelece que o Domain expõe `Criteria` como árvore de expressão e que a tradução depende do provedor, sendo verificada por quem consome a especificação. O ADR-0005 acrescenta ordenação declarada pelo Domain. Falhas na execução de `FindPagedAsync` (ADR-0004) têm origens distintas — especificação não traduzível, erro de infraestrutura, defeito de programação — e precisam de política única para que erros esperados não se misturem com defeitos.

---

## Alternativas consideradas

- **Capturar toda exceção do provedor e convertê-la em `Result.Failure`:** mascara defeitos e falhas operacionais. Rejeitada.
- **Classificar por mensagem ou tipo de exceção heurístico:** frágil entre versões e provedores. Rejeitada.
- **Deixar toda falha propagar como exceção:** falha de tradução previsível passaria a exigir `try/catch` nos chamadores. Rejeitada.
- **Fallback silencioso para avaliação em memória (client-side):** pode materializar a tabela inteira e viola o limite do ADR-0004. Rejeitada.

---

## Decisão

1. **Infrastructure é responsável pela tradução** de `ISpecification<T>.Criteria` e de `TotalOrder` para o provedor, e pela comprovação de que membros usados correspondem ao mapeamento persistido (ADR-0005, item 6).
2. **Erro `Query.TranslationFailed`.** Falha de tradução inequivocamente identificada é comunicada por `Result.Failure` com `Error.Code = "Query.TranslationFailed"` e `ErrorType.Failure`, sem acoplamento a transporte (HTTP). A mensagem **não** inclui valores de parâmetros, texto SQL, nomes físicos de esquema nem mensagem bruta do provedor.
3. **Classificação de falhas:**

| Classe | Definição | Tratamento |
|---|---|---|
| **C-A** | **Ausência de mapeamento de chave de ordenação**, identificada por metadados do modelo de persistência (`SortKey` sem coluna/campo correspondente), antes de executar a consulta. É a **única** hipótese de C-A. | `Result.Failure` com `Query.TranslationFailed`. |
| **C-B** | Falha de tradução sinalizada **pelo provedor** durante a execução, cuja classificação como tradução seja inequívoca (por exemplo, exceção ou código documentado e específico do provedor). | **Indisponível** até existir comprovação de classificação inequívoca para o provedor adotado. Enquanto indisponível, tratada como C-C. |
| **C-C** | Qualquer outra falha: erro de conexão, timeout, defeito de programação, exceção não reconhecida ou ambígua. | **Propagada como exceção não classificada**, sem conversão em `Result` e sem mascaramento. |

4. **C-B só é habilitada** por ADR/decisão posterior que apresente evidência (por exemplo, testes contra o provedor real) de que a exceção ou o código identifica tradução sem ambiguidade. Classificação por mensagem de texto é vedada.
5. **Cancelamento** continua sendo `OperationCanceledException` e nunca é classificado como C-A, C-B ou C-C.
6. **Proibido fallback client-side.** Falha de tradução não pode degradar para filtro ou ordenação em memória.
7. **Exceções propagadas preservam causa original** (nenhum `catch` que engula ou substitua a exceção sem relançar); registro de log, se houver, é responsabilidade da camada acima.
8. **Em caso de dúvida, C-C.** Uma falha só é C-A na hipótese única da tabela (chave de ordenação sem mapeamento, por metadados); só seria C-B com comprovação formal.

9. **Invariantes obrigatórios da Infrastructure sobre a identidade persistida (M1–M5).** Para que o desempate por `TId` (ADR-0005) seja uma ordem total no armazenamento, a Infrastructure deve garantir os invariantes abaixo. A validação deles pertence à **inicialização** e aos **testes de integração**:

| Id | Obrigação |
|---|---|
| M1 | A conversão de `TId` para a representação persistida é **injetiva** sobre identidades válidas. |
| M2 | A identidade é **não nula** e **única** no conjunto consultado. |
| M3 | A representação persistida é de **tipo escalar ordenável**: `uuid`, `integer`, `bigint` ou `text` com *collation* determinística. |
| M4 | A conversão é **determinística e estável**; qualquer mudança exige migração explícita. |
| M5 | A identidade é persistida em **uma única coluna**; identidade composta está **fora da v2.x**. |

   Violações de M1–M5 descobertas em execução seguem a política de erros correspondente à **causa concreta**, sem conversão automática para `Query.TranslationFailed`.

---

## Justificativa

- Separar o que a Infrastructure sabe com certeza (C-A) do que depende do provedor (C-B) evita falsos positivos.
- Propagar C-C preserva a visibilidade de defeitos e falhas operacionais.
- Mensagens sem dados sensíveis evitam vazamento de parâmetros e esquema.

---

## Consequências

### Positivas

- Chamadores tratam `Query.TranslationFailed` como falha esperada sem capturar exceções genéricas.
- Defeitos não são escondidos atrás de `Result`.

### Negativas / Custos

- Falhas de tradução que só o provedor detecta aparecem como exceção até C-B ser habilitada.
- A Infrastructure precisa de verificações prévias (C-A) e de testes de mapeamento.

### Escopo da autorização

Este ADR **não autoriza implementação** e **não habilita C-B**.

---

## Relação com outros ADRs

- ADR-0003: tradução depende do provedor e é verificada pelo consumidor.
- ADR-0004: `FindPagedAsync` aplica esta política.
- ADR-0005: `SortKey` não mapeada é C-A.

---

## Rastreabilidade de decisões

| Decisão | Aplicação neste ADR |
|---|---|
| D-02 | Aplicável; formalizada neste ADR |
| D-04 | Aplicável; formalizada neste ADR |
| D-08, D-09 | Decisões externas a este ADR; não geram cláusula aqui |

---

## Rastreabilidade

- `src/Business.Platform.Core.Domain/Shared/Errors/` (`Error`, `ErrorType`).
- `docs/foundation/Repository/`, `docs/foundation/Specification/`.

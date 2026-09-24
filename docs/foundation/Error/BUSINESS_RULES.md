# Regras de Negócio e Invariantes — Error

Este documento define as regras obrigatórias e invariantes arquiteturais do componente `Error`.

Todas as implementações devem respeitar estas regras.

---

## RN-ERR-01 — Imutabilidade

`Error` deve ser imutável após sua criação.

As propriedades públicas não devem possuir setters mutáveis.

---

## RN-ERR-02 — Código obrigatório

Todo erro válido deve possuir um `Code` não nulo, não vazio e não composto apenas por espaços em branco.

O `Code` representa a identificação programática e estável da falha.

Exemplo:

`User.EmailAlreadyExists`

A descrição do erro pode evoluir sem exigir alteração no código utilizado pelos consumidores.

---

## RN-ERR-03 — Descrição obrigatória

Todo erro válido deve possuir uma `Description` não nula, não vazia e não composta apenas por espaços em branco.

A `Description` representa uma mensagem legível sobre a falha.

Ela não deve ser utilizada como identificador programático.

---

## RN-ERR-04 — Tipo obrigatório

Todo erro válido deve possuir exatamente um `ErrorType`.

A primeira versão suporta:

- Failure
- Validation
- NotFound
- Conflict
- Unauthorized
- Forbidden

O tipo representa a categoria semântica da falha e não deve conter conhecimento sobre protocolos de transporte.

---

## RN-ERR-05 — Error.None

`Error.None` representa exclusivamente a ausência de erro.

`Error.None` deve possuir uma representação canônica única e previsível.

Ele não deve ser utilizado como uma falha válida.

Seu uso principal será representar o estado sem erro em componentes como `Result.Success`.

---

## RN-ERR-06 — Independência de HTTP

`Error` não deve possuir:

- HTTP status code;
- `ProblemDetails`;
- headers;
- rotas;
- objetos específicos de ASP.NET Core;
- qualquer dependência de camada de apresentação.

O mapeamento entre `ErrorType` e protocolos externos pertence às camadas responsáveis pela exposição da aplicação.

---

## RN-ERR-07 — Independência de infraestrutura

`Error` não deve depender de:

- banco de dados;
- ORM;
- mensageria;
- logging;
- cache;
- serviços externos;
- bibliotecas de infraestrutura.

O componente pertence à Shared Foundation do domínio.

---

## RN-ERR-08 — Igualdade por valor

Dois objetos `Error` com os mesmos valores de:

- `Code`;
- `Description`;
- `Type`;

devem ser considerados equivalentes.

A implementação deve preservar semântica de igualdade por valor.

---

## RN-ERR-09 — Validação de construção

A criação de um `Error` inválido deve ser impedida.

Valores inválidos incluem:

- `Code` nulo, vazio ou whitespace;
- `Description` nula, vazia ou whitespace;
- `ErrorType` inválido.

Violações do contrato de construção representam erro de programação e não uma falha de negócio esperada.

---

## RN-ERR-10 — Sem informações sensíveis

`Code` e `Description` não devem carregar informações sensíveis, segredos, credenciais, tokens ou detalhes internos que não devam ser expostos aos consumidores.

Informações técnicas detalhadas devem permanecer nos mecanismos apropriados de observabilidade e logging.
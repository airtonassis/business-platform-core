# Regras de negócio — Auditing

## AuditActor

| Regra | Descrição |
|---|---|
| AUD-01 | `Kind` deve ser `User` ou `System`. |
| AUD-02 | Identificador obrigatório (nulo, vazio ou só whitespace é rejeitado). |
| AUD-03 | Sem whitespace no início ou no final (`char.IsWhiteSpace`). |
| AUD-04 | Máximo de 256 unidades UTF-16. |
| AUD-05 | Sem caracteres de controle (`char.IsControl`). |
| AUD-06 | Identificador preservado sem normalização; comparação ordinal e case-sensitive. |
| AUD-07 | Mensagens de erro nunca incluem o identificador recebido. |

Precedência (retorna somente o primeiro erro): `Audit.Actor.KindUndefined`, `Audit.Actor.IdRequired`, `Audit.Actor.IdSurroundingWhitespace`, `Audit.Actor.IdTooLong`, `Audit.Actor.IdInvalidCharacter`.

## AuditMetadata

| Regra | Descrição |
|---|---|
| AUD-10 | Ator nulo lança `ArgumentNullException` (antes de qualquer validação de instante). |
| AUD-11 | `default(DateTimeOffset)` é rejeitado (`NotSet`). |
| AUD-12 | Offset deve ser zero (`NonUtcOffset`). |
| AUD-13 | `UpdatedAt` não pode ser anterior a `CreatedAt` (`BeforeCreation`). |
| AUD-14 | `UpdatedAt` não pode ser anterior à última atualização (`BeforeLastUpdate`). |
| AUD-15 | Instantes iguais são permitidos; o mesmo ator pode atualizar repetidamente. |
| AUD-16 | `CreatedBy`/`CreatedAt` são definidos uma única vez e preservados por `WithUpdate`. |
| AUD-17 | `UpdatedBy`/`UpdatedAt` são nulos até a primeira atualização. |
| AUD-18 | Igualdade estrutural sobre os quatro campos. |

Precedência de `Create`: `Audit.CreatedAt.NotSet`, `Audit.CreatedAt.NonUtcOffset`.
Precedência de `WithUpdate`: `Audit.UpdatedAt.NotSet`, `Audit.UpdatedAt.NonUtcOffset`, `Audit.UpdatedAt.BeforeCreation`, `Audit.UpdatedAt.BeforeLastUpdate`.

Todos os erros usam `ErrorType.Validation`.

# Regras de Negócio e Invariantes — Result

Este documento define os contratos rígidos e as regras invariantes que regem a abstração `Result`.

---

## RN-RES-01: Exclusividade de Estado
Um objeto `Result` deve estar **estritamente** em um dos dois estados: **Sucesso** ou **Falha**. É proibido estar em ambos ou nenhum.

## RN-RES-02: Integridade do Erro em Caso de Falha
- Todo `Result` de **Falha** deve conter **exatamente** um objeto `Error` válido.
- `Error` não pode ser `null` — lança `ArgumentNullException`.
- `Error.None` é explicitamente proibido em `Result.Failure` — lança `ArgumentException`.
- Após construção, `Error` nunca é `null`.

## RN-RES-03: Acesso Protegido ao Valor (Value)
- O acesso à propriedade `Value` em um `Result<T>` de **Falha** lança `InvalidOperationException`.
- O chamador deve verificar `IsSuccess` ou `IsFailure` antes de acessar o valor.

## RN-RES-04: Imutabilidade
- Uma vez instanciado, um `Result` é completamente **imutável**.
- Nenhuma propriedade pode ter *setters* públicos ou mutáveis.
- `Result<TValue>` é `sealed` e não pode ser estendido.

## RN-RES-05: Implicidade e Operadores
- Conversão implícita de `T` para `Result<T>` cria `Result.Success(T)`.
- Conversão implícita de `Error` para `Result<T>` cria `Result.Failure(Error)`.
- Ambas as conversões respeitam **exatamente** as mesmas validações das factories correspondentes.

## RN-RES-06: Valor em Sucesso (Success)
- `Result.Success<TValue>(value)` proíbe `value == null` — lança `ArgumentNullException`.
- `Result.Success()` não-genérico retorna `Result` (não introduz `Unit`).
- Todo `Success` possui `Error == Error.None` (invariante interno).

## RN-RES-07: Validação de Construção
- `Result.Failure(null)` lança `ArgumentNullException`.
- `Result.Failure(Error.None)` lança `ArgumentException`.
- `Result.Success<TValue>(null)` lança `ArgumentNullException`.

## RN-RES-08: Match (Padrão de Consumo)
- `Result` oferece dois `Match` síncronos (não assíncrono na V1).
- `Match` requer **dois delegates obrigatórios**; `null` lança `ArgumentNullException`.
- Sem sobrecargas `void`, `Action` ou `async` na V1.
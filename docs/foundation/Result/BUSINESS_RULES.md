# Regras de Negócio e Invariantes — Result

Este documento define os contratos rígidos e as regras invariantes que regem a abstração `Result`.

---

## RN-RES-01: Exclusividade de Estado
Um objeto `Result` deve estar **estritamente** em um dos dois estados: **Sucesso** ou **Falha**. É proibido estar em ambos ou nenhum.

## RN-RES-02: Integridade do Erro em Caso de Falha
- Todo `Result` de **Falha** deve conter exatamente um objeto `Error` válido (não nulo).
- Tentar criar um `Result.Failure` com `Error.None` deve disparar um erro de inicialização/invariante.

## RN-RES-03: Acesso Protegido ao Valor (Value)
- O acesso à propriedade `Value` em um `Result<T>` de **Falha** é proibido e deve lançar uma exceção de violação de contrato (`InvalidOperationException`).
- O chamador deve verificar `IsSuccess` ou `IsFailure` antes de acessar o valor.

## RN-RES-04: Imutabilidade
- Uma vez instanciado, um `Result` é completamente **imutável**.
- Nenhuma propriedade pode ter *setters* públicos ou mutáveis.

## RN-RES-05: Implicidade e Operadores
- Deve suportar conversão implícita de `T` para `Result<T>` (Sucesso).
- Deve suportar conversão implícita de `Error` para `Result<T>` (Falha).
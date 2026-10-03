# Estratégia de Testes e Cobertura — Result

## Matriz Completa de Cenários de Testes Unitários

Cada teste deve ser robusto, documentado e medir comportamento específico de invariantes.

### Seção A: Result.Success() — Não-Genérico

1. **`Success_Should_Create_Result_With_IsSuccess_True`**
   - `Result.Success()` cria Result com `IsSuccess == true`

2. **`Success_Should_Set_Error_To_Error_None`**
   - `Result.Success().Error == Error.None`

3. **`Success_Should_Not_Be_Failure`**
   - `Result.Success().IsFailure == false`

### Seção B: Result.Failure(Error) — Não-Genérico

4. **`Failure_Should_Create_Result_With_IsFailure_True`**
   - `Result.Failure(error).IsFailure == true`

5. **`Failure_Should_Store_Error`**
   - `Result.Failure(error).Error == error`

6. **`Failure_With_Null_Error_Should_Throw_ArgumentNullException`**
   - `Result.Failure(null)` lança `ArgumentNullException`

7. **`Failure_With_Error_None_Should_Throw_ArgumentException`**
   - `Result.Failure(Error.None)` lança `ArgumentException`

8. **`Failure_Should_Not_Be_Success`**
   - `Result.Failure(error).IsSuccess == false`

### Seção C: Result<TValue>.Success(value) — Genérico

9. **`Success_TValue_Should_Create_Result_With_IsSuccess_True`**
   - `Result.Success(user).IsSuccess == true` (para qualquer TValue)

10. **`Success_TValue_Should_Store_Value`**
    - `Result.Success(user).Value == user`

11. **`Success_TValue_Should_Set_Error_To_Error_None`**
    - `Result.Success(user).Error == Error.None`

12. **`Success_TValue_With_Null_Value_Should_Throw_ArgumentNullException`**
    - `Result.Success<User>(null)` lança `ArgumentNullException`

13. **`Success_TValue_Should_Not_Be_Failure`**
    - `Result.Success(user).IsFailure == false`

### Seção D: Result<TValue>.Failure(error) — Genérico

14. **`Failure_TValue_Should_Create_Result_With_IsFailure_True`**
    - `Result.Failure<User>(error).IsFailure == true`

15. **`Failure_TValue_Should_Store_Error`**
    - `Result.Failure<User>(error).Error == error`

16. **`Failure_TValue_With_Null_Error_Should_Throw_ArgumentNullException`**
    - `Result.Failure<User>(null)` lança `ArgumentNullException`

17. **`Failure_TValue_With_Error_None_Should_Throw_ArgumentException`**
    - `Result.Failure<User>(Error.None)` lança `ArgumentException`

18. **`Failure_TValue_Should_Not_Be_Success`**
    - `Result.Failure<User>(error).IsSuccess == false`

### Seção E: Result<TValue>.Value — Acesso Protegido

19. **`Accessing_Value_On_Success_Should_Return_Value`**
    - `Result.Success(user).Value` retorna `user` sem exceção

20. **`Accessing_Value_On_Failure_Should_Throw_InvalidOperationException`**
    - `Result.Failure<User>(error).Value` lança `InvalidOperationException`

### Seção F: Conversões Implícitas — TValue → Result<TValue>

21. **`ImplicitConversion_Value_To_Result_Success_Should_Create_Success`**
    - `Result<User> result = user;` → `result.IsSuccess == true` e `result.Value == user`

22. **`ImplicitConversion_Value_To_Result_Success_Should_Set_Error_None`**
    - `Result<User> result = user;` → `result.Error == Error.None`

23. **`ImplicitConversion_Null_Value_Should_Throw_ArgumentNullException`**
    - `Result<User> result = (User?)null;` → `ArgumentNullException`

24. **`ImplicitConversion_Value_Should_Not_Be_Failure`**
    - Conversão de value nunca cria Failure

### Seção G: Conversões Implícitas — Error → Result<TValue>

25. **`ImplicitConversion_Error_To_Result_Failure_Should_Create_Failure`**
    - `Result<User> result = error;` → `result.IsFailure == true` e `result.Error == error`

26. **`ImplicitConversion_Error_To_Result_Failure_Should_Not_Be_Success`**
    - Conversão de error nunca cria Success

27. **`ImplicitConversion_Null_Error_Should_Throw_ArgumentNullException`**
    - `Result<User> result = (Error?)null;` → `ArgumentNullException`

28. **`ImplicitConversion_Error_None_Should_Throw_ArgumentException`**
    - `Result<User> result = Error.None;` → `ArgumentException`

### Seção H: Match — Sobrecargas Síncronas

29. **`Match_With_Success_And_Onsuccessfunc_Should_Call_Onsuccess`**
    - Para `Result.Success()`: `Match` chama `onSuccess()` sem parâmetro
    - Retorno: resultado de `onSuccess`

30. **`Match_With_Failure_And_Onsuccessfunc_Should_Call_Onfailure`**
    - Para `Result.Failure(error)`: `Match` chama `onFailure(error)`
    - Retorno: resultado de `onFailure`

31. **`Match_With_Success_TValue_And_Onsuccesvalues_Should_Call_Onsuccess_With_Value`**
    - Para `Result.Success(user)`: `Match` chama `onSuccess(user)`
    - Parâmetro: valor tipado `TValue`

32. **`Match_With_Failure_And_Onsuccesvalues_Should_Call_Onfailure`**
    - Para `Result.Failure<User>(error)`: `Match` chama `onFailure(error)`
    - Não passa Value em nenhuma hipótese

33. **`Match_With_Null_OnSuccess_Should_Throw_ArgumentNullException`**
    - `result.Match(null, error => ...)` → `ArgumentNullException`

34. **`Match_With_Null_OnFailure_Should_Throw_ArgumentNullException`**
    - `result.Match(() => ..., null)` → `ArgumentNullException`

35. **`Match_Should_Return_Type_From_Delegates`**
    - `Match<TResult>` retorna exatamente o tipo `TResult`
    - `result.Match<string>(() => "ok", error => "fail")` retorna `string`

36. **`Match_With_Different_Return_Types_Should_Compile`**
    - Ambas as sobrecargas devem compilar sem ambiguidade

### Seção I: Imutabilidade

37. **`Result_Properties_Should_Be_Read_Only`**
    - `IsSuccess`, `IsFailure`, `Error` não possuem setters públicos

38. **`Result_TValue_Value_Property_Should_Be_Read_Only`**
    - `Value` não possui setter público

39. **`Result_TValue_Should_Be_Sealed`**
    - `Result<TValue>` é `sealed` — não pode ser herdada

40. **`Result_After_Construction_Should_Not_Change_State`**
    - Nenhuma operação após construção altera `IsSuccess`, `IsFailure`, `Error` ou `Value`

### Seção J: Invariantes de Construtor Protegido

41. **`Protected_Constructor_With_IsSuccess_True_And_Error_None_Should_Succeed`**
    - `Result(true, Error.None)` é válido (usado por Success)

42. **`Protected_Constructor_With_IsSuccess_False_And_Valid_Error_Should_Succeed`**
    - `Result(false, validError)` é válido (usado por Failure)

43. **`Protected_Constructor_With_IsSuccess_True_And_Non_None_Error_Should_Throw`**
    - `Result(true, validError)` lança exceção — invariante violado

44. **`Protected_Constructor_With_IsSuccess_False_And_Error_None_Should_Throw`**
    - `Result(false, Error.None)` lança exceção — invariante violado

45. **`Protected_Constructor_With_Null_Error_Should_Throw_ArgumentNullException`**
    - `Result(any, null)` lança `ArgumentNullException`

---

## Casos Extremos Adicionais

46. **`Match_Return_Type_Consistency_Different_Inputs`**
    - Mesmo `TResult`, `onSuccess()` e `onFailure(error)` retornam instância compatível de `TResult`

47. **`ImplicitConversion_Sequence_Success_Then_Failure`**
    - Conversão sequencial: `Result<int> r1 = 42;` depois `Result<int> r2 = error;`
    - Ambas funcionam corretamente

48. **`Failure_Error_Identity_Preserved`**
    - Erro passado a `Failure` é recuperado idêntico via `Error` property

49. **`Success_Value_Identity_Preserved`**
    - Valor passado a `Success` é recuperado idêntico via `Value` property

50. **`Result_TValue_Generic_Type_Does_Not_Leak`**
    - `Result<User>` não é compatível com `Result<Admin>` em atribuição (sem conversão)

---

## Matriz de Testes: Implementação

| # | Teste | Categoria | Status |
|---|-------|-----------|--------|
| 1-3 | Success() não-genérico | Seção A | ⏳ |
| 4-8 | Failure() não-genérico | Seção B | ⏳ |
| 9-13 | Success<T>(value) | Seção C | ⏳ |
| 14-18 | Failure<T>(error) | Seção D | ⏳ |
| 19-20 | Value property | Seção E | ⏳ |
| 21-24 | Conversão implícita: T → Result<T> | Seção F | ⏳ |
| 25-28 | Conversão implícita: Error → Result<T> | Seção G | ⏳ |
| 29-36 | Match: 2 sobrecargas | Seção H | ⏳ |
| 37-40 | Imutabilidade | Seção I | ⏳ |
| 41-45 | Invariantes do construtor | Seção J | ⏳ |
| 46-50 | Casos extremos | Adicionais | ⏳ |

---

## Cobertura Esperada

- **Testes Unitários:** 50 cenários
- **Arquitetura:** Validar dependências (apenas Error) e sealed/sealed members
- **Mutation Score:** ≥90% (conforme ADR-0001, usar `perTestInIsolation` se necessário)

---

## Notas Importantes

1. Cada teste deve ser independente; nenhuma ordem de execução obrigatória
2. Usar xUnit com Shouldly (conforme ADR-0001)
3. Validar tipos de exceção exatos com `.ShouldBeOfType<TException>()`
4. Testes de Match devem validar que ambas as sobrecargas são chamadas corretamente
5. Testes de conversão implícita devem ser distintos de factory methods para validar operadores
6. Nenhum teste deve ser marcado como executado antes da implementação; marcação é somente para rastreamento pós-conclusão

---

## Resultados de Execução Final Validados

### Testes Unitários
- **Total:** 113 testes (executados e passando)
  - Seções A-J: factories, conversões, Match, imutabilidade, construtor protegido
- **Framework:** xUnit + Shouldly (conforme ADR-0001)
- **Distribuição:** Result 60 testes, Error 53 testes

### Testes Arquiteturais  
- **Total:** 37 testes (executados e passando) — evidência: `tests/ArchitectureTests/TestResults/feature-0002-architecture.trx`
- **Distribuição:** Result 21 testes, Error 16 testes
- **Validações:** Sealed class, herança Result<TValue>:Result, imutabilidade, dependências, API fechada (comparação de assinatura canônica completa — métodos, overloads, generic arity, return/parameter types, propriedades, indexadores, getter/setter, campos, eventos, operadores)

### Mutation Testing (Stryker.NET 4.8.1) — Execução Atual Confirmada

Comando: `dotnet stryker --msbuild-path "C:\Program Files\dotnet\sdk\10.0.401\MSBuild.dll"`
Relatório: `StrykerOutput/2026-10-02.23-05-57/reports/mutation-report.json`
Correspondência com fontes atuais confirmada por hash SHA-256 (Result.cs e Error.cs embutidos no relatório são idênticos byte a byte aos arquivos em disco).

- **Result.cs específico:**
  - Generated: 72
  - Killed: 52
  - Survived: 2 (IDs 46, 62 — mutação "Statement mutation" substitui `throw new ArgumentNullException(...)` por `;`; equivalente porque o `null` propagado é capturado pelo construtor protegido `Result(bool, Error)`, que já valida `error is null` e lança a mesma exceção)
  - Timeout: 0
  - NoCoverage: 0
  - Ignored: 18
  - CompileError: 0
  - Denominator (Killed+Timeout+Survived+NoCoverage): 54
  - **Score: 96.30%** ✅ (≥90%)

- **Global (todos os componentes):**
  - Generated: 95
  - Killed: 69
  - Survived: 2
  - Timeout: 1
  - NoCoverage: 0
  - Ignored: 21
  - CompileError: 2
  - Denominator (Killed+Timeout+Survived+NoCoverage): 72
  - **Score: 97.22%** ✅ (≥90%)

Estes números são consistentes com execuções anteriores do mesmo código-fonte (Stryker não muta arquivos de teste, portanto o score de produção permanece estável entre reescritas de `ResultArchitectureTests.cs`).

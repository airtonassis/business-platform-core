using Business.Platform.Core.Domain.Shared;

namespace Business.Platform.Core.UnitTests.Domain.Shared.Results;

// Subclasse privada para testar o construtor protegido com null error
internal class TestableResult : Result
{
    public TestableResult(bool isSuccess, Error? error)
        : base(isSuccess, error!)
    {
    }
}

// Tipo privado de teste para RES-TST-049: garantir identidade de referência sem interning de string
internal sealed class TestReferenceValue
{
    public TestReferenceValue(string value)
    {
        Value = value;
    }

    public string Value { get; }
}

public sealed class ResultTests
{
    private static readonly Error ValidError = new("Test.Error", "Test error description", ErrorType.Failure);

    #region Seção A: Result.Success() — Não-Genérico

    // RES-TST-001
    [Fact]
    public void Success_Should_Create_Result_With_IsSuccess_True()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
    }

    // RES-TST-002
    [Fact]
    public void Success_Should_Set_Error_To_Error_None()
    {
        var result = Result.Success();

        result.Error.ShouldBe(Error.None);
    }

    // RES-TST-003
    [Fact]
    public void Success_Should_Not_Be_Failure()
    {
        var result = Result.Success();

        result.IsFailure.ShouldBeFalse();
    }

    #endregion

    #region Seção B: Result.Failure(Error) — Não-Genérico

    // RES-TST-004
    [Fact]
    public void Failure_Should_Create_Result_With_IsFailure_True()
    {
        var result = Result.Failure(ValidError);

        result.IsFailure.ShouldBeTrue();
    }

    // RES-TST-005
    [Fact]
    public void Failure_Should_Store_Error()
    {
        var result = Result.Failure(ValidError);

        result.Error.ShouldBe(ValidError);
    }

    // RES-TST-006: Failure(null) lança ArgumentNullException exato
    [Fact]
    public void Failure_With_Null_Error_Should_Throw_ArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => Result.Failure(null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("error");
        exception.Message.ShouldContain("Erro não pode ser null");
    }

    // RES-TST-007: Failure(Error.None) lança ArgumentException exato
    [Fact]
    public void Failure_With_Error_None_Should_Throw_ArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => Result.Failure(Error.None));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("error");
        exception.Message.ShouldContain("Error.None não é válido para Failure");
    }

    // RES-TST-008
    [Fact]
    public void Failure_Should_Not_Be_Success()
    {
        var result = Result.Failure(ValidError);

        result.IsSuccess.ShouldBeFalse();
    }

    #endregion

    #region Seção C: Result<TValue>.Success(value) — Genérico

    // RES-TST-009
    [Fact]
    public void Success_TValue_Should_Create_Result_With_IsSuccess_True()
    {
        const int value = 42;
        var result = Result.Success(value);

        result.IsSuccess.ShouldBeTrue();
    }

    // RES-TST-010
    [Fact]
    public void Success_TValue_Should_Store_Value()
    {
        const int value = 42;
        var result = Result.Success(value);

        result.Value.ShouldBe(value);
    }

    // RES-TST-011
    [Fact]
    public void Success_TValue_Should_Set_Error_To_Error_None()
    {
        const int value = 42;
        var result = Result.Success(value);

        result.Error.ShouldBe(Error.None);
    }

    // RES-TST-012: Success<T>(null) lança ArgumentNullException exato
    [Fact]
    public void Success_TValue_With_Null_Value_Should_Throw_ArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => Result.Success<string>(null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("value");
        exception.Message.ShouldContain("Valor não pode ser null");
    }

    // RES-TST-013
    [Fact]
    public void Success_TValue_Should_Not_Be_Failure()
    {
        const int value = 42;
        var result = Result.Success(value);

        result.IsFailure.ShouldBeFalse();
    }

    #endregion

    #region Seção D: Result<TValue>.Failure(error) — Genérico

    // RES-TST-014
    [Fact]
    public void Failure_TValue_Should_Create_Result_With_IsFailure_True()
    {
        var result = Result.Failure<int>(ValidError);

        result.IsFailure.ShouldBeTrue();
    }

    // RES-TST-015
    [Fact]
    public void Failure_TValue_Should_Store_Error()
    {
        var result = Result.Failure<int>(ValidError);

        result.Error.ShouldBe(ValidError);
    }

    // RES-TST-016: Failure<T>(null) lança ArgumentNullException exato
    [Fact]
    public void Failure_TValue_With_Null_Error_Should_Throw_ArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => Result.Failure<int>(null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("error");
        exception.Message.ShouldContain("Erro não pode ser null");
    }

    // RES-TST-017: Failure<T>(Error.None) lança ArgumentException exato
    [Fact]
    public void Failure_TValue_With_Error_None_Should_Throw_ArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => Result.Failure<int>(Error.None));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("error");
        exception.Message.ShouldContain("Error.None não é válido para Failure");
    }

    // RES-TST-018
    [Fact]
    public void Failure_TValue_Should_Not_Be_Success()
    {
        var result = Result.Failure<int>(ValidError);

        result.IsSuccess.ShouldBeFalse();
    }

    #endregion

    #region Seção E: Result<TValue>.Value — Acesso Protegido

    // RES-TST-019
    [Fact]
    public void Accessing_Value_On_Success_Should_Return_Value()
    {
        const int value = 42;
        var result = Result.Success(value);

        var returnedValue = result.Value;

        returnedValue.ShouldBe(value);
    }

    // RES-TST-020: Value em Failure lança InvalidOperationException exato
    [Fact]
    public void Accessing_Value_On_Failure_Should_Throw_InvalidOperationException()
    {
        var result = Result.Failure<int>(ValidError);

        var exception = Should.Throw<InvalidOperationException>(() => _ = result.Value);

        exception.GetType().ShouldBe(typeof(InvalidOperationException));
        exception.Message.ShouldContain("falha");
    }

    #endregion

    #region Seção F: Conversões Implícitas — TValue → Result<TValue>

    // RES-TST-021
    [Fact]
    public void ImplicitConversion_Value_To_Result_Success_Should_Create_Success()
    {
        const int value = 42;
        Result<int> result = value;

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(value);
    }

    // RES-TST-022
    [Fact]
    public void ImplicitConversion_Value_To_Result_Success_Should_Set_Error_None()
    {
        const int value = 42;
        Result<int> result = value;

        result.Error.ShouldBe(Error.None);
    }

    // RES-TST-023
    [Fact]
    public void ImplicitConversion_Null_Value_Should_Throw_ArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
        {
            string? nullValue = null;
            Result<string> result = nullValue!;
        });

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("value");
    }

    // RES-TST-024
    [Fact]
    public void ImplicitConversion_Value_Should_Not_Be_Failure()
    {
        const int value = 42;
        Result<int> result = value;

        result.IsFailure.ShouldBeFalse();
    }

    #endregion

    #region Seção G: Conversões Implícitas — Error → Result<TValue>

    // RES-TST-025
    [Fact]
    public void ImplicitConversion_Error_To_Result_Failure_Should_Create_Failure()
    {
        Result<int> result = ValidError;

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ValidError);
    }

    // RES-TST-026
    [Fact]
    public void ImplicitConversion_Error_To_Result_Failure_Should_Not_Be_Success()
    {
        Result<int> result = ValidError;

        result.IsSuccess.ShouldBeFalse();
    }

    // RES-TST-027
    [Fact]
    public void ImplicitConversion_Null_Error_Should_Throw_ArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
        {
            Error? nullError = null;
            Result<int> result = nullError!;
        });

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("error");
    }

    // RES-TST-028
    [Fact]
    public void ImplicitConversion_Error_None_Should_Throw_ArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() =>
        {
            Result<int> result = Error.None;
        });

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("error");
    }

    #endregion

    #region Seção H: Match — Sobrecargas Síncronas

    // RES-TST-029: Match(Success) executes onSuccess only, not onFailure
    [Fact]
    public void Match_With_Success_And_Onsuccess_Func_Should_Call_Onsuccess()
    {
        var result = Result.Success();
        var onSuccessCalled = false;
        var onFailureCalled = false;

        var matchResult = result.Match(
            onSuccess: () =>
            {
                onSuccessCalled = true;
                return "success";
            },
            onFailure: _ =>
            {
                onFailureCalled = true;
                return "failure";
            }
        );

        onSuccessCalled.ShouldBeTrue();
        onFailureCalled.ShouldBeFalse();
        matchResult.ShouldBe("success");
    }

    // RES-TST-030: Match(Failure) executes onFailure only, not onSuccess
    [Fact]
    public void Match_With_Failure_And_Onsuccess_Func_Should_Call_Onfailure()
    {
        var result = Result.Failure(ValidError);
        var onSuccessCalled = false;
        var onFailureCalled = false;
        Error? receivedError = null;

        var matchResult = result.Match(
            onSuccess: () =>
            {
                onSuccessCalled = true;
                return "success";
            },
            onFailure: error =>
            {
                onFailureCalled = true;
                receivedError = error;
                return "failure";
            }
        );

        onSuccessCalled.ShouldBeFalse();
        onFailureCalled.ShouldBeTrue();
        receivedError.ShouldNotBeNull();
        ReferenceEquals(receivedError, ValidError).ShouldBeTrue();
        matchResult.ShouldBe("failure");
    }

    // RES-TST-031: Match executa somente onSuccess, não onFailure
    [Fact]
    public void Match_With_Success_TValue_And_Onsuccess_Values_Should_Call_Onsuccess_With_Value()
    {
        const int value = 42;
        var result = Result.Success(value);
        var receivedValue = 0;
        var onFailureExecuted = false;

        var matchResult = result.Match(
            onSuccess: v =>
            {
                receivedValue = v;
                return v.ToString();
            },
            onFailure: _ =>
            {
                onFailureExecuted = true;
                return "failure";
            }
        );

        receivedValue.ShouldBe(value);
        onFailureExecuted.ShouldBeFalse();
        matchResult.ShouldBe("42");
    }

    // RES-TST-032: Match executa somente onFailure, não onSuccess
    [Fact]
    public void Match_With_Failure_And_Onsuccess_Values_Should_Call_Onfailure()
    {
        var result = Result.Failure<int>(ValidError);
        var onSuccessExecuted = false;
        var receivedError = (Error?)null;

        var matchResult = result.Match(
            onSuccess: _ =>
            {
                onSuccessExecuted = true;
                return "success";
            },
            onFailure: e =>
            {
                receivedError = e;
                return "failure";
            }
        );

        onSuccessExecuted.ShouldBeFalse();
        receivedError.ShouldNotBeNull();
        ReferenceEquals(receivedError, ValidError).ShouldBeTrue();
        matchResult.ShouldBe("failure");
    }

    // RES-TST-033
    [Fact]
    public void Match_With_Null_OnSuccess_Should_Throw_ArgumentNullException()
    {
        var result = Result.Success();

        var exception = Should.Throw<ArgumentNullException>(() =>
            result.Match(null!, _ => "failure")
        );

        exception.ParamName.ShouldBe("onSuccess");
        exception.Message.ShouldContain("onSuccess não pode ser null");
    }

    // RES-TST-034
    [Fact]
    public void Match_With_Null_OnFailure_Should_Throw_ArgumentNullException()
    {
        var result = Result.Success();

        var exception = Should.Throw<ArgumentNullException>(() =>
            result.Match(() => "success", null!)
        );

        exception.ParamName.ShouldBe("onFailure");
        exception.Message.ShouldContain("onFailure não pode ser null");
    }

    // RES-TST-035
    [Fact]
    public void Match_Should_Return_Type_From_Delegates()
    {
        var result = Result.Success("hello");

        string matchResult = result.Match(
            onSuccess: v => v.ToUpper(),
            onFailure: _ => "ERROR"
        );

        matchResult.ShouldBe("HELLO");
    }

    // RES-TST-036
    [Fact]
    public void Match_With_Different_Overloads_Should_Both_Compile()
    {
        // Overload 1: no parameters
        Result result1 = Result.Success();
        var r1 = result1.Match(
            onSuccess: () => 1,
            onFailure: _ => 2
        );
        r1.ShouldBe(1);

        // Overload 2: with value parameter
        Result<int> result2 = Result.Success(42);
        var r2 = result2.Match(
            onSuccess: v => v,
            onFailure: _ => 0
        );
        r2.ShouldBe(42);
    }

    #endregion

    #region Seção I: Imutabilidade

    // RES-TST-037
    [Fact]
    public void Result_Properties_Should_Be_Read_Only()
    {
        var result = Result.Success();

        // Verificar que IsSuccess e Error não têm setters públicos
        var successProp = typeof(Result).GetProperty(nameof(Result.IsSuccess));
        successProp!.CanWrite.ShouldBeFalse();

        var errorProp = typeof(Result).GetProperty(nameof(Result.Error));
        errorProp!.CanWrite.ShouldBeFalse();
    }

    // RES-TST-038
    [Fact]
    public void Result_TValue_Value_Property_Should_Be_Read_Only()
    {
        var result = Result.Success(42);

        var valueProp = typeof(Result<int>).GetProperty(nameof(Result<int>.Value));
        valueProp!.CanWrite.ShouldBeFalse();
    }

    // RES-TST-039
    [Fact]
    public void Result_TValue_Should_Be_Sealed()
    {
        typeof(Result<int>).IsSealed.ShouldBeTrue();
    }

    // RES-TST-040: Result state must be completely immutable after construction and public operations
    [Fact]
    public void Result_After_Construction_Should_Not_Change_State()
    {
        // Criar Result<T> de sucesso com objeto de referência
        var originalValue = "immutable reference";
        var result = Result.Success(originalValue);

        // Guardar estado inicial
        var isSuccessBefore = result.IsSuccess;
        var isFailureBefore = result.IsFailure;
        var errorBefore = result.Error;
        var valueBefore = result.Value;

        // Executar operação pública: Match (sem mutação)
        var matchResult = result.Match(
            onSuccess: v => $"Matched: {v}",
            onFailure: e => $"Error: {e.Code}"
        );

        // Verificar resultado da operação
        matchResult.ShouldBe("Matched: immutable reference");

        // Verificar que o estado permanece idêntico após operação
        result.IsSuccess.ShouldBe(isSuccessBefore);
        result.IsFailure.ShouldBe(isFailureBefore);
        ReferenceEquals(result.Error, errorBefore).ShouldBeTrue();
        ReferenceEquals(result.Value, valueBefore).ShouldBeTrue();

        // Múltiplos acessos devem retornar a mesma referência
        ReferenceEquals(result.Value, result.Value).ShouldBeTrue();
    }

    #endregion

    #region Seção J: Invariantes de Construtor Protegido

    // RES-TST-041: Protected_Constructor_With_IsSuccess_True_And_Error_None_Should_Succeed
    [Fact]
    public void Protected_Constructor_With_IsSuccess_True_And_Error_None_Should_Succeed()
    {
        // Testar via subclasse com IsSuccess=true e Error.None (válido)
        var result = new TestableResult(true, Error.None);

        result.IsSuccess.ShouldBeTrue();
        result.Error.ShouldBe(Error.None);
    }

    // RES-TST-042: Protected_Constructor_With_IsSuccess_False_And_Valid_Error_Should_Succeed
    [Fact]
    public void Protected_Constructor_With_IsSuccess_False_And_Valid_Error_Should_Succeed()
    {
        // Testar via subclasse com IsSuccess=false e Error válido (válido)
        var result = new TestableResult(false, ValidError);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(ValidError);
    }

    // RES-TST-043: Protected_Constructor_With_IsSuccess_True_And_Non_None_Error_Should_Throw
    [Fact]
    public void Protected_Constructor_With_IsSuccess_True_And_Non_None_Error_Should_Throw()
    {
        // IsSuccess=true com Error não-None viola invariante
        var exception = Should.Throw<ArgumentException>(() =>
            new TestableResult(true, ValidError)
        );

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("error");
        exception.Message.ShouldContain("Success deve estar associado a Error.None");
    }

    // RES-TST-044: Protected_Constructor_With_IsSuccess_False_And_Error_None_Should_Throw
    [Fact]
    public void Protected_Constructor_With_IsSuccess_False_And_Error_None_Should_Throw()
    {
        // IsSuccess=false com Error.None viola invariante
        var exception = Should.Throw<ArgumentException>(() =>
            new TestableResult(false, Error.None)
        );

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("error");
        exception.Message.ShouldContain("Failure não pode estar associado a Error.None");
    }

    // RES-TST-045: Protected_Constructor_With_Null_Error_Should_Throw_ArgumentNullException
    [Fact]
    public void Protected_Constructor_With_Null_Error_Should_Throw_ArgumentNullException()
    {
        // Qualquer estado com error null deve lançar ArgumentNullException
        var exception = Should.Throw<ArgumentNullException>(() =>
            new TestableResult(true, null!)
        );

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("error");
        exception.Message.ShouldContain("Erro não pode ser null");
    }

    #endregion

    #region Seção K: Cobertura de Validações Internas (NoCoverage)

    // RES-TST-051: Match com onSuccess null deve lançar ArgumentNullException
    [Fact]
    public void Match_With_Null_OnSuccess_Delegate_Should_Throw_ArgumentNullException()
    {
        var result = Result.Success(42);

        var exception = Should.Throw<ArgumentNullException>(() =>
            result.Match<string>(onSuccess: null!, onFailure: _ => "error")
        );

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("onSuccess");
        exception.Message.ShouldContain("onSuccess não pode ser null");
    }

    // RES-TST-052: Match com onFailure null deve lançar ArgumentNullException
    [Fact]
    public void Match_With_Null_OnFailure_Delegate_Should_Throw_ArgumentNullException()
    {
        var result = Result.Success(42);

        var exception = Should.Throw<ArgumentNullException>(() =>
            result.Match<string>(onSuccess: v => v.ToString(), onFailure: null!)
        );

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("onFailure");
        exception.Message.ShouldContain("onFailure não pode ser null");
    }

    // RES-TST-053: Match no-param com onSuccess null deve lançar ArgumentNullException
    [Fact]
    public void Match_NoParam_With_Null_OnSuccess_Should_Throw_ArgumentNullException()
    {
        var result = Result.Success();

        var exception = Should.Throw<ArgumentNullException>(() =>
            result.Match<string>(onSuccess: null!, onFailure: _ => "error")
        );

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("onSuccess");
        exception.Message.ShouldContain("onSuccess não pode ser null");
    }

    // RES-TST-054: Match no-param com onFailure null deve lançar ArgumentNullException
    [Fact]
    public void Match_NoParam_With_Null_OnFailure_Should_Throw_ArgumentNullException()
    {
        var result = Result.Success();

        var exception = Should.Throw<ArgumentNullException>(() =>
            result.Match<string>(onSuccess: () => "ok", onFailure: null!)
        );

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("onFailure");
        exception.Message.ShouldContain("onFailure não pode ser null");
    }

    // RES-TST-055: Match branch onSuccess executa com valor correto, não onFailure
    [Fact]
    public void Match_Should_Execute_OnSuccess_Branch_With_Correct_Value()
    {
        var result = Result.Success(42);
        var capturedValue = 0;
        var onFailureExecuted = false;

        var returnValue = result.Match(
            onSuccess: v => { capturedValue = v; return "success"; },
            onFailure: _ => { onFailureExecuted = true; return "failure"; }
        );

        capturedValue.ShouldBe(42);
        onFailureExecuted.ShouldBeFalse();
        returnValue.ShouldBe("success");
    }

    // RES-TST-056: Match branch onFailure recebe Error correto com identidade preservada
    [Fact]
    public void Match_Should_Execute_OnFailure_Branch_With_Correct_Error()
    {
        var result = Result.Failure<int>(ValidError);
        Error? capturedError = null;
        var onSuccessExecuted = false;

        var returnValue = result.Match(
            onSuccess: v => { onSuccessExecuted = true; return "success"; },
            onFailure: e => { capturedError = e; return "failure"; }
        );

        // Verificar que onFailure foi executado com Error correto
        onSuccessExecuted.ShouldBeFalse();
        capturedError.ShouldNotBeNull();

        // Verificar identidade de referência do Error
        ReferenceEquals(capturedError, ValidError).ShouldBeTrue();

        returnValue.ShouldBe("failure");
    }

    // RES-TST-060: Validação no construtor base: null error deve ser tratado
    [Fact]
    public void Protected_Constructor_With_Null_Error_Should_Preserve_Validation()
    {
        // Via factory, error null é validado
        var exception = Should.Throw<ArgumentNullException>(() => Result.Failure(null!));
        exception.ParamName.ShouldBe("error");
    }

    // RES-TST-061: Subclass constructor com error null deve lançar ArgumentNullException
    [Fact]
    public void Testable_Constructor_With_Null_Error_Should_Throw_ArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new TestableResult(true, null!)
        );

        exception.ParamName.ShouldBe("error");
        exception.Message.ShouldContain("Erro não pode ser null");
    }

    // RES-TST-061b: Construtor protegido base com IsSuccess=true mas Error!=Error.None deve lançar
    [Fact]
    public void Protected_Constructor_With_IsSuccess_True_But_Error_Not_None_Should_Throw()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            new TestableResult(true, ValidError)
        );

        exception.ParamName.ShouldBe("error");
        exception.Message.ShouldContain("Success deve estar associado a Error.None");
    }

    // RES-TST-062: Construtor protegido base com IsSuccess=false mas Error==Error.None deve lançar
    [Fact]
    public void Protected_Constructor_With_IsSuccess_False_But_Error_None_Should_Throw()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            new TestableResult(false, Error.None)
        );

        exception.ParamName.ShouldBe("error");
        exception.Message.ShouldContain("Failure não pode estar associado a Error.None");
    }


    #endregion

    #region Casos Extremos Adicionais

    // RES-TST-046
    [Fact]
    public void Match_Return_Type_Consistency_Different_Inputs()
    {
        var resultSuccess = Result.Success(42);
        var resultFailure = Result.Failure<int>(ValidError);

        var successOutput = resultSuccess.Match(
            onSuccess: v => v.ToString(),
            onFailure: _ => "ERROR"
        );

        var failureOutput = resultFailure.Match(
            onSuccess: v => v.ToString(),
            onFailure: _ => "ERROR"
        );

        successOutput.ShouldBe("42");
        failureOutput.ShouldBe("ERROR");
    }

    // RES-TST-047
    [Fact]
    public void ImplicitConversion_Sequence_Success_Then_Failure()
    {
        Result<int> r1 = 42;
        Result<int> r2 = ValidError;

        r1.IsSuccess.ShouldBeTrue();
        r1.Value.ShouldBe(42);

        r2.IsFailure.ShouldBeTrue();
        r2.Error.ShouldBe(ValidError);
    }

    // RES-TST-048: Failure must preserve Error reference identity
    [Fact]
    public void Failure_Error_Identity_Preserved()
    {
        var errorPassed = new Error("Test.Code", "Test description", ErrorType.Validation);
        var result = Result.Failure(errorPassed);

        // Verificar identidade de referência (mesma instância)
        ReferenceEquals(result.Error, errorPassed).ShouldBeTrue();

        // Verificar propriedades
        result.Error.Code.ShouldBe("Test.Code");
        result.Error.Description.ShouldBe("Test description");
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    // RES-TST-049: Success must preserve Value reference identity (using non-interned type)
    [Fact]
    public void Success_Value_Identity_Preserved()
    {
        // Usar TestReferenceValue para evitar interning de string
        var value = new TestReferenceValue("test value");
        var result = Result.Success(value);

        // Verificar identidade de referência (mesma instância)
        ReferenceEquals(result.Value, value).ShouldBeTrue();

        result.Value.ShouldBeOfType<TestReferenceValue>();
    }

    // RES-TST-050: Result<T> generic type parameter is not interchangeable across different T
    [Fact]
    public void Result_TValue_Generic_Type_Does_Not_Leak()
    {
        // Result<TestReferenceValue> e Result<Error> devem ser tipos distintos, não intercambiáveis
        var resultA = typeof(Result<TestReferenceValue>);
        var resultB = typeof(Result<Error>);

        // Result<TestReferenceValue> não pode ser atribuído a Result<Error>
        resultA.IsAssignableFrom(resultB).ShouldBeFalse();

        // Result<Error> não pode ser atribuído a Result<TestReferenceValue>
        resultB.IsAssignableFrom(resultA).ShouldBeFalse();

        // Ambos são tipos genéricos Result<T>
        resultA.Name.ShouldBe("Result`1");
        resultB.Name.ShouldBe("Result`1");

        // Argumentos de tipo são diferentes
        resultA.GetGenericArguments()[0].ShouldBe(typeof(TestReferenceValue));
        resultB.GetGenericArguments()[0].ShouldBe(typeof(Error));
    }

    #endregion
}

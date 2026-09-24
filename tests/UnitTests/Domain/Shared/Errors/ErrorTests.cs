using Business.Platform.Core.Domain.Shared;

namespace Business.Platform.Core.UnitTests.Domain.Shared.Errors;

public sealed class ErrorTests
{
    private const string ValidCode = "User.NotFound";
    private const string ValidDescription = "O usuário informado não foi encontrado.";

    // ERR-TST-001
    [Fact]
    public void Constructor_WithValidArguments_ShouldCreateError()
    {
        var error = new Error(ValidCode, ValidDescription, ErrorType.NotFound);

        error.Code.ShouldBe(ValidCode);
        error.Description.ShouldBe(ValidDescription);
        error.Type.ShouldBe(ErrorType.NotFound);
    }

    // ERR-TST-001 (todos os tipos definidos são aceitos)
    [Theory]
    [InlineData(ErrorType.Failure)]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Unauthorized)]
    [InlineData(ErrorType.Forbidden)]
    public void Constructor_WithEachDefinedErrorType_ShouldCreateError(ErrorType type)
    {
        var error = new Error(ValidCode, ValidDescription, type);

        error.Type.ShouldBe(type);
    }

    // ERR-TST-002
    [Fact]
    public void Constructor_WithoutExplicitType_ShouldUseFailure()
    {
        var error = new Error("Operation.Failed", "Não foi possível concluir a operação.");

        error.Type.ShouldBe(ErrorType.Failure);
    }

    // ERR-TST-003
    [Fact]
    public void Constructor_WithNullCode_ShouldThrowArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new Error(null!, ValidDescription));

        exception.ShouldBeOfType<ArgumentNullException>();
        exception.ParamName.ShouldBe("code");
    }

    // ERR-TST-004
    [Fact]
    public void Constructor_WithEmptyCode_ShouldThrowArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new Error(string.Empty, ValidDescription));

        exception.ShouldBeOfType<ArgumentException>();
        exception.ParamName.ShouldBe("code");
    }

    // ERR-TST-005
    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    public void Constructor_WithWhitespaceCode_ShouldThrowArgumentException(string code)
    {
        var exception = Should.Throw<ArgumentException>(
            () => new Error(code, ValidDescription));

        exception.ShouldBeOfType<ArgumentException>();
        exception.ParamName.ShouldBe("code");
    }

    // ERR-TST-006
    [Fact]
    public void Constructor_WithNullDescription_ShouldThrowArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new Error(ValidCode, null!));

        exception.ShouldBeOfType<ArgumentNullException>();
        exception.ParamName.ShouldBe("description");
    }

    // ERR-TST-007
    [Fact]
    public void Constructor_WithEmptyDescription_ShouldThrowArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new Error(ValidCode, string.Empty));

        exception.ShouldBeOfType<ArgumentException>();
        exception.ParamName.ShouldBe("description");
    }

    // ERR-TST-008
    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    public void Constructor_WithWhitespaceDescription_ShouldThrowArgumentException(string description)
    {
        var exception = Should.Throw<ArgumentException>(
            () => new Error(ValidCode, description));

        exception.ShouldBeOfType<ArgumentException>();
        exception.ParamName.ShouldBe("description");
    }

    // ERR-TST-009
    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(999)]
    public void Constructor_WithUndefinedErrorType_ShouldThrowArgumentOutOfRangeException(int value)
    {
        var invalidType = (ErrorType)value;

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new Error(ValidCode, ValidDescription, invalidType));

        exception.ShouldBeOfType<ArgumentOutOfRangeException>();
        exception.ParamName.ShouldBe("type");
        exception.ActualValue.ShouldBe(invalidType);
        exception.Message.ShouldStartWith("O ErrorType informado não é um valor definido.");
    }

    // ERR-TST-010
    [Fact]
    public void None_ShouldReturnCanonicalError()
    {
        Error.None.ShouldNotBeNull();
    }

    // ERR-TST-011
    [Fact]
    public void None_ShouldHaveEmptyCode()
    {
        Error.None.Code.ShouldBe(string.Empty);
    }

    // ERR-TST-012
    [Fact]
    public void None_ShouldHaveEmptyDescription()
    {
        Error.None.Description.ShouldBe(string.Empty);
    }

    // ERR-TST-013
    [Fact]
    public void None_ShouldHaveFailureType()
    {
        Error.None.Type.ShouldBe(ErrorType.Failure);
    }

    // ERR-TST-014
    [Fact]
    public void None_ShouldReturnSameInstance()
    {
        Error.None.ShouldBeSameAs(Error.None);
    }

    // ERR-TST-015
    [Fact]
    public void Errors_WithSameValues_ShouldBeEqual()
    {
        var first = new Error(ValidCode, ValidDescription, ErrorType.NotFound);
        var second = new Error(ValidCode, ValidDescription, ErrorType.NotFound);

        second.ShouldBe(first);
        (first == second).ShouldBeTrue();
        (first != second).ShouldBeFalse();
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    // ERR-TST-016
    [Fact]
    public void Errors_WithDifferentCode_ShouldNotBeEqual()
    {
        var first = new Error("User.NotFound", ValidDescription, ErrorType.NotFound);
        var second = new Error("Order.NotFound", ValidDescription, ErrorType.NotFound);

        second.ShouldNotBe(first);
        (first != second).ShouldBeTrue();
    }

    // ERR-TST-017
    [Fact]
    public void Errors_WithDifferentDescription_ShouldNotBeEqual()
    {
        var first = new Error(ValidCode, "Descrição A.", ErrorType.NotFound);
        var second = new Error(ValidCode, "Descrição B.", ErrorType.NotFound);

        second.ShouldNotBe(first);
        (first != second).ShouldBeTrue();
    }

    // ERR-TST-018
    [Fact]
    public void Errors_WithDifferentType_ShouldNotBeEqual()
    {
        var first = new Error(ValidCode, ValidDescription, ErrorType.NotFound);
        var second = new Error(ValidCode, ValidDescription, ErrorType.Conflict);

        second.ShouldNotBe(first);
        (first != second).ShouldBeTrue();
    }

    // RN-ERR-05: nenhum erro válido é equivalente a Error.None
    [Fact]
    public void ValidError_ShouldNotBeEqualToNone()
    {
        var error = new Error(ValidCode, ValidDescription);

        error.ShouldNotBe(Error.None);
    }
}

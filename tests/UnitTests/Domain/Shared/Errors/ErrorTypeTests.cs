using Business.Platform.Core.Domain.Shared;

namespace Business.Platform.Core.UnitTests.Domain.Shared.Errors;

public sealed class ErrorTypeTests
{
    // ERR-TST-020
    [Fact]
    public void ErrorType_ShouldContainExpectedValues()
    {
        string[] expected =
        [
            "Failure",
            "Validation",
            "NotFound",
            "Conflict",
            "Unauthorized",
            "Forbidden"
        ];

        Enum.GetNames<ErrorType>().ShouldBe(expected);
    }

    // ERR-TST-021
    [Theory]
    [InlineData(ErrorType.Failure, 0)]
    [InlineData(ErrorType.Validation, 1)]
    [InlineData(ErrorType.NotFound, 2)]
    [InlineData(ErrorType.Conflict, 3)]
    [InlineData(ErrorType.Unauthorized, 4)]
    [InlineData(ErrorType.Forbidden, 5)]
    public void ErrorType_ShouldHaveExpectedNumericValues(ErrorType type, int expected)
    {
        ((int)type).ShouldBe(expected);
    }
}

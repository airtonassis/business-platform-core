using Business.Platform.Core.Domain.Shared;

namespace Business.Platform.Core.UnitTests.Domain.Shared.Pagination;

public sealed class PaginationRequestTests
{
    private const string PageNumberCode = "Pagination.PageNumber.LessThanOne";
    private const string PageSizeMinCode = "Pagination.PageSize.LessThanOne";
    private const string PageSizeMaxCode = "Pagination.PageSize.ExceedsMaximum";

    [Fact]
    public void Constants_ShouldHaveApprovedValues()
    {
        PaginationRequest.DefaultPageSize.ShouldBe(20);
        PaginationRequest.MaxPageSize.ShouldBe(100);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Create_WithPageNumberLessThanOne_ShouldFail(int pageNumber)
    {
        var result = PaginationRequest.Create(pageNumber, 10);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(PageNumberCode);
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Description.ShouldBe("O número da página deve ser maior ou igual a 1.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Create_WithPageSizeLessThanOne_ShouldFail(int pageSize)
    {
        var result = PaginationRequest.Create(1, pageSize);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(PageSizeMinCode);
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Description.ShouldBe("O tamanho da página deve ser maior ou igual a 1.");
    }

    [Theory]
    [InlineData(101)]
    [InlineData(1000)]
    [InlineData(int.MaxValue)]
    public void Create_WithPageSizeAboveMaximum_ShouldFail(int pageSize)
    {
        var result = PaginationRequest.Create(1, pageSize);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(PageSizeMaxCode);
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Description.ShouldBe("O tamanho da página excede o máximo permitido de 100.");
    }

    [Fact]
    public void Errors_ShouldNotIncludeReceivedValues()
    {
        PaginationRequest.Create(-987, 10).Error.Description.ShouldNotContain("987");
        PaginationRequest.Create(1, -654).Error.Description.ShouldNotContain("654");
        PaginationRequest.Create(1, 321).Error.Description.ShouldNotContain("321");
    }

    [Theory]
    [InlineData(0, 0, PageNumberCode)]
    [InlineData(0, 101, PageNumberCode)]
    [InlineData(int.MinValue, int.MinValue, PageNumberCode)]
    [InlineData(1, 0, PageSizeMinCode)]
    [InlineData(5, int.MinValue, PageSizeMinCode)]
    [InlineData(1, 101, PageSizeMaxCode)]
    public void Create_ShouldReturnOnlyFirstErrorByPrecedence(int pageNumber, int pageSize, string expectedCode)
    {
        var result = PaginationRequest.Create(pageNumber, pageSize);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(expectedCode);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(int.MaxValue, 100)]
    [InlineData(3, 20)]
    public void Create_WithValidValues_ShouldSucceed(int pageNumber, int pageSize)
    {
        var result = PaginationRequest.Create(pageNumber, pageSize);

        result.IsSuccess.ShouldBeTrue();
        result.Error.ShouldBe(Error.None);
        result.Value.PageNumber.ShouldBe(pageNumber);
        result.Value.PageSize.ShouldBe(pageSize);
    }

    [Fact]
    public void Create_WithoutPageSize_ShouldUseDefaultPageSize()
    {
        var result = PaginationRequest.Create(2);

        result.IsSuccess.ShouldBeTrue();
        result.Value.PageSize.ShouldBe(20);
        result.Value.Offset.ShouldBe(20);
    }

    [Theory]
    [InlineData(1, 1, 0L)]
    [InlineData(1, 100, 0L)]
    [InlineData(2, 1, 1L)]
    [InlineData(2, 20, 20L)]
    [InlineData(5, 10, 40L)]
    [InlineData(int.MaxValue, 100, 214748364600L)]
    [InlineData(int.MaxValue, 1, 2147483646L)]
    public void Offset_ShouldBeComputedWithoutOverflow(int pageNumber, int pageSize, long expected)
    {
        PaginationRequest.Create(pageNumber, pageSize).Value.Offset.ShouldBe(expected);
    }

    [Fact]
    public void Default_ShouldBeSingleInstanceEquivalentToCreateOneTwenty()
    {
        PaginationRequest.Default.ShouldBeSameAs(PaginationRequest.Default);
        PaginationRequest.Default.PageNumber.ShouldBe(1);
        PaginationRequest.Default.PageSize.ShouldBe(20);
        PaginationRequest.Default.Offset.ShouldBe(0);
        PaginationRequest.Default.ShouldBe(PaginationRequest.Create(1, 20).Value);
    }

    [Fact]
    public void Equals_ShouldCompareByValue()
    {
        var a = PaginationRequest.Create(2, 10).Value;
        var same = PaginationRequest.Create(2, 10).Value;
        var otherPage = PaginationRequest.Create(3, 10).Value;
        var otherSize = PaginationRequest.Create(2, 11).Value;

        a.ShouldNotBeSameAs(same);
        a.Equals(same).ShouldBeTrue();
        a.Equals((object)same).ShouldBeTrue();
        a.Equals(otherPage).ShouldBeFalse();
        a.Equals(otherSize).ShouldBeFalse();
        a.Equals((object)otherPage).ShouldBeFalse();
        a.Equals(a).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithNullOrOtherType_ShouldBeFalse()
    {
        var a = PaginationRequest.Create(2, 10).Value;

        a.Equals((PaginationRequest?)null).ShouldBeFalse();
        a.Equals((object?)null).ShouldBeFalse();
        a.Equals("2,10").ShouldBeFalse();
        a.Equals((object)2).ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_ShouldBeConsistentWithEquality()
    {
        var a = PaginationRequest.Create(2, 10).Value;
        var same = PaginationRequest.Create(2, 10).Value;

        a.GetHashCode().ShouldBe(same.GetHashCode());
        a.GetHashCode().ShouldBe(a.GetHashCode());
        a.GetHashCode().ShouldBe(HashCode.Combine(2, 10));
        a.GetHashCode().ShouldNotBe(PaginationRequest.Create(10, 2).Value.GetHashCode());
    }

    [Fact]
    public void Operators_ShouldCompareByValue()
    {
        var a = PaginationRequest.Create(2, 10).Value;
        var same = PaginationRequest.Create(2, 10).Value;
        var other = PaginationRequest.Create(2, 11).Value;

        (a == same).ShouldBeTrue();
        (a != same).ShouldBeFalse();
        (a == other).ShouldBeFalse();
        (a != other).ShouldBeTrue();
    }

    [Fact]
    public void Operators_WithNull_ShouldHandleNullReferences()
    {
        var a = PaginationRequest.Create(2, 10).Value;
        PaginationRequest? none = null;
        PaginationRequest? alsoNone = null;

        (none == alsoNone).ShouldBeTrue();
        (none != alsoNone).ShouldBeFalse();
        (a == none).ShouldBeFalse();
        (none == a).ShouldBeFalse();
        (a != none).ShouldBeTrue();
        (none != a).ShouldBeTrue();
    }
}

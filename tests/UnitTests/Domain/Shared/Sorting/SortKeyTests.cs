using SortDirection = Business.Platform.Core.Domain.Shared.SortDirection;
using Business.Platform.Core.Domain.Shared;

namespace Business.Platform.Core.UnitTests.Domain.Shared.Sorting;

public sealed class SortKeyTests
{
    [Fact]
    public void Create_WithDirectPropertySelector_ShouldSucceedWithDefaultAscending()
    {
        var result = SortKey<SortingFixtures.Order>.Create(o => o.Name);

        result.IsSuccess.ShouldBeTrue();
        result.Value.MemberName.ShouldBe("Name");
        result.Value.KeyType.ShouldBe(typeof(string));
        result.Value.Direction.ShouldBe(SortDirection.Ascending);
        result.Value.Selector.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(SortDirection.Ascending)]
    [InlineData(SortDirection.Descending)]
    public void Create_ShouldPreserveDirection(SortDirection direction)
    {
        var result = SortKey<SortingFixtures.Order>.Create(o => o.Amount, direction);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Direction.ShouldBe(direction);
        result.Value.KeyType.ShouldBe(typeof(decimal));
    }

    [Fact]
    public void Create_ShouldAcceptValueTypeAndStronglyTypedIdentifierProperties()
    {
        SortKey<SortingFixtures.Order>.Create(o => o.Number).IsSuccess.ShouldBeTrue();
        SortKey<SortingFixtures.Customer>.Create(c => c.Id).Value.KeyType.ShouldBe(typeof(SortingFixtures.CustomerId));
    }

    [Fact]
    public void Create_WithNullSelector_ShouldFailWithValidationError()
    {
        var result = SortKey<SortingFixtures.Order>.Create<string>(null);

        AssertInvalidSelector(result);
    }

    [Fact]
    public void Create_WithInvalidDirection_ShouldFail()
    {
        var result = SortKey<SortingFixtures.Order>.Create(o => o.Name, (SortDirection)99);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Sorting.Direction.Invalid");
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void Create_WithInvalidSelectorAndInvalidDirection_ShouldReportSelectorFirst()
    {
        var result = SortKey<SortingFixtures.Order>.Create(o => o.Name.Length, (SortDirection)99);

        result.Error.Code.ShouldBe("Sorting.Selector.Invalid");
    }

    [Fact]
    public void Create_WithConversionSelector_ShouldFail()
    {
        // O compilador insere Convert ao boxear tipo valor para object; o nó Convert não é acesso direto
        AssertInvalidSelector(SortKey<SortingFixtures.Order>.Create<object>(o => o.Number));
        AssertInvalidSelector(SortKey<SortingFixtures.Order>.Create<long>(o => o.Number));
    }

    [Fact]
    public void Create_WithReferenceUpcastSelector_ShouldKeepPersistedPropertyType()
    {
        // Conversão de referência não gera nó Convert: o seletor continua sendo acesso direto à propriedade
        var result = SortKey<SortingFixtures.Order>.Create<object>(o => o.Name);

        result.IsSuccess.ShouldBeTrue();
        result.Value.KeyType.ShouldBe(typeof(string));
    }

    [Fact]
    public void Create_WithMethodCallSelector_ShouldFail() =>
        AssertInvalidSelector(SortKey<SortingFixtures.Order>.Create(o => o.Name.ToUpperInvariant()));

    [Fact]
    public void Create_WithChainedMemberSelector_ShouldFail() =>
        AssertInvalidSelector(SortKey<SortingFixtures.Order>.Create(o => o.Name.Length));

    [Fact]
    public void Create_WithNestedObjectSelector_ShouldFail() =>
        AssertInvalidSelector(SortKey<SortingFixtures.Order>.Create(o => o.Address.City));

    [Fact]
    public void Create_WithFieldSelector_ShouldFail() =>
        AssertInvalidSelector(SortKey<SortingFixtures.Order>.Create(o => o.Field));

    [Fact]
    public void Create_WithConstantSelector_ShouldFail() =>
        AssertInvalidSelector(SortKey<SortingFixtures.Order>.Create(_ => 1));

    [Fact]
    public void Create_WithClosureSelector_ShouldFail()
    {
        var captured = new SortingFixtures.Order(Guid.NewGuid());

        AssertInvalidSelector(SortKey<SortingFixtures.Order>.Create(_ => captured.Name));
    }

    [Fact]
    public void Create_WithStaticPropertySelector_ShouldFail() =>
        AssertInvalidSelector(SortKey<SortingFixtures.Order>.Create(_ => SortingFixtures.Order.StaticName));

    [Fact]
    public void Create_WithIndexerSelector_ShouldFail() =>
        AssertInvalidSelector(SortKey<SortingFixtures.Order>.Create(o => o.Tags[0]));

    [Fact]
    public void Equality_ShouldBeStructuralOverMemberAndDirection()
    {
        var first = SortKey<SortingFixtures.Order>.Create(o => o.Name).Value;
        var same = SortKey<SortingFixtures.Order>.Create(o => o.Name).Value;
        var otherDirection = SortKey<SortingFixtures.Order>.Create(o => o.Name, SortDirection.Descending).Value;
        var otherMember = SortKey<SortingFixtures.Order>.Create(o => o.Amount).Value;

        first.Equals(same).ShouldBeTrue();
        (first == same).ShouldBeTrue();
        first.Equals((object)same).ShouldBeTrue();
        first.GetHashCode().ShouldBe(same.GetHashCode());
        (first != otherDirection).ShouldBeTrue();
        (first != otherMember).ShouldBeTrue();
        first.Equals(null).ShouldBeFalse();
        first.Equals("Name").ShouldBeFalse();
        ((SortKey<SortingFixtures.Order>?)null == null).ShouldBeTrue();
        (first == null).ShouldBeFalse();
    }

    [Fact]
    public void ErrorCodes_ShouldBeStable()
    {
        SortingErrorCodes.SelectorInvalid.ShouldBe("Sorting.Selector.Invalid");
        SortingErrorCodes.DirectionInvalid.ShouldBe("Sorting.Direction.Invalid");
        SortingErrorCodes.KeysEmpty.ShouldBe("Sorting.Keys.Empty");
        SortingErrorCodes.KeysExceedMaximum.ShouldBe("Sorting.Keys.ExceedsMaximum");
        SortingErrorCodes.KeyNull.ShouldBe("Sorting.Key.Null");
        SortingErrorCodes.KeyDuplicated.ShouldBe("Sorting.Key.Duplicated");
        SortingErrorCodes.IdentifierNotLast.ShouldBe("Sorting.Identifier.NotLast");
    }

    [Fact]
    public void SortDirection_ShouldHaveStableValues()
    {
        ((int)SortDirection.Ascending).ShouldBe(0);
        ((int)SortDirection.Descending).ShouldBe(1);
        Enum.GetValues<SortDirection>().Length.ShouldBe(2);
    }

    private static void AssertInvalidSelector(Result<SortKey<SortingFixtures.Order>> result)
    {
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Sorting.Selector.Invalid");
        result.Error.Type.ShouldBe(ErrorType.Validation);
    }
}

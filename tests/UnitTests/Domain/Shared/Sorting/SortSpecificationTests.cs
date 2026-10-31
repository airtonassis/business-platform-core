using SortDirection = Business.Platform.Core.Domain.Shared.SortDirection;
using Business.Platform.Core.Domain.Shared;
using static Business.Platform.Core.UnitTests.Domain.Shared.Sorting.SortingFixtures;

namespace Business.Platform.Core.UnitTests.Domain.Shared.Sorting;

public sealed class SortSpecificationTests
{
    private static SortKey<Order> Name(SortDirection direction = SortDirection.Ascending) =>
        SortKey<Order>.Create(o => o.Name, direction).Value;

    private static SortKey<Order> Amount(SortDirection direction = SortDirection.Ascending) =>
        SortKey<Order>.Create(o => o.Amount, direction).Value;

    private static SortKey<Order> Number() => SortKey<Order>.Create(o => o.Number).Value;

    private static SortKey<Order> Created() => SortKey<Order>.Create(o => o.Created).Value;

    private static SortKey<Order> Id(SortDirection direction = SortDirection.Ascending) =>
        SortKey<Order>.Create(o => o.Id, direction).Value;

    private static Result<SortSpecification<Order, Guid>> Create(params SortKey<Order>?[] keys) =>
        SortSpecification<Order, Guid>.Create(keys);

    [Fact]
    public void Constants_ShouldHaveApprovedValues()
    {
        SortSpecification<Order, Guid>.MaxRequestedKeys.ShouldBe(4);
        SortSpecification<Order, Guid>.MaxTotalOrderKeys.ShouldBe(5);
    }

    [Fact]
    public void Create_WithSingleKey_ShouldAppendIdentifierAscending()
    {
        var result = Create(Name(SortDirection.Descending));

        result.IsSuccess.ShouldBeTrue();
        result.Value.RequestedKeys.Count.ShouldBe(1);
        result.Value.TotalOrder.Select(k => (k.MemberName, k.Direction))
            .ShouldBe([("Name", SortDirection.Descending), ("Id", SortDirection.Ascending)]);
        result.Value.TotalOrder[^1].KeyType.ShouldBe(typeof(Guid));
    }

    [Fact]
    public void Create_WithExplicitIdentifierAsLast_ShouldNotAppendAgainAndKeepDirection()
    {
        var result = Create(Name(), Id(SortDirection.Descending));

        result.IsSuccess.ShouldBeTrue();
        result.Value.RequestedKeys.Count.ShouldBe(2);
        result.Value.TotalOrder.Select(k => (k.MemberName, k.Direction))
            .ShouldBe([("Name", SortDirection.Ascending), ("Id", SortDirection.Descending)]);
    }

    [Fact]
    public void Create_WithOnlyExplicitIdentifier_ShouldSucceed()
    {
        var result = Create(Id());

        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalOrder.Count.ShouldBe(1);
    }

    [Fact]
    public void Create_WithExplicitIdentifierNotLast_ShouldFail()
    {
        var result = Create(Id(), Name());

        AssertFailure(result, "Sorting.Identifier.NotLast");
    }

    [Fact]
    public void Create_WithFourRequestedKeysWithoutIdentifier_ShouldProduceFiveKeysInTotalOrder()
    {
        var result = Create(Name(), Amount(), Number(), Created());

        result.IsSuccess.ShouldBeTrue();
        result.Value.RequestedKeys.Count.ShouldBe(4);
        result.Value.TotalOrder.Count.ShouldBe(5);
        result.Value.TotalOrder[^1].MemberName.ShouldBe("Id");
    }

    [Fact]
    public void Create_WithFourRequestedKeysIncludingExplicitIdentifier_ShouldProduceFourKeys()
    {
        var result = Create(Name(), Amount(), Number(), Id());

        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalOrder.Count.ShouldBe(4);
    }

    [Fact]
    public void Create_WithFiveRequestedKeys_ShouldFail()
    {
        var result = Create(Name(), Amount(), Number(), Created(), Id());

        AssertFailure(result, "Sorting.Keys.ExceedsMaximum");
    }

    [Fact]
    public void Create_WithLazySequenceOverLimit_ShouldNotEnumerateBeyondFirstExcess()
    {
        var produced = 0;

        IEnumerable<SortKey<Order>?> Infinite()
        {
            while (true)
            {
                produced++;
                yield return Name();
            }
        }

        var result = SortSpecification<Order, Guid>.Create(Infinite());

        AssertFailure(result, "Sorting.Keys.ExceedsMaximum");
        produced.ShouldBe(5);
    }

    [Fact]
    public void Create_WithNullSequence_ShouldFailAsEmpty() =>
        AssertFailure(SortSpecification<Order, Guid>.Create(null), "Sorting.Keys.Empty");

    [Fact]
    public void Create_WithEmptySequence_ShouldFail() =>
        AssertFailure(Create(), "Sorting.Keys.Empty");

    [Fact]
    public void Create_WithNullKey_ShouldFail() =>
        AssertFailure(Create(Name(), null), "Sorting.Key.Null");

    [Fact]
    public void Create_WithDuplicatedMember_ShouldFailEvenWithDifferentDirections()
    {
        AssertFailure(Create(Name(), Name()), "Sorting.Key.Duplicated");
        AssertFailure(Create(Name(), Name(SortDirection.Descending)), "Sorting.Key.Duplicated");
        AssertFailure(Create(Id(), Id(SortDirection.Descending)), "Sorting.Key.Duplicated");
    }

    [Fact]
    public void Create_WithSeveralProblems_ShouldReportFirstInDocumentedOrder()
    {
        AssertFailure(Create(Name(), Name(), null), "Sorting.Key.Null");
        AssertFailure(Create(Id(), Name(), Name()), "Sorting.Key.Duplicated");
    }

    [Fact]
    public void Create_ShouldPreserveRequestedOrder()
    {
        var result = Create(Amount(SortDirection.Descending), Name());

        result.Value.RequestedKeys.Select(k => k.MemberName).ShouldBe(["Amount", "Name"]);
        result.Value.TotalOrder.Select(k => k.MemberName).ShouldBe(["Amount", "Name", "Id"]);
    }

    [Fact]
    public void Create_ShouldSupportStronglyTypedIdentifierWithoutComparable()
    {
        typeof(IComparable<SortingFixtures.CustomerId>).IsAssignableFrom(typeof(SortingFixtures.CustomerId)).ShouldBeFalse();

        var name = SortKey<Customer>.Create(c => c.Name).Value;
        var result = SortSpecification<Customer, CustomerId>.Create([name]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalOrder[^1].KeyType.ShouldBe(typeof(CustomerId));
        result.Value.TotalOrder[^1].MemberName.ShouldBe("Id");
    }

    [Fact]
    public void Create_WithExplicitStronglyTypedIdentifier_ShouldBeRecognized()
    {
        var id = SortKey<Customer>.Create(c => c.Id, SortDirection.Descending).Value;
        var result = SortSpecification<Customer, CustomerId>.Create([id]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalOrder.Count.ShouldBe(1);
    }

    [Fact]
    public void Create_ShouldCopyInputAndExposeReadOnlyCollections()
    {
        var keys = new List<SortKey<Order>?> { Name() };
        var specification = SortSpecification<Order, Guid>.Create(keys).Value;

        keys.Add(Amount());

        specification.RequestedKeys.Count.ShouldBe(1);
        specification.TotalOrder.Count.ShouldBe(2);
        (specification.RequestedKeys is IList<SortKey<Order>> requested && requested.IsReadOnly).ShouldBeTrue();
        (specification.TotalOrder is IList<SortKey<Order>> total && total.IsReadOnly).ShouldBeTrue();
        Should.Throw<NotSupportedException>(() => ((IList<SortKey<Order>>)specification.TotalOrder).Add(Amount()));
    }

    [Fact]
    public void Equality_ShouldBeStructuralOverTotalOrder()
    {
        var first = Create(Name(), Amount(SortDirection.Descending)).Value;
        var same = Create(Name(), Amount(SortDirection.Descending)).Value;
        var otherDirection = Create(Name(), Amount()).Value;
        var otherOrder = Create(Amount(SortDirection.Descending), Name()).Value;
        var otherLength = Create(Name()).Value;

        first.Equals(same).ShouldBeTrue();
        (first == same).ShouldBeTrue();
        first.Equals((object)same).ShouldBeTrue();
        first.GetHashCode().ShouldBe(same.GetHashCode());
        (first != otherDirection).ShouldBeTrue();
        (first != otherOrder).ShouldBeTrue();
        (first != otherLength).ShouldBeTrue();
        first.Equals(null).ShouldBeFalse();
        first.Equals("x").ShouldBeFalse();
        (first == null).ShouldBeFalse();
        ((SortSpecification<Order, Guid>?)null == null).ShouldBeTrue();
    }

    [Fact]
    public void Equality_ShouldTreatImplicitAndExplicitAscendingIdentifierAsSameOrdering()
    {
        var implicitId = Create(Name()).Value;
        var explicitId = Create(Name(), Id()).Value;

        implicitId.ShouldBe(explicitId);
        implicitId.GetHashCode().ShouldBe(explicitId.GetHashCode());
        implicitId.RequestedKeys.Count.ShouldNotBe(explicitId.RequestedKeys.Count);
    }

    private static void AssertFailure(Result<SortSpecification<Order, Guid>> result, string code)
    {
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(code);
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Description.ShouldNotBeNullOrWhiteSpace();
    }
}

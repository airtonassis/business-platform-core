using Business.Platform.Core.Domain.Shared;

namespace Business.Platform.Core.UnitTests.Domain.Shared.AggregateRoots;

public sealed class AggregateRootTests
{
    private static readonly Guid SampleGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // Construção
    [Fact]
    public void Constructor_WithGuid_ShouldSetId()
    {
        new OrderAggregate(SampleGuid).Id.ShouldBe(SampleGuid);
    }

    [Fact]
    public void Constructor_WithInt_ShouldSetId()
    {
        new TestAggregate<int>(42).Id.ShouldBe(42);
    }

    [Fact]
    public void Constructor_WithLong_ShouldSetId()
    {
        new TestAggregate<long>(123L).Id.ShouldBe(123L);
    }

    [Fact]
    public void Constructor_WithString_ShouldSetId()
    {
        new TestAggregate<string>("abc").Id.ShouldBe("abc");
    }

    [Fact]
    public void Constructor_WithCustomIdentifier_ShouldSetId()
    {
        var id = new CustomId(7);

        new TestAggregate<CustomId>(id).Id.ShouldBe(id);
    }

    [Fact]
    public void Constructor_WithNullId_ShouldPropagateArgumentNullExceptionFromEntity()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new TestAggregate<string>(null!));

        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void Constructor_WithDefaultIdentifiers_ShouldBeAcceptedWithoutExtraValidation()
    {
        new OrderAggregate(Guid.Empty).Id.ShouldBe(Guid.Empty);
        new TestAggregate<int>(0).Id.ShouldBe(0);
        new TestAggregate<string>(string.Empty).Id.ShouldBe(string.Empty);
    }

    // Herança
    [Fact]
    public void Aggregate_ShouldBeAssignableToEntityAndEquatable()
    {
        var aggregate = new OrderAggregate(SampleGuid);

        aggregate.ShouldBeAssignableTo<Entity<Guid>>();
        aggregate.ShouldBeAssignableTo<IEquatable<Entity<Guid>>>();
        aggregate.ShouldBeAssignableTo<AggregateRoot<Guid>>();
    }

    // Igualdade herdada
    [Fact]
    public void Equality_SameTypeSameId_ShouldBeEqualIgnoringState()
    {
        var first = new OrderAggregate(SampleGuid, "first");
        var second = new OrderAggregate(SampleGuid, "second");

        first.Equals(second).ShouldBeTrue();
        first.Equals((object)second).ShouldBeTrue();
        (first == second).ShouldBeTrue();
        (first != second).ShouldBeFalse();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equality_SameTypeDifferentId_ShouldNotBeEqual()
    {
        var first = new OrderAggregate(SampleGuid);
        var second = new OrderAggregate(Guid.NewGuid());

        first.Equals(second).ShouldBeFalse();
        (first == second).ShouldBeFalse();
        (first != second).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentConcreteTypesWithSameId_ShouldNotBeEqual()
    {
        var order = new OrderAggregate(SampleGuid);
        var invoice = new InvoiceAggregate(SampleGuid);

        order.Equals(invoice).ShouldBeFalse();
        order.Equals((object)invoice).ShouldBeFalse();
        (order == invoice).ShouldBeFalse();
        (order != invoice).ShouldBeTrue();
    }

    [Fact]
    public void Equality_AggregateAndPlainEntityWithSameId_ShouldNotBeEqual()
    {
        var aggregate = new OrderAggregate(SampleGuid);
        var entity = new PlainEntity(SampleGuid);

        aggregate.Equals(entity).ShouldBeFalse();
        entity.Equals(aggregate).ShouldBeFalse();
    }

    [Fact]
    public void Equality_NullHandling_ShouldFollowEntityContract()
    {
        var aggregate = new OrderAggregate(SampleGuid);

        aggregate.Equals(null).ShouldBeFalse();
        (aggregate == null).ShouldBeFalse();
        (null == aggregate).ShouldBeFalse();
        ((OrderAggregate?)null == (OrderAggregate?)null).ShouldBeTrue();
    }

    [Fact]
    public void Equality_WithDifferentGenericIdentifiers_ShouldCompareByValue()
    {
        new TestAggregate<int>(5).Equals(new TestAggregate<int>(5)).ShouldBeTrue();
        new TestAggregate<int>(5).Equals(new TestAggregate<int>(6)).ShouldBeFalse();
        new TestAggregate<long>(5L).Equals(new TestAggregate<long>(5L)).ShouldBeTrue();
        new TestAggregate<string>("a" + "b").Equals(new TestAggregate<string>("ab")).ShouldBeTrue();
        new TestAggregate<CustomId>(new CustomId(1)).Equals(new TestAggregate<CustomId>(new CustomId(1))).ShouldBeTrue();
    }

    // Coleções
    [Fact]
    public void HashSet_ShouldDeduplicateAggregatesByTypeAndId()
    {
        var set = new HashSet<OrderAggregate>
        {
            new(SampleGuid, "first"),
            new(SampleGuid, "second"),
            new(Guid.NewGuid())
        };

        set.Count.ShouldBe(2);
    }

    [Fact]
    public void HashSet_WithDifferentConcreteTypesAndSameId_ShouldKeepBoth()
    {
        var set = new HashSet<AggregateRoot<Guid>>
        {
            new OrderAggregate(SampleGuid),
            new InvoiceAggregate(SampleGuid)
        };

        set.Count.ShouldBe(2);
    }

    [Fact]
    public void Dictionary_ShouldFindAggregateByEquivalentInstance()
    {
        var dictionary = new Dictionary<OrderAggregate, string>
        {
            [new OrderAggregate(SampleGuid, "stored")] = "value"
        };

        dictionary[new OrderAggregate(SampleGuid, "lookup")].ShouldBe("value");
    }

    [Fact]
    public void Linq_ShouldRespectAggregateEquality()
    {
        OrderAggregate[] aggregates = [new(SampleGuid), new(Guid.NewGuid()), new(SampleGuid)];

        aggregates.Distinct().Count().ShouldBe(2);
        aggregates.Contains(new OrderAggregate(SampleGuid, "other")).ShouldBeTrue();
    }

    private sealed class TestAggregate<TId>(TId id) : AggregateRoot<TId>(id)
        where TId : notnull;

    private sealed class OrderAggregate(Guid id, string name = "") : AggregateRoot<Guid>(id)
    {
        public string Name { get; } = name;
    }

    private sealed class InvoiceAggregate(Guid id) : AggregateRoot<Guid>(id);

    private sealed class PlainEntity(Guid id) : Entity<Guid>(id);

    private readonly record struct CustomId(int Value);
}

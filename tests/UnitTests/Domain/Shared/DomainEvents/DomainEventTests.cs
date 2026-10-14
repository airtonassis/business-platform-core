using Business.Platform.Core.Domain.Shared;

namespace Business.Platform.Core.UnitTests.Domain.Shared.DomainEvents;

public sealed class DomainEventTests
{
    private static readonly Guid SampleEventId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset SampleUtc = new(2026, 10, 13, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ConcreteEvent_ShouldImplementInterface()
    {
        IDomainEvent domainEvent = new SimpleEvent(SampleEventId, SampleUtc);

        domainEvent.ShouldBeAssignableTo<IDomainEvent>();
    }

    [Fact]
    public void EventId_ShouldBePreserved()
    {
        IDomainEvent domainEvent = new SimpleEvent(SampleEventId, SampleUtc);

        domainEvent.EventId.ShouldBe(SampleEventId);
    }

    [Fact]
    public void OccurredOnUtc_ShouldBePreserved()
    {
        IDomainEvent domainEvent = new SimpleEvent(SampleEventId, SampleUtc);

        domainEvent.OccurredOnUtc.ShouldBe(SampleUtc);
    }

    [Fact]
    public void OccurredOnUtc_WithUtcOffset_ShouldKeepZeroOffset()
    {
        IDomainEvent domainEvent = new SimpleEvent(SampleEventId, SampleUtc);

        domainEvent.OccurredOnUtc.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void EventId_ShouldAcceptAnyGuidWithoutInterfaceValidation()
    {
        IDomainEvent domainEvent = new SimpleEvent(Guid.Empty, SampleUtc);

        domainEvent.EventId.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void OccurredOnUtc_ShouldNotBeValidatedByInterface_WhenOffsetIsNotUtc()
    {
        var local = new DateTimeOffset(2026, 10, 13, 11, 30, 0, TimeSpan.FromHours(-3));

        IDomainEvent domainEvent = new SimpleEvent(SampleEventId, local);

        domainEvent.OccurredOnUtc.Offset.ShouldBe(TimeSpan.FromHours(-3));
        domainEvent.OccurredOnUtc.UtcDateTime.ShouldBe(SampleUtc.UtcDateTime);
    }

    [Fact]
    public void Properties_ShouldUseGuidAndDateTimeOffset()
    {
        IDomainEvent domainEvent = new SimpleEvent(SampleEventId, SampleUtc);

        domainEvent.EventId.ShouldBeOfType<Guid>();
        domainEvent.OccurredOnUtc.ShouldBeOfType<DateTimeOffset>();
    }

    [Fact]
    public void ConcreteEvent_ShouldCarryAdditionalDomainData()
    {
        var domainEvent = new OrderPlacedEvent(SampleEventId, SampleUtc, Guid.Parse("33333333-3333-3333-3333-333333333333"), 150m);

        IDomainEvent contract = domainEvent;

        contract.EventId.ShouldBe(SampleEventId);
        contract.OccurredOnUtc.ShouldBe(SampleUtc);
        domainEvent.OrderId.ShouldBe(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        domainEvent.Total.ShouldBe(150m);
    }

    [Fact]
    public void ConcreteEvents_ShouldSupportAggregateIdentifiersOfDistinctTypes()
    {
        var guidEvent = new AggregateEvent<Guid>(SampleEventId, SampleUtc, SampleGuidId);
        var intEvent = new AggregateEvent<int>(SampleEventId, SampleUtc, 42);
        var stringEvent = new AggregateEvent<string>(SampleEventId, SampleUtc, "INV-1");
        var customEvent = new AggregateEvent<CustomId>(SampleEventId, SampleUtc, new CustomId(7));

        guidEvent.AggregateId.ShouldBe(SampleGuidId);
        intEvent.AggregateId.ShouldBe(42);
        stringEvent.AggregateId.ShouldBe("INV-1");
        customEvent.AggregateId.ShouldBe(new CustomId(7));
        new IDomainEvent[] { guidEvent, intEvent, stringEvent, customEvent }
            .ShouldAllBe(e => e.EventId == SampleEventId && e.OccurredOnUtc == SampleUtc);
    }

    [Fact]
    public void Event_ShouldBeIndependentFromAggregateRoot()
    {
        IDomainEvent domainEvent = new SimpleEvent(SampleEventId, SampleUtc);

        domainEvent.ShouldNotBeAssignableTo<AggregateRoot<Guid>>();
        domainEvent.ShouldNotBeAssignableTo<Entity<Guid>>();
    }

    [Fact]
    public void AggregateRoot_ShouldNotBeADomainEvent_AndExistsWithoutEvents()
    {
        var aggregate = new PlainAggregate(SampleGuidId);

        aggregate.ShouldNotBeAssignableTo<IDomainEvent>();
        aggregate.Id.ShouldBe(SampleGuidId);
    }

    [Fact]
    public void EventsInDifferentInstances_ShouldKeepTheirOwnValues()
    {
        IDomainEvent first = new SimpleEvent(Guid.NewGuid(), SampleUtc);
        IDomainEvent second = new SimpleEvent(Guid.NewGuid(), SampleUtc.AddMinutes(1));

        first.EventId.ShouldNotBe(second.EventId);
        first.OccurredOnUtc.ShouldBeLessThan(second.OccurredOnUtc);
    }

    [Fact]
    public void Events_ShouldBeUsableAsHeterogeneousCollection()
    {
        List<IDomainEvent> events =
        [
            new SimpleEvent(SampleEventId, SampleUtc),
            new OrderPlacedEvent(Guid.NewGuid(), SampleUtc, SampleGuidId, 10m)
        ];

        events.Select(e => e.EventId).Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public void Event_ShouldBeUsableAsResultValue()
    {
        var result = Result.Success<IDomainEvent>(new SimpleEvent(SampleEventId, SampleUtc));

        result.IsSuccess.ShouldBeTrue();
        result.Value.EventId.ShouldBe(SampleEventId);
    }

    private static readonly Guid SampleGuidId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private sealed record SimpleEvent(Guid EventId, DateTimeOffset OccurredOnUtc) : IDomainEvent;

    private sealed record OrderPlacedEvent(Guid EventId, DateTimeOffset OccurredOnUtc, Guid OrderId, decimal Total) : IDomainEvent;

    private sealed record AggregateEvent<TId>(Guid EventId, DateTimeOffset OccurredOnUtc, TId AggregateId) : IDomainEvent
        where TId : notnull;

    private readonly record struct CustomId(int Value);

    private sealed class PlainAggregate(Guid id) : AggregateRoot<Guid>(id);
}

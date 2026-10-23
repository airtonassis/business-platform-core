using Business.Platform.Core.Domain.Shared;

namespace Business.Platform.Core.UnitTests.Domain.Shared.Time;

public sealed class TimeProviderCompatibilityTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly AuditActor Alice = AuditActor.Create(AuditActorKind.User, "alice").Value;
    private static readonly AuditActor Bob = AuditActor.Create(AuditActorKind.User, "bob").Value;

    [Fact]
    public void Create_WithInstantFromFakeClock_ShouldSucceedWithExactUtcInstant()
    {
        var clock = new FakeTimeProvider(T0);

        var result = AuditMetadata.Create(Alice, clock.GetUtcNow());

        result.IsSuccess.ShouldBeTrue();
        result.Value.CreatedAt.ShouldBe(T0);
        result.Value.CreatedAt.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void WithUpdate_AfterClockAdvance_ShouldSucceedAndPreserveCreatedAt()
    {
        var clock = new FakeTimeProvider(T0);
        var created = AuditMetadata.Create(Alice, clock.GetUtcNow()).Value;

        clock.Advance(TimeSpan.FromMinutes(1));
        var result = created.WithUpdate(Bob, clock.GetUtcNow());

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeSameAs(created);
        result.Value.CreatedBy.ShouldBeSameAs(Alice);
        result.Value.CreatedAt.ShouldBe(T0);
        result.Value.UpdatedAt.ShouldBe(T0.AddMinutes(1));
        result.Value.UpdatedBy.ShouldBeSameAs(Bob);
        created.UpdatedBy.ShouldBeNull();
        created.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public void Create_WithNonConformingFakeOffset_ShouldFailWithNonUtcOffset()
    {
        // Simula uma implementação de teste não conforme; não descreve o comportamento de TimeProvider.System.
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(-3)));

        var result = AuditMetadata.Create(Alice, clock.GetUtcNow());

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Audit.CreatedAt.NonUtcOffset");
    }

    [Fact]
    public void Create_WithDefaultInstantFromFake_ShouldFailWithNotSet()
    {
        var clock = new FakeTimeProvider(default);

        var result = AuditMetadata.Create(Alice, clock.GetUtcNow());

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Audit.CreatedAt.NotSet");
    }

    [Fact]
    public void WithUpdate_AfterClockRewindBeforeCreation_ShouldFailWithBeforeCreation()
    {
        var clock = new FakeTimeProvider(T0);
        var created = AuditMetadata.Create(Alice, clock.GetUtcNow()).Value;

        clock.Advance(TimeSpan.FromMinutes(-1));
        var result = created.WithUpdate(Bob, clock.GetUtcNow());

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Audit.UpdatedAt.BeforeCreation");
    }

    [Fact]
    public void WithUpdate_AfterClockRewindBeforeLastUpdate_ShouldFailWithBeforeLastUpdate()
    {
        var clock = new FakeTimeProvider(T0);
        var created = AuditMetadata.Create(Alice, clock.GetUtcNow()).Value;

        clock.Advance(TimeSpan.FromMinutes(10));
        var updated = created.WithUpdate(Bob, clock.GetUtcNow()).Value;

        clock.Advance(TimeSpan.FromMinutes(-5));
        var result = updated.WithUpdate(Bob, clock.GetUtcNow());

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Audit.UpdatedAt.BeforeLastUpdate");
    }

    [Fact]
    public void FakeClock_RepeatedReads_ShouldBeDeterministicAndYieldEqualMetadata()
    {
        var clock = new FakeTimeProvider(T0);

        clock.GetUtcNow().ShouldBe(clock.GetUtcNow());

        var first = AuditMetadata.Create(Alice, clock.GetUtcNow()).Value;
        var second = AuditMetadata.Create(AuditActor.Create(AuditActorKind.User, "alice").Value, clock.GetUtcNow()).Value;

        first.ShouldNotBeSameAs(second);
        first.Equals(second).ShouldBeTrue();
        (first == second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    private sealed class FakeTimeProvider(DateTimeOffset initial) : TimeProvider
    {
        private DateTimeOffset _now = initial;

        public void Advance(TimeSpan delta) => _now += delta;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}

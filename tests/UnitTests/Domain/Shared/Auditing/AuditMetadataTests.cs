using Business.Platform.Core.Domain.Shared;

namespace Business.Platform.Core.UnitTests.Domain.Shared.Auditing;

public sealed class AuditMetadataTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly AuditActor Alice = AuditActor.Create(AuditActorKind.User, "alice").Value;
    private static readonly AuditActor Bob = AuditActor.Create(AuditActorKind.User, "bob").Value;
    private static readonly AuditActor Job = AuditActor.Create(AuditActorKind.System, "job").Value;

    private static AuditMetadata Created() => AuditMetadata.Create(Alice, T0).Value;

    private static void ShouldFailWith(Result<AuditMetadata> result, string code, string description)
    {
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(code);
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Description.ShouldBe(description);
    }

    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        var result = AuditMetadata.Create(Alice, T0);

        result.IsSuccess.ShouldBeTrue();
        result.Value.CreatedBy.ShouldBeSameAs(Alice);
        result.Value.CreatedAt.ShouldBe(T0);
        result.Value.UpdatedBy.ShouldBeNull();
        result.Value.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public void Create_WithNullActor_ShouldThrow()
    {
        Should.Throw<ArgumentNullException>(() => AuditMetadata.Create(null!, T0))
            .ParamName.ShouldBe("createdBy");
    }

    [Fact]
    public void Create_WithDefaultTimestamp_ShouldFail()
    {
        ShouldFailWith(
            AuditMetadata.Create(Alice, default),
            "Audit.CreatedAt.NotSet",
            "O instante de criação não foi definido.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-3)]
    public void Create_WithNonUtcOffset_ShouldFail(int hours)
    {
        ShouldFailWith(
            AuditMetadata.Create(Alice, new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(hours))),
            "Audit.CreatedAt.NonUtcOffset",
            "O instante de criação deve estar em UTC (offset zero).");
    }

    [Fact]
    public void Create_NullActorTakesPrecedenceOverTimestampErrors()
    {
        Should.Throw<ArgumentNullException>(() => AuditMetadata.Create(null!, default));
    }

    [Fact]
    public void WithUpdate_FirstUpdate_ShouldSetUpdateAndPreserveCreation()
    {
        var created = Created();
        var updatedAt = T0.AddMinutes(5);

        var result = created.WithUpdate(Bob, updatedAt);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeSameAs(created);
        result.Value.CreatedBy.ShouldBeSameAs(Alice);
        result.Value.CreatedAt.ShouldBe(T0);
        result.Value.UpdatedBy.ShouldBeSameAs(Bob);
        result.Value.UpdatedAt.ShouldBe(updatedAt);
    }

    [Fact]
    public void WithUpdate_ShouldNotMutateOriginal()
    {
        var created = Created();

        created.WithUpdate(Bob, T0.AddMinutes(1));

        created.UpdatedBy.ShouldBeNull();
        created.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public void WithUpdate_SuccessiveUpdates_ShouldKeepLatestAndCreation()
    {
        var first = Created().WithUpdate(Bob, T0.AddMinutes(1)).Value;
        var second = first.WithUpdate(Job, T0.AddMinutes(2)).Value;

        second.CreatedBy.ShouldBeSameAs(Alice);
        second.CreatedAt.ShouldBe(T0);
        second.UpdatedBy.ShouldBeSameAs(Job);
        second.UpdatedAt.ShouldBe(T0.AddMinutes(2));
        first.UpdatedBy.ShouldBeSameAs(Bob);
        first.UpdatedAt.ShouldBe(T0.AddMinutes(1));
    }

    [Fact]
    public void WithUpdate_EqualToCreation_ShouldSucceed()
    {
        Created().WithUpdate(Bob, T0).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void WithUpdate_EqualToLastUpdate_ShouldSucceed()
    {
        var first = Created().WithUpdate(Bob, T0.AddMinutes(1)).Value;

        first.WithUpdate(Job, T0.AddMinutes(1)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void WithUpdate_BySameActor_ShouldSucceed()
    {
        var first = Created().WithUpdate(Alice, T0.AddMinutes(1)).Value;

        first.WithUpdate(Alice, T0.AddMinutes(2)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void WithUpdate_WithNullActor_ShouldThrow()
    {
        Should.Throw<ArgumentNullException>(() => Created().WithUpdate(null!, T0))
            .ParamName.ShouldBe("updatedBy");
    }

    [Fact]
    public void WithUpdate_WithDefaultTimestamp_ShouldFail()
    {
        ShouldFailWith(
            Created().WithUpdate(Bob, default),
            "Audit.UpdatedAt.NotSet",
            "O instante de atualização não foi definido.");
    }

    [Fact]
    public void WithUpdate_WithNonUtcOffset_ShouldFail()
    {
        ShouldFailWith(
            Created().WithUpdate(Bob, new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(2))),
            "Audit.UpdatedAt.NonUtcOffset",
            "O instante de atualização deve estar em UTC (offset zero).");
    }

    [Fact]
    public void WithUpdate_BeforeCreation_ShouldFail()
    {
        ShouldFailWith(
            Created().WithUpdate(Bob, T0.AddTicks(-1)),
            "Audit.UpdatedAt.BeforeCreation",
            "O instante de atualização não pode ser anterior ao de criação.");
    }

    [Fact]
    public void WithUpdate_BeforeLastUpdate_ShouldFail()
    {
        var first = Created().WithUpdate(Bob, T0.AddMinutes(10)).Value;

        ShouldFailWith(
            first.WithUpdate(Job, T0.AddMinutes(10).AddTicks(-1)),
            "Audit.UpdatedAt.BeforeLastUpdate",
            "O instante de atualização não pode ser anterior ao da última atualização.");
    }

    [Fact]
    public void WithUpdate_AfterFailure_OriginalRemainsUsable()
    {
        var created = Created();

        created.WithUpdate(Bob, T0.AddTicks(-1)).IsFailure.ShouldBeTrue();

        created.WithUpdate(Bob, T0.AddMinutes(1)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void WithUpdate_Precedence_DefaultBeforeOthers()
    {
        Created().WithUpdate(Bob, default).Error.Code.ShouldBe("Audit.UpdatedAt.NotSet");
    }

    [Fact]
    public void WithUpdate_Precedence_NonUtcBeforeBeforeCreation()
    {
        var earlierNonUtc = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.FromHours(1));

        Created().WithUpdate(Bob, earlierNonUtc).Error.Code.ShouldBe("Audit.UpdatedAt.NonUtcOffset");
    }

    [Fact]
    public void WithUpdate_Precedence_BeforeCreationBeforeBeforeLastUpdate()
    {
        var first = Created().WithUpdate(Bob, T0.AddMinutes(10)).Value;

        first.WithUpdate(Job, T0.AddMinutes(-1)).Error.Code.ShouldBe("Audit.UpdatedAt.BeforeCreation");
    }

    [Fact]
    public void WithUpdate_NullActorTakesPrecedenceOverTimestampErrors()
    {
        Should.Throw<ArgumentNullException>(() => Created().WithUpdate(null!, default));
    }

    [Fact]
    public void Equality_SameStructure_ShouldBeEqual()
    {
        var a = AuditMetadata.Create(Alice, T0).Value;
        var b = AuditMetadata.Create(AuditActor.Create(AuditActorKind.User, "alice").Value, T0).Value;

        a.Equals(b).ShouldBeTrue();
        a.Equals((object)b).ShouldBeTrue();
        (a == b).ShouldBeTrue();
        (a != b).ShouldBeFalse();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Equality_SameUpdates_ShouldBeEqual()
    {
        var a = Created().WithUpdate(Bob, T0.AddMinutes(1)).Value;
        var b = Created().WithUpdate(Bob, T0.AddMinutes(1)).Value;

        (a == b).ShouldBeTrue();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentCreatedBy_ShouldNotBeEqual()
    {
        var a = AuditMetadata.Create(Alice, T0).Value;
        var b = AuditMetadata.Create(Bob, T0).Value;

        a.Equals(b).ShouldBeFalse();
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentCreatedAt_ShouldNotBeEqual()
    {
        var a = AuditMetadata.Create(Alice, T0).Value;
        var b = AuditMetadata.Create(Alice, T0.AddTicks(1)).Value;

        a.Equals(b).ShouldBeFalse();
    }

    [Fact]
    public void Equality_DifferentUpdatedBy_ShouldNotBeEqual()
    {
        var a = Created().WithUpdate(Bob, T0.AddMinutes(1)).Value;
        var b = Created().WithUpdate(Job, T0.AddMinutes(1)).Value;

        a.Equals(b).ShouldBeFalse();
    }

    [Fact]
    public void Equality_DifferentUpdatedAt_ShouldNotBeEqual()
    {
        var a = Created().WithUpdate(Bob, T0.AddMinutes(1)).Value;
        var b = Created().WithUpdate(Bob, T0.AddMinutes(2)).Value;

        a.Equals(b).ShouldBeFalse();
    }

    [Fact]
    public void Equality_UpdatedVersusNotUpdated_ShouldNotBeEqual()
    {
        var a = Created();
        var b = Created().WithUpdate(Alice, T0).Value;

        a.Equals(b).ShouldBeFalse();
        b.Equals(a).ShouldBeFalse();
    }

    [Fact]
    public void Equality_WithNullAndOtherTypes_ShouldBeFalse()
    {
        var a = Created();

        a.Equals(null).ShouldBeFalse();
        a.Equals((object?)null).ShouldBeFalse();
        a.Equals("x").ShouldBeFalse();
        (a == null).ShouldBeFalse();
        (null == a).ShouldBeFalse();
        (a != null).ShouldBeTrue();
        ((AuditMetadata?)null == null).ShouldBeTrue();
        ((AuditMetadata?)null != null).ShouldBeFalse();
    }

    [Fact]
    public void Equality_ShouldBeSymmetricAndTransitive()
    {
        var a = Created().WithUpdate(Bob, T0.AddMinutes(1)).Value;
        var b = Created().WithUpdate(Bob, T0.AddMinutes(1)).Value;
        var c = Created().WithUpdate(Bob, T0.AddMinutes(1)).Value;
        var other = Created();

        a.Equals(b).ShouldBe(b.Equals(a));
        (a.Equals(b) && b.Equals(c)).ShouldBeTrue();
        a.Equals(c).ShouldBeTrue();
        a.Equals(other).ShouldBe(other.Equals(a));
        (a == other).ShouldBe(other == a);
    }

    [Fact]
    public void Operators_ShouldBeConsistentWithEquals()
    {
        var items = new[]
        {
            Created(),
            Created(),
            AuditMetadata.Create(Bob, T0).Value,
            Created().WithUpdate(Bob, T0.AddMinutes(1)).Value,
            Created().WithUpdate(Bob, T0.AddMinutes(1)).Value
        };

        foreach (var left in items)
        {
            foreach (var right in items)
            {
                (left == right).ShouldBe(left.Equals(right));
                (left != right).ShouldBe(!left.Equals(right));
                if (left == right)
                {
                    left.GetHashCode().ShouldBe(right.GetHashCode());
                }
            }
        }
    }

    [Fact]
    public void Create_WithExtremeValidTimestamps_ShouldSucceed()
    {
        var earliest = DateTimeOffset.MinValue.AddTicks(1);

        AuditMetadata.Create(Alice, earliest).Value.CreatedAt.ShouldBe(earliest);
        AuditMetadata.Create(Alice, DateTimeOffset.MaxValue).Value.CreatedAt.ShouldBe(DateTimeOffset.MaxValue);
    }

    [Fact]
    public void Create_WithExtremeInvalidTimestamps_ShouldFail()
    {
        AuditMetadata.Create(Alice, DateTimeOffset.MinValue).Error.Code.ShouldBe("Audit.CreatedAt.NotSet");

        var maxNonUtc = new DateTimeOffset(DateTime.MaxValue.Ticks, TimeSpan.FromHours(14));
        AuditMetadata.Create(Alice, maxNonUtc).Error.Code.ShouldBe("Audit.CreatedAt.NonUtcOffset");
    }

    [Fact]
    public void WithUpdate_WithExtremeTimestamps_ShouldApplyBoundaries()
    {
        var earliest = AuditMetadata.Create(Alice, DateTimeOffset.MinValue.AddTicks(1)).Value;

        var atMax = earliest.WithUpdate(Bob, DateTimeOffset.MaxValue);
        atMax.IsSuccess.ShouldBeTrue();
        atMax.Value.UpdatedAt.ShouldBe(DateTimeOffset.MaxValue);
        atMax.Value.WithUpdate(Job, DateTimeOffset.MaxValue).IsSuccess.ShouldBeTrue();
        atMax.Value.WithUpdate(Job, DateTimeOffset.MaxValue.AddTicks(-1)).Error.Code
            .ShouldBe("Audit.UpdatedAt.BeforeLastUpdate");

        earliest.WithUpdate(Bob, DateTimeOffset.MinValue).Error.Code.ShouldBe("Audit.UpdatedAt.NotSet");
        earliest.WithUpdate(Bob, new DateTimeOffset(DateTime.MaxValue.Ticks, TimeSpan.FromHours(14))).Error.Code
            .ShouldBe("Audit.UpdatedAt.NonUtcOffset");
    }

    [Fact]
    public void WithUpdate_SuccessiveUpdates_ShouldPreserveInvariants()
    {
        var actors = new[] { Alice, Bob, Job };
        var current = Created();
        var last = T0;

        for (var i = 0; i < 50; i++)
        {
            var at = last.AddTicks(i % 3 == 0 ? 0 : 7);
            var next = current.WithUpdate(actors[i % actors.Length], at);

            next.IsSuccess.ShouldBeTrue();
            next.Value.CreatedBy.ShouldBeSameAs(Alice);
            next.Value.CreatedAt.ShouldBe(T0);
            next.Value.UpdatedBy.ShouldBeSameAs(actors[i % actors.Length]);
            next.Value.UpdatedAt.ShouldBe(at);
            next.Value.UpdatedAt!.Value.ShouldBeGreaterThanOrEqualTo(next.Value.CreatedAt);
            next.Value.UpdatedAt!.Value.ShouldBeGreaterThanOrEqualTo(last);

            // Uma tentativa rejeitada não altera o estado nem impede a próxima atualização válida.
            next.Value.WithUpdate(Bob, at.AddTicks(-1)).IsFailure.ShouldBeTrue();
            next.Value.UpdatedAt.ShouldBe(at);

            current = next.Value;
            last = at;
        }
    }
}

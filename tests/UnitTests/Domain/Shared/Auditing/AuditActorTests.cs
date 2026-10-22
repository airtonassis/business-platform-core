using Business.Platform.Core.Domain.Shared;

namespace Business.Platform.Core.UnitTests.Domain.Shared.Auditing;

public sealed class AuditActorTests
{
    private const string KindCode = "Audit.Actor.KindUndefined";
    private const string RequiredCode = "Audit.Actor.IdRequired";
    private const string WhitespaceCode = "Audit.Actor.IdSurroundingWhitespace";
    private const string TooLongCode = "Audit.Actor.IdTooLong";
    private const string InvalidCharacterCode = "Audit.Actor.IdInvalidCharacter";

    private static void ShouldFailWith(Result<AuditActor> result, string code, string description)
    {
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(code);
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Description.ShouldBe(description);
    }

    [Fact]
    public void Constants_ShouldHaveApprovedValues()
    {
        AuditActor.MaxIdLength.ShouldBe(256);
        ((int)AuditActorKind.User).ShouldBe(1);
        ((int)AuditActorKind.System).ShouldBe(2);
        Enum.GetValues<AuditActorKind>().Length.ShouldBe(2);
    }

    [Theory]
    [InlineData(AuditActorKind.User)]
    [InlineData(AuditActorKind.System)]
    public void Create_WithValidKind_ShouldSucceed(AuditActorKind kind)
    {
        var result = AuditActor.Create(kind, "actor-1");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Kind.ShouldBe(kind);
        result.Value.Id.ShouldBe("actor-1");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Create_WithUndefinedKind_ShouldFail(int kind)
    {
        ShouldFailWith(
            AuditActor.Create((AuditActorKind)kind, "actor"),
            KindCode,
            "O tipo do ator de auditoria é indefinido.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    public void Create_WithNullEmptyOrWhitespaceId_ShouldFailAsRequired(string? id)
    {
        ShouldFailWith(
            AuditActor.Create(AuditActorKind.User, id),
            RequiredCode,
            "O identificador do ator de auditoria é obrigatório.");
    }

    [Theory]
    [InlineData(" user")]
    [InlineData("user ")]
    [InlineData(" user ")]
    [InlineData("\tuser")]
    [InlineData("user\n")]
    [InlineData(" user")]
    public void Create_WithSurroundingWhitespace_ShouldFail(string id)
    {
        ShouldFailWith(
            AuditActor.Create(AuditActorKind.User, id),
            WhitespaceCode,
            "O identificador do ator de auditoria não pode conter espaços no início ou no final.");
    }

    [Fact]
    public void Create_WithInnerSpaces_ShouldSucceedAndPreserveId()
    {
        var result = AuditActor.Create(AuditActorKind.User, "john  doe");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe("john  doe");
    }

    [Fact]
    public void Create_WithExactlyMaxLength_ShouldSucceed()
    {
        var id = new string('a', 256);

        var result = AuditActor.Create(AuditActorKind.User, id);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(id);
    }

    [Fact]
    public void Create_WithMoreThanMaxLength_ShouldFail()
    {
        ShouldFailWith(
            AuditActor.Create(AuditActorKind.User, new string('a', 257)),
            TooLongCode,
            "O identificador do ator de auditoria excede o máximo permitido de 256 caracteres.");
    }

    [Theory]
    [InlineData("a\u0000b")]
    [InlineData("a\tb")]
    [InlineData("a\nb")]
    [InlineData("a\rb")]
    [InlineData("a\u001Fb")]
    [InlineData("a\u007Fb")]
    [InlineData("a\u0085b")]
    [InlineData("a\u009Fb")]
    public void Create_WithControlCharacter_ShouldFail(string id)
    {
        ShouldFailWith(
            AuditActor.Create(AuditActorKind.User, id),
            InvalidCharacterCode,
            "O identificador do ator de auditoria não pode conter caracteres de controle.");
    }

    [Fact]
    public void Create_WithControlCharacterAtLastPosition_ShouldFail()
    {
        var id = new string('a', 255) + "\u0001";

        id.Length.ShouldBe(AuditActor.MaxIdLength);
        char.IsControl(id[^1]).ShouldBeTrue();
        AuditActor.Create(AuditActorKind.User, id).Error.Code.ShouldBe(InvalidCharacterCode);
    }

    [Fact]
    public void Create_Precedence_KindBeforeId()
    {
        AuditActor.Create((AuditActorKind)0, null).Error.Code.ShouldBe(KindCode);
        AuditActor.Create((AuditActorKind)0, " a\u0001" + new string('a', 300) + " ").Error.Code.ShouldBe(KindCode);
    }

    [Fact]
    public void Create_Precedence_RequiredBeforeOthers()
    {
        AuditActor.Create(AuditActorKind.User, "   ").Error.Code.ShouldBe(RequiredCode);
    }

    [Fact]
    public void Create_Precedence_WhitespaceBeforeTooLongAndControl()
    {
        var id = " \u0001" + new string('a', 300);

        AuditActor.Create(AuditActorKind.User, id).Error.Code.ShouldBe(WhitespaceCode);
    }

    [Fact]
    public void Create_Precedence_TooLongBeforeControl()
    {
        var id = "\u0001" + new string('a', 300);

        AuditActor.Create(AuditActorKind.User, id).Error.Code.ShouldBe(TooLongCode);
    }

    [Fact]
    public void Create_ErrorMessages_ShouldNotContainReceivedId()
    {
        const string secret = "secret\u0001id";

        var result = AuditActor.Create(AuditActorKind.User, secret);

        result.Error.Description.ShouldNotContain("secret");
    }

    [Fact]
    public void Create_ShouldPreserveIdWithoutNormalization()
    {
        const string id = "Ünï-cödé/Ａ";

        AuditActor.Create(AuditActorKind.System, id).Value.Id.ShouldBe(id);
    }

    [Fact]
    public void Equality_SameKindAndId_ShouldBeEqual()
    {
        var a = AuditActor.Create(AuditActorKind.User, "x").Value;
        var b = AuditActor.Create(AuditActorKind.User, "x").Value;

        a.Equals(b).ShouldBeTrue();
        a.Equals((object)b).ShouldBeTrue();
        (a == b).ShouldBeTrue();
        (a != b).ShouldBeFalse();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentKind_ShouldNotBeEqual()
    {
        var a = AuditActor.Create(AuditActorKind.User, "x").Value;
        var b = AuditActor.Create(AuditActorKind.System, "x").Value;

        a.Equals(b).ShouldBeFalse();
        (a == b).ShouldBeFalse();
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_IsCaseSensitive()
    {
        var a = AuditActor.Create(AuditActorKind.User, "abc").Value;
        var b = AuditActor.Create(AuditActorKind.User, "ABC").Value;

        a.Equals(b).ShouldBeFalse();
        (a == b).ShouldBeFalse();
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentId_ShouldNotBeEqual()
    {
        var a = AuditActor.Create(AuditActorKind.User, "a").Value;
        var b = AuditActor.Create(AuditActorKind.User, "b").Value;

        a.Equals(b).ShouldBeFalse();
    }

    [Fact]
    public void Equality_WithNullAndOtherTypes_ShouldBeFalse()
    {
        var a = AuditActor.Create(AuditActorKind.User, "a").Value;

        a.Equals(null).ShouldBeFalse();
        a.Equals((object?)null).ShouldBeFalse();
        a.Equals("a").ShouldBeFalse();
        (a == null).ShouldBeFalse();
        (null == a).ShouldBeFalse();
        (a != null).ShouldBeTrue();
        ((AuditActor?)null == null).ShouldBeTrue();
        ((AuditActor?)null != null).ShouldBeFalse();
    }

    [Fact]
    public void Equality_SameReference_ShouldBeEqual()
    {
        var a = AuditActor.Create(AuditActorKind.User, "a").Value;
        var same = a;

        (a == same).ShouldBeTrue();
    }

    [Fact]
    public void Equality_ShouldBeSymmetricAndTransitive()
    {
        var a = AuditActor.Create(AuditActorKind.User, "x").Value;
        var b = AuditActor.Create(AuditActorKind.User, "x").Value;
        var c = AuditActor.Create(AuditActorKind.User, "x").Value;
        var other = AuditActor.Create(AuditActorKind.System, "x").Value;

        a.Equals(b).ShouldBe(b.Equals(a));
        (a == b).ShouldBe(b == a);
        (a.Equals(b) && b.Equals(c)).ShouldBeTrue();
        a.Equals(c).ShouldBeTrue();
        a.Equals(other).ShouldBe(other.Equals(a));
        (a == other).ShouldBe(other == a);
    }

    [Fact]
    public void Operators_ShouldBeConsistentWithEquals()
    {
        var actors = new[]
        {
            AuditActor.Create(AuditActorKind.User, "x").Value,
            AuditActor.Create(AuditActorKind.User, "x").Value,
            AuditActor.Create(AuditActorKind.User, "X").Value,
            AuditActor.Create(AuditActorKind.System, "x").Value
        };

        foreach (var left in actors)
        {
            foreach (var right in actors)
            {
                (left == right).ShouldBe(left.Equals(right));
                (left != right).ShouldBe(!left.Equals(right));
                (left == right).ShouldBe(!(left != right));
                if (left == right)
                {
                    left.GetHashCode().ShouldBe(right.GetHashCode());
                }
            }
        }
    }

    [Fact]
    public void Create_WithCanonicallyEquivalentUnicodeIds_ShouldNotNormalize()
    {
        const string composed = "café";
        const string decomposed = "café";

        var a = AuditActor.Create(AuditActorKind.User, composed).Value;
        var b = AuditActor.Create(AuditActorKind.User, decomposed).Value;

        a.Id.ShouldBe(composed);
        b.Id.ShouldBe(decomposed);
        a.Equals(b).ShouldBeFalse();
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void Create_WithSupplementaryCharacters_ShouldCountUtf16CodeUnits()
    {
        const string emoji = "\U0001F600";
        emoji.Length.ShouldBe(2);

        var atLimit = string.Concat(Enumerable.Repeat(emoji, 128));
        var overLimit = string.Concat(Enumerable.Repeat(emoji, 129));
        var oneUnitOver = new string('a', 255) + emoji;

        var accepted = AuditActor.Create(AuditActorKind.User, atLimit);
        accepted.IsSuccess.ShouldBeTrue();
        accepted.Value.Id.ShouldBe(atLimit);

        AuditActor.Create(AuditActorKind.User, overLimit).Error.Code.ShouldBe(TooLongCode);
        AuditActor.Create(AuditActorKind.User, oneUnitOver).Error.Code.ShouldBe(TooLongCode);
    }
}

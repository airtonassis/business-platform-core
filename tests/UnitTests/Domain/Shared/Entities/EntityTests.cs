using Business.Platform.Core.Domain.Shared;

namespace Business.Platform.Core.UnitTests.Domain.Shared.Entities;

public sealed class EntityTests
{
    private static readonly Guid SampleGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // Construção
    [Fact]
    public void Constructor_WithGuid_ShouldSetId()
    {
        new UserEntity(SampleGuid).Id.ShouldBe(SampleGuid);
    }

    [Fact]
    public void Constructor_WithInt_ShouldSetId()
    {
        new TestEntity<int>(42).Id.ShouldBe(42);
    }

    [Fact]
    public void Constructor_WithString_ShouldSetId()
    {
        new TestEntity<string>("abc").Id.ShouldBe("abc");
    }

    [Fact]
    public void Constructor_WithCustomIdentifier_ShouldSetId()
    {
        var id = new CustomId(7);

        new TestEntity<CustomId>(id).Id.ShouldBe(id);
    }

    [Fact]
    public void Constructor_WithNullId_ShouldThrowArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new TestEntity<string>(null!));

        exception.ParamName.ShouldBe("id");
    }

    // D04 — a Foundation não rejeita valores padrão
    [Fact]
    public void Constructor_WithGuidEmpty_ShouldBeAccepted()
    {
        new UserEntity(Guid.Empty).Id.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void Constructor_WithZero_ShouldBeAccepted()
    {
        new TestEntity<int>(0).Id.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WithEmptyString_ShouldBeAccepted()
    {
        new TestEntity<string>(string.Empty).Id.ShouldBe(string.Empty);
    }

    // Equals(object?)
    [Fact]
    public void Equals_SameInstance_ShouldBeTrue()
    {
        var entity = new UserEntity(SampleGuid);

        entity.Equals((object)entity).ShouldBeTrue();
    }

    [Fact]
    public void Equals_SameTypeSameId_ShouldBeTrue()
    {
        new UserEntity(SampleGuid).Equals((object)new UserEntity(SampleGuid)).ShouldBeTrue();
    }

    [Fact]
    public void Equals_SameTypeDifferentId_ShouldBeFalse()
    {
        new UserEntity(SampleGuid).Equals((object)new UserEntity(Guid.NewGuid())).ShouldBeFalse();
    }

    [Fact]
    public void Equals_DifferentConcreteTypesSameId_ShouldBeFalse()
    {
        new UserEntity(SampleGuid).Equals((object)new AdminEntity(SampleGuid)).ShouldBeFalse();
    }

    [Fact]
    public void Equals_DerivedRuntimeTypeSameId_ShouldBeFalse()
    {
        new UserEntity(SampleGuid).Equals((object)new SuperUserEntity(SampleGuid)).ShouldBeFalse();
    }

    [Fact]
    public void Equals_NullObject_ShouldBeFalse()
    {
        new UserEntity(SampleGuid).Equals((object?)null).ShouldBeFalse();
    }

    [Fact]
    public void Equals_NonEntityObject_ShouldBeFalse()
    {
        new UserEntity(SampleGuid).Equals((object)SampleGuid).ShouldBeFalse();
    }

    [Fact]
    public void Equals_EntityWithDifferentIdType_ShouldBeFalse()
    {
        new TestEntity<int>(1).Equals((object)new TestEntity<long>(1)).ShouldBeFalse();
    }

    // Equals(Entity<TId>?)
    [Fact]
    public void EqualsTyped_SameInstance_ShouldBeTrue()
    {
        var entity = new UserEntity(SampleGuid);

        entity.Equals((Entity<Guid>)entity).ShouldBeTrue();
    }

    [Fact]
    public void EqualsTyped_SameTypeSameId_ShouldBeTrue()
    {
        new UserEntity(SampleGuid).Equals((Entity<Guid>)new UserEntity(SampleGuid)).ShouldBeTrue();
    }

    [Fact]
    public void EqualsTyped_SameTypeDifferentId_ShouldBeFalse()
    {
        new UserEntity(SampleGuid).Equals((Entity<Guid>)new UserEntity(Guid.NewGuid())).ShouldBeFalse();
    }

    [Fact]
    public void EqualsTyped_Null_ShouldBeFalse()
    {
        new UserEntity(SampleGuid).Equals((Entity<Guid>?)null).ShouldBeFalse();
    }

    [Fact]
    public void EqualsTyped_DifferentConcreteTypeSameId_ShouldBeFalse()
    {
        new UserEntity(SampleGuid).Equals((Entity<Guid>)new AdminEntity(SampleGuid)).ShouldBeFalse();
    }

    // Reflexividade, simetria, transitividade
    [Fact]
    public void Equality_ShouldBeReflexive()
    {
        var entity = new UserEntity(SampleGuid);

        entity.Equals(entity).ShouldBeTrue();
    }

    [Fact]
    public void Equality_ShouldBeSymmetric()
    {
        var a = new UserEntity(SampleGuid);
        var b = new UserEntity(SampleGuid);
        var c = new AdminEntity(SampleGuid);

        a.Equals(b).ShouldBe(b.Equals(a));
        a.Equals(c).ShouldBe(c.Equals(a));
    }

    [Fact]
    public void Equality_ShouldBeTransitive()
    {
        var a = new UserEntity(SampleGuid);
        var b = new UserEntity(SampleGuid);
        var c = new UserEntity(SampleGuid);

        a.Equals(b).ShouldBeTrue();
        b.Equals(c).ShouldBeTrue();
        a.Equals(c).ShouldBeTrue();
    }

    [Fact]
    public void Equality_ShouldBeConsistentAcrossEqualsOperatorsAndTypedEquals()
    {
        var a = new UserEntity(SampleGuid);
        var equal = new UserEntity(SampleGuid);
        var different = new UserEntity(Guid.NewGuid());

        (a == equal).ShouldBe(a.Equals((object)equal));
        (a == different).ShouldBe(a.Equals((object)different));
        (a != equal).ShouldBe(!a.Equals((object)equal));
        (a != different).ShouldBe(!a.Equals((object)different));
        a.Equals(equal).ShouldBe(a.Equals((object)equal));
    }

    // Atributos não participam da igualdade
    [Fact]
    public void Equality_ShouldIgnoreOtherAttributes()
    {
        var a = new NamedEntity(SampleGuid, "Alice");
        var b = new NamedEntity(SampleGuid, "Bob");

        a.Equals(b).ShouldBeTrue();
        (a == b).ShouldBeTrue();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    // Identificadores personalizados com igualdade por valor
    [Fact]
    public void Equality_WithCustomIdentifierEqualByValue_ShouldBeTrue()
    {
        new TestEntity<CustomId>(new CustomId(5)).Equals(new TestEntity<CustomId>(new CustomId(5))).ShouldBeTrue();
    }

    [Fact]
    public void Equality_WithCustomIdentifierDifferentValue_ShouldBeFalse()
    {
        new TestEntity<CustomId>(new CustomId(5)).Equals(new TestEntity<CustomId>(new CustomId(6))).ShouldBeFalse();
    }

    [Fact]
    public void Equality_WithReferenceIdentifierEqualByValue_ShouldBeTrue()
    {
        var a = new TestEntity<ReferenceId>(new ReferenceId("x"));
        var b = new TestEntity<ReferenceId>(new ReferenceId("x"));

        a.Equals(b).ShouldBeTrue();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Equality_WithDefaultValuesAsIds_ShouldCompareByValue()
    {
        new UserEntity(Guid.Empty).Equals(new UserEntity(Guid.Empty)).ShouldBeTrue();
        new TestEntity<int>(0).Equals(new TestEntity<int>(0)).ShouldBeTrue();
        new TestEntity<string>(string.Empty).Equals(new TestEntity<string>(string.Empty)).ShouldBeTrue();
        new TestEntity<int>(0).Equals(new TestEntity<int>(1)).ShouldBeFalse();
    }

    // GetHashCode (colisões são permitidas; exige-se apenas consistência com a igualdade)
    [Fact]
    public void GetHashCode_ShouldBeStableAcrossCalls()
    {
        var entity = new UserEntity(SampleGuid);

        entity.GetHashCode().ShouldBe(entity.GetHashCode());
    }

    [Fact]
    public void GetHashCode_EqualEntities_ShouldHaveSameHash()
    {
        new UserEntity(SampleGuid).GetHashCode().ShouldBe(new UserEntity(SampleGuid).GetHashCode());
    }

    [Fact]
    public void GetHashCode_ShouldCombineConcreteTypeAndIdentifierHash()
    {
        var expected = HashCode.Combine(typeof(UserEntity), EqualityComparer<Guid>.Default.GetHashCode(SampleGuid));

        new UserEntity(SampleGuid).GetHashCode().ShouldBe(expected);
    }

    // Operadores
    [Fact]
    public void OperatorEquality_SameInstance_ShouldBeTrue()
    {
        var entity = new UserEntity(SampleGuid);

#pragma warning disable CS1718 // Comparação feita para a mesma variável (intencional)
        (entity == entity).ShouldBeTrue();
#pragma warning restore CS1718
    }

    [Fact]
    public void OperatorEquality_SameTypeSameId_ShouldBeTrue()
    {
        (new UserEntity(SampleGuid) == new UserEntity(SampleGuid)).ShouldBeTrue();
    }

    [Fact]
    public void OperatorEquality_DifferentId_ShouldBeFalse()
    {
        (new UserEntity(SampleGuid) == new UserEntity(Guid.NewGuid())).ShouldBeFalse();
    }

    [Fact]
    public void OperatorEquality_DifferentConcreteTypes_ShouldBeFalse()
    {
        (new UserEntity(SampleGuid) == (Entity<Guid>)new AdminEntity(SampleGuid)).ShouldBeFalse();
    }

    [Fact]
    public void OperatorEquality_LeftNull_ShouldBeFalse()
    {
        ((Entity<Guid>?)null == new UserEntity(SampleGuid)).ShouldBeFalse();
    }

    [Fact]
    public void OperatorEquality_RightNull_ShouldBeFalse()
    {
        (new UserEntity(SampleGuid) == (Entity<Guid>?)null).ShouldBeFalse();
    }

    [Fact]
    public void OperatorEquality_BothNull_ShouldBeTrue()
    {
        ((Entity<Guid>?)null == (Entity<Guid>?)null).ShouldBeTrue();
    }

    [Fact]
    public void OperatorInequality_SameInstance_ShouldBeFalse()
    {
        var entity = new UserEntity(SampleGuid);

#pragma warning disable CS1718
        (entity != entity).ShouldBeFalse();
#pragma warning restore CS1718
    }

    [Fact]
    public void OperatorInequality_SameTypeSameId_ShouldBeFalse()
    {
        (new UserEntity(SampleGuid) != new UserEntity(SampleGuid)).ShouldBeFalse();
    }

    [Fact]
    public void OperatorInequality_DifferentId_ShouldBeTrue()
    {
        (new UserEntity(SampleGuid) != new UserEntity(Guid.NewGuid())).ShouldBeTrue();
    }

    [Fact]
    public void OperatorInequality_DifferentConcreteTypes_ShouldBeTrue()
    {
        (new UserEntity(SampleGuid) != (Entity<Guid>)new AdminEntity(SampleGuid)).ShouldBeTrue();
    }

    [Fact]
    public void OperatorInequality_LeftNull_ShouldBeTrue()
    {
        ((Entity<Guid>?)null != new UserEntity(SampleGuid)).ShouldBeTrue();
    }

    [Fact]
    public void OperatorInequality_RightNull_ShouldBeTrue()
    {
        (new UserEntity(SampleGuid) != (Entity<Guid>?)null).ShouldBeTrue();
    }

    [Fact]
    public void OperatorInequality_BothNull_ShouldBeFalse()
    {
        ((Entity<Guid>?)null != (Entity<Guid>?)null).ShouldBeFalse();
    }

    // Coleções
    [Fact]
    public void HashSet_ShouldDeduplicateEqualEntities()
    {
        var set = new HashSet<Entity<Guid>> { new UserEntity(SampleGuid), new UserEntity(SampleGuid) };

        set.Count.ShouldBe(1);
    }

    [Fact]
    public void HashSet_ShouldKeepEntitiesWithDifferentIds()
    {
        var set = new HashSet<Entity<Guid>> { new UserEntity(SampleGuid), new UserEntity(Guid.NewGuid()) };

        set.Count.ShouldBe(2);
    }

    [Fact]
    public void HashSet_ShouldKeepDifferentConcreteTypesWithSameId()
    {
        var set = new HashSet<Entity<Guid>> { new UserEntity(SampleGuid), new AdminEntity(SampleGuid) };

        set.Count.ShouldBe(2);
    }

    [Fact]
    public void Dictionary_ShouldFindValueByEquivalentEntity()
    {
        var dictionary = new Dictionary<Entity<Guid>, string> { [new UserEntity(SampleGuid)] = "value" };

        dictionary[new UserEntity(SampleGuid)].ShouldBe("value");
        dictionary.ContainsKey(new AdminEntity(SampleGuid)).ShouldBeFalse();
    }

    [Fact]
    public void Distinct_ShouldRemoveEqualEntities()
    {
        Entity<Guid>[] entities = [new UserEntity(SampleGuid), new UserEntity(SampleGuid), new AdminEntity(SampleGuid)];

        entities.Distinct().Count().ShouldBe(2);
    }

    // J2 — identificadores int
    [Fact]
    public void Equality_WithIntIds_ShouldCompareByValue()
    {
        var a = new TestEntity<int>(5);
        var same = new TestEntity<int>(5);
        var different = new TestEntity<int>(6);

        a.Equals(same).ShouldBeTrue();
        (a == same).ShouldBeTrue();
        a.GetHashCode().ShouldBe(same.GetHashCode());
        a.Equals(different).ShouldBeFalse();
        (a != different).ShouldBeTrue();
    }

    // J3 — identificadores long
    [Fact]
    public void Constructor_WithLong_ShouldSetId()
    {
        new TestEntity<long>(123L).Id.ShouldBe(123L);
    }

    [Fact]
    public void Equality_WithLongIds_ShouldCompareByValue()
    {
        var a = new TestEntity<long>(123L);
        var same = new TestEntity<long>(123L);
        var different = new TestEntity<long>(124L);

        a.Equals(same).ShouldBeTrue();
        (a == same).ShouldBeTrue();
        a.GetHashCode().ShouldBe(same.GetHashCode());
        a.Equals(different).ShouldBeFalse();
        (a != different).ShouldBeTrue();
    }

    // J4 — identificadores string (comparação ordinal por valor, não por referência)
    [Fact]
    public void Equality_WithStringIds_ShouldCompareByValueNotReference()
    {
        var a = new TestEntity<string>("UUID-" + "1234");
        var same = new TestEntity<string>(string.Concat("UUID-", 1234.ToString()));
        var differentCase = new TestEntity<string>("uuid-1234");

        ReferenceEquals(a.Id, same.Id).ShouldBeFalse();
        a.Equals(same).ShouldBeTrue();
        (a == same).ShouldBeTrue();
        a.GetHashCode().ShouldBe(same.GetHashCode());
        a.Equals(differentCase).ShouldBeFalse();
    }

    // K2 — LINQ (Where, Select, Distinct, Contains)
    [Fact]
    public void Linq_ShouldRespectEntityEquality()
    {
        var target = new UserEntity(SampleGuid);
        UserEntity[] entities = [new(SampleGuid), new(Guid.NewGuid()), new(SampleGuid)];

        entities.Where(entity => entity == target).Count().ShouldBe(2);
        entities.Select(entity => entity.Id).Distinct().Count().ShouldBe(2);
        entities.Distinct().Count().ShouldBe(2);
        entities.Contains(target).ShouldBeTrue();
        entities.Count(entity => entity != target).ShouldBe(1);
    }

    // Identificador não nulo mesmo com TId de referência
    [Fact]
    public void Id_ShouldReturnSameReferenceProvided()
    {
        var id = new ReferenceId("x");

        new TestEntity<ReferenceId>(id).Id.ShouldBeSameAs(id);
    }

    private sealed class TestEntity<TId>(TId id) : Entity<TId>(id)
        where TId : notnull;

    private class UserEntity(Guid id) : Entity<Guid>(id);

    private sealed class SuperUserEntity(Guid id) : UserEntity(id);

    private sealed class AdminEntity(Guid id) : Entity<Guid>(id);

    private sealed class NamedEntity(Guid id, string name) : Entity<Guid>(id)
    {
        public string Name { get; } = name;
    }

    private readonly record struct CustomId(int Value);

    private sealed record ReferenceId(string Value);
}

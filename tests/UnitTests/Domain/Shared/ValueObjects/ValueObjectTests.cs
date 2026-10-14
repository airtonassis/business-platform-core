using Business.Platform.Core.Domain.Shared;

namespace Business.Platform.Core.UnitTests.Domain.Shared.ValueObjects;

public sealed class ValueObjectTests
{
    // Reflexividade
    [Fact]
    public void Equals_WithSameInstance_ShouldReturnTrue()
    {
        var value = new Pair("a", 1);

        value.Equals(value).ShouldBeTrue();
        value.Equals((object)value).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithSameInstance_ShouldNotEnumerateComponents()
    {
        var value = new CountingValue(1, 2);

        value.Equals(value).ShouldBeTrue();

        value.Enumerations.ShouldBe(0);
    }

    // Igualdade e desigualdade
    [Fact]
    public void Equals_WithEqualComponents_ShouldReturnTrue()
    {
        new Pair("a", 1).Equals(new Pair("a", 1)).ShouldBeTrue();
        new Pair("a", 1).Equals((object)new Pair("a", 1)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("a", 2)]
    [InlineData("b", 1)]
    [InlineData("b", 2)]
    public void Equals_WithDifferentComponents_ShouldReturnFalse(string text, int number)
    {
        new Pair("a", 1).Equals(new Pair(text, number)).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithNonValueObjectArgument_ShouldReturnFalse()
    {
        new Pair("a", 1).Equals("a").ShouldBeFalse();
        new Pair("a", 1).Equals(1).ShouldBeFalse();
    }

    // Simetria, transitividade e consistência
    [Fact]
    public void Equals_ShouldBeSymmetric()
    {
        var left = new Pair("a", 1);
        var right = new Pair("a", 1);
        var other = new Pair("b", 1);

        left.Equals(right).ShouldBeTrue();
        right.Equals(left).ShouldBeTrue();
        left.Equals(other).ShouldBeFalse();
        other.Equals(left).ShouldBeFalse();
    }

    [Fact]
    public void Equals_ShouldBeTransitive()
    {
        var first = new Pair("a", 1);
        var second = new Pair("a", 1);
        var third = new Pair("a", 1);

        first.Equals(second).ShouldBeTrue();
        second.Equals(third).ShouldBeTrue();
        first.Equals(third).ShouldBeTrue();
    }

    [Fact]
    public void Equals_ShouldBeConsistentForStableComponents()
    {
        var left = new Pair("a", 1);
        var right = new Pair("a", 1);

        for (var i = 0; i < 10; i++)
        {
            left.Equals(right).ShouldBeTrue();
            left.GetHashCode().ShouldBe(right.GetHashCode());
        }
    }

    // Operadores
    [Fact]
    public void Operators_WithEqualValues_ShouldReportEquality()
    {
        var left = new Pair("a", 1);
        var right = new Pair("a", 1);

        (left == right).ShouldBeTrue();
        (left != right).ShouldBeFalse();
    }

    [Fact]
    public void Operators_WithDifferentValues_ShouldReportInequality()
    {
        var left = new Pair("a", 1);
        var right = new Pair("a", 2);

        (left == right).ShouldBeFalse();
        (left != right).ShouldBeTrue();
    }

    [Fact]
    public void Operators_WithSameInstance_ShouldReportEquality()
    {
        var value = new Pair("a", 1);
        var alias = value;

        (value == alias).ShouldBeTrue();
        (value != alias).ShouldBeFalse();
    }

    // Null
    [Fact]
    public void Equals_WithNull_ShouldReturnFalse()
    {
        var value = new Pair("a", 1);

        value.Equals((ValueObject?)null).ShouldBeFalse();
        value.Equals((object?)null).ShouldBeFalse();
    }

    [Fact]
    public void Operators_WithBothNull_ShouldBeEqual()
    {
        Pair? left = null;
        Pair? right = null;

        (left == right).ShouldBeTrue();
        (left != right).ShouldBeFalse();
    }

    [Fact]
    public void Operators_WithOneNull_ShouldBeDifferent()
    {
        var value = new Pair("a", 1);
        Pair? none = null;

        (value == none).ShouldBeFalse();
        (none == value).ShouldBeFalse();
        (value != none).ShouldBeTrue();
        (none != value).ShouldBeTrue();
    }

    [Fact]
    public void Operators_OnBaseReferences_ShouldNotRecurse()
    {
        ValueObject? left = new Pair("a", 1);
        ValueObject? right = new Pair("a", 1);

        (left == right).ShouldBeTrue();
        (left != right).ShouldBeFalse();
    }

    // Tipo concreto
    [Fact]
    public void Equals_WithDifferentConcreteTypesAndSameComponents_ShouldReturnFalse()
    {
        var pair = new Pair("a", 1);
        var twin = new TwinPair("a", 1);

        pair.Equals(twin).ShouldBeFalse();
        twin.Equals(pair).ShouldBeFalse();
        pair.Equals((object)twin).ShouldBeFalse();
        (pair == twin).ShouldBeFalse();
        (pair != twin).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithDerivedTypeAndSameComponents_ShouldReturnFalse()
    {
        var parent = new BasePair("a", 1);
        var derived = new DerivedPair("a", 1);

        parent.Equals(derived).ShouldBeFalse();
        derived.Equals(parent).ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_ShouldIncludeConcreteType()
    {
        // Verifica a composição incremental prevista no contrato, sem exigir ausência de colisões
        new Pair("a", 1).GetHashCode().ShouldBe(ExpectedHash(typeof(Pair), "a", 1));
        new TwinPair("a", 1).GetHashCode().ShouldBe(ExpectedHash(typeof(TwinPair), "a", 1));
    }

    // Componentes nulos
    [Fact]
    public void Equals_WithNullComponentsOnBothSides_ShouldReturnTrue()
    {
        new Pair(null, 1).Equals(new Pair(null, 1)).ShouldBeTrue();
        new Pair(null, 1).GetHashCode().ShouldBe(new Pair(null, 1).GetHashCode());
    }

    [Fact]
    public void Equals_WithNullComponentOnOneSide_ShouldReturnFalse()
    {
        new Pair(null, 1).Equals(new Pair("a", 1)).ShouldBeFalse();
        new Pair("a", 1).Equals(new Pair(null, 1)).ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_WithAllNullComponents_ShouldNotThrow()
    {
        Should.NotThrow(() => new Pair(null, null).GetHashCode());
        new Pair(null, null).Equals(new Pair(null, null)).ShouldBeTrue();
    }

    // Ordem e quantidade
    [Fact]
    public void Equals_ShouldCompareComponentsInDeclaredOrder()
    {
        new Sequence("a", "b").Equals(new Sequence("b", "a")).ShouldBeFalse();
        new Sequence("a", "b").Equals(new Sequence("a", "b")).ShouldBeTrue();
    }

    [Fact]
    public void GetHashCode_ShouldPreserveComponentOrder()
    {
        new Sequence("a", "b").GetHashCode().ShouldBe(ExpectedHash(typeof(Sequence), "a", "b"));
        new Sequence("b", "a").GetHashCode().ShouldBe(ExpectedHash(typeof(Sequence), "b", "a"));
    }

    [Fact]
    public void Equals_WithDifferentLengths_ShouldReturnFalse()
    {
        new Sequence("a").Equals(new Sequence("a", "b")).ShouldBeFalse();
        new Sequence("a", "b").Equals(new Sequence("a")).ShouldBeFalse();
        new Sequence().Equals(new Sequence("a")).ShouldBeFalse();
        new Sequence("a").Equals(new Sequence()).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithNoComponents_ShouldReturnTrue()
    {
        new Sequence().Equals(new Sequence()).ShouldBeTrue();
        new Sequence().GetHashCode().ShouldBe(new Sequence().GetHashCode());
    }

    [Fact]
    public void Equals_ShouldEnumerateEachSideOnce()
    {
        var left = new CountingValue(1, 2);
        var right = new CountingValue(1, 2);

        left.Equals(right).ShouldBeTrue();

        left.Enumerations.ShouldBe(1);
        right.Enumerations.ShouldBe(1);
    }

    [Fact]
    public void GetHashCode_ShouldEnumerateOnce()
    {
        var value = new CountingValue(1, 2);

        _ = value.GetHashCode();

        value.Enumerations.ShouldBe(1);
    }

    [Fact]
    public void Equals_WithDifferentTypes_ShouldNotEnumerateComponents()
    {
        var counting = new CountingValue(1, 2);

        counting.Equals(new Pair("a", 1)).ShouldBeFalse();

        counting.Enumerations.ShouldBe(0);
    }

    // Hash
    [Fact]
    public void GetHashCode_ShouldBeConsistentWithinTheSameInstance()
    {
        var value = new Pair("a", 1);

        value.GetHashCode().ShouldBe(value.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ShouldDependOnEveryComponent()
    {
        // Cada componente participa da composição; não exige hashes distintos para valores distintos
        new Pair("a", 1).GetHashCode().ShouldBe(ExpectedHash(typeof(Pair), "a", 1));
        new Pair("b", 1).GetHashCode().ShouldBe(ExpectedHash(typeof(Pair), "b", 1));
        new Pair("a", 2).GetHashCode().ShouldBe(ExpectedHash(typeof(Pair), "a", 2));
        new Pair(null, null).GetHashCode().ShouldBe(ExpectedHash(typeof(Pair), null, null));
    }

    // Coleções hash e LINQ
    [Fact]
    public void HashSet_ShouldDeduplicateEqualValues()
    {
        var set = new HashSet<Pair> { new("a", 1), new("b", 2), new("a", 1) };

        set.Count.ShouldBe(2);
        set.Contains(new Pair("b", 2)).ShouldBeTrue();
        set.Contains(new Pair("c", 3)).ShouldBeFalse();
    }

    [Fact]
    public void HashSet_OfBaseType_ShouldSeparateDifferentConcreteTypes()
    {
        var set = new HashSet<ValueObject> { new Pair("a", 1), new TwinPair("a", 1), new Pair("a", 1) };

        set.Count.ShouldBe(2);
    }

    [Fact]
    public void Dictionary_ShouldLookupByEqualValue()
    {
        var dictionary = new Dictionary<Pair, string> { [new Pair("a", 1)] = "found" };

        dictionary[new Pair("a", 1)].ShouldBe("found");
        dictionary.ContainsKey(new Pair("a", 2)).ShouldBeFalse();
    }

    [Fact]
    public void Distinct_ShouldRemoveEqualValues()
    {
        var values = new[] { new Pair("a", 1), new Pair("b", 1), new Pair("a", 1) };

        values.Distinct().Count().ShouldBe(2);
    }

    // Value Objects aninhados
    [Fact]
    public void Equals_WithNestedValueObjects_ShouldCompareStructurally()
    {
        new Line(new Pair("a", 1), new Pair("b", 2)).Equals(new Line(new Pair("a", 1), new Pair("b", 2))).ShouldBeTrue();
        new Line(new Pair("a", 1), new Pair("b", 2)).Equals(new Line(new Pair("a", 1), new Pair("b", 3))).ShouldBeFalse();
        new Line(new Pair("a", 1), new Pair("b", 2)).Equals(new Line(new Pair("b", 2), new Pair("a", 1))).ShouldBeFalse();
    }

    [Fact]
    public void GetHashCode_WithNestedValueObjects_ShouldBeConsistentWithEquals()
    {
        new Line(new Pair("a", 1), new Pair("b", 2)).GetHashCode()
            .ShouldBe(new Line(new Pair("a", 1), new Pair("b", 2)).GetHashCode());
    }

    [Fact]
    public void Equals_WithNestedNullValueObject_ShouldBeSupported()
    {
        new Line(null, new Pair("b", 2)).Equals(new Line(null, new Pair("b", 2))).ShouldBeTrue();
        new Line(null, new Pair("b", 2)).Equals(new Line(new Pair("a", 1), new Pair("b", 2))).ShouldBeFalse();
    }

    // Coleções: a representação estrutural pertence ao Value Object concreto
    [Fact]
    public void OrderedSequence_ShouldBeRepresentedByIndividualComponents()
    {
        new Sequence("a", "b", "c").Equals(new Sequence("a", "b", "c")).ShouldBeTrue();
        new Sequence("a", "b", "c").Equals(new Sequence("a", "c", "b")).ShouldBeFalse();
    }

    [Fact]
    public void UnorderedCollection_NormalizedByConcreteType_ShouldIgnoreInputOrder()
    {
        var left = new TagSet("web", "dotnet", "c#");
        var right = new TagSet("c#", "web", "dotnet");

        left.Equals(right).ShouldBeTrue();
        left.GetHashCode().ShouldBe(right.GetHashCode());
        left.Equals(new TagSet("web", "dotnet")).ShouldBeFalse();
    }

    [Fact]
    public void UnorderedCollection_NormalizedByConcreteType_ShouldNotMutateInput()
    {
        string[] input = ["web", "c#"];

        _ = new TagSet(input);

        input.ShouldBe(["web", "c#"]);
    }

    [Fact]
    public void EmptyCollection_ShouldEqualAnotherEmptyAndDifferFromNonEmpty()
    {
        new TagSet().Equals(new TagSet()).ShouldBeTrue();
        new TagSet().Equals(new TagSet("a")).ShouldBeFalse();
    }

    [Fact]
    public void Delimiters_ShouldAvoidAmbiguityBetweenDifferentStructures()
    {
        // Duas coleções adjacentes: sem delimitadores, ambas as estruturas produzem a sequência achatada a, b, c
        var structureA = (First: new[] { "a" }, Second: new[] { "b", "c" });
        var structureB = (First: new[] { "a", "b" }, Second: new[] { "c" });

        new FlatLists(structureA.First, structureA.Second).Equals(new FlatLists(structureB.First, structureB.Second)).ShouldBeTrue();

        // Com contagens como delimitadores estruturais, as estruturas passam a ser distintas
        new CountedLists(structureA.First, structureA.Second).Equals(new CountedLists(structureB.First, structureB.Second)).ShouldBeFalse();
        new CountedLists(structureA.First, structureA.Second).Equals(new CountedLists(["a"], ["b", "c"])).ShouldBeTrue();
    }

    [Fact]
    public void CollectionComponent_WithoutStructuralRepresentation_ShouldCompareByReference()
    {
        // A Foundation não trata IEnumerable como estrutura: listas distintas com o mesmo conteúdo são diferentes
        var left = new RawListComponent(["a"]);
        var right = new RawListComponent(["a"]);

        left.Equals(right).ShouldBeFalse();
        left.Equals(new RawListComponent(left.Items)).ShouldBeTrue();
    }

    // Estabilidade dos componentes e limitação com estado mutável
    [Fact]
    public void StableComponents_ShouldKeepEqualityAndHashAcrossCalls()
    {
        var left = new Pair("a", 1);
        var right = new Pair("a", 1);

        left.Equals(right).ShouldBeTrue();
        left.Equals(right).ShouldBeTrue();
        left.GetHashCode().ShouldBe(left.GetHashCode());
    }

    [Fact]
    public void MutableComponents_AfterMutation_ShouldChangeEquality()
    {
        // Limitação contratual: a Foundation não impõe imutabilidade
        var value = new MutableValue("a");
        var twin = new MutableValue("a");

        value.Equals(twin).ShouldBeTrue();

        value.Text = "b";

        value.Equals(twin).ShouldBeFalse();
        value.Equals(new MutableValue("b")).ShouldBeTrue();
    }

    [Fact]
    public void MutableComponents_AfterInsertionInHashSet_MayCompromiseLookup()
    {
        // O objeto continua no conjunto, mas operações de lookup baseadas em hash PODEM falhar
        // (o hash do bucket original deixa de corresponder ao atual); o contrato não define o resultado.
        var value = new MutableValue("a");
        var set = new HashSet<MutableValue> { value };

        value.Text = "b";

        set.Count.ShouldBe(1);
        Should.NotThrow(() => set.Contains(value));
        Should.NotThrow(() => set.Contains(new MutableValue("a")));
    }

    [Fact]
    public void MutableComponents_ShouldNotCacheHash()
    {
        var value = new MutableValue("a");

        value.GetHashCode().ShouldBe(ExpectedHash(typeof(MutableValue), "a"));

        value.Text = "b";

        value.GetHashCode().ShouldBe(ExpectedHash(typeof(MutableValue), "b"));
    }

    // Enumeradores: descarte e propagação de exceções
    [Fact]
    public void Equals_AfterSuccessfulComparison_ShouldDisposeBothEnumerators()
    {
        var left = new ProbeSequence("a", "b");
        var right = new ProbeSequence("a", "b");

        new ProbeValue(left).Equals(new ProbeValue(right)).ShouldBeTrue();

        left.Disposals.ShouldBe(1);
        right.Disposals.ShouldBe(1);
    }

    [Fact]
    public void Equals_AfterEarlyDifference_ShouldDisposeBothEnumerators()
    {
        var left = new ProbeSequence("a", "b", "c");
        var right = new ProbeSequence("x", "b", "c");

        new ProbeValue(left).Equals(new ProbeValue(right)).ShouldBeFalse();

        left.Disposals.ShouldBe(1);
        right.Disposals.ShouldBe(1);
    }

    [Fact]
    public void Equals_WithDifferentLengths_ShouldDisposeBothEnumerators()
    {
        var left = new ProbeSequence("a");
        var right = new ProbeSequence("a", "b");

        new ProbeValue(left).Equals(new ProbeValue(right)).ShouldBeFalse();

        left.Disposals.ShouldBe(1);
        right.Disposals.ShouldBe(1);
    }

    [Fact]
    public void Equals_WhenAcquiringRightEnumeratorFails_ShouldDisposeLeftAndPropagate()
    {
        var left = new ProbeSequence("a");
        var right = new ProbeSequence("a") { ThrowOnGetEnumerator = true };

        Should.Throw<ProbeException>(() => new ProbeValue(left).Equals(new ProbeValue(right)));

        left.Disposals.ShouldBe(1);
    }

    [Fact]
    public void Equals_WhenLeftMoveNextThrows_ShouldPropagateAndDisposeBoth()
    {
        var left = new ProbeSequence("a", "b") { ThrowOnMoveNextAt = 1 };
        var right = new ProbeSequence("a", "b");

        Should.Throw<ProbeException>(() => new ProbeValue(left).Equals(new ProbeValue(right)));

        left.Disposals.ShouldBe(1);
        right.Disposals.ShouldBe(1);
    }

    [Fact]
    public void Equals_WhenRightMoveNextThrows_ShouldPropagateAndDisposeBoth()
    {
        var left = new ProbeSequence("a", "b");
        var right = new ProbeSequence("a", "b") { ThrowOnMoveNextAt = 0 };

        Should.Throw<ProbeException>(() => new ProbeValue(left).Equals(new ProbeValue(right)));

        left.Disposals.ShouldBe(1);
        right.Disposals.ShouldBe(1);
    }

    [Fact]
    public void Equals_WhenCurrentThrows_ShouldPropagateAndDisposeBoth()
    {
        var left = new ProbeSequence("a", "b") { ThrowOnCurrentAt = 0 };
        var right = new ProbeSequence("a", "b");

        Should.Throw<ProbeException>(() => new ProbeValue(left).Equals(new ProbeValue(right)));

        left.Disposals.ShouldBe(1);
        right.Disposals.ShouldBe(1);
    }

    [Fact]
    public void Equals_WhenComponentComparisonThrows_ShouldPropagateAndDisposeBoth()
    {
        var left = new ProbeSequence(new ThrowingComponent());
        var right = new ProbeSequence(new ThrowingComponent());

        Should.Throw<ProbeException>(() => new ProbeValue(left).Equals(new ProbeValue(right)));

        left.Disposals.ShouldBe(1);
        right.Disposals.ShouldBe(1);
    }

    [Fact]
    public void Equals_WhenDisposeThrows_ShouldPropagateAndStillAttemptToDisposeBoth()
    {
        var left = new ProbeSequence("a") { ThrowOnDispose = true };
        var right = new ProbeSequence("a") { ThrowOnDispose = true };

        Should.Throw<ProbeException>(() => new ProbeValue(left).Equals(new ProbeValue(right)));

        left.Disposals.ShouldBe(1);
        right.Disposals.ShouldBe(1);
    }

    [Fact]
    public void GetHashCode_WhenEnumerationThrows_ShouldPropagateAndDispose()
    {
        var sequence = new ProbeSequence("a", "b") { ThrowOnMoveNextAt = 1 };

        Should.Throw<ProbeException>(() => new ProbeValue(sequence).GetHashCode());

        sequence.Disposals.ShouldBe(1);
    }

    [Fact]
    public void GetHashCode_AfterEnumeration_ShouldDisposeEnumerator()
    {
        var sequence = new ProbeSequence("a", "b");

        _ = new ProbeValue(sequence).GetHashCode();

        sequence.Disposals.ShouldBe(1);
    }

    [Fact]
    public void Equals_WithSharedSequence_ShouldEnumerateIndependentlyAndRemainEqual()
    {
        // Duas instâncias podem compartilhar a mesma sequência reiniciável: cada comparação usa enumeradores próprios
        var shared = new ProbeSequence("a", "b");

        new ProbeValue(shared).Equals(new ProbeValue(shared)).ShouldBeTrue();
        shared.Disposals.ShouldBe(2);
    }

    // Limitações (documentadas, não testadas): sequências infinitas não terminam em Equals/GetHashCode;
    // sequências instáveis (que mudam entre enumerações) violam as leis de igualdade e hash.
    private static int ExpectedHash(Type type, params object?[] components)
    {
        var hash = new HashCode();
        hash.Add(type);

        foreach (var component in components)
        {
            hash.Add(component);
        }

        return hash.ToHashCode();
    }

    private sealed class ProbeException : Exception;

    private sealed class ThrowingComponent
    {
        public override bool Equals(object? obj) => throw new ProbeException();

        public override int GetHashCode() => 0;
    }

    private sealed class ProbeValue(ProbeSequence sequence) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents() => sequence;
    }

    private sealed class ProbeSequence(params object?[] items) : IEnumerable<object?>
    {
        public bool ThrowOnGetEnumerator { get; init; }

        public int? ThrowOnMoveNextAt { get; init; }

        public int? ThrowOnCurrentAt { get; init; }

        public bool ThrowOnDispose { get; init; }

        public int Disposals { get; private set; }

        public IEnumerator<object?> GetEnumerator() =>
            ThrowOnGetEnumerator ? throw new ProbeException() : new Probe(this, items);

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class Probe(ProbeSequence owner, object?[] items) : IEnumerator<object?>
        {
            private int index = -1;

            public object? Current =>
                owner.ThrowOnCurrentAt == index ? throw new ProbeException() : items[index];

            public bool MoveNext()
            {
                index++;

                return owner.ThrowOnMoveNextAt == index ? throw new ProbeException() : index < items.Length;
            }

            public void Reset() => index = -1;

            public void Dispose()
            {
                owner.Disposals++;

                if (owner.ThrowOnDispose)
                {
                    throw new ProbeException();
                }
            }
        }
    }

    private sealed class Pair(string? text, int? number) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return text;
            yield return number;
        }
    }

    private sealed class TwinPair(string? text, int? number) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return text;
            yield return number;
        }
    }

    private class BasePair(string? text, int? number) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return text;
            yield return number;
        }
    }

    private sealed class DerivedPair(string? text, int? number) : BasePair(text, number);

    private sealed class Sequence(params string[] items) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents() => items;
    }

    private sealed class FlatLists(string[] first, string[] second) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents() => [.. first, .. second];
    }

    private sealed class CountedLists(string[] first, string[] second) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return first.Length;

            foreach (var item in first)
            {
                yield return item;
            }

            yield return second.Length;

            foreach (var item in second)
            {
                yield return item;
            }
        }
    }

    private sealed class TagSet : ValueObject
    {
        private readonly string[] tags;

        public TagSet(params string[] tags) => this.tags = [.. tags.Distinct().OrderBy(tag => tag, StringComparer.Ordinal)];

        protected override IEnumerable<object?> GetEqualityComponents() => tags;
    }

    private sealed class Line(Pair? start, Pair? end) : ValueObject
    {
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return start;
            yield return end;
        }
    }

    private sealed class RawListComponent(List<string> items) : ValueObject
    {
        public List<string> Items { get; } = items;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Items;
        }
    }

    private sealed class MutableValue(string text) : ValueObject
    {
        public string Text { get; set; } = text;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Text;
        }
    }

    private sealed class CountingValue(int first, int second) : ValueObject
    {
        public int Enumerations { get; private set; }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            Enumerations++;

            yield return first;
            yield return second;
        }
    }
}

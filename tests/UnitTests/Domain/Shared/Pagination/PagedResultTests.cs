using System.Collections;
using Business.Platform.Core.Domain.Shared;

namespace Business.Platform.Core.UnitTests.Domain.Shared.Pagination;

public sealed class PagedResultTests
{
    private static PaginationRequest Request(int page, int size) => PaginationRequest.Create(page, size).Value;

    private static string[] Items(int count) => Enumerable.Range(1, count).Select(i => "item" + i).ToArray();

    [Fact]
    public void Create_WithTotalCountZero_ShouldHaveNoPages()
    {
        var result = PagedResult<string>.Create([], Request(1, 10), 0);

        result.TotalPages.ShouldBe(0);
        result.TotalCount.ShouldBe(0);
        result.HasPreviousPage.ShouldBeFalse();
        result.HasNextPage.ShouldBeFalse();
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public void Create_WithSinglePage_ShouldNotHaveNavigation()
    {
        var result = PagedResult<string>.Create(Items(5), Request(1, 10), 5);

        result.TotalPages.ShouldBe(1);
        result.HasPreviousPage.ShouldBeFalse();
        result.HasNextPage.ShouldBeFalse();
        result.PageNumber.ShouldBe(1);
        result.PageSize.ShouldBe(10);
    }

    [Fact]
    public void Create_WithExactMultipleOfPageSize_ShouldNotAddExtraPage()
    {
        PagedResult<string>.Create(Items(10), Request(1, 10), 10).TotalPages.ShouldBe(1);
        PagedResult<string>.Create(Items(10), Request(1, 10), 30).TotalPages.ShouldBe(3);
    }

    [Fact]
    public void Create_WithRemainder_ShouldAddExtraPage()
    {
        PagedResult<string>.Create(Items(10), Request(1, 10), 11).TotalPages.ShouldBe(2);
        PagedResult<string>.Create(Items(10), Request(1, 10), 29).TotalPages.ShouldBe(3);
        PagedResult<string>.Create(Items(1), Request(1, 10), 1).TotalPages.ShouldBe(1);
    }

    [Fact]
    public void Create_WithMiddlePage_ShouldHavePreviousAndNext()
    {
        var result = PagedResult<string>.Create(Items(10), Request(2, 10), 30);

        result.HasPreviousPage.ShouldBeTrue();
        result.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithLastPage_ShouldHavePreviousButNoNext()
    {
        var result = PagedResult<string>.Create(Items(10), Request(3, 10), 30);

        result.HasPreviousPage.ShouldBeTrue();
        result.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public void Create_WithFirstPageOfMany_ShouldHaveNextButNoPrevious()
    {
        var result = PagedResult<string>.Create(Items(10), Request(1, 10), 30);

        result.HasPreviousPage.ShouldBeFalse();
        result.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithPageBeyondTotal_ShouldBeAllowed()
    {
        var result = PagedResult<string>.Create([], Request(10, 10), 25);

        result.TotalPages.ShouldBe(3);
        result.HasPreviousPage.ShouldBeTrue();
        result.HasNextPage.ShouldBeFalse();
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public void Create_WithPageBeyondTotalAndItems_ShouldBeAllowed()
    {
        var result = PagedResult<string>.Create(Items(3), Request(10, 10), 25);

        result.Items.Count.ShouldBe(3);
        result.HasNextPage.ShouldBeFalse();
        result.HasPreviousPage.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithPageEqualToTotalPagesPlusOne_ShouldNotHaveNext()
    {
        var result = PagedResult<string>.Create([], Request(4, 10), 30);

        result.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public void Create_WithTotalCountOnePastBoundary_ShouldHaveNextOnLastFullPage()
    {
        var result = PagedResult<string>.Create(Items(10), Request(3, 10), 31);

        result.TotalPages.ShouldBe(4);
        result.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithLongMaxValue_ShouldComputeTotalPagesWithoutOverflow()
    {
        var result = PagedResult<string>.Create([], Request(1, 100), long.MaxValue);

        result.TotalCount.ShouldBe(long.MaxValue);
        result.TotalPages.ShouldBe((long.MaxValue / 100) + 1);
        result.HasNextPage.ShouldBeTrue();

        PagedResult<string>.Create([], Request(1, 1), long.MaxValue).TotalPages.ShouldBe(long.MaxValue);
    }

    [Fact]
    public void Create_WithNullItems_ShouldThrow()
    {
        var ex = Should.Throw<ArgumentNullException>(() => PagedResult<string>.Create(null!, Request(1, 10), 0));

        ex.ParamName.ShouldBe("items");
    }

    [Fact]
    public void Create_WithNullRequest_ShouldThrow()
    {
        var ex = Should.Throw<ArgumentNullException>(() => PagedResult<string>.Create([], null!, 0));

        ex.ParamName.ShouldBe("request");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void Create_WithNegativeTotalCount_ShouldThrow(long totalCount)
    {
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => PagedResult<string>.Create([], Request(1, 10), totalCount));

        ex.ParamName.ShouldBe("totalCount");
    }

    [Fact]
    public void Create_WithNegativeCount_ShouldThrow()
    {
        var items = new FakeCollection<string>(-1, []);

        var ex = Should.Throw<ArgumentException>(() => PagedResult<string>.Create(items, Request(1, 10), 0));

        ex.ParamName.ShouldBe("items");
        ex.ShouldNotBeOfType<ArgumentNullException>();
        items.Enumerated.ShouldBeFalse();
    }

    [Fact]
    public void Create_WithCountAbovePageSize_ShouldThrowBeforeEnumerating()
    {
        var items = new FakeCollection<string>(11, Items(11));

        var ex = Should.Throw<ArgumentException>(() => PagedResult<string>.Create(items, Request(1, 10), 100));

        ex.ParamName.ShouldBe("items");
        items.Enumerated.ShouldBeFalse();
    }

    [Fact]
    public void Create_WithCountEqualToPageSize_ShouldSucceed()
    {
        var result = PagedResult<string>.Create(Items(10), Request(1, 10), 10);

        result.Items.Count.ShouldBe(10);
    }

    [Fact]
    public void Create_ShouldReadCountExactlyOnce()
    {
        var items = new FakeCollection<string>(3, Items(3));

        PagedResult<string>.Create(items, Request(1, 10), 3);

        items.CountReads.ShouldBe(1);
    }

    [Fact]
    public void Create_WithEnumeratorMatchingCount_ShouldSucceed()
    {
        var items = new FakeCollection<string>(3, Items(3));

        var result = PagedResult<string>.Create(items, Request(1, 10), 3);

        result.Items.ShouldBe(Items(3));
    }

    [Fact]
    public void Create_WithEnumeratorYieldingMoreThanCount_ShouldThrow()
    {
        var items = new FakeCollection<string>(2, Items(3));

        var ex = Should.Throw<ArgumentException>(() => PagedResult<string>.Create(items, Request(1, 10), 3));

        ex.ParamName.ShouldBe("items");
        ex.ShouldNotBeOfType<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithEnumeratorYieldingLessThanCount_ShouldThrow()
    {
        var items = new FakeCollection<string>(3, Items(2));

        var ex = Should.Throw<ArgumentException>(() => PagedResult<string>.Create(items, Request(1, 10), 3));

        ex.ParamName.ShouldBe("items");
    }

    [Fact]
    public void Create_WithCountZeroAndOneElement_ShouldThrow()
    {
        var items = new FakeCollection<string>(0, Items(1));

        Should.Throw<ArgumentException>(() => PagedResult<string>.Create(items, Request(1, 10), 1))
            .ParamName.ShouldBe("items");
    }

    [Fact]
    public void Create_WithCountPositiveAndEmptyEnumeration_ShouldThrow()
    {
        var items = new FakeCollection<string>(1, []);

        Should.Throw<ArgumentException>(() => PagedResult<string>.Create(items, Request(1, 10), 1))
            .ParamName.ShouldBe("items");
    }

    [Fact]
    public void Create_WithInfiniteEnumeratorAndFiniteCount_ShouldThrowAndStop()
    {
        var items = new InfiniteCollection(3);

        var ex = Should.Throw<ArgumentException>(() => PagedResult<string>.Create(items, Request(1, 10), 3));

        ex.ParamName.ShouldBe("items");
        items.Produced.ShouldBe(4);
    }

    [Fact]
    public void Create_WithNullElement_ShouldThrow()
    {
        var items = new FakeCollection<string?>(3, ["a", null, "c"]);

        var ex = Should.Throw<ArgumentException>(() => PagedResult<string?>.Create(items, Request(1, 10), 3));

        ex.ParamName.ShouldBe("items");
        ex.ShouldNotBeOfType<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullAsFirstAndLastElement_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() =>
            PagedResult<string?>.Create(new FakeCollection<string?>(2, [null, "b"]), Request(1, 10), 2));
        Should.Throw<ArgumentException>(() =>
            PagedResult<string?>.Create(new FakeCollection<string?>(2, ["a", null]), Request(1, 10), 2));
    }

    [Fact]
    public void Create_WithNullElementAtPositionCountPlusOne_ShouldThrowArgumentException()
    {
        var items = new FakeCollection<string?>(2, ["a", "b", null]);

        var ex = Should.Throw<ArgumentException>(() => PagedResult<string?>.Create(items, Request(1, 10), 3));

        ex.ParamName.ShouldBe("items");
        ex.Message.ShouldContain("mais itens");
    }

    [Fact]
    public void Create_WithValueTypes_ShouldAcceptDefaultValues()
    {
        var result = PagedResult<int>.Create([0, 0, 1], Request(1, 10), 3);

        result.Items.ShouldBe([0, 0, 1]);
    }

    [Fact]
    public void Create_WhenEnumeratorThrows_ShouldPropagateOriginalException()
    {
        var original = new InvalidOperationException("boom");
        var items = new ThrowingCollection(original);

        var ex = Should.Throw<InvalidOperationException>(() => PagedResult<string>.Create(items, Request(1, 10), 3));

        ex.ShouldBeSameAs(original);
        ex.Message.ShouldBe("boom");
    }

    [Fact]
    public void Create_WithMoreItemsThanTotalCount_ShouldNotThrow()
    {
        var result = PagedResult<string>.Create(Items(5), Request(1, 10), 2);

        result.Items.Count.ShouldBe(5);
        result.TotalCount.ShouldBe(2);
        result.TotalPages.ShouldBe(1);
        result.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public void Create_WithItemsAndTotalCountZero_ShouldNotThrow()
    {
        var result = PagedResult<string>.Create(Items(3), Request(1, 10), 0);

        result.Items.Count.ShouldBe(3);
        result.TotalCount.ShouldBe(0);
        result.TotalPages.ShouldBe(0);
        result.HasPreviousPage.ShouldBeFalse();
        result.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public void Create_WithItemsAndTotalCountZeroOnLaterPage_ShouldNotThrow()
    {
        var result = PagedResult<string>.Create(Items(3), Request(4, 10), 0);

        result.Items.Count.ShouldBe(3);
        result.TotalPages.ShouldBe(0);
        result.HasPreviousPage.ShouldBeTrue();
        result.HasNextPage.ShouldBeFalse();
    }

    [Theory]
    [InlineData(1, 1, 10)]
    [InlineData(100, 100, 0)]
    [InlineData(7, 50, 3)]
    public void Create_WithPageBeyondTotalAndNonEmptyItems_ShouldNotThrowWhenCountWithinPageSize(int page, int size, long total)
    {
        var count = Math.Min(size, 3);

        var result = PagedResult<string>.Create(Items(count), Request(page, size), total);

        result.Items.Count.ShouldBe(count);
        result.HasNextPage.ShouldBe(page < result.TotalPages);
    }

    [Fact]
    public void Create_ShouldFollowValidationPrecedence()
    {
        var oversized = new FakeCollection<string>(-1, []);
        var tooMany = new FakeCollection<string>(50, []);

        Should.Throw<ArgumentNullException>(() => PagedResult<string>.Create(null!, null!, -1))
            .ParamName.ShouldBe("items");
        Should.Throw<ArgumentNullException>(() => PagedResult<string>.Create(tooMany, null!, -1))
            .ParamName.ShouldBe("request");
        Should.Throw<ArgumentOutOfRangeException>(() => PagedResult<string>.Create(tooMany, Request(1, 10), -1))
            .ParamName.ShouldBe("totalCount");
        Should.Throw<ArgumentException>(() => PagedResult<string>.Create(oversized, Request(1, 10), 1))
            .Message.ShouldContain("negativa");
        Should.Throw<ArgumentException>(() => PagedResult<string>.Create(tooMany, Request(1, 10), 1))
            .Message.ShouldContain("excede");
        tooMany.Enumerated.ShouldBeFalse();
    }

    [Fact]
    public void Create_ShouldCopyItemsDefensively()
    {
        var source = Items(3);

        var result = PagedResult<string>.Create(source, Request(1, 10), 3);
        source[0] = "changed";

        result.Items[0].ShouldBe("item1");

        var list = new List<string>(Items(2));
        var fromList = PagedResult<string>.Create(list, Request(1, 10), 2);
        list.Add("extra");
        list.Clear();

        fromList.Items.ShouldBe(Items(2));
    }

    [Fact]
    public void Create_ShouldPreserveOrder()
    {
        string[] source = ["z", "a", "m", "b"];

        var result = PagedResult<string>.Create(source, Request(1, 10), 4);

        result.Items.ShouldBe(source);
    }

    [Fact]
    public void Items_ShouldRejectMutationThroughIList()
    {
        var result = PagedResult<string>.Create(Items(2), Request(1, 10), 2);
        var list = result.Items.ShouldBeAssignableTo<IList<string>>().ShouldNotBeNull();

        list.IsReadOnly.ShouldBeTrue();
        Should.Throw<NotSupportedException>(() => list[0] = "x");
        Should.Throw<NotSupportedException>(() => list.Add("x"));
        Should.Throw<NotSupportedException>(() => list.Insert(0, "x"));
        Should.Throw<NotSupportedException>(() => list.Remove("item1"));
        Should.Throw<NotSupportedException>(() => list.RemoveAt(0));
        Should.Throw<NotSupportedException>(list.Clear);
        result.Items.ShouldBe(Items(2));
    }

    [Fact]
    public void Items_ShouldNotBeCastableToArray()
    {
        var result = PagedResult<string>.Create(Items(2), Request(1, 10), 2);

        result.Items.ShouldNotBeAssignableTo<string[]>();
    }

    [Fact]
    public void Items_ShouldOnlyProtectCollectionNotElements()
    {
        var element = new Box { Value = 1 };

        var result = PagedResult<Box>.Create([element], Request(1, 10), 1);
        result.Items[0].Value = 2;

        element.Value.ShouldBe(2);
    }

    [Fact]
    public void Create_WithEmptyCollection_ShouldProduceEmptyItems()
    {
        var result = PagedResult<string>.Create([], Request(1, 10), 7);

        result.Items.Count.ShouldBe(0);
        result.TotalPages.ShouldBe(1);
    }

    private sealed class Box
    {
        public int Value { get; set; }
    }

    private sealed class FakeCollection<T>(int count, IEnumerable<T> source) : IReadOnlyCollection<T>
    {
        private int _countReads;

        public int CountReads => _countReads;

        public bool Enumerated { get; private set; }

        public int Count
        {
            get
            {
                _countReads++;
                return count;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            Enumerated = true;
            return source.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class InfiniteCollection(int count) : IReadOnlyCollection<string>
    {
        public int Produced { get; private set; }

        public int Count => count;

        public IEnumerator<string> GetEnumerator()
        {
            while (true)
            {
                Produced++;
                yield return "x";
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ThrowingCollection(Exception exception) : IReadOnlyCollection<string>
    {
        public int Count => 3;

        public IEnumerator<string> GetEnumerator()
        {
            yield return "a";
            throw exception;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

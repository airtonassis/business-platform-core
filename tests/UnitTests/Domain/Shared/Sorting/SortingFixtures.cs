using Business.Platform.Core.Domain.Shared;

namespace Business.Platform.Core.UnitTests.Domain.Shared.Sorting;

internal static class SortingFixtures
{
    // Identificador fortemente tipado sem IComparable<T>
    internal readonly record struct CustomerId(Guid Value);

    internal sealed class Order(Guid id) : AggregateRoot<Guid>(id)
    {
        public static string StaticName => "static";

        public string Name { get; init; } = string.Empty;

        public decimal Amount { get; init; }

        public int Number { get; init; }

        public DateTime Created { get; init; }

        public Address Address { get; init; } = new();

        public IReadOnlyList<string> Tags { get; init; } = [];

        public string Field = string.Empty;
    }

    internal sealed class Address
    {
        public string City { get; init; } = string.Empty;
    }

    internal sealed class Customer(CustomerId id) : AggregateRoot<CustomerId>(id)
    {
        public string Name { get; init; } = string.Empty;
    }
}

using System.Security.Cryptography;
using Business.Platform.Core.Application.Platform;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;

namespace Business.Platform.Core.UnitTests.Application.Platform;

public sealed class PlatformOperationsTests
{
    private static readonly OidcSubject Actor = new("https://issuer.example", "opaque-subject");

    [Fact]
    public void ReferenceCodec_HashesTheDecodedSecretAndRejectsNoncanonicalPaddingBits()
    {
        var canonical = new string('A', 43);
        OwnerReferenceCodec.TryHash(canonical, out var hash).ShouldBeTrue();
        hash.ShouldBe(SHA256.HashData(new byte[32]));
        OwnerReferenceCodec.TryHash(new string('A', 42) + "B", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+")]
    public void ReferenceCodec_RejectsMalformedReferences(string? input) =>
        OwnerReferenceCodec.TryHash(input, out _).ShouldBeFalse();

    [Fact]
    public void OperationId_IsStableCaseSensitiveAndRejectsWhitespace()
    {
        PlatformOperationId.TryCreate("operation-123", out var first).ShouldBeTrue();
        PlatformOperationId.TryCreate("operation-123", out var replay).ShouldBeTrue();
        PlatformOperationId.TryCreate("Operation-123", out var different).ShouldBeTrue();
        replay.ShouldBe(first);
        different.ShouldNotBe(first);
        PlatformOperationId.TryCreate(" operation-123", out _).ShouldBeFalse();
        PlatformOperationId.TryCreate(new string('a', 129), out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Issue_PersistsOnlyDigestAndReturnsSecretAfterSuccess()
    {
        var store = new RecordingStore();
        var result = await new PlatformOperations(store).IssueAsync(Actor, " Verified Name ", CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
        OwnerReferenceCodec.TryHash(result.Value.Reference, out var expectedHash).ShouldBeTrue();
        store.Hash.ShouldBe(expectedHash);
        store.DisplayName.ShouldBe("Verified Name");
        store.Subject.ShouldBeSameAs(Actor);
        result.Value.ToString().ShouldNotContain(result.Value.Reference);
    }

    [Fact]
    public async Task MalformedReference_DoesNotReachStore()
    {
        var store = new RecordingStore();
        var result = await new PlatformOperations(store).PreviewAsync(Guid.NewGuid(), "bad", CancellationToken.None);
        result.Error.Code.ShouldBe("Recipient.Unavailable");
        store.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task ChangedPayload_ReusesOperationIdSoStoreCanDetectConflict()
    {
        var store = new RecordingStore();
        var operations = new PlatformOperations(store);
        var reference = OwnerReferenceCodec.Generate();
        var user = Guid.NewGuid();
        await operations.CreateAsync(Actor, user, reference, "First", "same-key", CancellationToken.None);
        var operation = store.Operation;
        await operations.CreateAsync(Actor, user, reference, "Changed", "same-key", CancellationToken.None);
        store.Operation.ShouldBe(operation);
        store.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task CancelledRequest_DoesNotReachStore()
    {
        var store = new RecordingStore();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new PlatformOperations(store).IssueAsync(Actor, "Verified", cancellation.Token));
        store.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Issue_DependencyFailure_DoesNotExposeIssuedSecret()
    {
        var failure = new Error("Dependency.Unavailable", "Dependency unavailable.");
        var store = new RecordingStore { Failure = failure };
        var result = await new PlatformOperations(store).IssueAsync(Actor, "Verified", CancellationToken.None);
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeSameAs(failure);
        Should.Throw<InvalidOperationException>(() => { _ = result.Value; });
        store.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Preview_StoreFailure_PreservesFailureWithoutOwnerDisclosure()
    {
        var failure = new Error("Recipient.Unavailable", "Recipient unavailable.", ErrorType.NotFound);
        var store = new RecordingStore { Failure = failure };
        var result = await new PlatformOperations(store).PreviewAsync(Guid.NewGuid(), OwnerReferenceCodec.Generate(), CancellationToken.None);
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeSameAs(failure);
        Should.Throw<InvalidOperationException>(() => { _ = result.Value; });
        store.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Create_UnconfirmedOutcome_PreservesFailureWithoutCreationIds()
    {
        var failure = new Error("Operation.Unconfirmed", "Outcome unconfirmed.");
        var store = new RecordingStore { Failure = failure };
        var result = await new PlatformOperations(store).CreateAsync(Actor, Guid.NewGuid(), OwnerReferenceCodec.Generate(),
            "Organization", "same-key", CancellationToken.None);
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeSameAs(failure);
        Should.Throw<InvalidOperationException>(() => { _ = result.Value; });
        store.Calls.ShouldBe(1);
    }

    [Theory]
    [InlineData("issue")]
    [InlineData("preview")]
    [InlineData("create")]
    public async Task StoreCancellation_PropagatesWithoutConvertingToSuccessOrFailure(string operation)
    {
        using var cancellation = new CancellationTokenSource();
        var store = new RecordingStore { CancelDuringCall = cancellation };
        var operations = new PlatformOperations(store);
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            switch (operation)
            {
                case "issue":
                    await operations.IssueAsync(Actor, "Verified", cancellation.Token);
                    break;
                case "preview":
                    await operations.PreviewAsync(Guid.NewGuid(), OwnerReferenceCodec.Generate(), cancellation.Token);
                    break;
                default:
                    await operations.CreateAsync(Actor, Guid.NewGuid(), OwnerReferenceCodec.Generate(), "Organization", "same-key", cancellation.Token);
                    break;
            }
        });
        exception.CancellationToken.ShouldBe(cancellation.Token);
        store.Calls.ShouldBe(1);
    }

    private sealed class RecordingStore : IPlatformStore
    {
        public Error? Failure { get; init; }
        public CancellationTokenSource? CancelDuringCall { get; init; }
        public int Calls { get; private set; }
        public byte[]? Hash { get; private set; }
        public string? DisplayName { get; private set; }
        public OidcSubject? Subject { get; private set; }
        public Guid Operation { get; private set; }

        public Task<Result<RecipientReferenceIssued>> IssueAsync(OidcSubject subject, byte[] hash, string displayName, CancellationToken cancellationToken)
        {
            Calls++; Hash = hash; Subject = subject; DisplayName = displayName;
            CancelDuringCall?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is { } failure) return Task.FromResult(Result.Failure<RecipientReferenceIssued>(failure));
            return Task.FromResult(Result.Success(new RecipientReferenceIssued(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(24))));
        }

        public Task<Result<OwnerPreview>> PreviewAsync(Guid userId, byte[] hash, CancellationToken cancellationToken)
        {
            Calls++; Hash = hash;
            CancelDuringCall?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is { } failure) return Task.FromResult(Result.Failure<OwnerPreview>(failure));
            return Task.FromResult(Result.Success(new OwnerPreview(userId, "Verified")));
        }

        public Task<Result<PlatformCreation>> CreateAsync(OidcSubject actor, Guid userId, byte[] hash, string name, Guid operation, CancellationToken cancellationToken)
        {
            Calls++; Hash = hash; Subject = actor; Operation = operation;
            CancelDuringCall?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is { } failure) return Task.FromResult(Result.Failure<PlatformCreation>(failure));
            return Task.FromResult(Result.Success(new PlatformCreation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), false)));
        }
    }
}

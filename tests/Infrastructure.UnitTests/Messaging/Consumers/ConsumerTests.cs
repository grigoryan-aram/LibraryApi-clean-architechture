using Application.DTOs;
using Application.IntegrationEvents;
using Application.Jobs;
using Application.ServiceInterfaces;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Infrastructure.Messaging.Consumers;
using LibraryApi.Domain.Entities;
using LibraryApi.Domain.RepositoryInterfaces;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Infrastructure.UnitTests.Messaging.Consumers;

/// <summary>
/// Both consumers resolve the member to an address and queue a Hangfire email.
/// </summary>
/// <remarks>
/// Enqueue is an extension method Moq cannot see, so these assert on
/// <c>IBackgroundJobClient.Create</c> and the job's arguments — which is what
/// catches an argument-order swap.
/// </remarks>
public class ConsumerTests
{
    private static readonly DateTime DueAt = new(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IMembersRepository> _members = new();
    private readonly Mock<IIdentityService> _identity = new();
    private readonly Mock<IBooksRepository> _books = new();
    private readonly Mock<IBackgroundJobClient> _jobs = new();

    private MemberContactLookup Contacts =>
        new(_members.Object, _identity.Object, NullLogger<MemberContactLookup>.Instance);

    private static ConsumeContext<T> ContextFor<T>(T message) where T : class
    {
        var context = new Mock<ConsumeContext<T>>();
        context.Setup(c => c.Message).Returns(message);
        context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }

    private void GivenMemberWithAccount(int memberId = 2)
    {
        _members.Setup(r => r.GetMemberByIdAsync(memberId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemberModel { Id = memberId, Name = "ada", IdentityUserId = "user-1" });
        _identity.Setup(s => s.FindContactAsync("user-1", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new AccountContactDTO("ada", "ada@example.com"));
    }

    private void GivenWalkInMember(int memberId = 2) =>
        _members.Setup(r => r.GetMemberByIdAsync(memberId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemberModel { Id = memberId, Name = "Walk-in" });

    private void VerifyQueued<TJob>(params object[] args) =>
        _jobs.Verify(c => c.Create(
            It.Is<Job>(job => job.Type == typeof(TJob) && job.Args.SequenceEqual(args)),
            It.IsAny<EnqueuedState>()), Times.Once);

    private void VerifyNothingQueued() =>
        _jobs.Verify(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Never);

    // ---------- BookBorrowed ----------

    private BookBorrowedConsumer BorrowedSut() =>
        new(Contacts, _jobs.Object, NullLogger<BookBorrowedConsumer>.Instance);

    private static BookBorrowed Borrowed =>
        new(LoanId: 99, BookId: 1, BookTitle: "Dune", MemberId: 2,
            BorrowedAt: DueAt.AddDays(-14), DueAt: DueAt);

    [Fact]
    public async Task BookBorrowed_queues_a_loan_receipt_to_the_member()
    {
        GivenMemberWithAccount();

        await BorrowedSut().Consume(ContextFor(Borrowed));

        VerifyQueued<SendLoanReceiptEmailJob>("ada@example.com", "ada", "Dune", DueAt);
    }

    // A walk-in has no account and no address. That is normal, so the message
    // is acknowledged rather than retried into the error queue.
    [Fact]
    public async Task BookBorrowed_queues_nothing_for_a_walk_in_and_does_not_fail()
    {
        GivenWalkInMember();

        await BorrowedSut().Consume(ContextFor(Borrowed));

        VerifyNothingQueued();
        _identity.Verify(s => s.FindContactAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BookBorrowed_queues_nothing_when_the_member_has_since_been_deleted()
    {
        _members.Setup(r => r.GetMemberByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MemberModel?)null);

        await BorrowedSut().Consume(ContextFor(Borrowed));

        VerifyNothingQueued();
    }

    // ---------- BookReturned ----------

    private BookReturnedConsumer ReturnedSut() =>
        new(Contacts, _books.Object, _jobs.Object, NullLogger<BookReturnedConsumer>.Instance);

    private static BookReturned ReturnedLate =>
        new(LoanId: 99, BookId: 1, MemberId: 2,
            BorrowedAt: DueAt.AddDays(-14), DueAt: DueAt, ReturnedAt: DueAt.AddDays(3));

    [Fact]
    public async Task BookReturned_queues_a_return_receipt_carrying_the_title_and_lateness()
    {
        GivenMemberWithAccount();
        _books.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new BookModel { Id = 1, Title = "Dune" });

        await ReturnedSut().Consume(ContextFor(ReturnedLate));

        VerifyQueued<SendReturnReceiptEmailJob>(
            "ada@example.com", "ada", "Dune", DueAt.AddDays(3), true);
    }

    [Fact]
    public async Task BookReturned_still_sends_the_receipt_when_the_book_has_since_been_deleted()
    {
        GivenMemberWithAccount();
        _books.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((BookModel?)null);

        await ReturnedSut().Consume(ContextFor(ReturnedLate));

        VerifyQueued<SendReturnReceiptEmailJob>(
            "ada@example.com", "ada", "book #1", DueAt.AddDays(3), true);
    }

    [Fact]
    public async Task BookReturned_queues_nothing_for_a_walk_in()
    {
        GivenWalkInMember();

        await ReturnedSut().Consume(ContextFor(ReturnedLate));

        VerifyNothingQueued();
    }
}

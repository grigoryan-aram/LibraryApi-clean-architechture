using Application.IntegrationEvents;
using Application.Jobs;
using Hangfire;
using LibraryApi.Domain.RepositoryInterfaces;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging.Consumers
{
    /// <summary>
    /// Emails the borrower a return receipt, noting a late return.
    /// </summary>
    /// <remarks>
    /// Queues a Hangfire job for the same reasons as
    /// <see cref="BookBorrowedConsumer"/>.
    /// </remarks>
    public class BookReturnedConsumer : IConsumer<BookReturned>
    {
        private readonly MemberContactLookup _contacts;
        private readonly IBooksRepository _books;
        private readonly IBackgroundJobClient _jobs;
        private readonly ILogger<BookReturnedConsumer> _logger;

        public BookReturnedConsumer(
            MemberContactLookup contacts,
            IBooksRepository books,
            IBackgroundJobClient jobs,
            ILogger<BookReturnedConsumer> logger)
        {
            _contacts = contacts;
            _books = books;
            _jobs = jobs;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<BookReturned> context)
        {
            var loan = context.Message;

            var contact = await _contacts.FindAsync(loan.MemberId, context.CancellationToken);

            if (contact is null)
            {
                return;
            }

            // The book may have been deleted since; the receipt still goes out.
            var book = await _books.GetByIdAsync(loan.BookId, context.CancellationToken);
            var title = book?.Title ?? $"book #{loan.BookId}";

            _jobs.Enqueue<SendReturnReceiptEmailJob>(job => job.ExecuteAsync(
                contact.Email,
                contact.Username,
                title,
                loan.ReturnedAt,
                loan.WasOverdue));

            _logger.LogInformation(
                "Queued a return receipt for loan {LoanId}.",
                loan.LoanId);
        }
    }
}

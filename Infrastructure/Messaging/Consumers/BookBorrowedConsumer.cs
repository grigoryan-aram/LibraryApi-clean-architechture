using Application.IntegrationEvents;
using Application.Jobs;
using Hangfire;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging.Consumers
{
    /// <summary>
    /// Emails the borrower a receipt with the due date.
    /// </summary>
    /// <remarks>
    /// Queues a Hangfire job rather than sending inline, the same path as the
    /// welcome email: the send then gets Hangfire's retries and dashboard, and
    /// SendLoanReceiptEmailJob stays the one place that throws to signal
    /// failure. Should the enqueue itself throw, MassTransit retries the
    /// message.
    /// </remarks>
    public class BookBorrowedConsumer : IConsumer<BookBorrowed>
    {
        private readonly MemberContactLookup _contacts;
        private readonly IBackgroundJobClient _jobs;
        private readonly ILogger<BookBorrowedConsumer> _logger;

        public BookBorrowedConsumer(
            MemberContactLookup contacts,
            IBackgroundJobClient jobs,
            ILogger<BookBorrowedConsumer> logger)
        {
            _contacts = contacts;
            _jobs = jobs;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<BookBorrowed> context)
        {
            var loan = context.Message;

            var contact = await _contacts.FindAsync(loan.MemberId, context.CancellationToken);

            if (contact is null)
            {
                return;
            }

            _jobs.Enqueue<SendLoanReceiptEmailJob>(job => job.ExecuteAsync(
                contact.Email,
                contact.Username,
                loan.BookTitle,
                loan.DueAt));

            _logger.LogInformation(
                "Queued a loan receipt for loan {LoanId}.",
                loan.LoanId);
        }
    }
}

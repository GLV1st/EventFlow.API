using Azure;
using Azure.Communication.Email;
using EventFlow.Shared.DTOs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mime;

namespace EventFlow.Api.Functions;

public class EmailPhysicalTicketsFunction
{
    private readonly ILogger<EmailPhysicalTicketsFunction> _logger;

    public EmailPhysicalTicketsFunction(
        ILogger<EmailPhysicalTicketsFunction> logger)
    {
        _logger = logger;
    }

    [Function("EmailPhysicalTickets")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "tickets/email-physical"
        )]
        HttpRequestData req)
    {
        try
        {
            var request =
                await req.ReadFromJsonAsync<EmailPhysicalTicketsRequest>();

            if (request == null)
            {
                return ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "Invalid request."
                );
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "Email address is required."
                );
            }

            if (!IsValidEmail(request.Email))
            {
                return ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "Invalid email address."
                );
            }

            if (string.IsNullOrWhiteSpace(request.EventId))
            {
                return ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "Event ID is required."
                );
            }

            if (string.IsNullOrWhiteSpace(request.EventName))
            {
                return ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "Event name is required."
                );
            }

            if (string.IsNullOrWhiteSpace(request.TicketFrom))
            {
                return ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "Starting ticket number is required."
                );
            }

            if (string.IsNullOrWhiteSpace(request.TicketTo))
            {
                return ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "Ending ticket number is required."
                );
            }

            if (string.IsNullOrWhiteSpace(request.Pdf))
            {
                return ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "PDF data is required."
                );
            }

            var connectionString =
                Environment.GetEnvironmentVariable(
                    "COMMUNICATION_SERVICES_CONNECTION_STRING");

            var sender =
                Environment.GetEnvironmentVariable(
                    "COMMUNICATION_SERVICES_SENDER");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogError(
                    "COMMUNICATION_SERVICES_CONNECTION_STRING application setting is missing.");

                return ErrorResponse(
                    req,
                    HttpStatusCode.InternalServerError,
                    "Email service connection is not configured."
                );
            }

            if (string.IsNullOrWhiteSpace(sender))
            {
                _logger.LogError(
                    "COMMUNICATION_SERVICES_SENDER application setting is missing.");

                return ErrorResponse(
                    req,
                    HttpStatusCode.InternalServerError,
                    "Email sender is not configured."
                );
            }

            byte[] pdfBytes;

            try
            {
                pdfBytes = ConvertPdfToBytes(request.Pdf);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to decode the supplied PDF.");

                return ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "The supplied PDF could not be decoded."
                );
            }

            // Keep the attachment size sensible.
            const int maxPdfSize = 7 * 1024 * 1024;

            if (pdfBytes.Length > maxPdfSize)
            {
                return ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "The PDF is too large to email. Please select a smaller ticket range."
                );
            }

            _logger.LogInformation(
                "Preparing physical ticket email. Recipient: {Email}, Sender: {Sender}, Event: {EventId}, Tickets: {TicketFrom} to {TicketTo}, PDF size: {PdfSize} bytes",
                request.Email,
                sender,
                request.EventId,
                request.TicketFrom,
                request.TicketTo,
                pdfBytes.Length
            );

            var emailClient =
                new EmailClient(connectionString);

            var subject =
                $"{request.EventName} - Physical Tickets";

            var plainText =
                $"""
                EventFlow Physical Tickets

                Event: {request.EventName}
                Ticket Type: {request.TicketType}
                Tickets: {request.TicketFrom} to {request.TicketTo}

                The requested physical tickets are attached to this email.
                """;

            var html =
                $"""
                <html>
                <body style="font-family:Arial,sans-serif;">
                    <h2>EventFlow Physical Tickets</h2>

                    <p>
                        <strong>Event:</strong>
                        {System.Net.WebUtility.HtmlEncode(request.EventName)}
                    </p>

                    <p>
                        <strong>Ticket Type:</strong>
                        {System.Net.WebUtility.HtmlEncode(request.TicketType)}
                    </p>

                    <p>
                        <strong>Tickets:</strong>
                        {System.Net.WebUtility.HtmlEncode(request.TicketFrom)}
                        to
                        {System.Net.WebUtility.HtmlEncode(request.TicketTo)}
                    </p>

                    <p>
                        The requested physical tickets are attached to this email.
                    </p>

                    <p>
                        <strong>EventFlow</strong>
                    </p>
                </body>
                </html>
                """;

            var recipient =
                new EmailAddress(request.Email);

            var recipients =
                new EmailRecipients(
                    new List<EmailAddress>
                    {
                        recipient
                    });

            var emailContent =
                new EmailContent(subject)
                {
                    PlainText = plainText,
                    Html = html
                };

            var emailMessage =
                new EmailMessage(
                    senderAddress: sender,
                    recipients: recipients,
                    content: emailContent);

            var attachment =
                new EmailAttachment(
                    "EventFlow-Physical-Tickets.pdf",
                    MediaTypeNames.Application.Pdf,
                    BinaryData.FromBytes(pdfBytes));

            emailMessage.Attachments.Add(attachment);

            _logger.LogInformation(
                "Sending physical ticket email to {Email} from {Sender}",
                request.Email,
                sender
            );

            var operation =
                await emailClient.SendAsync(
                    Azure.WaitUntil.Completed,
                    emailMessage);

            _logger.LogInformation(
                "Azure Communication Services email send completed. OperationId: {OperationId}",
                operation.Id
            );

            var response =
                req.CreateResponse(HttpStatusCode.OK);

            await response.WriteAsJsonAsync(
                new
                {
                    Status = "OK",
                    Message = "Physical tickets emailed successfully.",
                    Email = request.Email,
                    TicketFrom = request.TicketFrom,
                    TicketTo = request.TicketTo,
                    OperationId = operation.Id
                });

            return response;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(
                ex,
                "Azure Communication Services email failed. Status: {Status}, ErrorCode: {ErrorCode}, Message: {Message}",
                ex.Status,
                ex.ErrorCode,
                ex.Message);

            return ErrorResponse(
                req,
                HttpStatusCode.BadGateway,
                $"Azure email service failed. Status: {ex.Status}, ErrorCode: {ex.ErrorCode}, Message: {ex.Message}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while sending physical ticket email.");

            return ErrorResponse(
                req,
                HttpStatusCode.InternalServerError,
                $"Unexpected email error: {ex.Message}"
            );
        }
    }

    private static byte[] ConvertPdfToBytes(string pdfData)
    {
        var base64 = pdfData;

        var commaIndex = base64.IndexOf(',');

        if (commaIndex >= 0)
        {
            base64 = base64[(commaIndex + 1)..];
        }

        base64 = base64.Trim();

        return Convert.FromBase64String(base64);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address = new System.Net.Mail.MailAddress(email);

            return address.Address.Equals(
                email,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static HttpResponseData ErrorResponse(
        HttpRequestData req,
        HttpStatusCode statusCode,
        string message)
    {
        var response = req.CreateResponse(statusCode);

        response.WriteAsJsonAsync(
            new
            {
                Status = "ERROR",
                Message = message
            }).GetAwaiter().GetResult();

        return response;
    }

    private class EmailPhysicalTicketsRequest
    {
        public string Email { get; set; } = "";

        public string EventId { get; set; } = "";

        public string EventName { get; set; } = "";

        public string TicketType { get; set; } = "";

        public string TicketFrom { get; set; } = "";

        public string TicketTo { get; set; } = "";

        public string Pdf { get; set; } = "";
    }
}
using Azure;
using Azure.Communication.Email;
using GateKeeper.Shared.DTOs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Net.Mime;

namespace GateKeeper.Api.Functions;

public class EmailPhysicalTicketsFunction
{
    [Function("EmailPhysicalTickets")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "tickets/email-physical"
        )]
        HttpRequestData req)
    {
        // =====================================
        // READ REQUEST
        // =====================================

        var request =
            await req.ReadFromJsonAsync<
                EmailPhysicalTicketsRequest>();


        if (request == null)
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "Invalid request.");
        }


        // =====================================
        // VALIDATE EMAIL
        // =====================================

        if (string.IsNullOrWhiteSpace(
            request.Email))
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "Email address is required.");
        }


        if (!IsValidEmail(
            request.Email))
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "Invalid email address.");
        }


        // =====================================
        // VALIDATE EVENT
        // =====================================

        if (string.IsNullOrWhiteSpace(
            request.EventId))
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "EventId is required.");
        }


        if (string.IsNullOrWhiteSpace(
            request.EventName))
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "EventName is required.");
        }


        // =====================================
        // VALIDATE TICKET RANGE
        // =====================================

        if (string.IsNullOrWhiteSpace(
            request.TicketFrom))
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "TicketFrom is required.");
        }


        if (string.IsNullOrWhiteSpace(
            request.TicketTo))
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "TicketTo is required.");
        }


        // =====================================
        // VALIDATE PDF
        // =====================================

        if (string.IsNullOrWhiteSpace(
            request.Pdf))
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "PDF data is required.");
        }


        // =====================================
        // GET AZURE SETTINGS
        // =====================================

        var connectionString =
            Environment.GetEnvironmentVariable(
                "COMMUNICATION_SERVICES_CONNECTION_STRING");


        var sender =
            Environment.GetEnvironmentVariable(
                "COMMUNICATION_SERVICES_SENDER");


        if (string.IsNullOrWhiteSpace(
            connectionString))
        {
            Console.WriteLine(
                "COMMUNICATION_SERVICES_CONNECTION_STRING is missing."
            );

            return await ErrorResponse(
                req,
                HttpStatusCode.InternalServerError,
                "Email service connection is not configured.");
        }


        if (string.IsNullOrWhiteSpace(
            sender))
        {
            Console.WriteLine(
                "COMMUNICATION_SERVICES_SENDER is missing."
            );

            return await ErrorResponse(
                req,
                HttpStatusCode.InternalServerError,
                "Email sender is not configured.");
        }


        // =====================================
        // CONVERT PDF
        // =====================================

        byte[] pdfBytes;

        try
        {
            pdfBytes =
                ConvertPdfToBytes(
                    request.Pdf);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "PDF conversion failed:"
            );

            Console.WriteLine(
                ex.ToString()
            );

            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "Invalid PDF data.");
        }


        if (pdfBytes.Length == 0)
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "PDF is empty.");
        }


        // =====================================
        // PROTECT AGAINST HUGE EMAILS
        // =====================================

        // Azure Communication Services has
        // message-size limitations.
        //
        // Keep a sensible limit here so that
        // accidentally selecting thousands of
        // tickets doesn't create an enormous
        // email request.

        const int maxPdfSize =
            7 * 1024 * 1024;


        if (pdfBytes.Length > maxPdfSize)
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "The selected tickets create a PDF that is too large to email. Select a smaller range.");
        }


        // =====================================
        // CREATE EMAIL CLIENT
        // =====================================

        try
        {
            var emailClient =
                new EmailClient(
                    connectionString);


            // =================================
            // EMAIL CONTENT
            // =================================

            var subject =
                $"{request.EventName} - Physical Tickets";


            var plainText =
                $"""
                Please find attached the physical tickets for:

                Event: {request.EventName}
                Ticket Type: {request.TicketType}
                Tickets: {request.TicketFrom} to {request.TicketTo}

                These tickets were generated by GateKeeper.
                """;


            var html =
                $"""
                <html>
                <body style="font-family:Arial,Helvetica,sans-serif;">

                    <h2>
                        GateKeeper Physical Tickets
                    </h2>

                    <p>
                        Please find your physical tickets attached.
                    </p>

                    <table
                        style="
                            border-collapse:collapse;
                            margin-top:20px;
                        "
                    >

                        <tr>

                            <td
                                style="
                                    padding:8px 15px;
                                    font-weight:bold;
                                "
                            >
                                Event
                            </td>

                            <td
                                style="
                                    padding:8px 15px;
                                "
                            >
                                {System.Net.WebUtility.HtmlEncode(
                                    request.EventName)}
                            </td>

                        </tr>

                        <tr>

                            <td
                                style="
                                    padding:8px 15px;
                                    font-weight:bold;
                                "
                            >
                                Ticket Type
                            </td>

                            <td
                                style="
                                    padding:8px 15px;
                                "
                            >
                                {System.Net.WebUtility.HtmlEncode(
                                    request.TicketType)}
                            </td>

                        </tr>

                        <tr>

                            <td
                                style="
                                    padding:8px 15px;
                                    font-weight:bold;
                                "
                            >
                                Tickets
                            </td>

                            <td
                                style="
                                    padding:8px 15px;
                                "
                            >
                                {System.Net.WebUtility.HtmlEncode(
                                    request.TicketFrom)}
                                -
                                {System.Net.WebUtility.HtmlEncode(
                                    request.TicketTo)}
                            </td>

                        </tr>

                    </table>


                    <p
                        style="
                            margin-top:25px;
                            color:#666;
                        "
                    >
                        Generated by GateKeeper.
                    </p>

                </body>
                </html>
                """;


            var emailContent =
                new EmailContent(
                    subject)
                {
                    PlainText =
                        plainText,

                    Html =
                        html
                };


            // =================================
            // RECIPIENT
            // =================================

            var recipient =
                new EmailAddress(
                    request.Email);


            var recipients =
                new EmailRecipients(
                    new List<EmailAddress>
                    {
                        recipient
                    });


            // =================================
            // EMAIL MESSAGE
            // =================================

            var emailMessage =
                new EmailMessage(
                    sender,
                    recipients,
                    emailContent);


            // =================================
            // PDF ATTACHMENT
            // =================================

            var attachment =
                new EmailAttachment(
                    "GateKeeper-Physical-Tickets.pdf",
                    MediaTypeNames.Application.Pdf,
                    BinaryData.FromBytes(
                        pdfBytes));


            emailMessage.Attachments.Add(
                attachment);


            // =================================
            // SEND EMAIL
            // =================================

            var operation =
                await emailClient.SendAsync(
                    Azure.WaitUntil.Completed,
                    emailMessage);


            Console.WriteLine(
                $"Physical ticket email sent. Operation ID: {operation.Id}"
            );


            // =================================
            // SUCCESS
            // =================================

            var response =
                req.CreateResponse(
                    HttpStatusCode.OK);


            await response.WriteAsJsonAsync(
                new
                {
                    Status = "SUCCESS",

                    Message =
                        "Physical ticket PDF emailed successfully.",

                    Email =
                        request.Email,

                    TicketFrom =
                        request.TicketFrom,

                    TicketTo =
                        request.TicketTo,

                    OperationId =
                        operation.Id
                });


            return response;
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine(
                "Azure Communication Services email failed:"
            );

            Console.WriteLine(
                ex.ToString()
            );


            return await ErrorResponse(
                req,
                HttpStatusCode.BadGateway,
                "Azure email service failed to send the message.");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "Physical ticket email failed:"
            );

            Console.WriteLine(
                ex.ToString()
            );


            return await ErrorResponse(
                req,
                HttpStatusCode.InternalServerError,
                "Unable to send the physical ticket email.");
        }
    }


    // =====================================
    // PDF CONVERSION
    // =====================================

    private static byte[] ConvertPdfToBytes(
        string pdfData)
    {
        // The browser sends the PDF as:

        // data:application/pdf;base64,AAAA...

        // Remove the data URI prefix.

        var commaIndex =
            pdfData.IndexOf(',');


        if (commaIndex >= 0)
        {
            pdfData =
                pdfData[
                    (commaIndex + 1)..];
        }


        // Remove whitespace.

        pdfData =
            pdfData.Trim();


        return Convert.FromBase64String(
            pdfData);
    }


    // =====================================
    // EMAIL VALIDATION
    // =====================================

    private static bool IsValidEmail(
        string email)
    {
        try
        {
            var address =
                new System.Net.Mail.MailAddress(
                    email);

            return
                address.Address.Equals(
                    email,
                    StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }


    // =====================================
    // ERROR RESPONSE
    // =====================================

    private static async Task<HttpResponseData>
        ErrorResponse(
            HttpRequestData req,
            HttpStatusCode status,
            string message)
    {
        var response =
            req.CreateResponse(
                status);


        await response.WriteAsJsonAsync(
            new
            {
                Status = "ERROR",

                Message =
                    message
            });


        return response;
    }


    // =====================================
    // REQUEST MODEL
    // =====================================

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
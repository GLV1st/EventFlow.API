using Azure;
using Azure.Communication.Email;
using Azure.Communication;
using Microsoft.Extensions.Configuration;

namespace EventFlow.Api.Services;

public sealed class TicketPilotEmailService
{
    private readonly EmailClient? _client;
    private readonly string _sender;
    private readonly string _appUrl;

    public TicketPilotEmailService(IConfiguration configuration)
    {
        var connectionString = configuration["CommunicationServicesConnectionString"];
        _sender = configuration["TicketPilotEmailSender"] ?? "noreply@ticketpilot.uk";
        _appUrl = (configuration["TicketPilotAppUrl"] ?? "https://ticketpilot.uk").TrimEnd('/');

        if (!string.IsNullOrWhiteSpace(connectionString))
            _client = new EmailClient(connectionString);
    }

    public async Task SendInvitationAsync(string email, string displayName, string role, string token)
    {
        var link = $"{_appUrl}/accept-invitation?token={Uri.EscapeDataString(token)}";
        var subject = "You've been invited to TicketPilot";
        var safeName = Escape(displayName);
        var safeRole = Escape(FormatRole(role));
        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:auto;color:#172033">
              <div style="background:#123b68;padding:24px;color:#fff">
                <h1 style="margin:0">TicketPilot</h1>
              </div>
              <div style="padding:28px">
                <h2>You've been invited</h2>
                <p>Hello {safeName},</p>
                <p>You've been invited to join an organisation on TicketPilot as <strong>{safeRole}</strong>.</p>
                <p>Use the button below to accept your invitation and create your password.</p>
                <p style="margin:30px 0"><a href="{Escape(link)}" style="background:#1769c2;color:#fff;text-decoration:none;padding:14px 22px;border-radius:6px;display:inline-block">Accept invitation</a></p>
                <p>If the button doesn't work, copy this link into your browser:<br><a href="{Escape(link)}">{Escape(link)}</a></p>
                <p>This invitation remains valid until it is accepted or revoked by an organisation administrator.</p>
                <p>If you weren't expecting this invitation, you can ignore this email.</p>
              </div>
            </div>
            """;
        var plain = $"Hello {displayName},\n\nYou've been invited to join TicketPilot as {FormatRole(role)}.\nAccept your invitation and create your password here:\n{link}\n\nThis invitation remains valid until accepted or revoked.";
        await SendAsync(email, subject, html, plain);
    }

    public async Task SendPasswordResetAsync(string email, string displayName, string organisationName, string token)
    {
        var link = $"{_appUrl}/auth.html?resetToken={Uri.EscapeDataString(token)}";
        var subject = "Reset your TicketPilot password";
        var safeName = Escape(displayName);
        var safeOrganisation = Escape(organisationName);
        var safeLink = Escape(link);
        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:auto;color:#172033">
              <div style="background:#123b68;padding:24px;color:#fff"><h1 style="margin:0">TicketPilot</h1></div>
              <div style="padding:28px">
                <h2>Password reset requested</h2>
                <p>Hello {safeName},</p>
                <p>A password reset was requested for your account with <strong>{safeOrganisation}</strong>.</p>
                <p style="margin:30px 0"><a href="{safeLink}" style="background:#1769c2;color:#fff;text-decoration:none;padding:14px 22px;border-radius:6px;display:inline-block">Reset password</a></p>
                <p>This link expires in 30 minutes and can only be used once.</p>
                <p>If you did not request this, you can safely ignore this email. Your password will not change.</p>
              </div>
            </div>
            """;
        var plain = $"Hello {displayName},\n\nA password reset was requested for your TicketPilot account with {organisationName}.\nReset your password within 30 minutes using this one-time link:\n{link}\n\nIf you did not request this, ignore this email.";
        await SendAsync(email, subject, html, plain);
    }

    public async Task SendOwnerWelcomeAsync(string email, string displayName, string organisationName)
    {
        var subject = "Welcome to TicketPilot";
        var dashboard = _appUrl;
        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:auto;color:#172033">
              <div style="background:#123b68;padding:24px;color:#fff"><h1 style="margin:0">TicketPilot</h1></div>
              <div style="padding:28px">
                <h2>Welcome to TicketPilot</h2>
                <p>Hello {Escape(displayName)},</p>
                <p>Your TicketPilot Owner account for <strong>{Escape(organisationName)}</strong> has been created.</p>
                <p>You can now sign in, configure your events and invite your team.</p>
                <p style="margin:30px 0"><a href="{Escape(dashboard)}" style="background:#1769c2;color:#fff;text-decoration:none;padding:14px 22px;border-radius:6px;display:inline-block">Open TicketPilot</a></p>
                <p>For security, keep your password private and only invite people who need access.</p>
              </div>
            </div>
            """;
        var plain = $"Hello {displayName},\n\nYour TicketPilot Owner account for {organisationName} has been created.\nSign in at {dashboard}\n\nWelcome to TicketPilot.";
        await SendAsync(email, subject, html, plain);
    }

    private async Task SendAsync(string recipient, string subject, string html, string plain)
    {
        if (_client == null)
            throw new InvalidOperationException("TicketPilot email is not configured. Set CommunicationServicesConnectionString in the Function App settings.");

        var content = new EmailContent(subject)
        {
            Html = html,
            PlainText = plain
        };
        var message = new EmailMessage(_sender, recipient, content);
        await _client.SendAsync(WaitUntil.Completed, message);
    }

    private static string FormatRole(string role) =>
        string.Join(" ", role.ToLowerInvariant().Split('_').Select(x =>
            char.ToUpperInvariant(x[0]) + x[1..]));

    private static string Escape(string value) =>
        System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}

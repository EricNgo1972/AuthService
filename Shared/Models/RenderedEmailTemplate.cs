namespace AuthService.Shared.Models;

public sealed record RenderedEmailTemplate(string Subject, string MarkdownBody, string HtmlBody, string TextBody);

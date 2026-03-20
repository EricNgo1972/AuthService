using System.Reflection;
using System.Text.RegularExpressions;
using AuthService.Application.Interfaces;
using AuthService.Shared.Models;
using Markdig;

namespace AuthService.Infrastructure.Email;

public sealed class EmbeddedEmailTemplateRenderer : IEmailTemplateRenderer
{
    private readonly Assembly _assembly = typeof(EmbeddedEmailTemplateRenderer).Assembly;
    private readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    public async Task<RenderedEmailTemplate> RenderAsync(string templateName, IReadOnlyDictionary<string, string> variables, string defaultSubject, CancellationToken cancellationToken = default)
    {
        var resourceName = _assembly.GetManifestResourceNames()
            .FirstOrDefault(x => x.EndsWith($".Templates.Emails.{templateName}.md", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
        {
            throw new InvalidOperationException($"Email template '{templateName}' was not found.");
        }

        await using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Email template '{templateName}' stream is missing.");
        using var reader = new StreamReader(stream);
        var markdown = await reader.ReadToEndAsync(cancellationToken);

        foreach (var pair in variables)
        {
            markdown = markdown.Replace($"{{{{{pair.Key}}}}}", pair.Value ?? string.Empty, StringComparison.Ordinal);
        }

        var subject = defaultSubject;
        var subjectMatch = Regex.Match(markdown, @"^Subject:\s*(.+)$", RegexOptions.Multiline);
        if (subjectMatch.Success)
        {
            subject = subjectMatch.Groups[1].Value.Trim();
            markdown = Regex.Replace(markdown, @"^Subject:\s*.+\r?\n", string.Empty, RegexOptions.Multiline);
        }

        var html = Markdown.ToHtml(markdown, _markdownPipeline);
        var text = Regex.Replace(markdown, @"[#>*_`-]", string.Empty).Trim();
        return new RenderedEmailTemplate(subject, markdown, html, text);
    }
}

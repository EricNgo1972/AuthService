using AuthService.Shared.Models;

namespace AuthService.Application.Interfaces;

public interface IEmailTemplateRenderer
{
    Task<RenderedEmailTemplate> RenderAsync(string templateName, IReadOnlyDictionary<string, string> variables, string defaultSubject, CancellationToken cancellationToken = default);
}

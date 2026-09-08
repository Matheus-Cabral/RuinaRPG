namespace RuinaRPG.Contracts.Rules;

public record RulebookDocumentResponse(string Slug, string Titulo, string? IntroHtml, List<RulebookSectionResponse> Sections);

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Rules;

/// <summary>
/// Keeps the Livro de Regras' Graus & Círculos document (the "graus-e-circulos"
/// RulebookDocumentOverride, created from the embedded GRAUS &amp; CÍRCULOS.md when none exists
/// yet) in sync with the Efeitos catalog — see
/// docs/superpowers/specs/2026-09-29-efeitos-no-livro-de-regras-design.md and Requisitos -
/// Auditoria de Regras R0006.
///
/// AoCriar/AoEditar/AoExcluir only stage the change on the DbContext: the caller saves once, so
/// the Efeito write and the Livro write succeed or fail together. The override is only written
/// (or created) when the text actually changes — comparing after normalizing line endings — so a
/// no-op (a shared block, a block already present, a hand-written heading) never touches
/// UpdatedAt/UpdatedByUserId. SincronizarFaltantesAsync (startup, --migrate, "Restaurar padrão")
/// only adds missing blocks, never rewrites or removes, and saves itself.
/// </summary>
public class LivroDeRegrasEfeitosSync(RuinaRpgDbContext db)
{
    public const string Slug = "graus-e-circulos";

    public Task AoCriarAsync(Efeito efeito, Guid autor) =>
        AplicarAsync(md => GrauValido(efeito.Grau)
            ? GrausECirculosMarkdown.Inserir(md, efeito.Grau, EfeitoMarkdownBlock.Gerar(ParaLivro(efeito)))
            : md, autor);

    public Task AoEditarAsync(string nomeAntigo, Efeito efeito, Guid autor) =>
        AplicarAsync(md => GrauValido(efeito.Grau)
            ? GrausECirculosMarkdown.Substituir(md, nomeAntigo, efeito.Grau, EfeitoMarkdownBlock.Gerar(ParaLivro(efeito)))
            : md, autor);

    public Task AoExcluirAsync(Efeito efeito, Guid autor) =>
        AplicarAsync(md => GrausECirculosMarkdown.Remover(md, efeito.Nome), autor);

    public async Task<int> SincronizarFaltantesAsync(Guid? autor = null)
    {
        var efeitos = await db.Efeitos
            .Where(e => !e.IsDeleted)
            .OrderBy(e => e.Grau).ThenBy(e => e.Nome)
            .ToListAsync();

        var inseridos = 0;
        await AplicarAsync(md =>
        {
            foreach (var efeito in efeitos)
            {
                if (!GrauValido(efeito.Grau) || GrausECirculosMarkdown.Contem(md, efeito.Nome))
                    continue;
                md = GrausECirculosMarkdown.Inserir(md, efeito.Grau, EfeitoMarkdownBlock.Gerar(ParaLivro(efeito)));
                inseridos++;
            }
            return md;
        }, autor);

        await db.SaveChangesAsync();
        return inseridos;
    }

    public static EfeitoParaLivro ParaLivro(Efeito e) => new(
        e.Nome, e.Grau, e.Descricao, e.TipoDeCusto,
        e.CustoFixo, e.CustoPorUnidade, e.UnidadeLabel, e.QuantidadeDerivadaDeEfeito,
        e.MaxUnidades, e.MaxEscalaPorGrau, e.CustoAlternativo, e.CustoAlternativoAPartirDoGrau,
        string.IsNullOrEmpty(e.PreRequisitosJson) ? [] : JsonSerializer.Deserialize<List<List<string>>>(e.PreRequisitosJson)!);

    // The document only has sections for Graus 1-9. EfeitosController rejects any other Grau, so
    // this is only a defensive skip for a legacy row written before that validation existed.
    private static bool GrauValido(int grau) => grau is >= 1 and <= 9;

    private async Task AplicarAsync(Func<string, string> operacao, Guid? autor)
    {
        var over = db.RulebookDocumentOverrides.Local.FirstOrDefault(o => o.Slug == Slug)
                   ?? await db.RulebookDocumentOverrides.FirstOrDefaultAsync(o => o.Slug == Slug);

        var atual = NormalizarQuebrasDeLinha(over?.MarkdownText ?? RulebookRenderer.ReadEmbeddedMarkdown(Slug));
        var resultado = NormalizarQuebrasDeLinha(operacao(atual));
        if (resultado == atual)
            return;

        if (over is null)
        {
            db.RulebookDocumentOverrides.Add(new RulebookDocumentOverride
            {
                Id = Guid.NewGuid(), Slug = Slug, MarkdownText = resultado,
                UpdatedByUserId = autor, UpdatedAt = DateTime.UtcNow,
            });
            return;
        }

        over.MarkdownText = resultado;
        over.UpdatedByUserId = autor;
        over.UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizarQuebrasDeLinha(string md) => md.Replace("\r\n", "\n");
}

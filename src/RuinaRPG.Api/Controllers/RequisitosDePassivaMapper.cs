using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

/// <summary>Converte os requisitos de uma Passiva entre o contrato (texto) e o domínio, validando.</summary>
public static class RequisitosDePassivaMapper
{
    public static bool TryParse(RequisitosDePassivaDto? dto, out RequisitosDePassiva? requisitos, out string? erro)
    {
        requisitos = null;
        erro = null;
        if (dto is null)
            return true;

        if (!TryEnum<Vocacao>(dto.Vocacao, "Vocação", out var vocacao, ref erro)
            || !TryEnum<Linhagem>(dto.Linhagem, "Linhagem", out var linhagem, ref erro)
            || !TryEnum<Variante>(dto.Variante, "Variante", out var variante, ref erro)
            || !TryEnum<AfinidadeElemental>(dto.Afinidade, "Afinidade", out var afinidade, ref erro)
            || !TryEnum<Estrela>(dto.Estrela, "Estrela", out var estrela, ref erro))
            return false;

        var classe = string.IsNullOrWhiteSpace(dto.Classe) ? null : dto.Classe.Trim();
        if (classe is not null && vocacao is null)
            return Fail("Uma Classe exige a Vocação.", out erro);
        if (variante is not null && (linhagem is null || !LinhagemVarianteValidator.IsValidCombination(linhagem.Value, variante.Value)))
            return Fail("A Variante exige a Linhagem correspondente.", out erro);
        if (dto.Nivel is < 1)
            return Fail("O Nível mínimo é 1.", out erro);
        if (dto.Graduacao is < 0 or > 9)
            return Fail("Grau/Círculo vai de 0 a 9.", out erro);

        Guid? historicoId = null;
        if (!string.IsNullOrWhiteSpace(dto.HistoricoId))
        {
            if (!Guid.TryParse(dto.HistoricoId, out var parsed))
                return Fail("Histórico não encontrado.", out erro);
            historicoId = parsed;
        }

        if (!TryList<Atributo>(dto.Atributos, "Atributo", out var atributos, out erro)
            || !TryList<SubAtributo>(dto.SubAtributos, "Sub-Atributo", out var subAtributos, out erro)
            || !TryList<Pericia>(dto.Pericias, "Perícia", out var pericias, out erro))
            return false;

        requisitos = new RequisitosDePassiva
        {
            Nivel = dto.Nivel, Vocacao = vocacao, Classe = classe, Linhagem = linhagem, Variante = variante,
            Graduacao = dto.Graduacao, CoracaoDeMana = dto.CoracaoDeMana == true ? true : null,
            Afinidade = afinidade, Estrela = estrela, HistoricoId = historicoId,
            Atributos = atributos.Select(a => new RequisitoDeAtributo(a.Alvo, a.Minimo)).ToList(),
            SubAtributos = subAtributos.Select(s => new RequisitoDeSubAtributo(s.Alvo, s.Minimo)).ToList(),
            Pericias = pericias.Select(p => new RequisitoDePericia(p.Alvo, p.Minimo)).ToList(),
        };
        return true;
    }

    public static RequisitosDePassivaDto? ToDto(RequisitosDePassiva? r) => r is null ? null : new(
        r.Nivel, r.Vocacao?.ToString(), r.Classe, r.Linhagem?.ToString(), r.Variante?.ToString(), r.Graduacao, r.CoracaoDeMana,
        r.Afinidade?.ToString(), r.Estrela?.ToString(), r.HistoricoId?.ToString(),
        r.Atributos.Select(a => new RequisitoMinimoDto(a.Atributo.ToString(), a.Minimo)).ToList(),
        r.SubAtributos.Select(s => new RequisitoMinimoDto(s.SubAtributo.ToString(), s.Minimo)).ToList(),
        r.Pericias.Select(p => new RequisitoMinimoDto(p.Pericia.ToString(), p.Minimo)).ToList());

    /// <summary>Nome do Histórico exigido, para a pendência; nulo se não há requisito ou ele foi removido.</summary>
    public static async Task<string?> NomeDoHistoricoAsync(RuinaRpgDbContext db, RequisitosDePassiva? r) =>
        r?.HistoricoId is { } id ? await db.Historicos.Where(h => h.Id == id).Select(h => h.Nome).FirstOrDefaultAsync() : null;

    private static bool TryEnum<T>(string? raw, string campo, out T? value, ref string? erro) where T : struct, Enum
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;
        if (Enum.TryParse<T>(raw, out var parsed) && Enum.IsDefined(parsed))
        {
            value = parsed;
            return true;
        }
        erro = $"{campo} desconhecido(a): {raw}.";
        return false;
    }

    private static bool TryList<T>(List<RequisitoMinimoDto>? items, string campo, out List<(T Alvo, int Minimo)> parsed, out string? erro) where T : struct, Enum
    {
        parsed = [];
        erro = null;
        foreach (var item in items ?? [])
        {
            if (!Enum.TryParse<T>(item.Alvo, out var alvo) || !Enum.IsDefined(alvo))
                return Fail($"{campo} desconhecido(a): {item.Alvo}.", out erro);
            if (item.Minimo < 0)
                return Fail($"O mínimo de {campo} não pode ser negativo.", out erro);
            if (parsed.Any(p => p.Alvo.Equals(alvo)))
                return Fail($"{campo} repetido(a): {item.Alvo}.", out erro);
            parsed.Add((alvo, item.Minimo));
        }
        return true;
    }

    private static bool Fail(string mensagem, out string? erro)
    {
        erro = mensagem;
        return false;
    }
}

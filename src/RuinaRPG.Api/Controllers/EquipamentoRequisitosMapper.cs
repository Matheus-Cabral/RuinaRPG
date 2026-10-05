using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Requisitos e Penalidade de um equipamento entre o contrato e o domínio. Os Requisitos reaproveitam o
/// mapper das Passivas e descartam os campos que um equipamento não aceita (Nível, Linhagem, Variante,
/// Grau/Círculo, Coração de Mana, Histórico). Estruturas vazias viram null.
/// </summary>
public static class EquipamentoRequisitosMapper
{
    public static bool TryParse(ItemTipo tipo, RequisitosDePassivaDto? requisitosDto, PenalidadeDeEquipamentoDto? penalidadeDto,
        IReadOnlyList<PericiaDefinicao> pericias, out RequisitosDePassiva? requisitos, out PenalidadeDeEquipamento? penalidade, out string? erro)
    {
        requisitos = null;
        penalidade = null;
        erro = null;
        if (!ItemFactory.AceitaRequisitos(tipo))
            return true;

        var soAceitos = requisitosDto is null ? null : requisitosDto with { Nivel = null, Linhagem = null, Variante = null, Graduacao = null, CoracaoDeMana = null, HistoricoId = null };
        if (!RequisitosDePassivaMapper.TryParse(soAceitos, pericias, out var parsed, out erro))
            return false;
        requisitos = EstaVazio(parsed) ? null : parsed;

        if (penalidadeDto is null)
            return true;
        if (!TryLinhas<Atributo>(penalidadeDto.Atributos, "Atributo", out var atributos, out erro)
            || !TryLinhas<SubAtributo>(penalidadeDto.SubAtributos, "Sub-Atributo", out var subAtributos, out erro)
            || !TryPericias(penalidadeDto.Pericias, pericias, out var periciasPenalizadas, out erro))
            return false;

        var montada = new PenalidadeDeEquipamento
        {
            Atributos = atributos.Select(a => new PenalidadeDeAtributo(a.Alvo, a.Valor)).ToList(),
            SubAtributos = subAtributos.Select(s => new PenalidadeDeSubAtributo(s.Alvo, s.Valor)).ToList(),
            Pericias = periciasPenalizadas,
            Texto = string.IsNullOrWhiteSpace(penalidadeDto.Texto) ? null : penalidadeDto.Texto.Trim(),
        };
        penalidade = montada.EstaVazia ? null : montada;
        return true;
    }

    public static PenalidadeDeEquipamentoDto? ToDto(PenalidadeDeEquipamento? p, IReadOnlyDictionary<int, PericiaDefinicao> porId) => p is null ? null : new(
        p.Atributos.Select(a => new PenalidadeLinhaDto(a.Atributo.ToString(), a.Valor)).ToList(),
        p.SubAtributos.Select(s => new PenalidadeLinhaDto(s.SubAtributo.ToString(), s.Valor)).ToList(),
        p.Pericias.Where(x => porId.TryGetValue(x.Pericia, out var def) && !def.IsDeleted)
            .Select(x => new PenalidadeLinhaDto(porId[x.Pericia].Chave, x.Valor)).ToList(),
        p.Texto);

    private static bool EstaVazio(RequisitosDePassiva? r) => r is null
        || (r.Vocacao is null && string.IsNullOrWhiteSpace(r.Classe) && r.Afinidade is null && r.Estrela is null
            && r.Atributos.Count == 0 && r.SubAtributos.Count == 0 && r.Pericias.Count == 0);

    private static bool TryLinhas<T>(List<PenalidadeLinhaDto>? items, string campo, out List<(T Alvo, int Valor)> parsed, out string? erro) where T : struct, Enum
    {
        parsed = [];
        erro = null;
        foreach (var item in items ?? [])
        {
            if (!Enum.TryParse<T>(item.Alvo, out var alvo) || !Enum.IsDefined(alvo))
                return Fail($"{campo} desconhecido(a): {item.Alvo}.", out erro);
            if (item.Valor < 1)
                return Fail($"A penalidade de {campo} deve ser pelo menos 1.", out erro);
            if (parsed.Any(p => p.Alvo.Equals(alvo)))
                return Fail($"{campo} repetido(a): {item.Alvo}.", out erro);
            parsed.Add((alvo, item.Valor));
        }
        return true;
    }

    private static bool TryPericias(List<PenalidadeLinhaDto>? items, IReadOnlyList<PericiaDefinicao> pericias, out List<PenalidadeDePericia> parsed, out string? erro)
    {
        const string campo = "Perícia";
        parsed = [];
        erro = null;
        foreach (var item in items ?? [])
        {
            var pericia = pericias.FirstOrDefault(p => !p.IsDeleted && p.Chave == item.Alvo);
            if (pericia is null)
                return Fail($"{campo} desconhecido(a): {item.Alvo}.", out erro);
            if (item.Valor < 1)
                return Fail($"A penalidade de {campo} deve ser pelo menos 1.", out erro);
            if (parsed.Any(p => p.Pericia == pericia.Id))
                return Fail($"{campo} repetido(a): {item.Alvo}.", out erro);
            parsed.Add(new PenalidadeDePericia(pericia.Id, item.Valor));
        }
        return true;
    }

    private static bool Fail(string mensagem, out string? erro)
    {
        erro = mensagem;
        return false;
    }
}

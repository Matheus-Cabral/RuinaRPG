using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Services;

/// <summary>Um equipamento presente na ficha. EmUso: Arma/Escudo equipados, Armadura num slot, Artefato na lista.</summary>
public record EquipamentoDaFicha(Guid ItemId, bool EmUso);

/// <summary>O que a linha de um equipamento mostra: requisitos, o que falta, as linhas numéricas da penalidade e o texto livre (só exibido).</summary>
public record AvaliacaoDeEquipamento(List<string> Requisitos, List<string> RequisitosPendentes, List<string> Penalidade, string? OutrasPenalidades);

/// <summary>
/// Avalia os Requisitos dos equipamentos de uma ficha (Personagem, NPC ou Criatura) e devolve as
/// penalidades ativas. A ficha é sempre o retrato SEM penalidades — recebido como função para só ser
/// calculado quando algum equipamento em uso tem requisitos.
/// </summary>
public class EquipmentPenaltyService(RuinaRpgDbContext db, IPericiaCatalogo pericias)
{
    public async Task<List<PenalidadeAtiva>> AtivasAsync(IReadOnlyCollection<EquipamentoDaFicha> equipamentos, Func<Task<FichaParaRequisitos>> fichaSemPenalidades)
    {
        var idsEmUso = equipamentos.Where(e => e.EmUso).Select(e => e.ItemId).Distinct().ToList();
        if (idsEmUso.Count == 0)
            return [];

        var itens = await db.Items.AsNoTracking().Where(i => idsEmUso.Contains(i.Id) && i.Requisitos != null).ToListAsync();
        if (itens.Count == 0)
            return [];

        var porId = itens.ToDictionary(i => i.Id);
        // Uma linha por equipamento em uso: dois exemplares do mesmo item penalizam duas vezes.
        var emUso = equipamentos.Where(e => e.EmUso && porId.ContainsKey(e.ItemId))
            .Select(e => porId[e.ItemId])
            .Select(i => new EquipamentoEmUso(i.Nome, i.Requisitos, i.PenalidadeDeRequisitos));
        return PenalidadesDeEquipamento.Ativas(emUso, await fichaSemPenalidades(), await NomeDaPericiaAsync());
    }

    public async Task<List<ArtifactBonusInput>> ComoModificadoresAsync(IEnumerable<PenalidadeAtiva> ativas)
    {
        var catalogo = await pericias.PorIdAsync();
        return PenalidadesDeEquipamento.ComoModificadores(ativas, id => catalogo.TryGetValue(id, out var p) && !p.IsDeleted ? p.Chave : null);
    }

    public async Task<List<PenalidadeAtivaResponse>> ComoRespostaAsync(IEnumerable<PenalidadeAtiva> ativas)
    {
        var nome = await NomeOuNuloAsync();
        return ativas.Select(a => new PenalidadeAtivaResponse(a.Nome, a.Pendencias.ToList(), PenalidadesDeEquipamento.Descrever(a.Penalidade, nome).ToList(), PenalidadesDeEquipamento.TextoLivre(a.Penalidade))).ToList();
    }

    /// <summary>Requisitos, pendências e penalidade por extenso de um item, para a linha da ficha.</summary>
    public async Task<AvaliacaoDeEquipamento> AvaliarAsync(Item item, Func<Task<FichaParaRequisitos>> fichaSemPenalidades)
    {
        var nome = await NomeOuNuloAsync();
        var penalidade = PenalidadesDeEquipamento.Descrever(item.PenalidadeDeRequisitos, nome).ToList();
        var outras = PenalidadesDeEquipamento.TextoLivre(item.PenalidadeDeRequisitos);
        // Sem requisitos não há o que avaliar (e a ficha não é calculada), mas a penalidade do item aparece.
        if (item.Requisitos is null)
            return new([], [], penalidade, outras);
        return new(
            PassivaRequisitosEvaluator.Descrever(item.Requisitos, null, nome).ToList(),
            PassivaRequisitosEvaluator.Pendencias(item.Requisitos, await fichaSemPenalidades(), null, await NomeDaPericiaAsync()).ToList(),
            penalidade,
            outras);
    }

    private async Task<Func<int, string>> NomeDaPericiaAsync()
    {
        var catalogo = await pericias.PorIdAsync();
        return id => catalogo.TryGetValue(id, out var p) ? p.Nome : id.ToString();
    }

    private async Task<Func<int, string?>> NomeOuNuloAsync()
    {
        var catalogo = await pericias.PorIdAsync();
        return id => catalogo.TryGetValue(id, out var p) && !p.IsDeleted ? p.Nome : null;
    }
}

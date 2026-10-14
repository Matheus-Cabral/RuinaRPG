using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Condicoes;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Services;

/// <summary>
/// Monta o resumo de um NPC/Criatura participante do encontro. Cada número sai dos mesmos serviços de cálculo que as
/// telas da ficha usam, duas vezes: com as Condições e sem elas. Quais Condições valem depende de quem é o dono:
/// numa ficha do GM (sem dono) são as do próprio participante, e só aqui elas agem; numa ficha concedida são as da
/// ficha (<see cref="CondicoesAtivasDaFicha.ParaNpcAsync"/>), as mesmas que o dono e o GM veem nos endpoints dela,
/// o que inclui as de outro encontro em andamento — o resumo nunca difere da ficha. Não escreve nada.
/// </summary>
public class ResumoDeFichaDoParticipante(RuinaRpgDbContext db, NpcSheetStats npcStats, CreatureSheetStats criaturaStats,
    CondicoesAtivasDaFicha condicoes, ItensDesabilitaveisDaFicha itens)
{
    private static readonly CategoriaDesabilitavel[] CategoriasDeEquipamento =
        [CategoriaDesabilitavel.Arma, CategoriaDesabilitavel.Armadura, CategoriaDesabilitavel.Escudo, CategoriaDesabilitavel.Artefato];

    private static readonly CategoriaDesabilitavel[] CategoriasDeMagia =
        [CategoriaDesabilitavel.Magia, CategoriaDesabilitavel.Habilidade, CategoriaDesabilitavel.Passiva, CategoriaDesabilitavel.Runa];

    /// <summary>Tudo o que difere entre os dois tipos de ficha, já calculado para um conjunto de Condições.</summary>
    private sealed record Numeros(List<AtributoCalculado> Atributos, SubAttributesResponseLite Sub, (int Vitalidade, int Foco, int Adrenalina) Maximos, List<PericiaCalculada> Pericias);

    private sealed record SubAttributesResponseLite(int Iniciativa, int Movimentacao, int EsquivaNatural, int DefesaNatural, int ReducaoFisica, int ReducaoMagica, int? EficienciaElemental, int? DanoElemental);

    /// <returns>null quando a ficha de origem não existe mais ou o participante não é de NPC nem de Criatura.</returns>
    public async Task<ResumoDeFichaResponse?> MontarAsync(EncounterParticipant participante)
    {
        if (participante.SourceNpcSheetId is { } npcId)
        {
            var ficha = await db.NpcSheets.FindAsync(npcId);
            if (ficha is null)
                return null;
            var efeitos = ficha.OwnerId is null ? await condicoes.DoParticipanteAsync(participante.Id) : await condicoes.ParaNpcAsync(ficha);
            var ativas = ficha.OwnerId is null ? await condicoes.ListarDoParticipanteAsync(participante.Id) : await condicoes.ListarDoNpcAsync(ficha);

            Func<EfeitosDeCondicao, Task<Numeros>> calcular = async e =>
            {
                var sub = await npcStats.SubAtributosAsync(ficha, e);
                var (vitalidade, foco, adrenalina, _) = await npcStats.MaximosAsync(ficha, e);
                return new Numeros(await npcStats.AtributosAsync(ficha, e), Enxuto(sub), (vitalidade, foco, adrenalina), await npcStats.PericiasAsync(ficha, e));
            };

            var traits = await db.NpcTraits.Where(t => t.NpcSheetId == ficha.Id).Join(db.Traits, nt => nt.TraitId, t => t.Id, (nt, t) => new ResumoDeCaracteristica(t.Nome, nt.Polaridade.ToString(), nt.Especificacao)).ToListAsync();
            var equipados = (await db.NpcWeapons.Where(w => w.NpcSheetId == ficha.Id && w.IsEquipped).Select(w => w.Id).ToListAsync())
                .Concat(await db.NpcShields.Where(s => s.NpcSheetId == ficha.Id && s.IsEquipped).Select(s => s.Id).ToListAsync()).ToHashSet();
            return await ComporAsync(participante, "Npc", ficha.Id, ficha.Nome, ficha.ImageId, ficha.Nivel, null, ficha.OwnerId is not null,
                (ficha.VitalidadeAtual, ficha.FocoAtual, ficha.AdrenalinaAtual), efeitos, ativas, calcular, traits, equipados);
        }

        if (participante.SourceCreatureSheetId is { } criaturaId)
        {
            var ficha = await db.CreatureSheets.FindAsync(criaturaId);
            if (ficha is null)
                return null;
            var efeitos = ficha.OwnerId is null ? await condicoes.DoParticipanteAsync(participante.Id) : await condicoes.ParaCriaturaAsync(ficha);
            var ativas = ficha.OwnerId is null ? await condicoes.ListarDoParticipanteAsync(participante.Id) : await condicoes.ListarDaCriaturaAsync(ficha);

            Func<EfeitosDeCondicao, Task<Numeros>> calcular = async e =>
                new(await criaturaStats.AtributosAsync(ficha, e), Enxuto(await criaturaStats.SubAtributosAsync(ficha, e)), await criaturaStats.MaximosAsync(ficha, e), await criaturaStats.PericiasAsync(ficha, e));

            var normais = await db.CreatureTraits.Where(t => t.CreatureSheetId == ficha.Id && t.TraitId != null)
                .Join(db.Traits, ct => ct.TraitId, t => t.Id, (ct, t) => new ResumoDeCaracteristica(t.Nome, ct.Polaridade.ToString(), ct.Especificacao)).ToListAsync();
            var exclusivas = await db.CreatureTraits.Where(t => t.CreatureSheetId == ficha.Id && t.CreatureExclusiveTraitId != null)
                .Join(db.Set<CreatureExclusiveTrait>(), ct => ct.CreatureExclusiveTraitId, t => t.Id, (ct, t) => new ResumoDeCaracteristica(t.Nome, ct.Polaridade.ToString(), ct.Especificacao)).ToListAsync();
            var equipados = (await db.CreatureWeapons.Where(w => w.CreatureSheetId == ficha.Id && w.IsEquipped).Select(w => w.Id).ToListAsync())
                .Concat(await db.CreatureShields.Where(s => s.CreatureSheetId == ficha.Id && s.IsEquipped).Select(s => s.Id).ToListAsync()).ToHashSet();
            return await ComporAsync(participante, "Criatura", ficha.Id, ficha.Nome, ficha.ImageId, ficha.Nivel, ficha.Rank?.ToString(), ficha.OwnerId is not null,
                (ficha.VitalidadeAtual, ficha.FocoAtual, ficha.AdrenalinaAtual), efeitos, ativas, calcular, [.. normais, .. exclusivas], equipados);
        }

        return null;
    }

    private async Task<ResumoDeFichaResponse> ComporAsync(EncounterParticipant participante, string tipo, Guid fichaId, string? nomeDaFicha, Guid? imageId, int nivel, string? rank,
        bool concedida, (int Vitalidade, int Foco, int Adrenalina) atuaisDaFicha, EfeitosDeCondicao efeitos, List<CondicaoAtivaNaFichaResponse> ativas,
        Func<EfeitosDeCondicao, Task<Numeros>> calcular, List<ResumoDeCaracteristica> caracteristicas, HashSet<Guid> emUsoSeEquipavel)
    {
        var com = await calcular(efeitos);
        var sem = efeitos.Vazio ? com : await calcular(EfeitosDeCondicao.Nenhum);

        string? imagemUrl = null;
        if (imageId is { } id && await db.Images.FindAsync(id) is { } imagem)
            imagemUrl = $"/images/{imagem.Path}";

        var atributos = com.Atributos.Select(a => new ResumoDeValor(a.Atributo, new(a.Total, sem.Atributos.Single(s => s.Atributo == a.Atributo).Total))).ToList();

        var subAtributos = new List<ResumoDeValor>
        {
            new(SubAtributoAlvo.Iniciativa, new(com.Sub.Iniciativa, sem.Sub.Iniciativa)),
            new(SubAtributoAlvo.Movimentacao, new(com.Sub.Movimentacao, sem.Sub.Movimentacao)),
            new(SubAtributoAlvo.EsquivaNatural, new(com.Sub.EsquivaNatural, sem.Sub.EsquivaNatural)),
            new(SubAtributoAlvo.DefesaNatural, new(com.Sub.DefesaNatural, sem.Sub.DefesaNatural)),
            new(SubAtributoAlvo.ReducaoFisica, new(com.Sub.ReducaoFisica, sem.Sub.ReducaoFisica)),
            new(SubAtributoAlvo.ReducaoMagica, new(com.Sub.ReducaoMagica, sem.Sub.ReducaoMagica)),
        };
        if (com.Sub is { EficienciaElemental: { } ef, DanoElemental: { } de } && sem.Sub is { EficienciaElemental: { } efSem, DanoElemental: { } deSem })
        {
            subAtributos.Add(new("Eficiência Elemental", new(ef, efSem)));
            subAtributos.Add(new("Dano Elemental", new(de, deSem)));
        }

        // Ficha do GM: o Atual é o da instância (PV/PF/PA do participante); concedida: o da própria ficha, igual ao encontro.
        var (pv, pf, pa) = concedida ? atuaisDaFicha : (participante.PVAtual ?? atuaisDaFicha.Vitalidade, participante.PFAtual ?? atuaisDaFicha.Foco, participante.PAAtual ?? atuaisDaFicha.Adrenalina);
        var recursos = new List<ResumoDeRecurso>
        {
            new("Vitalidade", pv, new(com.Maximos.Vitalidade, sem.Maximos.Vitalidade)),
            new("Foco", pf, new(com.Maximos.Foco, sem.Maximos.Foco)),
            new("Adrenalina", pa, new(com.Maximos.Adrenalina, sem.Maximos.Adrenalina)),
        };

        // Só as Perícias com pontos e as que uma Condição mudou.
        var pericias = com.Pericias
            .Where(p => p.Total is not null)
            .Select(p => (Com: p, Sem: sem.Pericias.Single(s => s.Chave == p.Chave)))
            .Where(x => x.Com.Gasto > 0 || x.Com.Total != x.Sem.Total)
            .Select(x => new ResumoDePericia(x.Com.Chave, x.Com.Nome, x.Com.Gasto, x.Com.Atributo, new(x.Com.Total!.Value, x.Sem.Total ?? x.Com.Total.Value)))
            .ToList();

        var porCategoria = await itens.ListarAsync(participante);
        List<ResumoDeLinha> Linhas(CategoriaDesabilitavel[] categorias, bool soEmUso) => categorias
            .SelectMany(c => porCategoria[c].Where(i => !soEmUso || !(c is CategoriaDesabilitavel.Arma or CategoriaDesabilitavel.Escudo) || emUsoSeEquipavel.Contains(i.Id))
                .OrderBy(i => i.Nome, StringComparer.CurrentCulture)
                .Select(i => new ResumoDeLinha(c.ToString(), i.Nome, efeitos.DesabilitadoPor(c, i.Id))))
            .ToList();

        return new ResumoDeFichaResponse(fichaId.ToString(), tipo, nomeDaFicha ?? participante.Nome, imagemUrl, nivel, rank,
            atributos, subAtributos, recursos, pericias, Linhas(CategoriasDeEquipamento, soEmUso: true), Linhas(CategoriasDeMagia, soEmUso: false),
            caracteristicas, ativas);
    }

    private static SubAttributesResponseLite Enxuto(RuinaRPG.Contracts.CharacterSheets.SubAttributesResponse s) =>
        new(s.Iniciativa, s.Movimentacao, s.EsquivaNatural, s.DefesaNatural, s.ReducaoFisica, s.ReducaoMagica, s.EficienciaElemental, s.DanoElemental);
}

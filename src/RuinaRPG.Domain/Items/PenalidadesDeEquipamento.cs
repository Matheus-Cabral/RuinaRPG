using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Domain.Items;

/// <summary>Um equipamento em uso na ficha: Arma/Escudo equipados, Armadura num slot, Artefato na lista.</summary>
public sealed record EquipamentoEmUso(string Nome, RequisitosDePassiva? Requisitos, PenalidadeDeEquipamento? Penalidade);

/// <summary>Um equipamento em uso cujos Requisitos a ficha não cumpre. Penalidade pode ser nula (só aviso).</summary>
public sealed record PenalidadeAtiva(string Nome, IReadOnlyList<string> Pendencias, PenalidadeDeEquipamento? Penalidade);

public static class PenalidadesDeEquipamento
{
    /// <summary>
    /// Os equipamentos em uso com requisito pendente. <paramref name="fichaSemPenalidades"/> tem de ser o
    /// retrato da ficha SEM penalidades: assim uma penalidade nunca faz outro equipamento falhar.
    /// </summary>
    public static List<PenalidadeAtiva> Ativas(IEnumerable<EquipamentoEmUso> emUso, FichaParaRequisitos fichaSemPenalidades, Func<int, string> nomeDaPericia)
    {
        var ativas = new List<PenalidadeAtiva>();
        foreach (var equipamento in emUso)
        {
            if (equipamento.Requisitos is null)
                continue;
            var pendencias = PassivaRequisitosEvaluator.Pendencias(equipamento.Requisitos, fichaSemPenalidades, nomeDoHistoricoExigido: null, nomeDaPericia);
            if (pendencias.Count > 0)
                ativas.Add(new PenalidadeAtiva(equipamento.Nome, pendencias, equipamento.Penalidade));
        }
        return ativas;
    }

    /// <summary>
    /// As penalidades como entradas negativas no mesmo caminho dos bônus de Artefato, para somar nas
    /// fórmulas que já têm o termo Artefatos. Perícia removida (chave nula) não gera entrada.
    /// </summary>
    public static List<ArtifactBonusInput> ComoModificadores(IEnumerable<PenalidadeAtiva> ativas, Func<int, string?> chaveDaPericia)
    {
        var modificadores = new List<ArtifactBonusInput>();
        foreach (var penalidade in ativas.Select(a => a.Penalidade).OfType<PenalidadeDeEquipamento>())
        {
            modificadores.AddRange(penalidade.Atributos.Select(a => new ArtifactBonusInput(TipoDeAlvo.Atributo, a.Atributo.ToString(), -a.Valor)));
            modificadores.AddRange(penalidade.SubAtributos.Select(s => new ArtifactBonusInput(TipoDeAlvo.SubAtributo, AlvoDe(s.SubAtributo), -s.Valor)));
            foreach (var p in penalidade.Pericias)
                if (chaveDaPericia(p.Pericia) is { } chave)
                    modificadores.Add(new ArtifactBonusInput(TipoDeAlvo.Pericia, chave, -p.Valor));
        }
        return modificadores;
    }

    /// <summary>A penalidade por extenso: só as linhas numéricas, na ordem dos campos (o texto livre vai por TextoLivre).</summary>
    public static IReadOnlyList<string> Descrever(PenalidadeDeEquipamento? penalidade, Func<int, string?> nomeDaPericia)
    {
        var itens = new List<string>();
        if (penalidade is null)
            return itens;

        itens.AddRange(penalidade.Atributos.Select(a => $"{RequisitoLabels.Atributo(a.Atributo)} −{a.Valor}"));
        itens.AddRange(penalidade.SubAtributos.Select(s => $"{RequisitoLabels.SubAtributo(s.SubAtributo)} −{s.Valor}"));
        foreach (var p in penalidade.Pericias)
            if (nomeDaPericia(p.Pericia) is { } nome)
                itens.Add($"{nome} −{p.Valor}");
        return itens;
    }

    /// <summary>O texto livre da penalidade, só para exibição (nunca é aplicado), ou nulo quando em branco.</summary>
    public static string? TextoLivre(PenalidadeDeEquipamento? penalidade) =>
        string.IsNullOrWhiteSpace(penalidade?.Texto) ? null : penalidade.Texto.Trim();

    private static string AlvoDe(SubAtributo subAtributo) => subAtributo switch
    {
        SubAtributo.Iniciativa => SubAtributoAlvo.Iniciativa,
        SubAtributo.Movimentacao => SubAtributoAlvo.Movimentacao,
        SubAtributo.EsquivaNatural => SubAtributoAlvo.EsquivaNatural,
        SubAtributo.DefesaNatural => SubAtributoAlvo.DefesaNatural,
        SubAtributo.ReducaoFisica => SubAtributoAlvo.ReducaoFisica,
        SubAtributo.ReducaoMagica => SubAtributoAlvo.ReducaoMagica,
        _ => subAtributo.ToString()
    };
}

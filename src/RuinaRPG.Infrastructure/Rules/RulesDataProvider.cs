using System.Reflection;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Infrastructure.Rules;

public class RulesDataProvider : IRulesDataProvider
{
    private readonly Lazy<IReadOnlyList<LevelBonus>> _niveis;
    private readonly Lazy<IReadOnlyList<VocacaoProgressao>> _vocacoes;
    private readonly Lazy<IReadOnlyList<ArquetipoProgressao>> _arquetipos;
    private readonly Lazy<IReadOnlyList<CirculoGrauPorEap>> _circuloGrauPorEap;
    private readonly Lazy<IReadOnlyList<XpPorNivel>> _xpPorNivel;
    private readonly Lazy<IReadOnlyList<GraduacaoEfeito>> _efeitos;
    private readonly Lazy<IReadOnlyList<RegraEntry>> _regras;

    public RulesDataProvider()
    {
        _niveis = new Lazy<IReadOnlyList<LevelBonus>>(() => NivelBonusParser.Parse(ReadResource("Tabela de Níveis.md")));
        _vocacoes = new Lazy<IReadOnlyList<VocacaoProgressao>>(() => VocacaoProgressaoParser.Parse(ReadResource("Tabela de Vocação.md")));
        _arquetipos = new Lazy<IReadOnlyList<ArquetipoProgressao>>(() => ArquetipoProgressaoParser.Parse(ReadResource("Tabela de Arquetipos.md")));
        _circuloGrauPorEap = new Lazy<IReadOnlyList<CirculoGrauPorEap>>(() => CirculoGrauPorEapParser.Parse(ReadResource("Tabela de Circulo e Grau por EAP.md")));
        _xpPorNivel = new Lazy<IReadOnlyList<XpPorNivel>>(() => XpPorNivelParser.Parse(ReadResource("Tabelas de XP, Atributos, Características e EAP.md")));
        _efeitos = new Lazy<IReadOnlyList<GraduacaoEfeito>>(() => GraduacaoEfeitoParser.Parse(ReadResource("GRAUS e CIRCULOS.md")));
        _regras = new Lazy<IReadOnlyList<RegraEntry>>(() => RegraEntryParser.Parse(ReadResource("Sistema Basico.md")));
    }

    public IReadOnlyList<LevelBonus> Niveis => _niveis.Value;
    public IReadOnlyList<VocacaoProgressao> Vocacoes => _vocacoes.Value;
    public IReadOnlyList<ArquetipoProgressao> Arquetipos => _arquetipos.Value;
    public IReadOnlyList<CirculoGrauPorEap> CirculoGrauPorEap => _circuloGrauPorEap.Value;
    public IReadOnlyList<XpPorNivel> XpPorNivel => _xpPorNivel.Value;
    public IReadOnlyList<GraduacaoEfeito> Efeitos => _efeitos.Value;
    public IReadOnlyList<RegraEntry> Regras => _regras.Value;

    public static string ReadResource(string logicalName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"Embedded resource '{logicalName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

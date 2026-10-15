using RuinaRPG.Domain.Condicoes;

namespace RuinaRPG.Infrastructure.Encounters;

/// <summary>
/// Uma Condição do catálogo aplicada a um participante: cópia independente (padrão 1 da legenda do Modelo de Dados),
/// então editar ou apagar a entrada do catálogo não a altera. <c>CondicaoId</c> é só procedência (sem FK).
/// </summary>
public class EncounterParticipantCondicao
{
    public Guid Id { get; set; }
    public Guid EncounterParticipantId { get; set; }
    public Guid? CondicaoId { get; set; }
    public required string Nome { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public required string Icone { get; set; }

    /// <summary>O Turno em que foi aplicada; 0 se na preparação (conta a partir do Turno 1).</summary>
    public int AplicadaNoTurno { get; set; }
    public int? DuracaoTurnos { get; set; }

    /// <summary>jsonb. As linhas com os valores como o GM ajustou.</summary>
    public List<LinhaDeCondicaoAplicada> Modificadores { get; set; } = [];

    /// <summary>jsonb. Por categoria: lista vazia = a categoria inteira; senão os ids das linhas da ficha.</summary>
    public Dictionary<CategoriaDesabilitavel, List<Guid>> Desabilitacoes { get; set; } = [];

    /// <summary>jsonb. Linhas de ajuste de valor atual já aplicadas, e em que Turno.</summary>
    public List<AjusteAplicado> AjustesAplicados { get; set; } = [];
}

/// <summary>
/// Uma linha da Condição aplicada; <c>LinhaId</c> é estável e identifica a linha em <see cref="AjusteAplicado"/>.
/// <c>Maestrias</c>: numa linha de Maestria, as linhas de Maestria da ficha a que o GM a restringiu ao aplicar (nulo = todas).
/// </summary>
public sealed record LinhaDeCondicaoAplicada(Guid LinhaId, AlvoDeCondicao Alvo, string? AlvoEspecifico, TipoDeValorDeCondicao TipoDeValor, int Valor, MomentoDeCondicao? Momento,
    List<Guid>? Maestrias = null);

public sealed record AjusteAplicado(Guid LinhaId, int Turno);

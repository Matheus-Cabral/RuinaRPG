using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Client.Shared.Ficha;

/// <summary>
/// A linha nova da tabela de Afinidades (o "Adicionar"). A Essência 2 só vale se cruza com a Essência 1 na Matriz
/// Elemental; sem ela, o valor dela também é descartado.
/// </summary>
public sealed class AffinityFormModel
{
    public string? Elemento { get; set; }
    public int? ElementoValor { get; set; }
    public string? SegundaEssencia { get; set; }
    public int? SegundaEssenciaValor { get; set; }
    public int? SubElementoValor { get; set; }
    public int? Experiencia { get; set; }

    /// <summary>O Sub-Elemento que a linha terá: a interseção das duas Essências (nenhum enquanto falta uma).</summary>
    public string? SubElemento =>
        Enum.TryParse<Elemento>(Elemento, out var e1) && Enum.TryParse<EssenciaBasica>(SegundaEssencia, out var e2)
            ? MatrizElemental.Intersecao(e1, e2)?.ToString()
            : null;

    /// <summary>Uma Essência 1 nova descarta a Essência 2 que não cruza mais com ela.</summary>
    public void TrocarEssencia1(string? elemento)
    {
        Elemento = elemento;
        if (!AfinidadeEssenciaField.OpcoesSegundaEssencia(elemento, null).Any(o => o.Valor == SegundaEssencia))
            TrocarEssencia2(null);
    }

    /// <summary>Sem o nome da Essência 2, o Valor dela também é descartado.</summary>
    public void TrocarEssencia2(string? segunda)
    {
        SegundaEssencia = segunda;
        if (segunda is null)
            SegundaEssenciaValor = null;
    }

    public void Limpar() =>
        (Elemento, ElementoValor, SegundaEssencia, SegundaEssenciaValor, SubElementoValor, Experiencia) = (null, null, null, null, null, null);
}

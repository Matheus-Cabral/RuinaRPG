using RuinaRPG.Domain.Runes;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterRune
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public int Grau { get; set; }

    /// <summary>Classificação opcional (Arcana/Negra), só exibida; null = sem tipo. Guardada como texto.</summary>
    public TipoDeRuna? Tipo { get; set; }

    /// <summary>Obrigatória em Runas novas; null só em Runas anteriores à 1.4.3. Guardada como texto.</summary>
    public DisciplinaDeRuna? Disciplina { get; set; }

    /// <summary>Imagem opcional (uma por Runa). FK para Images com SetNull.</summary>
    public Guid? ImageId { get; set; }

    /// <summary>Só rastreio de origem (sem FK): apagar a entrada do banco não afeta a Runa da ficha.</summary>
    public Guid? SourceBankEntryId { get; set; }
}

using RuinaRPG.Domain.Runes;

namespace RuinaRPG.Infrastructure.Runes;

/// <summary>
/// Uma Runa na biblioteca do GM (Requisitos - Banco de Runas). Os campos são os mesmos de
/// CharacterRune/NpcRune — Nome, Descrição e Grau; toda Runa de ficha tem uma cópia independente aqui.
/// </summary>
public class RuneBankEntry
{
    public Guid Id { get; set; }
    public Guid GmId { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public int Grau { get; set; }

    /// <summary>Classificação opcional (Arcana/Negra), só exibida; null = sem tipo. Guardada como texto.</summary>
    public TipoDeRuna? Tipo { get; set; }

    /// <summary>Obrigatória em Runas novas; null só em Runas anteriores à 1.4.3. Guardada como texto.</summary>
    public DisciplinaDeRuna? Disciplina { get; set; }

    /// <summary>Imagem opcional (uma por Runa). FK para Images com SetNull.</summary>
    public Guid? ImageId { get; set; }
}

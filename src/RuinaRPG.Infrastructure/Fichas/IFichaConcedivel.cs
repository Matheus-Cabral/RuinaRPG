namespace RuinaRPG.Infrastructure.Fichas;

/// <summary>
/// Uma ficha do GM que pode ser concedida a um Jogador (NPC e Criatura; Campanha R0010): é concessão enquanto tem dono.
/// </summary>
public interface IFichaConcedivel : IFicha
{
    /// <summary>O GM dono do registro, a quem a ficha volta quando fica sem dono.</summary>
    Guid GmId { get; }

    /// <summary>O Jogador que recebeu a ficha; null quando ela é do GM.</summary>
    Guid? OwnerId { get; set; }
}

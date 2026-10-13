namespace RuinaRPG.Domain.Encounters;

/// <summary>Tipo de uma linha do log do encontro (Gerenciador de Encontros R0010).</summary>
public enum EncounterLogTipo
{
    Iniciado,
    Finalizado,
    Reaberto,
    NovoTurno,
    VezPassada,
    VezVoltou,
    VezEscolhida,
    ParticipanteAdicionado,
    ParticipanteRemovido,
    CampoAlterado,
    CondicaoAdicionada,
    CondicaoRemovida,
    RecursoDaFicha
}

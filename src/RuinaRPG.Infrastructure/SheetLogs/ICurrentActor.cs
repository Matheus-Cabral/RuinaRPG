namespace RuinaRPG.Infrastructure.SheetLogs;

/// <summary>Quem está fazendo a requisição atual; nulo fora de uma requisição autenticada.</summary>
public interface ICurrentActor
{
    Guid? UserId { get; }
}

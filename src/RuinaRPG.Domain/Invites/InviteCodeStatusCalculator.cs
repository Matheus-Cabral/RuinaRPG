namespace RuinaRPG.Domain.Invites;

public static class InviteCodeStatusCalculator
{
    public static InviteCodeStatus Compute(DateTime? revokedAt, Guid? redeemedByUserId, DateTime expiresAt, DateTime now)
    {
        if (revokedAt is not null)
            return InviteCodeStatus.Revogado;

        if (redeemedByUserId is not null)
            return InviteCodeStatus.Usado;

        if (now >= expiresAt)
            return InviteCodeStatus.Expirado;

        return InviteCodeStatus.Ativo;
    }
}

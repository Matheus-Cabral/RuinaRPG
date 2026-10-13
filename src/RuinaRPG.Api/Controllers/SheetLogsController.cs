using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.SheetLogs;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.SheetLogs;

namespace RuinaRPG.Api.Controllers;

/// <summary>Campanha R0016: o log de ficha é só do GM. Quem não é o GM (inclusive o dono da ficha) recebe 404.</summary>
[ApiController]
[Route("api/sheet-logs")]
[Authorize]
public class SheetLogsController(RuinaRpgDbContext db) : ControllerBase
{
    private const int LimitePadrao = 100;
    private const int LimiteMaximo = 500;

    [HttpGet("{kind}/{sheetId:guid}")]
    public async Task<ActionResult<SheetLogPageResponse>> List(string kind, Guid sheetId,
        [FromQuery] DateTime? de, [FromQuery] DateTime? ate, [FromQuery] bool recursos = true, [FromQuery] bool estresse = true,
        [FromQuery] string? antesDe = null, [FromQuery] int limite = LimitePadrao)
    {
        var gmId = CurrentUserId();
        IQueryable<SheetLogEntry> query = db.SheetLogEntries.AsNoTracking();

        switch (kind)
        {
            case "character":
                if (!await db.CharacterSheets.AnyAsync(s => s.Id == sheetId && db.Campaigns.Any(c => c.Id == s.CampaignId && c.GmId == gmId)))
                    return NotFound();
                query = query.Where(l => l.CharacterSheetId == sheetId);
                break;
            case "npc":
                if (!await db.NpcSheets.AnyAsync(s => s.Id == sheetId && s.GmId == gmId))
                    return NotFound();
                query = query.Where(l => l.NpcSheetId == sheetId);
                break;
            case "creature":
                if (!await db.CreatureSheets.AnyAsync(s => s.Id == sheetId && s.GmId == gmId))
                    return NotFound();
                query = query.Where(l => l.CreatureSheetId == sheetId);
                break;
            default:
                return NotFound();
        }

        if (de is not null)
            query = query.Where(l => l.OccurredAt >= AsUtc(de.Value));
        if (ate is not null)
            query = query.Where(l => l.OccurredAt < AsUtc(ate.Value));
        if (!recursos)
            query = query.Where(l => l.Categoria != SheetLogCategoria.RecursoVFA);
        if (!estresse)
            query = query.Where(l => l.Categoria != SheetLogCategoria.Estresse);

        if (antesDe is not null)
        {
            if (!TryParseCursor(antesDe, out var cursorAt, out var cursorId))
                return BadRequest("Cursor inválido.");
            query = query.Where(l => l.OccurredAt < cursorAt || (l.OccurredAt == cursorAt && l.Id.CompareTo(cursorId) < 0));
        }

        limite = Math.Clamp(limite, 1, LimiteMaximo);
        var linhas = await query.OrderByDescending(l => l.OccurredAt).ThenByDescending(l => l.Id).Take(limite + 1).ToListAsync();

        string? proximo = null;
        if (linhas.Count > limite)
        {
            linhas.RemoveAt(limite);
            var ultima = linhas[^1];
            proximo = $"{ultima.OccurredAt.Ticks}_{ultima.Id}";
        }

        return new SheetLogPageResponse(linhas.Select(ToResponse).ToList(), proximo);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    private static bool TryParseCursor(string cursor, out DateTime at, out Guid id)
    {
        at = default;
        id = default;
        var partes = cursor.Split('_');
        if (partes.Length != 2
            || !long.TryParse(partes[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            || ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks
            || !Guid.TryParse(partes[1], out id))
            return false;
        at = new DateTime(ticks, DateTimeKind.Utc);
        return true;
    }

    private static SheetLogEntryResponse ToResponse(SheetLogEntry l) => new(
        l.Id.ToString(), DateTime.SpecifyKind(l.OccurredAt, DateTimeKind.Utc), l.ActorNickname, l.ActorIsGm,
        l.Categoria.ToString(), l.Acao.ToString(), l.Secao, l.Campo, l.ItemNome, l.ValorAntes, l.ValorDepois);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}

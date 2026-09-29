using Npgsql;
using RuinaRPG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace RuinaRPG.Tests.Integration.Persistence;

// Para testes de migration que param num ponto antigo do schema: as entidades EF já têm as colunas
// de migrations posteriores, então as linhas "legadas" entram por SQL cru.
internal static class SchemaInsert
{
    // INSERT com os valores dados, preenchendo cada coluna NOT NULL sem default com o "zero" do seu tipo.
    public static async Task AtCurrentSchemaAsync(RuinaRpgDbContext db, string table, Dictionary<string, object> values)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        await using (var query = new NpgsqlCommand(
            "SELECT column_name, data_type FROM information_schema.columns WHERE table_name = @t AND is_nullable = 'NO' AND column_default IS NULL", connection))
        {
            query.Parameters.AddWithValue("t", table);
            await using var reader = await query.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var column = reader.GetString(0);
                if (values.ContainsKey(column))
                    continue;
                values[column] = reader.GetString(1) switch
                {
                    "boolean" => false,
                    "text" or "character varying" => "",
                    "numeric" => 0m,
                    "timestamp with time zone" => DateTime.UtcNow,
                    _ => 0,
                };
            }
        }

        var columns = values.Keys.ToList();
        await using var insert = new NpgsqlCommand(
            $"INSERT INTO \"{table}\" ({string.Join(", ", columns.Select(c => $"\"{c}\""))}) VALUES ({string.Join(", ", columns.Select((_, i) => $"@p{i}"))})", connection);
        for (var i = 0; i < columns.Count; i++)
            insert.Parameters.AddWithValue($"p{i}", values[columns[i]]);
        await insert.ExecuteNonQueryAsync();
    }
}

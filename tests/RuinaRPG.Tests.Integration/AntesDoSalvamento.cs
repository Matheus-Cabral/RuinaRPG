using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.SheetLogs;

namespace RuinaRPG.Tests.Integration;

/// <summary>
/// Para testes de concorrência sem corrida de tempo: roda uma ação UMA vez, logo antes do próximo SaveChanges da API,
/// no lugar da "outra ação" que leu o mesmo estado e salvou primeiro. A ação usa <see cref="NovoContexto"/> (sem
/// interceptor), então o salvamento dela não dispara o gancho de novo.
/// </summary>
public sealed class AntesDoSalvamento(string connectionString) : SaveChangesInterceptor
{
    private Func<RuinaRpgDbContext, Task>? _acao;

    public void Armar(Func<RuinaRpgDbContext, Task> acao) => _acao = acao;

    public void Configurar(DbContextOptionsBuilder options, SheetLogInterceptor logDaFicha) =>
        options.UseNpgsql(connectionString).AddInterceptors(logDaFicha, this);

    public RuinaRpgDbContext NovoContexto() =>
        new(new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(connectionString).Options);

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _acao, null) is { } acao)
        {
            await using var outro = NovoContexto();
            await acao(outro);
        }
        return result;
    }

    /// <summary>A API de teste com este gancho registrado junto ao interceptor do log de ficha.</summary>
    public sealed class Factory(string connectionString, AntesDoSalvamento gancho) : ApiFactory(connectionString)
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<RuinaRpgDbContext>>();
                services.AddDbContext<RuinaRpgDbContext>((sp, options) => gancho.Configurar(options, sp.GetRequiredService<SheetLogInterceptor>()));
            });
        }
    }
}

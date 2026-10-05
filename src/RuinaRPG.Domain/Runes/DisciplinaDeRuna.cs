namespace RuinaRPG.Domain.Runes;

/// <summary>
/// Disciplina de uma Runa (Requisitos - Banco de Runas). Obrigatória em Runas novas; Runas anteriores
/// à 1.4.3 guardam null. No fio trafega como o nome exato do enum.
/// </summary>
public enum DisciplinaDeRuna
{
    Adicao,
    Alteracao,
    Emissao,
    Manifestacao
}

/// <summary>Rótulo e descrição de cada Disciplina — fonte única para o popup, as listas e os testes.</summary>
public static class DisciplinaDeRunaInfo
{
    public static IReadOnlyList<DisciplinaDeRuna> Todas { get; } = Enum.GetValues<DisciplinaDeRuna>();

    public static string Rotulo(DisciplinaDeRuna disciplina) => disciplina switch
    {
        DisciplinaDeRuna.Adicao => "Adição",
        DisciplinaDeRuna.Alteracao => "Alteração",
        DisciplinaDeRuna.Emissao => "Emissão",
        DisciplinaDeRuna.Manifestacao => "Manifestação",
        _ => disciplina.ToString()
    };

    public static string Descricao(DisciplinaDeRuna disciplina) => disciplina switch
    {
        DisciplinaDeRuna.Adicao => "Influencia o corpo do usuário",
        DisciplinaDeRuna.Alteracao => "Influencia objetos inanimados",
        DisciplinaDeRuna.Emissao => "Influencia alvos inanimados",
        DisciplinaDeRuna.Manifestacao => "Manifesta a aura do usuário",
        _ => ""
    };
}

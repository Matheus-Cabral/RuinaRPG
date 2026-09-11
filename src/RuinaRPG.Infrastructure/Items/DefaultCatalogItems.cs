using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Items;

/// <summary>
/// The GM's starting catalog, transcribed from Docs/Sistema RPG/ruina-itens.docx — the exact
/// source Requisitos - Catálogo de Itens e Equipamentos.md's own preamble names ("um levantamento
/// real de itens do sistema... os requisitos abaixo espelham os esquemas de campos encontrados
/// lá"). Every GM starts with this catalog (new GMs via AuthController.RegisterGm; GMs that
/// existed before this feature via a one-time backfill wired into the same "--migrate"/Development
/// startup path TraitSeeder already uses — see Program.cs) and can freely edit or delete any of
/// these rows afterward (Catálogo R0007) — they are ordinary catalog items from the moment they're
/// created, not a distinct read-only "default" category.
///
/// Fields absent from the source document are left at their type's default rather than invented:
/// Peso = 0, Preço = 0 on every item (the docx has no economy columns at all), and
/// DurabilidadeMaxima/RF/RM stay null on Armas/Armaduras (no durability or reduction columns
/// either — Requisitos - Catálogo R0004/R0005 both call these GM-defined, not derived from the
/// rulebook). The GM fills these in by editing, same as any other catalog item.
///
/// Two lossy mappings from the source table format, both called out per-row below where they
/// apply:
/// - A few Lâminas/Lanças entries show a *ranged* Alcance (e.g. "1–3 Hex") rather than a single
///   value. Arma.Alcance only holds one int, so the upper bound is stored there and the original
///   range is preserved verbatim in Descricao so the nuance isn't silently lost.
/// - Escudos' source column is literally "Req. Fortitude", but the only requirement field Escudo
///   has is RequisitoVigor (the same field Armadura's "Req. Vigor" column maps to) — there's no
///   separate Fortitude-requirement concept anywhere else in this app, so it's mapped there as the
///   closest existing field.
///
/// No Artefato entries: the source document has no Artefatos section (only Itens Gerais, Armas,
/// Armaduras and Escudos) — that item type has nothing to transcribe from this source.
/// </summary>
public static class DefaultCatalogItems
{
    public static List<Item> Build(Guid gmId)
    {
        var items = new List<Item>
        {
            // ===== ARMAS =====
            new Arma { Nome = "Faca", Subcategoria = "Lâminas (Facas e Punhais)", Tier = Tier.F, Empunhadura = Empunhadura.UmaMao, Dados = "1D4", Dano = 2, Critico = "19", Alcance = 3, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = null, Descricao = "Alcance 1–3 Hex (variável)" },
            new Arma { Nome = "Adaga", Subcategoria = "Lâminas (Facas e Punhais)", Tier = Tier.E, Empunhadura = Empunhadura.UmaMao, Dados = "1D6", Dano = 3, Critico = "19", Alcance = 4, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = null, Descricao = "Alcance 1–4 Hex (variável)" },
            new Arma { Nome = "Adaga +1", Subcategoria = "Lâminas (Facas e Punhais)", Tier = Tier.D, Empunhadura = Empunhadura.UmaMao, Dados = "1D8", Dano = 4, Critico = "2x", Alcance = 4, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = null, Descricao = "Alcance 1–4 Hex (variável)" },
            new Arma { Nome = "Punhal", Subcategoria = "Lâminas (Facas e Punhais)", Tier = Tier.C, Empunhadura = Empunhadura.UmaMao, Dados = "2D10", Dano = null, Critico = "2x", Alcance = 4, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = null, Descricao = "Alcance 1–4 Hex (variável)" },
            new Arma { Nome = "Punhal +1", Subcategoria = "Lâminas (Facas e Punhais)", Tier = Tier.B, Empunhadura = Empunhadura.UmaMao, Dados = "3D10", Dano = 5, Critico = "2x", Alcance = 4, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = null, Descricao = "Alcance 1–4 Hex (variável)" },
            new Arma { Nome = "Espada Curta", Subcategoria = "Espadas", Tier = Tier.D, Empunhadura = Empunhadura.UmaMao, Dados = "1D8", Dano = 7, Critico = "19", Alcance = 1, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Espada Curta +1", Subcategoria = "Espadas", Tier = Tier.C, Empunhadura = Empunhadura.UmaMao, Dados = "2D10", Dano = 2, Critico = "2x", Alcance = 1, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Espada Curta +2", Subcategoria = "Espadas", Tier = Tier.B, Empunhadura = Empunhadura.UmaMao, Dados = "2D12", Dano = 5, Critico = "2x", Alcance = 1, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Espada", Subcategoria = "Espadas", Tier = Tier.E, Empunhadura = Empunhadura.UmaMao, Dados = "2D6", Dano = null, Critico = "19", Alcance = 2, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Espada +1", Subcategoria = "Espadas", Tier = Tier.D, Empunhadura = Empunhadura.UmaMao, Dados = "2D8", Dano = 5, Critico = "2x", Alcance = 2, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Espada +2", Subcategoria = "Espadas", Tier = Tier.C, Empunhadura = Empunhadura.UmaMao, Dados = "3D10", Dano = 3, Critico = "2x", Alcance = 3, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Espada Longa", Subcategoria = "Espadas", Tier = Tier.C, Empunhadura = Empunhadura.DuasMaos, Dados = "3D10", Dano = null, Critico = "18", Alcance = 3, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Espada Longa +1", Subcategoria = "Espadas", Tier = Tier.B, Empunhadura = Empunhadura.DuasMaos, Dados = "4D12", Dano = 5, Critico = "2x", Alcance = 3, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Florete", Subcategoria = "Espadas", Tier = Tier.D, Empunhadura = Empunhadura.UmaMao, Dados = "1D8", Dano = 8, Critico = "2x", Alcance = 1, TipoDeDano = TipoDeDano.Perfurante, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Florete +1", Subcategoria = "Espadas", Tier = Tier.C, Empunhadura = Empunhadura.UmaMao, Dados = "2D10", Dano = 5, Critico = "2x", Alcance = 1, TipoDeDano = TipoDeDano.Perfurante, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Florete +2", Subcategoria = "Espadas", Tier = Tier.B, Empunhadura = Empunhadura.UmaMao, Dados = "3D12", Dano = 3, Critico = "3x", Alcance = 1, TipoDeDano = TipoDeDano.Perfurante, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Machado", Subcategoria = "Machados", Tier = Tier.E, Empunhadura = Empunhadura.DuasMaos, Dados = "3D6", Dano = 5, Critico = "19", Alcance = 1, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Machado +1", Subcategoria = "Machados", Tier = Tier.D, Empunhadura = Empunhadura.DuasMaos, Dados = "3D8", Dano = 5, Critico = "19", Alcance = 1, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Machado Pesado", Subcategoria = "Machados", Tier = Tier.C, Empunhadura = Empunhadura.DuasMaos, Dados = "3D10", Dano = 6, Critico = "18", Alcance = 1, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Machado Pesado +1", Subcategoria = "Machados", Tier = Tier.B, Empunhadura = Empunhadura.DuasMaos, Dados = "4D12", Dano = 3, Critico = "18", Alcance = 2, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Lança", Subcategoria = "Lanças", Tier = Tier.E, Empunhadura = Empunhadura.UmaMao, Dados = "2D6", Dano = null, Critico = "19", Alcance = 5, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = null, Descricao = "Alcance 2–5 Hex (variável)" },
            new Arma { Nome = "Lança +1", Subcategoria = "Lanças", Tier = Tier.D, Empunhadura = Empunhadura.UmaMao, Dados = "2D8", Dano = 5, Critico = "2x", Alcance = 2, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Lança +2", Subcategoria = "Lanças", Tier = Tier.C, Empunhadura = Empunhadura.UmaMao, Dados = "3D10", Dano = 3, Critico = "2x", Alcance = 3, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Mangual", Subcategoria = "Mangual", Tier = Tier.E, Empunhadura = Empunhadura.UmaMao, Dados = "3D6", Dano = 4, Critico = "2x", Alcance = 3, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Mangual +1", Subcategoria = "Mangual", Tier = Tier.D, Empunhadura = Empunhadura.UmaMao, Dados = "3D8", Dano = 4, Critico = "19", Alcance = 4, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Mangual +2", Subcategoria = "Mangual", Tier = Tier.C, Empunhadura = Empunhadura.UmaMao, Dados = "3D10", Dano = 5, Critico = "18", Alcance = 4, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Mangual +3", Subcategoria = "Mangual", Tier = Tier.B, Empunhadura = Empunhadura.UmaMao, Dados = "4D12", Dano = 2, Critico = "2x", Alcance = 6, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Porrete", Subcategoria = "Clavas e Bastões", Tier = Tier.F, Empunhadura = Empunhadura.UmaMao, Dados = "1D4", Dano = 2, Critico = "2x", Alcance = 1, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Porrete +1", Subcategoria = "Clavas e Bastões", Tier = Tier.E, Empunhadura = Empunhadura.UmaMao, Dados = "1D6", Dano = 3, Critico = "19", Alcance = 1, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Clava", Subcategoria = "Clavas e Bastões", Tier = Tier.D, Empunhadura = Empunhadura.DuasMaos, Dados = "1D8", Dano = 4, Critico = "2x", Alcance = 2, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Bastão", Subcategoria = "Clavas e Bastões", Tier = Tier.C, Empunhadura = Empunhadura.DuasMaos, Dados = "2D10", Dano = null, Critico = "18", Alcance = 2, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Bastão Reforçado", Subcategoria = "Clavas e Bastões", Tier = Tier.B, Empunhadura = Empunhadura.DuasMaos, Dados = "3D10", Dano = 5, Critico = "3x", Alcance = 2, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Martelo", Subcategoria = "Martelos", Tier = Tier.D, Empunhadura = Empunhadura.UmaMao, Dados = "3D8", Dano = 4, Critico = "2x", Alcance = 1, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Martelo de Ferro Fundido", Subcategoria = "Martelos", Tier = Tier.C, Empunhadura = Empunhadura.UmaMao, Dados = "3D10", Dano = 2, Critico = "18", Alcance = 1, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Martelo de Ferro Negro", Subcategoria = "Martelos", Tier = Tier.B, Empunhadura = Empunhadura.UmaMao, Dados = "3D12", Dano = 3, Critico = "2x", Alcance = 1, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Martelo de Guerra", Subcategoria = "Martelos", Tier = Tier.D, Empunhadura = Empunhadura.DuasMaos, Dados = "3D8", Dano = 6, Critico = "18", Alcance = 3, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Martelo de Guerra Fundido", Subcategoria = "Martelos", Tier = Tier.C, Empunhadura = Empunhadura.DuasMaos, Dados = "3D10", Dano = 5, Critico = "2x", Alcance = 4, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Martelo de Guerra Negro", Subcategoria = "Martelos", Tier = Tier.B, Empunhadura = Empunhadura.DuasMaos, Dados = "3D12", Dano = 6, Critico = "18", Alcance = 4, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Maça", Subcategoria = "Maças", Tier = Tier.E, Empunhadura = Empunhadura.UmaMao, Dados = "3D6", Dano = 6, Critico = "2x", Alcance = 1, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Maça de Ferro Fundido", Subcategoria = "Maças", Tier = Tier.D, Empunhadura = Empunhadura.UmaMao, Dados = "3D8", Dano = 6, Critico = "2x", Alcance = 1, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Maça de Vafiti", Subcategoria = "Maças", Tier = Tier.C, Empunhadura = Empunhadura.DuasMaos, Dados = "3D10", Dano = 5, Critico = "18", Alcance = 3, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Maça de Ferro Negro", Subcategoria = "Maças", Tier = Tier.B, Empunhadura = Empunhadura.DuasMaos, Dados = "3D12", Dano = 4, Critico = "2x", Alcance = 3, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Manopla de Ferro", Subcategoria = "Manoplas", Tier = Tier.E, Empunhadura = Empunhadura.DuasMaos, Dados = "3D6", Dano = 5, Critico = "19", Alcance = 1, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Manopla de Ferro Fundido", Subcategoria = "Manoplas", Tier = Tier.D, Empunhadura = Empunhadura.DuasMaos, Dados = "3D8", Dano = 5, Critico = "2x", Alcance = 1, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Manopla de Vafiti", Subcategoria = "Manoplas", Tier = Tier.C, Empunhadura = Empunhadura.DuasMaos, Dados = "3D10", Dano = 6, Critico = "18", Alcance = 1, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Manopla de Ferro Negro", Subcategoria = "Manoplas", Tier = Tier.B, Empunhadura = Empunhadura.DuasMaos, Dados = "4D12", Dano = 3, Critico = "3x", Alcance = 2, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Funda", Subcategoria = "Fundas e Baladeiras", Tier = Tier.E, Empunhadura = Empunhadura.DuasMaos, Dados = "1D6", Dano = 2, Critico = "19", Alcance = 4, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Baladeira", Subcategoria = "Fundas e Baladeiras", Tier = Tier.D, Empunhadura = Empunhadura.UmaMao, Dados = "2D8", Dano = 2, Critico = "2x", Alcance = 6, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Baladeira +1", Subcategoria = "Fundas e Baladeiras", Tier = Tier.C, Empunhadura = Empunhadura.UmaMao, Dados = "2D10", Dano = 5, Critico = "2x", Alcance = 8, TipoDeDano = TipoDeDano.Contundente, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Faca de Caça Inicial", Subcategoria = "Facas de Caça", Tier = Tier.F, Empunhadura = Empunhadura.UmaMao, Dados = null, Dano = 2, Critico = null, Alcance = null, TipoDeDano = null, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Faca de Caça Intermediária", Subcategoria = "Facas de Caça", Tier = Tier.E, Empunhadura = Empunhadura.UmaMao, Dados = null, Dano = 4, Critico = null, Alcance = null, TipoDeDano = null, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Faca de Caça Elite", Subcategoria = "Facas de Caça", Tier = Tier.D, Empunhadura = Empunhadura.UmaMao, Dados = null, Dano = 6, Critico = null, Alcance = null, TipoDeDano = null, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Arco Curto", Subcategoria = "Arcos", Tier = Tier.E, Empunhadura = Empunhadura.DuasMaos, Dados = "2D6", Dano = 1, Critico = "19", Alcance = 6, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = "3 Dex", Descricao = null },
            new Arma { Nome = "Arco Curto +1", Subcategoria = "Arcos", Tier = Tier.D, Empunhadura = Empunhadura.DuasMaos, Dados = "2D8", Dano = 3, Critico = "19", Alcance = 6, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = "3 Dex", Descricao = null },
            new Arma { Nome = "Arco Curto +2", Subcategoria = "Arcos", Tier = Tier.C, Empunhadura = Empunhadura.DuasMaos, Dados = "2D10", Dano = 5, Critico = "18", Alcance = 6, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = "5 Dex", Descricao = null },
            new Arma { Nome = "Arco Curto +3", Subcategoria = "Arcos", Tier = Tier.B, Empunhadura = Empunhadura.DuasMaos, Dados = "3D12", Dano = 6, Critico = "18", Alcance = 7, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = "7 Dex", Descricao = null },
            new Arma { Nome = "Arco", Subcategoria = "Arcos", Tier = Tier.E, Empunhadura = Empunhadura.DuasMaos, Dados = "2D6", Dano = null, Critico = "2x", Alcance = 10, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = "10 Dex", Descricao = null },
            new Arma { Nome = "Arco +1", Subcategoria = "Arcos", Tier = Tier.D, Empunhadura = Empunhadura.DuasMaos, Dados = "2D8", Dano = 1, Critico = "2x", Alcance = 10, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = "10 Dex", Descricao = null },
            new Arma { Nome = "Arco +2", Subcategoria = "Arcos", Tier = Tier.C, Empunhadura = Empunhadura.DuasMaos, Dados = "2D10", Dano = 3, Critico = "2x", Alcance = 10, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = "15 Dex", Descricao = null },
            new Arma { Nome = "Arco +3", Subcategoria = "Arcos", Tier = Tier.B, Empunhadura = Empunhadura.DuasMaos, Dados = "3D12", Dano = 5, Critico = "3x", Alcance = 12, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = "18 Dex", Descricao = null },
            new Arma { Nome = "Arco Longo", Subcategoria = "Arcos", Tier = Tier.E, Empunhadura = Empunhadura.DuasMaos, Dados = "3D6", Dano = 3, Critico = "18", Alcance = 14, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = "12 Dex", Descricao = null },
            new Arma { Nome = "Arco Longo +1", Subcategoria = "Arcos", Tier = Tier.D, Empunhadura = Empunhadura.DuasMaos, Dados = "2D8", Dano = 5, Critico = "2x", Alcance = 14, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = "14 Dex", Descricao = null },
            new Arma { Nome = "Arco Longo +2", Subcategoria = "Arcos", Tier = Tier.C, Empunhadura = Empunhadura.DuasMaos, Dados = "3D10", Dano = null, Critico = "2x", Alcance = 15, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = "16 Dex", Descricao = null },
            new Arma { Nome = "Arco Longo +3", Subcategoria = "Arcos", Tier = Tier.B, Empunhadura = Empunhadura.DuasMaos, Dados = "3D12", Dano = 4, Critico = "17", Alcance = 20, TipoDeDano = TipoDeDano.Cortante, RequisitoAtributo = "23 Dex", Descricao = null },
            new Arma { Nome = "Varinha de Carvalho", Subcategoria = "Varinhas Mágicas", Tier = Tier.F, Empunhadura = Empunhadura.UmaMao, Dados = "2D4", Dano = 2, Critico = "2x", Alcance = 6, TipoDeDano = TipoDeDano.Arcano, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Varinha Reforçada de Carvalho", Subcategoria = "Varinhas Mágicas", Tier = Tier.E, Empunhadura = Empunhadura.UmaMao, Dados = "2D6", Dano = 4, Critico = "19", Alcance = 6, TipoDeDano = TipoDeDano.Arcano, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Varinha de Juazeiro", Subcategoria = "Varinhas Mágicas", Tier = Tier.D, Empunhadura = Empunhadura.UmaMao, Dados = "2D8", Dano = 4, Critico = "2x", Alcance = 6, TipoDeDano = TipoDeDano.Arcano, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Varinha Retorcida de Juazeiro", Subcategoria = "Varinhas Mágicas", Tier = Tier.C, Empunhadura = Empunhadura.UmaMao, Dados = "2D10", Dano = 5, Critico = "19", Alcance = 7, TipoDeDano = TipoDeDano.Arcano, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Varinha de Teixo", Subcategoria = "Varinhas Mágicas", Tier = Tier.B, Empunhadura = Empunhadura.UmaMao, Dados = "3D12", Dano = 6, Critico = "3x", Alcance = 8, TipoDeDano = TipoDeDano.Arcano, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Cajado de Carvalho", Subcategoria = "Cajados Mágicos", Tier = Tier.E, Empunhadura = Empunhadura.DuasMaos, Dados = "2D6", Dano = 2, Critico = "2x", Alcance = 10, TipoDeDano = TipoDeDano.Arcano, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Cajado Reforçado de Carvalho", Subcategoria = "Cajados Mágicos", Tier = Tier.D, Empunhadura = Empunhadura.DuasMaos, Dados = "2D8", Dano = 3, Critico = "19", Alcance = 10, TipoDeDano = TipoDeDano.Arcano, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Cajado Retorcido de Juazeiro", Subcategoria = "Cajados Mágicos", Tier = Tier.C, Empunhadura = Empunhadura.DuasMaos, Dados = "2D10", Dano = 6, Critico = "2x", Alcance = 12, TipoDeDano = TipoDeDano.Arcano, RequisitoAtributo = null, Descricao = null },
            new Arma { Nome = "Cajado de Teixo", Subcategoria = "Cajados Mágicos", Tier = Tier.B, Empunhadura = Empunhadura.DuasMaos, Dados = "3D12", Dano = 4, Critico = "18", Alcance = 15, TipoDeDano = TipoDeDano.Arcano, RequisitoAtributo = null, Descricao = null },

            // ===== ARMADURAS =====
            new Armadura { Nome = "Armadura Acolchoada", Categoria = CategoriaProtecao.Leve, Defesa = 2, Penalidade = null, RequisitoVigor = null },
            new Armadura { Nome = "Armadura de Couro", Categoria = CategoriaProtecao.Leve, Defesa = 4, Penalidade = null, RequisitoVigor = 2 },
            new Armadura { Nome = "Armadura de Couro Batido", Categoria = CategoriaProtecao.Leve, Defesa = 6, Penalidade = "-1 Reflexo", RequisitoVigor = 4 },
            new Armadura { Nome = "Cota de Malha", Categoria = CategoriaProtecao.Medio, Defesa = 8, Penalidade = "-3 Reflexo", RequisitoVigor = 6 },
            new Armadura { Nome = "Cota de Malha Composta", Categoria = CategoriaProtecao.Medio, Defesa = 10, Penalidade = "-5 Reflexo", RequisitoVigor = 8 },
            new Armadura { Nome = "Cota de Escama Metálica", Categoria = CategoriaProtecao.Medio, Defesa = 12, Penalidade = "-8 Reflexo", RequisitoVigor = 10 },
            new Armadura { Nome = "Armadura de Ferro", Categoria = CategoriaProtecao.Pesada, Defesa = 14, Penalidade = "-10 Reflexo", RequisitoVigor = 12 },
            new Armadura { Nome = "Armadura de Placas", Categoria = CategoriaProtecao.Pesada, Defesa = 16, Penalidade = "-13 Reflexo", RequisitoVigor = 14 },
            new Armadura { Nome = "Couraça de Placas Composta", Categoria = CategoriaProtecao.Pesada, Defesa = 18, Penalidade = "-16 Reflexo", RequisitoVigor = 16 },

            // ===== ESCUDOS =====
            new Escudo { Nome = "Tampa de Madeira", Categoria = CategoriaProtecao.Leve, BonusDefesa = 2, Penalidade = null, RequisitoVigor = null },
            new Escudo { Nome = "Escudo de Couro", Categoria = CategoriaProtecao.Leve, BonusDefesa = 4, Penalidade = null, RequisitoVigor = null },
            new Escudo { Nome = "Escudo de Couro Batido", Categoria = CategoriaProtecao.Leve, BonusDefesa = 6, Penalidade = null, RequisitoVigor = null },
            new Escudo { Nome = "Escudo de Carapaça de Javali", Categoria = CategoriaProtecao.Medio, BonusDefesa = 8, Penalidade = "-3 Reflexo", RequisitoVigor = 5 },
            new Escudo { Nome = "Escudo de Ferro", Categoria = CategoriaProtecao.Medio, BonusDefesa = 10, Penalidade = "-5 Reflexo", RequisitoVigor = 10 },
            new Escudo { Nome = "Escudo Pesado de Aço", Categoria = CategoriaProtecao.Pesada, BonusDefesa = 12, Penalidade = "-8 Reflexo", RequisitoVigor = 15 },
            new Escudo { Nome = "Escudo Pesado de Placas", Categoria = CategoriaProtecao.Pesada, BonusDefesa = 15, Penalidade = "-13 Reflexo", RequisitoVigor = 30 },
            new Escudo { Nome = "Escudo Pesado de Elite", Categoria = CategoriaProtecao.Pesada, BonusDefesa = 20, Penalidade = "-16 Reflexo", RequisitoVigor = 35 },

            // ===== ITENS GERAIS =====
            new ItemGeral { Nome = "Óleo", Subcategoria = "Equipamentos de Aventura", Descricao = "Normalmente utilizado em lanternas" },
            new ItemGeral { Nome = "Tocha", Subcategoria = "Equipamentos de Aventura", Descricao = "Ilumina uma área 2 por 3 turnos" },
            new ItemGeral { Nome = "Vara de Madeira", Subcategoria = "Equipamentos de Aventura", Descricao = "+10 em testes de Ofício em Pescaria" },
            new ItemGeral { Nome = "Isca de Pesca", Subcategoria = "Equipamentos de Aventura", Descricao = "Grande chance de atrair peixes de pequeno porte" },
            new ItemGeral { Nome = "Isca de Pesca +1", Subcategoria = "Equipamentos de Aventura", Descricao = "Grande chance de atrair peixes de grande porte" },
            new ItemGeral { Nome = "Corda", Subcategoria = "Equipamentos de Aventura", Descricao = "1 metro por compra" },
            new ItemGeral { Nome = "Saco de Dormir", Subcategoria = "Equipamentos de Aventura", Descricao = "1d8 em descanso curto, e 2 em longos / -3 de Estresse" },
            new ItemGeral { Nome = "Mochila", Subcategoria = "Equipamentos de Aventura", Descricao = "+5 de peso" },
            new ItemGeral { Nome = "Mochila Grande", Subcategoria = "Equipamentos de Aventura", Descricao = "+10 de peso" },
            new ItemGeral { Nome = "Arpéu", Subcategoria = "Equipamentos de Aventura", Descricao = "+10 em testes de Acrobacia/Escalada" },
            new ItemGeral { Nome = "Símbolo Sagrado", Subcategoria = "Equipamentos de Aventura", Descricao = "+5 em testes de Carisma em Templos" },
            new ItemGeral { Nome = "Lampião", Subcategoria = "Equipamentos de Aventura", Descricao = "Ilumina uma área 4 por 6 turnos" },
            new ItemGeral { Nome = "Pé de Cabra", Subcategoria = "Equipamentos de Aventura", Descricao = "+10 em testes de Arrombamento" },
            new ItemGeral { Nome = "Gazúa", Subcategoria = "Equipamentos de Aventura", Descricao = "+5 em testes de Arrombamento Sorrateiro (10 usos)" },
            new ItemGeral { Nome = "Barraca", Subcategoria = "Equipamentos de Aventura", Descricao = "+1 dado em descansos curtos, +2 em longos / -3 de Estresse" },
            new ItemGeral { Nome = "Espelho", Subcategoria = "Equipamentos de Aventura", Descricao = "Excelente para avaliar a própria aparência" },
            new ItemGeral { Nome = "Algemas", Subcategoria = "Equipamentos de Aventura", Descricao = "Atas de metal antiquado, comumente utilizadas em masmorras" },
            new ItemGeral { Nome = "Luneta", Subcategoria = "Equipamentos de Aventura", Descricao = "Permite ver o distante com clareza / +5 de Percepção" },
            new ItemGeral { Nome = "Repelente", Subcategoria = "Equipamentos de Aventura", Descricao = "Afasta criaturas por 3 dados de viagem" },
            new ItemGeral { Nome = "Repelente Potente", Subcategoria = "Equipamentos de Aventura", Descricao = "Afasta criaturas por 6 dados de viagem" },
            new ItemGeral { Nome = "Isca", Subcategoria = "Equipamentos de Aventura", Descricao = "Atrai criaturas por 3 dados de viagem" },
            new ItemGeral { Nome = "Isca Potente", Subcategoria = "Equipamentos de Aventura", Descricao = "Atrai criaturas por 6 dados de viagem" },
            new ItemGeral { Nome = "Aljava", Subcategoria = "Equipamentos de Aventura", Descricao = "Comporta 20 flechas" },
            new ItemGeral { Nome = "Alforje", Subcategoria = "Equipamentos Animais", Descricao = "10 slots a mais para carregar a cavalo" },
            new ItemGeral { Nome = "Sela", Subcategoria = "Equipamentos Animais", Descricao = "+5 em testes de condução" },
            new ItemGeral { Nome = "Flecha de Madeira", Subcategoria = "Munição", Descricao = "Simples e muito utilizada" },
            new ItemGeral { Nome = "Flecha de Ferro", Subcategoria = "Munição", Descricao = "Pontiagudas e resistentes / +2 de dano cortante" },
            new ItemGeral { Nome = "Flecha de Ferro Negro", Subcategoria = "Munição", Descricao = "Ornamentadas e ainda mais afiadas / +6 de dano cortante" },
            new ItemGeral { Nome = "Projétil de Ferro", Subcategoria = "Munição", Descricao = "Munição simples para armas de fogo / +3 de dano perfurante" },
            new ItemGeral { Nome = "Projétil de Ferro Negro", Subcategoria = "Munição", Descricao = "Projétil bem trabalhado para armas de fogo / +8 de dano perfurante" },
            new ItemGeral { Nome = "Ração de Viagem", Subcategoria = "Alimentação", Descricao = "Comida de viagem para uma pessoa" },
            new ItemGeral { Nome = "Prato Simples", Subcategoria = "Alimentação", Descricao = "Nutrição básica — ainda sentirá fome durante o dia" },
            new ItemGeral { Nome = "Prato Robusto", Subcategoria = "Alimentação", Descricao = "Nutrição moderada — mais satisfeito durante o dia" },
            new ItemGeral { Nome = "Prato Nobre", Subcategoria = "Alimentação", Descricao = "Nutrição em fartura — completamente satisfeito por 1 dia" },
            new ItemGeral { Nome = "Banquete", Subcategoria = "Alimentação", Descricao = "Grande fartura para grupos — 2 dias completamente satisfeito" },
            new ItemGeral { Nome = "Cura", Subcategoria = "Serviços", Descricao = "Oferecido em templos de Aminoar" },
            new ItemGeral { Nome = "Estadia (Comum)", Subcategoria = "Serviços", Descricao = "Oferecido em tabernas" },
            new ItemGeral { Nome = "Estadia (Confortável)", Subcategoria = "Serviços", Descricao = "Oferecido em estalagens e hospedarias" },
            new ItemGeral { Nome = "Estadia (Luxuosa)", Subcategoria = "Serviços", Descricao = "Oferecido em hospedarias e pousadas" },
            new ItemGeral { Nome = "Estábulo (por dia)", Subcategoria = "Serviços", Descricao = "Local preparado para cuidado e alimentação de montarias" },
            new ItemGeral { Nome = "Escolta (Terrestre)", Subcategoria = "Serviços", Descricao = "2 soldados designados para proteção de carga ou comitiva" },
            new ItemGeral { Nome = "Mensageiro", Subcategoria = "Serviços", Descricao = "Transporta cartas ou pacotes leves" },
            new ItemGeral { Nome = "Carroça", Subcategoria = "Veículos", Descricao = "Comporta 4 pessoas, não é muito confortável" },
            new ItemGeral { Nome = "Carruagem", Subcategoria = "Veículos", Descricao = "Bastante confortável, comporta 6 pessoas" },
            new ItemGeral { Nome = "Tinta", Subcategoria = "Materiais de Estudo & Rituais", Descricao = "Um pequeno vidro com tinta escura" },
            new ItemGeral { Nome = "Pena", Subcategoria = "Materiais de Estudo & Rituais", Descricao = "Utilizada normalmente para escrever" },
            new ItemGeral { Nome = "Papel", Subcategoria = "Materiais de Estudo & Rituais", Descricao = "Uma folha de papel em branco" },
            new ItemGeral { Nome = "Diário", Subcategoria = "Materiais de Estudo & Rituais", Descricao = "Utilizado para anotações pessoais (+30 folhas)" },
            new ItemGeral { Nome = "Manuscrito Arcano Vol.1", Subcategoria = "Materiais de Estudo & Rituais", Descricao = "Ensina os princípios de um coração de mana e de arcana" },
            new ItemGeral { Nome = "Manuscrito Arcano Vol.2", Subcategoria = "Materiais de Estudo & Rituais", Descricao = "Ensina o conceito de círculos e se aprofunda na arcana" },
            new ItemGeral { Nome = "Cozinha para Principiantes", Subcategoria = "Materiais de Estudo & Rituais", Descricao = "+5 na maestria culinária com Ofício" },
            new ItemGeral { Nome = "Guia do Cozinheiro Intermediário", Subcategoria = "Materiais de Estudo & Rituais", Descricao = "+10 na maestria culinária com Ofício" },
            new ItemGeral { Nome = "Guia do Mestre Cozinheiro", Subcategoria = "Materiais de Estudo & Rituais", Descricao = "+15 na maestria culinária com Ofício" },
            new ItemGeral { Nome = "Saque para Principiantes", Subcategoria = "Materiais de Estudo & Rituais", Descricao = "+5 em Saquear" },
            new ItemGeral { Nome = "Guia do Saqueador Intermediário", Subcategoria = "Materiais de Estudo & Rituais", Descricao = "+10 em Saquear" },
            new ItemGeral { Nome = "Guia do Mestre Saqueador", Subcategoria = "Materiais de Estudo & Rituais", Descricao = "+15 em Saquear" },
            new ItemGeral { Nome = "Cera-viz (Material Ritualístico)", Subcategoria = "Materiais de Estudo & Rituais", Descricao = "Cinzas de criatura mundana, muito usada em rituais" },
        };

        foreach (var item in items)
        {
            item.Id = Guid.NewGuid();
            item.GmId = gmId;
        }

        return items;
    }
}

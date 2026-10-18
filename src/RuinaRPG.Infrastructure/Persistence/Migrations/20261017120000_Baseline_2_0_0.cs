using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Baseline_2_0_0 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nickname = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    InvitedByGmId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false),
                    NormalizedNickname = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    IsRulesAuditor = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastSeenAppVersion = table.Column<string>(type: "text", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ColunasDeNivel",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    ChaveDeSistema = table.Column<string>(type: "text", nullable: true),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColunasDeNivel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Condicoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Icone = table.Column<string>(type: "text", nullable: false),
                    DuracaoPadraoTurnos = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Condicoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CreatureExclusiveTraits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Custo = table.Column<int>(type: "integer", nullable: false),
                    Polaridade = table.Column<int>(type: "integer", nullable: false),
                    RequerEspecificacao = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureExclusiveTraits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DurabilidadesPorRank",
                columns: table => new
                {
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Durabilidade = table.Column<int>(type: "integer", nullable: true),
                    Inquebravel = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DurabilidadesPorRank", x => x.Rank);
                });

            migrationBuilder.CreateTable(
                name: "Efeitos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Grau = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    TipoDeCusto = table.Column<int>(type: "integer", nullable: false),
                    CustoFixo = table.Column<int>(type: "integer", nullable: true),
                    CustoPorUnidade = table.Column<int>(type: "integer", nullable: true),
                    UnidadeLabel = table.Column<string>(type: "text", nullable: true),
                    QuantidadeDerivadaDeEfeito = table.Column<string>(type: "text", nullable: true),
                    MaxUnidades = table.Column<int>(type: "integer", nullable: true),
                    MaxEscalaPorGrau = table.Column<bool>(type: "boolean", nullable: false),
                    MaxContandoAPartirDoGrau = table.Column<int>(type: "integer", nullable: true),
                    CustoAlternativo = table.Column<int>(type: "integer", nullable: true),
                    CustoAlternativoAPartirDoGrau = table.Column<int>(type: "integer", nullable: true),
                    PreRequisitosJson = table.Column<string>(type: "text", nullable: true),
                    IsCustomized = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Efeitos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentKitFixedItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Dados = table.Column<string>(type: "jsonb", nullable: false),
                    Requisitos = table.Column<string>(type: "jsonb", nullable: true),
                    PenalidadeDeRequisitos = table.Column<string>(type: "jsonb", nullable: true),
                    DetalhesIncompletos = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentKitFixedItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentKits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Ciclos = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentKits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NiveisProgressao",
                columns: table => new
                {
                    Nivel = table.Column<int>(type: "integer", nullable: false),
                    OutrosBonus = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NiveisProgressao", x => x.Nivel);
                });

            migrationBuilder.CreateTable(
                name: "Pericias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Chave = table.Column<string>(type: "text", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: true),
                    AtributoSugerido = table.Column<int>(type: "integer", nullable: true),
                    DisponivelParaCriaturas = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pericias", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Pericias",
                columns: new[] { "Id", "Chave", "Nome", "Descricao", "AtributoSugerido", "DisponivelParaCriaturas", "IsDeleted" },
                values: new object[,]
                {
                    { 0, "Acrobacia", "Acrobacia", null, null, true, false },
                    { 1, "Alquimia", "Alquimia", null, null, false, false },
                    { 2, "Arcano", "Arcano", null, null, false, false },
                    { 3, "Armadilhas", "Armadilhas", null, null, false, false },
                    { 4, "ArmasBrancas", "Armas Brancas", null, null, false, false },
                    { 5, "ArtefatosMagicos", "Artefatos Mágicos", null, null, true, false },
                    { 6, "Artistico", "Artístico", null, null, false, false },
                    { 7, "Atletismo", "Atletismo", null, null, true, false },
                    { 8, "Avaliacao", "Avaliação", null, null, false, false },
                    { 9, "Biblioteca", "Biblioteca", null, null, false, false },
                    { 10, "Brigar", "Brigar", null, null, true, false },
                    { 11, "Conducao", "Condução", null, null, false, false },
                    { 12, "Conhecimentos", "Conhecimentos", null, null, false, false },
                    { 13, "Crime", "Crime", null, null, false, false },
                    { 14, "EmpatiaComAnimais", "Empatia c/ Animais", null, null, true, false },
                    { 15, "Enganacao", "Enganação", null, null, true, false },
                    { 16, "ForcaDeVontade", "Força de Vontade", null, null, true, false },
                    { 17, "Fortitude", "Fortitude", null, null, true, false },
                    { 18, "Furtividade", "Furtividade", null, null, true, false },
                    { 19, "Herborismo", "Herborismo", null, null, false, false },
                    { 20, "Intimidacao", "Intimidação", null, null, true, false },
                    { 21, "Intuicao", "Intuição", null, null, true, false },
                    { 22, "Investigacao", "Investigação", null, null, true, false },
                    { 23, "Labia", "Lábia", null, null, false, false },
                    { 24, "Lideranca", "Liderança", null, null, false, false },
                    { 25, "Linguistica", "Linguística", null, null, false, false },
                    { 26, "Medicina", "Medicina", null, null, false, false },
                    { 27, "Navegacao", "Navegação", null, null, true, false },
                    { 28, "Ocultismo", "Ocultismo", null, null, true, false },
                    { 29, "Oficio", "Ofício", null, null, false, false },
                    { 30, "Percepcao", "Percepção", null, null, true, false },
                    { 31, "Pontaria", "Pontaria", null, null, true, false },
                    { 32, "Prontidao", "Prontidão", null, null, true, false },
                    { 33, "Reflexos", "Reflexos", null, null, true, false },
                    { 34, "Religiao", "Religião", null, null, false, false },
                    { 35, "Saquear", "Saquear", null, null, false, false },
                    { 36, "Seducao", "Sedução", null, null, true, false },
                    { 37, "SensoComum", "Senso Comum", null, null, false, false },
                    { 38, "Sobrevivencia", "Sobrevivência", null, null, true, false },
                });

            migrationBuilder.CreateTable(
                name: "SubcategoriaOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Facet = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubcategoriaOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TabelaDeAfinidades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Afinidade = table.Column<int>(type: "integer", nullable: false),
                    Eficiencia = table.Column<int>(type: "integer", nullable: false),
                    Dano = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TabelaDeAfinidades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TabelaDeNiveisConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    MostrarLimitesNoLivro = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TabelaDeNiveisConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Traits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Custo = table.Column<int>(type: "integer", nullable: false),
                    Polaridade = table.Column<int>(type: "integer", nullable: false),
                    RequerEspecificacao = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsCustomized = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Traits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArcaEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GmId = table.Column<Guid>(type: "uuid", nullable: false),
                    Roll = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArcaEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArcaEntries_AspNetUsers_GmId",
                        column: x => x.GmId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArcaTabelas",
                columns: table => new
                {
                    GmId = table.Column<Guid>(type: "uuid", nullable: false),
                    Dado = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArcaTabelas", x => x.GmId);
                    table.ForeignKey(
                        name: "FK_ArcaTabelas_AspNetUsers_GmId",
                        column: x => x.GmId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Images",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Path = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Images_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InviteCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    GmId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RedeemedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RedeemedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InviteCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InviteCodes_AspNetUsers_GmId",
                        column: x => x.GmId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InviteCodes_AspNetUsers_RedeemedByUserId",
                        column: x => x.RedeemedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RacialAbilityOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GmId = table.Column<Guid>(type: "uuid", nullable: false),
                    Variante = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    NomeDaVariante = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RacialAbilityOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RacialAbilityOverrides_AspNetUsers_GmId",
                        column: x => x.GmId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RacialTraitOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GmId = table.Column<Guid>(type: "uuid", nullable: false),
                    Variante = table.Column<int>(type: "integer", nullable: false),
                    GratuitaOptionsJson = table.Column<string>(type: "text", nullable: false),
                    ObrigatoriaOptionsJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RacialTraitOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RacialTraitOverrides_AspNetUsers_GmId",
                        column: x => x.GmId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RulebookDocumentOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    MarkdownText = table.Column<string>(type: "text", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RulebookDocumentOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RulebookDocumentOverrides_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SpellAbilityBankEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GmId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Grau = table.Column<int>(type: "integer", nullable: false),
                    GastoEmPI = table.Column<int>(type: "integer", nullable: false),
                    Custo = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    DeCriatura = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Categoria = table.Column<int>(type: "integer", nullable: true),
                    Requisitos = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpellAbilityBankEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpellAbilityBankEntries_AspNetUsers_GmId",
                        column: x => x.GmId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CondicaoDesabilitacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CondicaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Categoria = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondicaoDesabilitacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CondicaoDesabilitacoes_Condicoes_CondicaoId",
                        column: x => x.CondicaoId,
                        principalTable: "Condicoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CondicaoModificadores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CondicaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Alvo = table.Column<string>(type: "text", nullable: false),
                    AlvoEspecifico = table.Column<string>(type: "text", nullable: true),
                    TipoDeValor = table.Column<string>(type: "text", nullable: false),
                    Valor = table.Column<int>(type: "integer", nullable: false),
                    Momento = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondicaoModificadores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CondicaoModificadores_Condicoes_CondicaoId",
                        column: x => x.CondicaoId,
                        principalTable: "Condicoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentKitChoiceSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    SubcategoriasCsv = table.Column<string>(type: "text", nullable: true),
                    Rank = table.Column<int>(type: "integer", nullable: true),
                    Qtd = table.Column<int>(type: "integer", nullable: false),
                    BonusSubcategoria = table.Column<string>(type: "text", nullable: true),
                    BonusNome = table.Column<string>(type: "text", nullable: true),
                    BonusQtd = table.Column<int>(type: "integer", nullable: true),
                    ArmorSlot = table.Column<int>(type: "integer", nullable: true),
                    BonusFixedItemId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentKitChoiceSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentKitChoiceSlots_EquipmentKitFixedItems_BonusFixedIt~",
                        column: x => x.BonusFixedItemId,
                        principalTable: "EquipmentKitFixedItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentKitChoiceSlots_EquipmentKits_KitId",
                        column: x => x.KitId,
                        principalTable: "EquipmentKits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentKitItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: true),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Qtd = table.Column<int>(type: "integer", nullable: false),
                    SubcategoriaHint = table.Column<string>(type: "text", nullable: true),
                    ArmorSlot = table.Column<int>(type: "integer", nullable: true),
                    FixedItemId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentKitItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentKitItems_EquipmentKitFixedItems_FixedItemId",
                        column: x => x.FixedItemId,
                        principalTable: "EquipmentKitFixedItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentKitItems_EquipmentKits_KitId",
                        column: x => x.KitId,
                        principalTable: "EquipmentKits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValoresDeNivel",
                columns: table => new
                {
                    Nivel = table.Column<int>(type: "integer", nullable: false),
                    ColunaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Valor = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValoresDeNivel", x => new { x.Nivel, x.ColunaId });
                    table.ForeignKey(
                        name: "FK_ValoresDeNivel_ColunasDeNivel_ColunaId",
                        column: x => x.ColunaId,
                        principalTable: "ColunasDeNivel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ValoresDeNivel_NiveisProgressao_Nivel",
                        column: x => x.Nivel,
                        principalTable: "NiveisProgressao",
                        principalColumn: "Nivel",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Historicos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    PericiaMaisSeisId = table.Column<int>(type: "integer", nullable: false),
                    PericiaMaisTresId = table.Column<int>(type: "integer", nullable: false),
                    IsCustomized = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Historicos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Historicos_Pericias_PericiaMaisSeisId",
                        column: x => x.PericiaMaisSeisId,
                        principalTable: "Pericias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Historicos_Pericias_PericiaMaisTresId",
                        column: x => x.PericiaMaisTresId,
                        principalTable: "Pericias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArcaEvolucoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArcaEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nivel = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    CriadaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArcaEvolucoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArcaEvolucoes_ArcaEntries_ArcaEntryId",
                        column: x => x.ArcaEntryId,
                        principalTable: "ArcaEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GmId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    BonusDeCarga = table.Column<decimal>(type: "numeric", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Campaigns_AspNetUsers_GmId",
                        column: x => x.GmId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Campaigns_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CreatureSheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GmId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nome = table.Column<string>(type: "text", nullable: true),
                    Raca = table.Column<string>(type: "text", nullable: true),
                    Arquetipo = table.Column<int>(type: "integer", nullable: true),
                    SubArquetipo = table.Column<string>(type: "text", nullable: true),
                    Afinidade = table.Column<int>(type: "integer", nullable: true),
                    Rank = table.Column<int>(type: "integer", nullable: true),
                    Nivel = table.Column<int>(type: "integer", nullable: false),
                    ExperienciaAtual = table.Column<int>(type: "integer", nullable: false),
                    PontosDeIgnicao = table.Column<int>(type: "integer", nullable: false),
                    VitalidadeAtual = table.Column<int>(type: "integer", nullable: false),
                    FocoAtual = table.Column<int>(type: "integer", nullable: false),
                    AdrenalinaAtual = table.Column<int>(type: "integer", nullable: false),
                    Cobertura = table.Column<int>(type: "integer", nullable: false),
                    LastDismissedLevelUpLevel = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureSheets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureSheets_AspNetUsers_GmId",
                        column: x => x.GmId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreatureSheets_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CreatureSheets_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GmId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Peso = table.Column<decimal>(type: "numeric", nullable: false),
                    Preco = table.Column<int>(type: "integer", nullable: false),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tipo = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    Arma_Subcategoria = table.Column<string>(type: "text", nullable: true),
                    Arma_Rank = table.Column<int>(type: "integer", nullable: true),
                    Empunhadura = table.Column<int>(type: "integer", nullable: true),
                    Dados = table.Column<string>(type: "text", nullable: true),
                    Dano = table.Column<int>(type: "integer", nullable: true),
                    Critico = table.Column<string>(type: "text", nullable: true),
                    Alcance = table.Column<int>(type: "integer", nullable: true),
                    TipoDeDano = table.Column<int>(type: "integer", nullable: true),
                    Armadura_Categoria = table.Column<int>(type: "integer", nullable: true),
                    Defesa = table.Column<int>(type: "integer", nullable: true),
                    RF = table.Column<int>(type: "integer", nullable: true),
                    RM = table.Column<int>(type: "integer", nullable: true),
                    TipoDeAlvo = table.Column<int>(type: "integer", nullable: true),
                    Alvo = table.Column<string>(type: "text", nullable: true),
                    Valor = table.Column<int>(type: "integer", nullable: true),
                    Categoria = table.Column<int>(type: "integer", nullable: true),
                    BonusDefesa = table.Column<int>(type: "integer", nullable: true),
                    ItemGeral_Subcategoria = table.Column<string>(type: "text", nullable: true),
                    Descricao = table.Column<string>(type: "text", nullable: true),
                    CapacidadeExtra = table.Column<decimal>(type: "numeric", nullable: true),
                    Armadura_Subcategoria = table.Column<string>(type: "text", nullable: true),
                    Artefato_Subcategoria = table.Column<string>(type: "text", nullable: true),
                    Escudo_Subcategoria = table.Column<string>(type: "text", nullable: true),
                    Armadura_Rank = table.Column<int>(type: "integer", nullable: true),
                    Escudo_Rank = table.Column<int>(type: "integer", nullable: true),
                    PenalidadeDeRequisitos = table.Column<string>(type: "jsonb", nullable: true),
                    Requisitos = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Items_AspNetUsers_GmId",
                        column: x => x.GmId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Items_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RuneBankEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GmId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Grau = table.Column<int>(type: "integer", nullable: false),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tipo = table.Column<string>(type: "text", nullable: true),
                    Disciplina = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuneBankEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuneBankEntries_AspNetUsers_GmId",
                        column: x => x.GmId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RuneBankEntries_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SpellAbilityBankEffects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpellAbilityBankEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    EfeitoNome = table.Column<string>(type: "text", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: true),
                    CustoPI = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpellAbilityBankEffects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpellAbilityBankEffects_SpellAbilityBankEntries_SpellAbilit~",
                        column: x => x.SpellAbilityBankEntryId,
                        principalTable: "SpellAbilityBankEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcSheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GmId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nome = table.Column<string>(type: "text", nullable: true),
                    Linhagem = table.Column<int>(type: "integer", nullable: true),
                    Variante = table.Column<int>(type: "integer", nullable: true),
                    Vocacao = table.Column<int>(type: "integer", nullable: true),
                    SubVocacao = table.Column<string>(type: "text", nullable: true),
                    Afinidade = table.Column<int>(type: "integer", nullable: true),
                    Propriedade = table.Column<string>(type: "text", nullable: true),
                    Nivel = table.Column<int>(type: "integer", nullable: false),
                    Circulo = table.Column<int>(type: "integer", nullable: false),
                    Grau = table.Column<int>(type: "integer", nullable: false),
                    PossuiCoracaoDeMana = table.Column<bool>(type: "boolean", nullable: false),
                    ExperienciaAtual = table.Column<int>(type: "integer", nullable: false),
                    EAPAtual = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankF = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankE = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankD = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankC = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankB = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankA = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankS = table.Column<int>(type: "integer", nullable: false),
                    PontosDeIgnicaoAtual = table.Column<int>(type: "integer", nullable: false),
                    PontosDeIgnicaoTotal = table.Column<int>(type: "integer", nullable: false),
                    VitalidadeAtual = table.Column<int>(type: "integer", nullable: false),
                    FocoAtual = table.Column<int>(type: "integer", nullable: false),
                    AdrenalinaAtual = table.Column<int>(type: "integer", nullable: false),
                    EstresseAtual = table.Column<int>(type: "integer", nullable: false),
                    Cobertura = table.Column<int>(type: "integer", nullable: false),
                    Ciclos = table.Column<int>(type: "integer", nullable: false),
                    LastDismissedLevelUpLevel = table.Column<int>(type: "integer", nullable: true),
                    ArcaRolada = table.Column<int>(type: "integer", nullable: true),
                    Estrela = table.Column<int>(type: "integer", nullable: true),
                    SinaAtual = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    HistoricoId = table.Column<Guid>(type: "uuid", nullable: true),
                    EquipmentKitId = table.Column<Guid>(type: "uuid", nullable: true),
                    Historia = table.Column<string>(type: "text", nullable: true),
                    AfinidadeAdicional = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcSheets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcSheets_AspNetUsers_GmId",
                        column: x => x.GmId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NpcSheets_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NpcSheets_EquipmentKits_EquipmentKitId",
                        column: x => x.EquipmentKitId,
                        principalTable: "EquipmentKits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NpcSheets_Historicos_HistoricoId",
                        column: x => x.HistoricoId,
                        principalTable: "Historicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NpcSheets_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CampaignMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignMembers_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterSheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nome = table.Column<string>(type: "text", nullable: true),
                    Linhagem = table.Column<int>(type: "integer", nullable: true),
                    Variante = table.Column<int>(type: "integer", nullable: true),
                    Vocacao = table.Column<int>(type: "integer", nullable: true),
                    SubVocacao = table.Column<string>(type: "text", nullable: true),
                    Afinidade = table.Column<int>(type: "integer", nullable: true),
                    Propriedade = table.Column<string>(type: "text", nullable: true),
                    Nivel = table.Column<int>(type: "integer", nullable: false),
                    Circulo = table.Column<int>(type: "integer", nullable: false),
                    Grau = table.Column<int>(type: "integer", nullable: false),
                    PossuiCoracaoDeMana = table.Column<bool>(type: "boolean", nullable: false),
                    ExperienciaAtual = table.Column<int>(type: "integer", nullable: false),
                    EAPAtual = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankF = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankE = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankD = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankC = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankB = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankA = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankS = table.Column<int>(type: "integer", nullable: false),
                    PontosDeIgnicaoAtual = table.Column<int>(type: "integer", nullable: false),
                    PontosDeIgnicaoTotal = table.Column<int>(type: "integer", nullable: false),
                    VitalidadeAtual = table.Column<int>(type: "integer", nullable: false),
                    FocoAtual = table.Column<int>(type: "integer", nullable: false),
                    AdrenalinaAtual = table.Column<int>(type: "integer", nullable: false),
                    EstresseAtual = table.Column<int>(type: "integer", nullable: false),
                    Cobertura = table.Column<int>(type: "integer", nullable: false),
                    Ciclos = table.Column<int>(type: "integer", nullable: false),
                    LastDismissedLevelUpLevel = table.Column<int>(type: "integer", nullable: true),
                    PontosDeIgnicaoBonusManual = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PontosDePericiaBonusCritico = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ArcaRolada = table.Column<int>(type: "integer", nullable: true),
                    Estrela = table.Column<int>(type: "integer", nullable: true),
                    SinaAtual = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    HistoricoId = table.Column<Guid>(type: "uuid", nullable: true),
                    EquipmentKitId = table.Column<Guid>(type: "uuid", nullable: true),
                    Historia = table.Column<string>(type: "text", nullable: true),
                    AfinidadeAdicional = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterSheets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterSheets_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterSheets_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterSheets_EquipmentKits_EquipmentKitId",
                        column: x => x.EquipmentKitId,
                        principalTable: "EquipmentKits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CharacterSheets_Historicos_HistoricoId",
                        column: x => x.HistoricoId,
                        principalTable: "Historicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CharacterSheets_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CreatureAffections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Favorabilidade = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureAffections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureAffections_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreatureAttributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Atributo = table.Column<int>(type: "integer", nullable: false),
                    Gasto = table.Column<int>(type: "integer", nullable: false),
                    Bonus = table.Column<int>(type: "integer", nullable: false),
                    TemMaestria = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureAttributes_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreatureMasteries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    PericiaId = table.Column<int>(type: "integer", nullable: false),
                    Atributo = table.Column<int>(type: "integer", nullable: false),
                    GastoMaestria = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureMasteries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureMasteries_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreatureMasteries_Pericias_PericiaId",
                        column: x => x.PericiaId,
                        principalTable: "Pericias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreatureSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    PericiaId = table.Column<int>(type: "integer", nullable: false),
                    Gasto = table.Column<int>(type: "integer", nullable: false),
                    AtributoEscolhido = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureSkills_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreatureSkills_Pericias_PericiaId",
                        column: x => x.PericiaId,
                        principalTable: "Pericias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreatureSpellAbilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceBankEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Grau = table.Column<int>(type: "integer", nullable: false),
                    GastoEmPI = table.Column<int>(type: "integer", nullable: false),
                    Custo = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Categoria = table.Column<int>(type: "integer", nullable: true),
                    Requisitos = table.Column<string>(type: "jsonb", nullable: true),
                    CriadaNaFicha = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureSpellAbilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureSpellAbilities_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreatureTraits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TraitId = table.Column<Guid>(type: "uuid", nullable: true),
                    Polaridade = table.Column<int>(type: "integer", nullable: false),
                    Especificacao = table.Column<string>(type: "text", nullable: true),
                    CreatureExclusiveTraitId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureTraits", x => x.Id);
                    table.CheckConstraint("CK_CreatureTraits_ExactlyOneTraitSource", "num_nonnulls(\"TraitId\", \"CreatureExclusiveTraitId\") = 1");
                    table.ForeignKey(
                        name: "FK_CreatureTraits_CreatureExclusiveTraits_CreatureExclusiveTra~",
                        column: x => x.CreatureExclusiveTraitId,
                        principalTable: "CreatureExclusiveTraits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreatureTraits_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreatureTraits_Traits_TraitId",
                        column: x => x.TraitId,
                        principalTable: "Traits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreatureArmorSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    DurabilidadeAtual = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureArmorSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureArmorSlots_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreatureArmorSlots_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreatureArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactItemId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureArtifacts_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreatureArtifacts_Items_ArtifactItemId",
                        column: x => x.ArtifactItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreatureShields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEquipped = table.Column<bool>(type: "boolean", nullable: false),
                    DurabilidadeAtual = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureShields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureShields_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreatureShields_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreatureSpoils",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qtd = table.Column<int>(type: "integer", nullable: false),
                    DT = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureSpoils", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureSpoils_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreatureSpoils_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreatureWeapons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEquipped = table.Column<bool>(type: "boolean", nullable: false),
                    DurabilidadeAtual = table.Column<int>(type: "integer", nullable: true),
                    ManualNome = table.Column<string>(type: "text", nullable: true),
                    ManualTipoDeDano = table.Column<int>(type: "integer", nullable: true),
                    ManualDados = table.Column<string>(type: "text", nullable: true),
                    ManualDano = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureWeapons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureWeapons_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreatureWeapons_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CampaignAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: true),
                    SpellAbilityBankEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    NpcNomePublico = table.Column<bool>(type: "boolean", nullable: false),
                    NpcImagemPublica = table.Column<bool>(type: "boolean", nullable: false),
                    CreatureNomePublico = table.Column<bool>(type: "boolean", nullable: false),
                    CreatureImagemPublica = table.Column<bool>(type: "boolean", nullable: false),
                    RuneBankEntryId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignAttachments_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignAttachments_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignAttachments_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignAttachments_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignAttachments_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignAttachments_RuneBankEntries_RuneBankEntryId",
                        column: x => x.RuneBankEntryId,
                        principalTable: "RuneBankEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignAttachments_SpellAbilityBankEntries_SpellAbilityBan~",
                        column: x => x.SpellAbilityBankEntryId,
                        principalTable: "SpellAbilityBankEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcAffections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Favorabilidade = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcAffections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcAffections_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcAffinities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Elemento = table.Column<int>(type: "integer", nullable: true),
                    SubElemento = table.Column<int>(type: "integer", nullable: true),
                    Experiencia = table.Column<int>(type: "integer", nullable: true),
                    ElementoValor = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    SubElementoValor = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    SegundaEssencia = table.Column<int>(type: "integer", nullable: true),
                    SegundaEssenciaValor = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcAffinities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcAffinities_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcArmorSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    DurabilidadeAtual = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcArmorSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcArmorSlots_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NpcArmorSlots_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactItemId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcArtifacts_Items_ArtifactItemId",
                        column: x => x.ArtifactItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NpcArtifacts_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcAttributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Atributo = table.Column<int>(type: "integer", nullable: false),
                    Gasto = table.Column<int>(type: "integer", nullable: false),
                    Bonus = table.Column<int>(type: "integer", nullable: false),
                    TemMaestria = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcAttributes_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcInventoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qtd = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcInventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcInventoryItems_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NpcInventoryItems_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcMasteries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    PericiaId = table.Column<int>(type: "integer", nullable: false),
                    Atributo = table.Column<int>(type: "integer", nullable: false),
                    GastoMaestria = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcMasteries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcMasteries_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NpcMasteries_Pericias_PericiaId",
                        column: x => x.PericiaId,
                        principalTable: "Pericias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NpcRunes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Grau = table.Column<int>(type: "integer", nullable: false),
                    SourceBankEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tipo = table.Column<string>(type: "text", nullable: true),
                    Disciplina = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcRunes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcRunes_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NpcRunes_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcSheetHistoriaImages",
                columns: table => new
                {
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcSheetHistoriaImages", x => new { x.NpcSheetId, x.ImageId });
                    table.ForeignKey(
                        name: "FK_NpcSheetHistoriaImages_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NpcSheetHistoriaImages_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcShields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEquipped = table.Column<bool>(type: "boolean", nullable: false),
                    DurabilidadeAtual = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcShields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcShields_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NpcShields_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    PericiaId = table.Column<int>(type: "integer", nullable: false),
                    Gasto = table.Column<int>(type: "integer", nullable: false),
                    AtributoEscolhido = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcSkills_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NpcSkills_Pericias_PericiaId",
                        column: x => x.PericiaId,
                        principalTable: "Pericias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NpcSpellAbilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceBankEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Grau = table.Column<int>(type: "integer", nullable: false),
                    GastoEmPI = table.Column<int>(type: "integer", nullable: false),
                    Custo = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Categoria = table.Column<int>(type: "integer", nullable: true),
                    Requisitos = table.Column<string>(type: "jsonb", nullable: true),
                    CriadaNaFicha = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcSpellAbilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcSpellAbilities_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcTraits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TraitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Polaridade = table.Column<int>(type: "integer", nullable: false),
                    Especificacao = table.Column<string>(type: "text", nullable: true),
                    IsRacial = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RacialVariante = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcTraits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcTraits_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NpcTraits_Traits_TraitId",
                        column: x => x.TraitId,
                        principalTable: "Traits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NpcWeapons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEquipped = table.Column<bool>(type: "boolean", nullable: false),
                    DurabilidadeAtual = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcWeapons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcWeapons_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NpcWeapons_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterAffections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Favorabilidade = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterAffections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterAffections_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterAffinities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Elemento = table.Column<int>(type: "integer", nullable: true),
                    SubElemento = table.Column<int>(type: "integer", nullable: true),
                    Experiencia = table.Column<int>(type: "integer", nullable: true),
                    ElementoValor = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    SubElementoValor = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    SegundaEssencia = table.Column<int>(type: "integer", nullable: true),
                    SegundaEssenciaValor = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterAffinities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterAffinities_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterArmorSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    DurabilidadeAtual = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterArmorSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterArmorSlots_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterArmorSlots_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CharacterArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactItemId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterArtifacts_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterArtifacts_Items_ArtifactItemId",
                        column: x => x.ArtifactItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CharacterAttributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Atributo = table.Column<int>(type: "integer", nullable: false),
                    Gasto = table.Column<int>(type: "integer", nullable: false),
                    Bonus = table.Column<int>(type: "integer", nullable: false),
                    TemMaestria = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterAttributes_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterInventoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qtd = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterInventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterInventoryItems_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterInventoryItems_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CharacterMasteries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    PericiaId = table.Column<int>(type: "integer", nullable: false),
                    Atributo = table.Column<int>(type: "integer", nullable: false),
                    GastoMaestria = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterMasteries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterMasteries_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterMasteries_Pericias_PericiaId",
                        column: x => x.PericiaId,
                        principalTable: "Pericias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CharacterRunes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Grau = table.Column<int>(type: "integer", nullable: false),
                    SourceBankEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tipo = table.Column<string>(type: "text", nullable: true),
                    Disciplina = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterRunes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterRunes_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterRunes_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CharacterSheetHistoriaImages",
                columns: table => new
                {
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterSheetHistoriaImages", x => new { x.CharacterSheetId, x.ImageId });
                    table.ForeignKey(
                        name: "FK_CharacterSheetHistoriaImages_CharacterSheets_CharacterSheet~",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterSheetHistoriaImages_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterShields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEquipped = table.Column<bool>(type: "boolean", nullable: false),
                    DurabilidadeAtual = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterShields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterShields_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterShields_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CharacterSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    PericiaId = table.Column<int>(type: "integer", nullable: false),
                    Gasto = table.Column<int>(type: "integer", nullable: false),
                    AtributoEscolhido = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterSkills_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterSkills_Pericias_PericiaId",
                        column: x => x.PericiaId,
                        principalTable: "Pericias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CharacterSpellAbilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceBankEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Grau = table.Column<int>(type: "integer", nullable: false),
                    GastoEmPI = table.Column<int>(type: "integer", nullable: false),
                    Custo = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Categoria = table.Column<int>(type: "integer", nullable: true),
                    Requisitos = table.Column<string>(type: "jsonb", nullable: true),
                    CriadaNaFicha = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterSpellAbilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterSpellAbilities_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterTraits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TraitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Polaridade = table.Column<int>(type: "integer", nullable: false),
                    Especificacao = table.Column<string>(type: "text", nullable: true),
                    IsRacial = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RacialVariante = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterTraits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterTraits_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterTraits_Traits_TraitId",
                        column: x => x.TraitId,
                        principalTable: "Traits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CharacterWeapons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEquipped = table.Column<bool>(type: "boolean", nullable: false),
                    DurabilidadeAtual = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterWeapons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterWeapons_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterWeapons_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiaryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsSecretNote = table.Column<bool>(type: "boolean", nullable: false),
                    Texto = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiaryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiaryEntries_AspNetUsers_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiaryEntries_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiaryEntries_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SheetLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: true),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorNickname = table.Column<string>(type: "text", nullable: false),
                    ActorIsGm = table.Column<bool>(type: "boolean", nullable: false),
                    Categoria = table.Column<string>(type: "text", nullable: false),
                    Acao = table.Column<string>(type: "text", nullable: false),
                    Secao = table.Column<string>(type: "text", nullable: false),
                    Campo = table.Column<string>(type: "text", nullable: true),
                    ItemNome = table.Column<string>(type: "text", nullable: true),
                    ValorAntes = table.Column<string>(type: "text", nullable: true),
                    ValorDepois = table.Column<string>(type: "text", nullable: true),
                    Automatico = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SheetLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SheetLogEntries_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SheetLogEntries_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SheetLogEntries_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreatureSpellAbilityEffects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSpellAbilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EfeitoNome = table.Column<string>(type: "text", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: true),
                    CustoPI = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureSpellAbilityEffects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureSpellAbilityEffects_CreatureSpellAbilities_Creature~",
                        column: x => x.CreatureSpellAbilityId,
                        principalTable: "CreatureSpellAbilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcSpellAbilityEffects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSpellAbilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EfeitoNome = table.Column<string>(type: "text", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: true),
                    CustoPI = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcSpellAbilityEffects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcSpellAbilityEffects_NpcSpellAbilities_NpcSpellAbilityId",
                        column: x => x.NpcSpellAbilityId,
                        principalTable: "NpcSpellAbilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterSpellAbilityEffects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSpellAbilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EfeitoNome = table.Column<string>(type: "text", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: true),
                    CustoPI = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterSpellAbilityEffects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterSpellAbilityEffects_CharacterSpellAbilities_Charac~",
                        column: x => x.CharacterSpellAbilityId,
                        principalTable: "CharacterSpellAbilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiaryEntryImages",
                columns: table => new
                {
                    DiaryEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiaryEntryImages", x => new { x.DiaryEntryId, x.ImageId });
                    table.ForeignKey(
                        name: "FK_DiaryEntryImages_DiaryEntries_DiaryEntryId",
                        column: x => x.DiaryEntryId,
                        principalTable: "DiaryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiaryEntryImages_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiaryEntryRecipients",
                columns: table => new
                {
                    DiaryEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiaryEntryRecipients", x => new { x.DiaryEntryId, x.UserId });
                    table.ForeignKey(
                        name: "FK_DiaryEntryRecipients_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiaryEntryRecipients_DiaryEntries_DiaryEntryId",
                        column: x => x.DiaryEntryId,
                        principalTable: "DiaryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EncounterLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequencia = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Turno = table.Column<int>(type: "integer", nullable: false),
                    VezParticipantId = table.Column<Guid>(type: "uuid", nullable: true),
                    VezNome = table.Column<string>(type: "text", nullable: true),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    ParticipanteNome = table.Column<string>(type: "text", nullable: true),
                    Campo = table.Column<string>(type: "text", nullable: true),
                    ValorAntes = table.Column<string>(type: "text", nullable: true),
                    ValorDepois = table.Column<string>(type: "text", nullable: true),
                    Texto = table.Column<string>(type: "text", nullable: true),
                    Ordem = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterLogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EncounterParticipantCondicoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EncounterParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CondicaoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Icone = table.Column<string>(type: "text", nullable: false),
                    AplicadaNoTurno = table.Column<int>(type: "integer", nullable: false),
                    DuracaoTurnos = table.Column<int>(type: "integer", nullable: true),
                    Modificadores = table.Column<string>(type: "jsonb", nullable: false),
                    Desabilitacoes = table.Column<string>(type: "jsonb", nullable: false),
                    AjustesAplicados = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterParticipantCondicoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EncounterParticipantConditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EncounterParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Texto = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterParticipantConditions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EncounterParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCharacterSheetId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceNpcSheetId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceCreatureSheetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Iniciativa = table.Column<int>(type: "integer", nullable: false),
                    PVAtual = table.Column<int>(type: "integer", nullable: true),
                    PFAtual = table.Column<int>(type: "integer", nullable: true),
                    PAAtual = table.Column<int>(type: "integer", nullable: true),
                    AcoesRestantes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncounterParticipants_CharacterSheets_SourceCharacterSheetId",
                        column: x => x.SourceCharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EncounterParticipants_CreatureSheets_SourceCreatureSheetId",
                        column: x => x.SourceCreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EncounterParticipants_NpcSheets_SourceNpcSheetId",
                        column: x => x.SourceNpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Encounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: true),
                    CurrentRound = table.Column<int>(type: "integer", nullable: false),
                    CurrentParticipantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinalizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Preparacao"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Encounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Encounters_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Encounters_EncounterParticipants_CurrentParticipantId",
                        column: x => x.CurrentParticipantId,
                        principalTable: "EncounterParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArcaEntries_GmId_Roll",
                table: "ArcaEntries",
                columns: new[] { "GmId", "Roll" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArcaEvolucoes_ArcaEntryId_Nivel",
                table: "ArcaEvolucoes",
                columns: new[] { "ArcaEntryId", "Nivel" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_NormalizedNickname",
                table: "AspNetUsers",
                column: "NormalizedNickname",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAttachments_CampaignId",
                table: "CampaignAttachments",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAttachments_CreatureSheetId",
                table: "CampaignAttachments",
                column: "CreatureSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAttachments_ImageId",
                table: "CampaignAttachments",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAttachments_ItemId",
                table: "CampaignAttachments",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAttachments_NpcSheetId",
                table: "CampaignAttachments",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAttachments_RuneBankEntryId",
                table: "CampaignAttachments",
                column: "RuneBankEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAttachments_SpellAbilityBankEntryId",
                table: "CampaignAttachments",
                column: "SpellAbilityBankEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMembers_CampaignId_UserId",
                table: "CampaignMembers",
                columns: new[] { "CampaignId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMembers_UserId",
                table: "CampaignMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_GmId",
                table: "Campaigns",
                column: "GmId");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_ImageId",
                table: "Campaigns",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterAffections_CharacterSheetId",
                table: "CharacterAffections",
                column: "CharacterSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterAffinities_CharacterSheetId",
                table: "CharacterAffinities",
                column: "CharacterSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterArmorSlots_CharacterSheetId_Slot",
                table: "CharacterArmorSlots",
                columns: new[] { "CharacterSheetId", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterArmorSlots_ItemId",
                table: "CharacterArmorSlots",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterArtifacts_ArtifactItemId",
                table: "CharacterArtifacts",
                column: "ArtifactItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterArtifacts_CharacterSheetId",
                table: "CharacterArtifacts",
                column: "CharacterSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterAttributes_CharacterSheetId_Atributo",
                table: "CharacterAttributes",
                columns: new[] { "CharacterSheetId", "Atributo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterInventoryItems_CharacterSheetId",
                table: "CharacterInventoryItems",
                column: "CharacterSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterInventoryItems_ItemId",
                table: "CharacterInventoryItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterMasteries_CharacterSheetId",
                table: "CharacterMasteries",
                column: "CharacterSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterMasteries_PericiaId",
                table: "CharacterMasteries",
                column: "PericiaId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterRunes_CharacterSheetId",
                table: "CharacterRunes",
                column: "CharacterSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterRunes_ImageId",
                table: "CharacterRunes",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSheetHistoriaImages_ImageId",
                table: "CharacterSheetHistoriaImages",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSheets_CampaignId",
                table: "CharacterSheets",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSheets_EquipmentKitId",
                table: "CharacterSheets",
                column: "EquipmentKitId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSheets_HistoricoId",
                table: "CharacterSheets",
                column: "HistoricoId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSheets_ImageId",
                table: "CharacterSheets",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSheets_OwnerId",
                table: "CharacterSheets",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterShields_CharacterSheetId",
                table: "CharacterShields",
                column: "CharacterSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterShields_ItemId",
                table: "CharacterShields",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSkills_CharacterSheetId_PericiaId",
                table: "CharacterSkills",
                columns: new[] { "CharacterSheetId", "PericiaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSkills_PericiaId",
                table: "CharacterSkills",
                column: "PericiaId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSpellAbilities_CharacterSheetId",
                table: "CharacterSpellAbilities",
                column: "CharacterSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSpellAbilityEffects_CharacterSpellAbilityId",
                table: "CharacterSpellAbilityEffects",
                column: "CharacterSpellAbilityId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterTraits_CharacterSheetId",
                table: "CharacterTraits",
                column: "CharacterSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterTraits_TraitId",
                table: "CharacterTraits",
                column: "TraitId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterWeapons_CharacterSheetId",
                table: "CharacterWeapons",
                column: "CharacterSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterWeapons_ItemId",
                table: "CharacterWeapons",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ColunasDeNivel_ChaveDeSistema",
                table: "ColunasDeNivel",
                column: "ChaveDeSistema",
                unique: true,
                filter: "\"ChaveDeSistema\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CondicaoDesabilitacoes_CondicaoId",
                table: "CondicaoDesabilitacoes",
                column: "CondicaoId");

            migrationBuilder.CreateIndex(
                name: "IX_CondicaoModificadores_CondicaoId",
                table: "CondicaoModificadores",
                column: "CondicaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Condicoes_Nome",
                table: "Condicoes",
                column: "Nome",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureAffections_CreatureSheetId",
                table: "CreatureAffections",
                column: "CreatureSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureArmorSlots_CreatureSheetId_Slot",
                table: "CreatureArmorSlots",
                columns: new[] { "CreatureSheetId", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreatureArmorSlots_ItemId",
                table: "CreatureArmorSlots",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureArtifacts_ArtifactItemId",
                table: "CreatureArtifacts",
                column: "ArtifactItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureArtifacts_CreatureSheetId",
                table: "CreatureArtifacts",
                column: "CreatureSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureAttributes_CreatureSheetId_Atributo",
                table: "CreatureAttributes",
                columns: new[] { "CreatureSheetId", "Atributo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreatureMasteries_CreatureSheetId",
                table: "CreatureMasteries",
                column: "CreatureSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureMasteries_PericiaId",
                table: "CreatureMasteries",
                column: "PericiaId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSheets_GmId",
                table: "CreatureSheets",
                column: "GmId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSheets_ImageId",
                table: "CreatureSheets",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSheets_OwnerId",
                table: "CreatureSheets",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureShields_CreatureSheetId",
                table: "CreatureShields",
                column: "CreatureSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureShields_ItemId",
                table: "CreatureShields",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSkills_CreatureSheetId_PericiaId",
                table: "CreatureSkills",
                columns: new[] { "CreatureSheetId", "PericiaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSkills_PericiaId",
                table: "CreatureSkills",
                column: "PericiaId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSpellAbilities_CreatureSheetId",
                table: "CreatureSpellAbilities",
                column: "CreatureSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSpellAbilityEffects_CreatureSpellAbilityId",
                table: "CreatureSpellAbilityEffects",
                column: "CreatureSpellAbilityId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSpoils_CreatureSheetId",
                table: "CreatureSpoils",
                column: "CreatureSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSpoils_ItemId",
                table: "CreatureSpoils",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureTraits_CreatureExclusiveTraitId",
                table: "CreatureTraits",
                column: "CreatureExclusiveTraitId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureTraits_CreatureSheetId",
                table: "CreatureTraits",
                column: "CreatureSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureTraits_TraitId",
                table: "CreatureTraits",
                column: "TraitId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureWeapons_CreatureSheetId",
                table: "CreatureWeapons",
                column: "CreatureSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureWeapons_ItemId",
                table: "CreatureWeapons",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_AuthorUserId",
                table: "DiaryEntries",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_CampaignId",
                table: "DiaryEntries",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_CharacterSheetId",
                table: "DiaryEntries",
                column: "CharacterSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntryImages_ImageId",
                table: "DiaryEntryImages",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntryRecipients_UserId",
                table: "DiaryEntryRecipients",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterLogEntries_EncounterId_Ordem",
                table: "EncounterLogEntries",
                columns: new[] { "EncounterId", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_EncounterParticipantCondicoes_EncounterParticipantId",
                table: "EncounterParticipantCondicoes",
                column: "EncounterParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterParticipantConditions_EncounterParticipantId",
                table: "EncounterParticipantConditions",
                column: "EncounterParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterParticipants_EncounterId",
                table: "EncounterParticipants",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterParticipants_SourceCharacterSheetId",
                table: "EncounterParticipants",
                column: "SourceCharacterSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterParticipants_SourceCreatureSheetId",
                table: "EncounterParticipants",
                column: "SourceCreatureSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterParticipants_SourceNpcSheetId",
                table: "EncounterParticipants",
                column: "SourceNpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_CampaignId",
                table: "Encounters",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_CurrentParticipantId",
                table: "Encounters",
                column: "CurrentParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentKitChoiceSlots_BonusFixedItemId",
                table: "EquipmentKitChoiceSlots",
                column: "BonusFixedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentKitChoiceSlots_KitId",
                table: "EquipmentKitChoiceSlots",
                column: "KitId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentKitFixedItems_Nome_Tipo",
                table: "EquipmentKitFixedItems",
                columns: new[] { "Nome", "Tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentKitItems_FixedItemId",
                table: "EquipmentKitItems",
                column: "FixedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentKitItems_KitId",
                table: "EquipmentKitItems",
                column: "KitId");

            migrationBuilder.CreateIndex(
                name: "IX_Historicos_PericiaMaisSeisId",
                table: "Historicos",
                column: "PericiaMaisSeisId");

            migrationBuilder.CreateIndex(
                name: "IX_Historicos_PericiaMaisTresId",
                table: "Historicos",
                column: "PericiaMaisTresId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_UploadedByUserId",
                table: "Images",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InviteCodes_Code",
                table: "InviteCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InviteCodes_GmId",
                table: "InviteCodes",
                column: "GmId");

            migrationBuilder.CreateIndex(
                name: "IX_InviteCodes_RedeemedByUserId",
                table: "InviteCodes",
                column: "RedeemedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_GmId",
                table: "Items",
                column: "GmId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ImageId",
                table: "Items",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcAffections_NpcSheetId",
                table: "NpcAffections",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcAffinities_NpcSheetId",
                table: "NpcAffinities",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcArmorSlots_ItemId",
                table: "NpcArmorSlots",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcArmorSlots_NpcSheetId_Slot",
                table: "NpcArmorSlots",
                columns: new[] { "NpcSheetId", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NpcArtifacts_ArtifactItemId",
                table: "NpcArtifacts",
                column: "ArtifactItemId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcArtifacts_NpcSheetId",
                table: "NpcArtifacts",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcAttributes_NpcSheetId_Atributo",
                table: "NpcAttributes",
                columns: new[] { "NpcSheetId", "Atributo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NpcInventoryItems_ItemId",
                table: "NpcInventoryItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcInventoryItems_NpcSheetId",
                table: "NpcInventoryItems",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcMasteries_NpcSheetId",
                table: "NpcMasteries",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcMasteries_PericiaId",
                table: "NpcMasteries",
                column: "PericiaId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcRunes_ImageId",
                table: "NpcRunes",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcRunes_NpcSheetId",
                table: "NpcRunes",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcSheetHistoriaImages_ImageId",
                table: "NpcSheetHistoriaImages",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcSheets_EquipmentKitId",
                table: "NpcSheets",
                column: "EquipmentKitId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcSheets_GmId",
                table: "NpcSheets",
                column: "GmId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcSheets_HistoricoId",
                table: "NpcSheets",
                column: "HistoricoId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcSheets_ImageId",
                table: "NpcSheets",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcSheets_OwnerId",
                table: "NpcSheets",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcShields_ItemId",
                table: "NpcShields",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcShields_NpcSheetId",
                table: "NpcShields",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcSkills_NpcSheetId_PericiaId",
                table: "NpcSkills",
                columns: new[] { "NpcSheetId", "PericiaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NpcSkills_PericiaId",
                table: "NpcSkills",
                column: "PericiaId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcSpellAbilities_NpcSheetId",
                table: "NpcSpellAbilities",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcSpellAbilityEffects_NpcSpellAbilityId",
                table: "NpcSpellAbilityEffects",
                column: "NpcSpellAbilityId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcTraits_NpcSheetId",
                table: "NpcTraits",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcTraits_TraitId",
                table: "NpcTraits",
                column: "TraitId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcWeapons_ItemId",
                table: "NpcWeapons",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcWeapons_NpcSheetId",
                table: "NpcWeapons",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_Pericias_Chave",
                table: "Pericias",
                column: "Chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pericias_Nome",
                table: "Pericias",
                column: "Nome",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_RacialAbilityOverrides_GmId_Variante",
                table: "RacialAbilityOverrides",
                columns: new[] { "GmId", "Variante" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RacialTraitOverrides_GmId_Variante",
                table: "RacialTraitOverrides",
                columns: new[] { "GmId", "Variante" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RulebookDocumentOverrides_Slug",
                table: "RulebookDocumentOverrides",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RulebookDocumentOverrides_UpdatedByUserId",
                table: "RulebookDocumentOverrides",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RuneBankEntries_GmId",
                table: "RuneBankEntries",
                column: "GmId");

            migrationBuilder.CreateIndex(
                name: "IX_RuneBankEntries_ImageId",
                table: "RuneBankEntries",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_SheetLogEntries_CharacterSheetId_OccurredAt",
                table: "SheetLogEntries",
                columns: new[] { "CharacterSheetId", "OccurredAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_SheetLogEntries_CreatureSheetId_OccurredAt",
                table: "SheetLogEntries",
                columns: new[] { "CreatureSheetId", "OccurredAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_SheetLogEntries_NpcSheetId_OccurredAt",
                table: "SheetLogEntries",
                columns: new[] { "NpcSheetId", "OccurredAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_SpellAbilityBankEffects_SpellAbilityBankEntryId",
                table: "SpellAbilityBankEffects",
                column: "SpellAbilityBankEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_SpellAbilityBankEntries_GmId",
                table: "SpellAbilityBankEntries",
                column: "GmId");

            migrationBuilder.CreateIndex(
                name: "IX_TabelaDeAfinidades_Afinidade",
                table: "TabelaDeAfinidades",
                column: "Afinidade",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValoresDeNivel_ColunaId",
                table: "ValoresDeNivel",
                column: "ColunaId");

            migrationBuilder.AddForeignKey(
                name: "FK_EncounterLogEntries_Encounters_EncounterId",
                table: "EncounterLogEntries",
                column: "EncounterId",
                principalTable: "Encounters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EncounterParticipantCondicoes_EncounterParticipants_Encount~",
                table: "EncounterParticipantCondicoes",
                column: "EncounterParticipantId",
                principalTable: "EncounterParticipants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EncounterParticipantConditions_EncounterParticipants_Encoun~",
                table: "EncounterParticipantConditions",
                column: "EncounterParticipantId",
                principalTable: "EncounterParticipants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EncounterParticipants_Encounters_EncounterId",
                table: "EncounterParticipants",
                column: "EncounterId",
                principalTable: "Encounters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Campaigns_AspNetUsers_GmId",
                table: "Campaigns");

            migrationBuilder.DropForeignKey(
                name: "FK_CharacterSheets_AspNetUsers_OwnerId",
                table: "CharacterSheets");

            migrationBuilder.DropForeignKey(
                name: "FK_CreatureSheets_AspNetUsers_GmId",
                table: "CreatureSheets");

            migrationBuilder.DropForeignKey(
                name: "FK_CreatureSheets_AspNetUsers_OwnerId",
                table: "CreatureSheets");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_AspNetUsers_UploadedByUserId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_NpcSheets_AspNetUsers_GmId",
                table: "NpcSheets");

            migrationBuilder.DropForeignKey(
                name: "FK_NpcSheets_AspNetUsers_OwnerId",
                table: "NpcSheets");

            migrationBuilder.DropForeignKey(
                name: "FK_CharacterSheets_Campaigns_CampaignId",
                table: "CharacterSheets");

            migrationBuilder.DropForeignKey(
                name: "FK_Encounters_Campaigns_CampaignId",
                table: "Encounters");

            migrationBuilder.DropForeignKey(
                name: "FK_EncounterParticipants_CreatureSheets_SourceCreatureSheetId",
                table: "EncounterParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_CharacterSheets_Images_ImageId",
                table: "CharacterSheets");

            migrationBuilder.DropForeignKey(
                name: "FK_NpcSheets_Images_ImageId",
                table: "NpcSheets");

            migrationBuilder.DropForeignKey(
                name: "FK_EncounterParticipants_NpcSheets_SourceNpcSheetId",
                table: "EncounterParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_EncounterParticipants_CharacterSheets_SourceCharacterSheetId",
                table: "EncounterParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_EncounterParticipants_Encounters_EncounterId",
                table: "EncounterParticipants");

            migrationBuilder.DropTable(
                name: "ArcaEvolucoes");

            migrationBuilder.DropTable(
                name: "ArcaTabelas");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "CampaignAttachments");

            migrationBuilder.DropTable(
                name: "CampaignMembers");

            migrationBuilder.DropTable(
                name: "CharacterAffections");

            migrationBuilder.DropTable(
                name: "CharacterAffinities");

            migrationBuilder.DropTable(
                name: "CharacterArmorSlots");

            migrationBuilder.DropTable(
                name: "CharacterArtifacts");

            migrationBuilder.DropTable(
                name: "CharacterAttributes");

            migrationBuilder.DropTable(
                name: "CharacterInventoryItems");

            migrationBuilder.DropTable(
                name: "CharacterMasteries");

            migrationBuilder.DropTable(
                name: "CharacterRunes");

            migrationBuilder.DropTable(
                name: "CharacterSheetHistoriaImages");

            migrationBuilder.DropTable(
                name: "CharacterShields");

            migrationBuilder.DropTable(
                name: "CharacterSkills");

            migrationBuilder.DropTable(
                name: "CharacterSpellAbilityEffects");

            migrationBuilder.DropTable(
                name: "CharacterTraits");

            migrationBuilder.DropTable(
                name: "CharacterWeapons");

            migrationBuilder.DropTable(
                name: "CondicaoDesabilitacoes");

            migrationBuilder.DropTable(
                name: "CondicaoModificadores");

            migrationBuilder.DropTable(
                name: "CreatureAffections");

            migrationBuilder.DropTable(
                name: "CreatureArmorSlots");

            migrationBuilder.DropTable(
                name: "CreatureArtifacts");

            migrationBuilder.DropTable(
                name: "CreatureAttributes");

            migrationBuilder.DropTable(
                name: "CreatureMasteries");

            migrationBuilder.DropTable(
                name: "CreatureShields");

            migrationBuilder.DropTable(
                name: "CreatureSkills");

            migrationBuilder.DropTable(
                name: "CreatureSpellAbilityEffects");

            migrationBuilder.DropTable(
                name: "CreatureSpoils");

            migrationBuilder.DropTable(
                name: "CreatureTraits");

            migrationBuilder.DropTable(
                name: "CreatureWeapons");

            migrationBuilder.DropTable(
                name: "DiaryEntryImages");

            migrationBuilder.DropTable(
                name: "DiaryEntryRecipients");

            migrationBuilder.DropTable(
                name: "DurabilidadesPorRank");

            migrationBuilder.DropTable(
                name: "Efeitos");

            migrationBuilder.DropTable(
                name: "EncounterLogEntries");

            migrationBuilder.DropTable(
                name: "EncounterParticipantCondicoes");

            migrationBuilder.DropTable(
                name: "EncounterParticipantConditions");

            migrationBuilder.DropTable(
                name: "EquipmentKitChoiceSlots");

            migrationBuilder.DropTable(
                name: "EquipmentKitItems");

            migrationBuilder.DropTable(
                name: "InviteCodes");

            migrationBuilder.DropTable(
                name: "NpcAffections");

            migrationBuilder.DropTable(
                name: "NpcAffinities");

            migrationBuilder.DropTable(
                name: "NpcArmorSlots");

            migrationBuilder.DropTable(
                name: "NpcArtifacts");

            migrationBuilder.DropTable(
                name: "NpcAttributes");

            migrationBuilder.DropTable(
                name: "NpcInventoryItems");

            migrationBuilder.DropTable(
                name: "NpcMasteries");

            migrationBuilder.DropTable(
                name: "NpcRunes");

            migrationBuilder.DropTable(
                name: "NpcSheetHistoriaImages");

            migrationBuilder.DropTable(
                name: "NpcShields");

            migrationBuilder.DropTable(
                name: "NpcSkills");

            migrationBuilder.DropTable(
                name: "NpcSpellAbilityEffects");

            migrationBuilder.DropTable(
                name: "NpcTraits");

            migrationBuilder.DropTable(
                name: "NpcWeapons");

            migrationBuilder.DropTable(
                name: "RacialAbilityOverrides");

            migrationBuilder.DropTable(
                name: "RacialTraitOverrides");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "RulebookDocumentOverrides");

            migrationBuilder.DropTable(
                name: "SheetLogEntries");

            migrationBuilder.DropTable(
                name: "SpellAbilityBankEffects");

            migrationBuilder.DropTable(
                name: "SubcategoriaOptions");

            migrationBuilder.DropTable(
                name: "TabelaDeAfinidades");

            migrationBuilder.DropTable(
                name: "TabelaDeNiveisConfig");

            migrationBuilder.DropTable(
                name: "ValoresDeNivel");

            migrationBuilder.DropTable(
                name: "ArcaEntries");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "RuneBankEntries");

            migrationBuilder.DropTable(
                name: "CharacterSpellAbilities");

            migrationBuilder.DropTable(
                name: "Condicoes");

            migrationBuilder.DropTable(
                name: "CreatureSpellAbilities");

            migrationBuilder.DropTable(
                name: "CreatureExclusiveTraits");

            migrationBuilder.DropTable(
                name: "DiaryEntries");

            migrationBuilder.DropTable(
                name: "EquipmentKitFixedItems");

            migrationBuilder.DropTable(
                name: "NpcSpellAbilities");

            migrationBuilder.DropTable(
                name: "Traits");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "SpellAbilityBankEntries");

            migrationBuilder.DropTable(
                name: "ColunasDeNivel");

            migrationBuilder.DropTable(
                name: "NiveisProgressao");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Campaigns");

            migrationBuilder.DropTable(
                name: "CreatureSheets");

            migrationBuilder.DropTable(
                name: "Images");

            migrationBuilder.DropTable(
                name: "NpcSheets");

            migrationBuilder.DropTable(
                name: "CharacterSheets");

            migrationBuilder.DropTable(
                name: "EquipmentKits");

            migrationBuilder.DropTable(
                name: "Historicos");

            migrationBuilder.DropTable(
                name: "Pericias");

            migrationBuilder.DropTable(
                name: "Encounters");

            migrationBuilder.DropTable(
                name: "EncounterParticipants");
        }
    }
}

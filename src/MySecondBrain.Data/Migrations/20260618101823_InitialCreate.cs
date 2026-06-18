using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MySecondBrain.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CustomProviderName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CustomEndpointUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    KeyValue = table.Column<string>(type: "TEXT", nullable: false),
                    IsValid = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastTestedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessageDrafts",
                columns: table => new
                {
                    ThreadId = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CursorPosition = table.Column<int>(type: "INTEGER", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageDrafts", x => x.ThreadId);
                });

            migrationBuilder.CreateTable(
                name: "PromptTemplates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: true),
                    FolderId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true),
                    ValueType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "WikiFiles",
                columns: table => new
                {
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    H1Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Headings = table.Column<string>(type: "TEXT", nullable: true),
                    Content = table.Column<string>(type: "TEXT", nullable: true),
                    WordCount = table.Column<int>(type: "INTEGER", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CrossLinksOut = table.Column<string>(type: "TEXT", nullable: true),
                    CrossLinksIn = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiFiles", x => x.FilePath);
                });

            migrationBuilder.CreateTable(
                name: "ModelConfigurations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ApiKeyId = table.Column<string>(type: "TEXT", nullable: true),
                    ModelIdentifier = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Temperature = table.Column<double>(type: "REAL", nullable: false),
                    MaxOutputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxContextWindow = table.Column<int>(type: "INTEGER", nullable: false),
                    ThinkingEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    PricingInputPer1K = table.Column<decimal>(type: "TEXT", nullable: true),
                    PricingOutputPer1K = table.Column<decimal>(type: "TEXT", nullable: true),
                    ContextOverflowStrategy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelConfigurations_ApiKeys_ApiKeyId",
                        column: x => x.ApiKeyId,
                        principalTable: "ApiKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WikiVersionSnapshots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    WikiFilePath = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WikiVersionSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WikiVersionSnapshots_WikiFiles_WikiFilePath",
                        column: x => x.WikiFilePath,
                        principalTable: "WikiFiles",
                        principalColumn: "FilePath",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Personas",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SystemPrompt = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultModelConfigId = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultChatMode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Personas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Personas_ModelConfigurations_DefaultModelConfigId",
                        column: x => x.DefaultModelConfigId,
                        principalTable: "ModelConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TextActions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SystemPrompt = table.Column<string>(type: "TEXT", nullable: false),
                    ModelConfigId = table.Column<string>(type: "TEXT", nullable: true),
                    Hotkey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CaptureScope = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ApplyMode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TextActions_ModelConfigurations_ModelConfigId",
                        column: x => x.ModelConfigId,
                        principalTable: "ModelConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ChatThreads",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsTransient = table.Column<bool>(type: "INTEGER", nullable: false),
                    PersonaId = table.Column<string>(type: "TEXT", nullable: true),
                    ModelConfigId = table.Column<string>(type: "TEXT", nullable: true),
                    SystemMessage = table.Column<string>(type: "TEXT", nullable: true),
                    ChatMode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ThinkingEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsMuted = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPinned = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    ColorLabel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Tags = table.Column<string>(type: "TEXT", nullable: true),
                    FolderId = table.Column<string>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    SourceHWND = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceAppName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SourceDocTitle = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    OriginalHighlightedText = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatThreads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatThreads_ModelConfigurations_ModelConfigId",
                        column: x => x.ModelConfigId,
                        principalTable: "ModelConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChatThreads_Personas_PersonaId",
                        column: x => x.PersonaId,
                        principalTable: "Personas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Artifacts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ThreadId = table.Column<string>(type: "TEXT", nullable: false),
                    VersionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Artifacts_ChatThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "ChatThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ThreadId = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    RawContent = table.Column<string>(type: "TEXT", nullable: true),
                    PersonaId = table.Column<string>(type: "TEXT", nullable: true),
                    ModelConfigId = table.Column<string>(type: "TEXT", nullable: true),
                    TokenCount = table.Column<string>(type: "TEXT", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "TEXT", nullable: true),
                    GenerationTimeMs = table.Column<long>(type: "INTEGER", nullable: true),
                    Feedback = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ParentMessageId = table.Column<string>(type: "TEXT", nullable: true),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    BranchId = table.Column<string>(type: "TEXT", nullable: false),
                    IsActiveBranch = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDirectTransformation = table.Column<bool>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_ChatThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "ChatThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Messages_Messages_ParentMessageId",
                        column: x => x.ParentMessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Messages_ModelConfigurations_ModelConfigId",
                        column: x => x.ModelConfigId,
                        principalTable: "ModelConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Messages_Personas_PersonaId",
                        column: x => x.PersonaId,
                        principalTable: "Personas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MediaItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    MediaType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    MimeType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ThreadId = table.Column<string>(type: "TEXT", nullable: false),
                    MessageId = table.Column<string>(type: "TEXT", nullable: true),
                    GeneratedPrompt = table.Column<string>(type: "TEXT", nullable: true),
                    IsSavedToDisk = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSavedToWiki = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaItems_ChatThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "ChatThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaItems_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UsageRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    MessageId = table.Column<string>(type: "TEXT", nullable: false),
                    ThreadId = table.Column<string>(type: "TEXT", nullable: false),
                    PersonaId = table.Column<string>(type: "TEXT", nullable: true),
                    ModelConfigId = table.Column<string>(type: "TEXT", nullable: true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ModelIdentifier = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PromptTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletionTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsageRecords_ChatThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "ChatThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UsageRecords_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UsageRecords_ModelConfigurations_ModelConfigId",
                        column: x => x.ModelConfigId,
                        principalTable: "ModelConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UsageRecords_Personas_PersonaId",
                        column: x => x.PersonaId,
                        principalTable: "Personas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "Personas",
                columns: new[] { "Id", "CreatedAt", "DefaultChatMode", "DefaultModelConfigId", "DisplayName", "IsBuiltIn", "SystemPrompt", "UpdatedAt" },
                values: new object[,]
                {
                    { "00000000000000000000000000000001", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Standard", null, "General Assistant", true, "You are a helpful, thoughtful assistant.", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "00000000000000000000000000000002", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Standard", null, "Code Helper", true, "You are an expert software developer. Provide clean, well-documented code.", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.InsertData(
                table: "TextActions",
                columns: new[] { "Id", "ApplyMode", "CaptureScope", "CreatedAt", "DisplayName", "Hotkey", "IsBuiltIn", "ModelConfigId", "SystemPrompt", "UpdatedAt" },
                values: new object[,]
                {
                    { "a000000000000000000000000000001", "replaceSelection", "selection", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Rewrite", "Alt+Q", true, null, "Rewrite the following text to improve clarity, flow, and impact while preserving the original meaning.", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "a000000000000000000000000000002", "showOnly", "selection", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Summarize", "Alt+W", true, null, "Summarize the following text concisely, capturing the key points.", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "a000000000000000000000000000003", "showOnly", "selection", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Explain", "Alt+E", true, null, "Explain the following text clearly and thoroughly, as if teaching someone new to the topic.", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "a000000000000000000000000000004", "replaceSelection", "selection", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Translate", "Alt+R", true, null, "Translate the following text to English. Preserve formatting and tone.", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "a000000000000000000000000000005", "replaceSelection", "selection", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Fix Grammar", null, true, null, "Fix grammar, spelling, and punctuation errors in the following text. Preserve the original meaning and style.", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "a000000000000000000000000000006", "replaceSelection", "selection", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Enhance Prompt", null, true, null, "Improve the following prompt to be more specific, detailed, and effective. Add relevant context and constraints.", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "a000000000000000000000000000007", "insertAtCursor", "focusedElement", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Continue Writing", "Alt+C", true, null, "Continue writing from where the text left off. Match the existing tone, style, and formatting. Maintain coherence with the preceding content.", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "a000000000000000000000000000008", "replaceFocusedElement", "focusedElement", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Improve Flow", null, true, null, "Rewrite the following text to improve logical flow, transitions between ideas, and overall readability while preserving the original meaning.", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "a000000000000000000000000000009", "showOnly", "fullDocument", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Summarize Page", null, true, null, "Summarize the following content concisely, capturing the key points and overall structure.", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { "a000000000000000000000000000010", "showOnly", "fullDocument,screenshot", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Explain Screen", null, true, null, "Explain what is shown in the provided content. Describe the layout, key elements, and purpose clearly.", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_ThreadId",
                table: "Artifacts",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_IsDeleted",
                table: "ChatThreads",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_IsTransient",
                table: "ChatThreads",
                column: "IsTransient");

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_LastActivityAt",
                table: "ChatThreads",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_ModelConfigId",
                table: "ChatThreads",
                column: "ModelConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_PersonaId",
                table: "ChatThreads",
                column: "PersonaId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_MessageId",
                table: "MediaItems",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_ThreadId",
                table: "MediaItems",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_CreatedAt",
                table: "Messages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ModelConfigId",
                table: "Messages",
                column: "ModelConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ParentMessageId",
                table: "Messages",
                column: "ParentMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_PersonaId",
                table: "Messages",
                column: "PersonaId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ThreadId",
                table: "Messages",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelConfigurations_ApiKeyId",
                table: "ModelConfigurations",
                column: "ApiKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelConfigurations_DisplayName",
                table: "ModelConfigurations",
                column: "DisplayName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Personas_DefaultModelConfigId",
                table: "Personas",
                column: "DefaultModelConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_Personas_DisplayName",
                table: "Personas",
                column: "DisplayName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TextActions_ModelConfigId",
                table: "TextActions",
                column: "ModelConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_MessageId",
                table: "UsageRecords",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_ModelConfigId",
                table: "UsageRecords",
                column: "ModelConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_PersonaId",
                table: "UsageRecords",
                column: "PersonaId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_ThreadId",
                table: "UsageRecords",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_WikiVersionSnapshots_WikiFilePath",
                table: "WikiVersionSnapshots",
                column: "WikiFilePath");

            // ────────────────────────────────────────────────────────────
            // FTS5 virtual tables for full-text search on Messages and WikiFiles.
            // Content-sync triggers keep the FTS index synchronized with the
            // source tables on INSERT, UPDATE, and DELETE.
            // ────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                CREATE VIRTUAL TABLE IF NOT EXISTS MessageFts USING fts5(
                    Content,
                    content=Messages,
                    content_rowid=rowid
                );

                CREATE TRIGGER IF NOT EXISTS Messages_AI AFTER INSERT ON Messages BEGIN
                    INSERT INTO MessageFts(rowid, Content) VALUES (new.rowid, new.Content);
                END;

                CREATE TRIGGER IF NOT EXISTS Messages_AD AFTER DELETE ON Messages BEGIN
                    INSERT INTO MessageFts(MessageFts, rowid, Content) VALUES('delete', old.rowid, old.Content);
                END;

                CREATE TRIGGER IF NOT EXISTS Messages_AU AFTER UPDATE ON Messages BEGIN
                    INSERT INTO MessageFts(MessageFts, rowid, Content) VALUES('delete', old.rowid, old.Content);
                    INSERT INTO MessageFts(rowid, Content) VALUES (new.rowid, new.Content);
                END;

                CREATE VIRTUAL TABLE IF NOT EXISTS WikiFileFts USING fts5(
                    Content,
                    content=WikiFiles,
                    content_rowid=rowid
                );

                CREATE TRIGGER IF NOT EXISTS WikiFiles_AI AFTER INSERT ON WikiFiles BEGIN
                    INSERT INTO WikiFileFts(rowid, Content) VALUES (new.rowid, new.Content);
                END;

                CREATE TRIGGER IF NOT EXISTS WikiFiles_AD AFTER DELETE ON WikiFiles BEGIN
                    INSERT INTO WikiFileFts(WikiFileFts, rowid, Content) VALUES('delete', old.rowid, old.Content);
                END;

                CREATE TRIGGER IF NOT EXISTS WikiFiles_AU AFTER UPDATE ON WikiFiles BEGIN
                    INSERT INTO WikiFileFts(WikiFileFts, rowid, Content) VALUES('delete', old.rowid, old.Content);
                    INSERT INTO WikiFileFts(rowid, Content) VALUES (new.rowid, new.Content);
                END;
            ", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ────────────────────────────────────────────────────────────
            // Drop FTS5 triggers and virtual tables before dropping source tables.
            // ────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS Messages_AI;
                DROP TRIGGER IF EXISTS Messages_AD;
                DROP TRIGGER IF EXISTS Messages_AU;
                DROP TABLE IF EXISTS MessageFts;
                DROP TRIGGER IF EXISTS WikiFiles_AI;
                DROP TRIGGER IF EXISTS WikiFiles_AD;
                DROP TRIGGER IF EXISTS WikiFiles_AU;
                DROP TABLE IF EXISTS WikiFileFts;
            ", suppressTransaction: true);

            migrationBuilder.DropTable(
                name: "Artifacts");

            migrationBuilder.DropTable(
                name: "MediaItems");

            migrationBuilder.DropTable(
                name: "MessageDrafts");

            migrationBuilder.DropTable(
                name: "PromptTemplates");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "TextActions");

            migrationBuilder.DropTable(
                name: "UsageRecords");

            migrationBuilder.DropTable(
                name: "WikiVersionSnapshots");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "WikiFiles");

            migrationBuilder.DropTable(
                name: "ChatThreads");

            migrationBuilder.DropTable(
                name: "Personas");

            migrationBuilder.DropTable(
                name: "ModelConfigurations");

            migrationBuilder.DropTable(
                name: "ApiKeys");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FieldOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "fieldops");

            migrationBuilder.CreateTable(
                name: "agent_events",
                schema: "fieldops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AgentVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Exception = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ContextJson = table.Column<string>(type: "jsonb", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TableName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RunId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "android_devices",
                schema: "fieldops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OsVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AppVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ApiKeyId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_android_devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox",
                schema: "fieldops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DispatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ErpRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LockedByAgentId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sync_data",
                schema: "fieldops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TableName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourcePk = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    SourceModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SyncBatchId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_data", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sync_state",
                schema: "fieldops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TableName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TableSchema = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LastRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastStatus = table.Column<int>(type: "integer", nullable: false),
                    RowsTotalSynced = table.Column<long>(type: "bigint", nullable: false),
                    RowsInLastRun = table.Column<long>(type: "bigint", nullable: false),
                    CheckpointTs = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckpointRv = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsInitial = table.Column<bool>(type: "boolean", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    DeadLettered = table.Column<bool>(type: "boolean", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_state", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "fieldops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MikroServer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MikroDatabase = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sync_runs",
                schema: "fieldops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    TableName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AgentId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BatchId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    RowsTotal = table.Column<long>(type: "bigint", nullable: false),
                    RowsSynced = table.Column<long>(type: "bigint", nullable: false),
                    RowsFailed = table.Column<long>(type: "bigint", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ErrorCategory = table.Column<int>(type: "integer", nullable: true),
                    CheckpointFrom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CheckpointTo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sync_runs_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "fieldops",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_api_keys",
                schema: "fieldops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AgentId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_api_keys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_api_keys_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "fieldops",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_events_TenantId_Level_OccurredAt",
                schema: "fieldops",
                table: "agent_events",
                columns: new[] { "TenantId", "Level", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_events_TenantId_OccurredAt",
                schema: "fieldops",
                table: "agent_events",
                columns: new[] { "TenantId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_android_devices_TenantId_DeviceId",
                schema: "fieldops",
                table: "android_devices",
                columns: new[] { "TenantId", "DeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_TenantId_IdempotencyKey",
                schema: "fieldops",
                table: "outbox",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_TenantId_Status_CreatedAt",
                schema: "fieldops",
                table: "outbox",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sync_data_TenantId_TableName_SourcePk",
                schema: "fieldops",
                table: "sync_data",
                columns: new[] { "TenantId", "TableName", "SourcePk" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sync_data_TenantId_TableName_SyncedAt",
                schema: "fieldops",
                table: "sync_data",
                columns: new[] { "TenantId", "TableName", "SyncedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sync_runs_BatchId",
                schema: "fieldops",
                table: "sync_runs",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_sync_runs_TenantId_StartedAt",
                schema: "fieldops",
                table: "sync_runs",
                columns: new[] { "TenantId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sync_runs_TenantId_TableName_StartedAt",
                schema: "fieldops",
                table: "sync_runs",
                columns: new[] { "TenantId", "TableName", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sync_state_TenantId_DeadLettered",
                schema: "fieldops",
                table: "sync_state",
                columns: new[] { "TenantId", "DeadLettered" });

            migrationBuilder.CreateIndex(
                name: "IX_sync_state_TenantId_TableName",
                schema: "fieldops",
                table: "sync_state",
                columns: new[] { "TenantId", "TableName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_api_keys_KeyHash",
                schema: "fieldops",
                table: "tenant_api_keys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_api_keys_TenantId_IsActive",
                schema: "fieldops",
                table: "tenant_api_keys",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Code",
                schema: "fieldops",
                table: "tenants",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_events",
                schema: "fieldops");

            migrationBuilder.DropTable(
                name: "android_devices",
                schema: "fieldops");

            migrationBuilder.DropTable(
                name: "outbox",
                schema: "fieldops");

            migrationBuilder.DropTable(
                name: "sync_data",
                schema: "fieldops");

            migrationBuilder.DropTable(
                name: "sync_runs",
                schema: "fieldops");

            migrationBuilder.DropTable(
                name: "sync_state",
                schema: "fieldops");

            migrationBuilder.DropTable(
                name: "tenant_api_keys",
                schema: "fieldops");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "fieldops");
        }
    }
}

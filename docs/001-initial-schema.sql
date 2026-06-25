DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'fieldops') THEN
        CREATE SCHEMA fieldops;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS fieldops.__ef_migrations (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___ef_migrations" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'fieldops') THEN
        CREATE SCHEMA fieldops;
    END IF;
END $EF$;

CREATE TABLE fieldops.agent_events (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "AgentId" character varying(200),
    "AgentVersion" character varying(50),
    "Level" integer NOT NULL,
    "Message" character varying(4000) NOT NULL,
    "Exception" text,
    "Category" character varying(50),
    "ContextJson" jsonb,
    "OccurredAt" timestamp with time zone NOT NULL,
    "ReceivedAt" timestamp with time zone NOT NULL,
    "TableName" character varying(200),
    "RunId" uuid,
    CONSTRAINT "PK_agent_events" PRIMARY KEY ("Id")
);

CREATE TABLE fieldops.android_devices (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "DeviceId" character varying(200) NOT NULL,
    "Name" character varying(200),
    "Model" character varying(200),
    "OsVersion" character varying(50),
    "AppVersion" character varying(50),
    "RegisteredAt" timestamp with time zone NOT NULL,
    "LastSeenAt" timestamp with time zone,
    "IsActive" boolean NOT NULL,
    "ApiKeyId" character varying(100),
    CONSTRAINT "PK_android_devices" PRIMARY KEY ("Id")
);

CREATE TABLE fieldops.outbox (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "IdempotencyKey" character varying(100) NOT NULL,
    "DocumentType" character varying(50) NOT NULL,
    "PayloadJson" jsonb NOT NULL,
    "DeviceId" character varying(200),
    "Status" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "DispatchedAt" timestamp with time zone,
    "AckedAt" timestamp with time zone,
    "RetryCount" integer NOT NULL,
    "LastError" character varying(2000),
    "ErpRef" character varying(200),
    "LockedByAgentId" character varying(200),
    "LockedAt" timestamp with time zone,
    "LockExpiresAt" timestamp with time zone,
    CONSTRAINT "PK_outbox" PRIMARY KEY ("Id")
);

CREATE TABLE fieldops.sync_data (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "TableName" character varying(200) NOT NULL,
    "SourcePk" character varying(200) NOT NULL,
    "PayloadJson" jsonb NOT NULL,
    "SourceModifiedAt" timestamp with time zone,
    "SyncedAt" timestamp with time zone NOT NULL,
    "SyncBatchId" character varying(100),
    CONSTRAINT "PK_sync_data" PRIMARY KEY ("Id")
);

CREATE TABLE fieldops.sync_state (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "TableName" character varying(200) NOT NULL,
    "TableSchema" character varying(50),
    "LastRunAt" timestamp with time zone,
    "LastStatus" integer NOT NULL,
    "RowsTotalSynced" bigint NOT NULL,
    "RowsInLastRun" bigint NOT NULL,
    "CheckpointTs" timestamp with time zone,
    "CheckpointRv" character varying(100),
    "IsInitial" boolean NOT NULL,
    "RetryCount" integer NOT NULL,
    "DeadLettered" boolean NOT NULL,
    "LastError" character varying(2000),
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_sync_state" PRIMARY KEY ("Id")
);

CREATE TABLE fieldops.tenants (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Code" character varying(50) NOT NULL,
    "ContactEmail" character varying(200),
    "ContactPhone" character varying(50),
    "MikroServer" character varying(200),
    "MikroDatabase" character varying(200),
    "IsActive" boolean NOT NULL DEFAULT TRUE,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "Notes" character varying(2000),
    CONSTRAINT "PK_tenants" PRIMARY KEY ("Id")
);

CREATE TABLE fieldops.sync_runs (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Direction" integer NOT NULL,
    "TableName" character varying(200),
    "AgentId" character varying(200),
    "BatchId" character varying(100),
    "Status" integer NOT NULL,
    "StartedAt" timestamp with time zone NOT NULL,
    "FinishedAt" timestamp with time zone,
    "DurationMs" bigint,
    "RowsTotal" bigint NOT NULL,
    "RowsSynced" bigint NOT NULL,
    "RowsFailed" bigint NOT NULL,
    "ErrorMessage" character varying(2000),
    "ErrorCategory" integer,
    "CheckpointFrom" character varying(200),
    "CheckpointTo" character varying(200),
    CONSTRAINT "PK_sync_runs" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_sync_runs_tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES fieldops.tenants ("Id") ON DELETE CASCADE
);

CREATE TABLE fieldops.tenant_api_keys (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "KeyHash" character varying(64) NOT NULL,
    "KeyPrefix" character varying(20) NOT NULL,
    "Label" character varying(200),
    "AgentId" character varying(200),
    "Scope" integer NOT NULL,
    "IsActive" boolean NOT NULL DEFAULT TRUE,
    "CreatedAt" timestamp with time zone NOT NULL,
    "LastUsedAt" timestamp with time zone,
    "ExpiresAt" timestamp with time zone,
    "RevokedAt" timestamp with time zone,
    "CreatedBy" character varying(200),
    CONSTRAINT "PK_tenant_api_keys" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_tenant_api_keys_tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES fieldops.tenants ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_agent_events_TenantId_Level_OccurredAt" ON fieldops.agent_events ("TenantId", "Level", "OccurredAt");

CREATE INDEX "IX_agent_events_TenantId_OccurredAt" ON fieldops.agent_events ("TenantId", "OccurredAt");

CREATE UNIQUE INDEX "IX_android_devices_TenantId_DeviceId" ON fieldops.android_devices ("TenantId", "DeviceId");

CREATE UNIQUE INDEX "IX_outbox_TenantId_IdempotencyKey" ON fieldops.outbox ("TenantId", "IdempotencyKey");

CREATE INDEX "IX_outbox_TenantId_Status_CreatedAt" ON fieldops.outbox ("TenantId", "Status", "CreatedAt");

CREATE UNIQUE INDEX "IX_sync_data_TenantId_TableName_SourcePk" ON fieldops.sync_data ("TenantId", "TableName", "SourcePk");

CREATE INDEX "IX_sync_data_TenantId_TableName_SyncedAt" ON fieldops.sync_data ("TenantId", "TableName", "SyncedAt");

CREATE INDEX "IX_sync_runs_BatchId" ON fieldops.sync_runs ("BatchId");

CREATE INDEX "IX_sync_runs_TenantId_StartedAt" ON fieldops.sync_runs ("TenantId", "StartedAt");

CREATE INDEX "IX_sync_runs_TenantId_TableName_StartedAt" ON fieldops.sync_runs ("TenantId", "TableName", "StartedAt");

CREATE INDEX "IX_sync_state_TenantId_DeadLettered" ON fieldops.sync_state ("TenantId", "DeadLettered");

CREATE UNIQUE INDEX "IX_sync_state_TenantId_TableName" ON fieldops.sync_state ("TenantId", "TableName");

CREATE UNIQUE INDEX "IX_tenant_api_keys_KeyHash" ON fieldops.tenant_api_keys ("KeyHash");

CREATE INDEX "IX_tenant_api_keys_TenantId_IsActive" ON fieldops.tenant_api_keys ("TenantId", "IsActive");

CREATE UNIQUE INDEX "IX_tenants_Code" ON fieldops.tenants ("Code");

INSERT INTO fieldops.__ef_migrations ("MigrationId", "ProductVersion")
VALUES ('20260625102622_InitialCreate', '8.0.10');

COMMIT;


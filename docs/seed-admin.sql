-- Seed admin tenant + admin API key
-- Admin Key: fo_live_rU4yGOT-lSWArqa87MMOB_u8UxsApjEFtVGYiAIjfyk
-- Bu anahtar development ortamı içindir, prod'da yenisi üretilmelidir.

INSERT INTO fieldops.tenants ("Id", "Name", "Code", "IsActive", "CreatedAt", "UpdatedAt")
VALUES ('00000000-0000-0000-0000-000000000001'::uuid, 'System Admin', 'SYSTEM', TRUE, NOW(), NOW())
ON CONFLICT ("Code") DO NOTHING;

INSERT INTO fieldops.tenant_api_keys ("Id", "TenantId", "KeyHash", "KeyPrefix", "Scope", "IsActive", "CreatedAt")
VALUES (
    gen_random_uuid(),
    '00000000-0000-0000-0000-000000000001'::uuid,
    '7efc885e6fd09d0083d878858f651fb76e881336b763fe1ce96ad74c1ddde42e',
    'fo_live_rU4y',
    2,
    TRUE,
    NOW()
);

SELECT t."Name" AS tenant, k."KeyPrefix" AS key_prefix, k."Scope" AS scope, k."IsActive" AS active
FROM fieldops.tenants t
JOIN fieldops.tenant_api_keys k ON k."TenantId" = t."Id";

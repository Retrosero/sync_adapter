namespace FieldOps.Domain.Enums;

public enum SyncDirection
{
    ErpToServer = 1,    // ERP → Sunucu (Windows ajanı tarafı)
    ServerToErp = 2,    // Sunucu → ERP (Windows ajanı outbox dispatch)
    ServerToAndroid = 3 // Sunucu → Android (veri çekme)
}

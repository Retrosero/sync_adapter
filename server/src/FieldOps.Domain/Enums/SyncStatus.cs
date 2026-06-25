namespace FieldOps.Domain.Enums;

public enum SyncStatus
{
    Pending = 0,
    InProgress = 1,
    Ok = 2,
    PartialOk = 3,   // Bazı batch'ler başarılı, bazıları değil
    Failed = 4,
    DeadLettered = 5 // Defalarca denendi, artık manuel müdahale gerek
}

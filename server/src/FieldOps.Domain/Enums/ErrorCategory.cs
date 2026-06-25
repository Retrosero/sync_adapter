namespace FieldOps.Domain.Enums;

public enum ErrorCategory
{
    Unknown = 0,
    Transient = 1,   // Network, timeout, deadlock — otomatik retry
    Schema = 2,      // Kolon uyumsuz, tablo yok — alarm, manuel inceleme
    Auth = 3,        // Windows Auth fail, login expire — alarm
    Data = 4,        // Constraint, invalid char — batch atla, devam et
    Config = 5       // appsettings yanlış, env variable eksik — alarm
}

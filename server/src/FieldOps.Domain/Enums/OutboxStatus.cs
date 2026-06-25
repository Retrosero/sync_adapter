namespace FieldOps.Domain.Enums;

public enum OutboxStatus
{
    Pending = 0,        // Android gönderdi, Windows ajanı henüz almadı
    Dispatched = 1,     // Ajan aldı, ERP'ye yazıyor
    Acked = 2,          // ERP'ye başarıyla yazıldı, ACK döndü
    Failed = 3,         // Yazma başarısız (henüz max retry'a ulaşmadı)
    DeadLettered = 4    // Çok denendi, artık manuel inceleme lazım
}

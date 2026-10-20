

namespace Nordiska.Modules.Banking.Domain
{

    public enum MessageThreadStatus
    {
        Open,
        Closed,
    }
    public sealed class MessageThread
    {
        public long Id { get; set; }
        public long CustomerId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public MessageThreadStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastMessageAt { get; set; }
        public DateTime? ClosedAt { get; set; }

        // Navigation properties
        public Customer Customer { get; set; } = null!;
        public ICollection<Message> Messages { get; set; } = [];
        public MessageThreadState State { get; set; }  = null!;
    }
}
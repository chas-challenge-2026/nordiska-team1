

namespace Nordiska.Modules.Banking.Domain;

    public enum MessageThreadFolder
    {
        Inbox,
        sent,
        Archived
    }
    public sealed class MessageThreadState
    {
        public long Id { get; set; }
        public long ThreadId { get; set; }
        public long CustomerId { get; set; }
        public MessageThreadFolder Folder { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime? ArchivedAt { get; set; }
    

        // Navigation properties
        public MessageThread Thread { get; set; } = null!;
        public Customer Customer { get; set; } = null!;
    }

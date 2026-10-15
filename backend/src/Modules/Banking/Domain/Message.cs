
namespace Nordiska.Modules.Banking.Domain
{
   public enum MessageSenderType
    {
        Customer,
        Bank,
        System
    }


    public sealed class Message
    {
        public long Id { get; set; }
        public long ThreadId { get; set; }
        public string Body { get; set; } = string.Empty;
        public MessageSenderType SenderType { get; set; }
        public long? SenderCustomerId { get; set; }
        public string Sender { get; set; } = string.Empty;
        public bool ReplyAllowed { get; set; }
        public DateTime? ReceivedAt { get; set; } 
        public DateTime SentAt { get; set; }

        // Navigation properties 
        public MessageThread Thread { get; set; } = null!;
        public Customer? SenderCustomer { get; set; }
    }
}
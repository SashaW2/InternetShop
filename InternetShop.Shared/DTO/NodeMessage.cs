namespace InternetShop.Shared.DTO
{
    public enum NodeMessageType
    {
        Heartbeat,
        HeartbeatAck,
        Proposal,
        ProposalVote,
    }

    public class NodeMessage
    {
        public NodeMessageType Type { get; set; }
        public int FromNodeId { get; set; }
        public int ToNodeId { get; set; }      
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string Payload { get; set; } 
    }
}
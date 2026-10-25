using InternetShop.Shared.Enums;

namespace InternetShop.Shared.Models
{
    public class NodeInfo
    {
        public int Id { get; set; }    
        public NodeStatus Status { get; set; } = NodeStatus.Available;
        public NodeRole Role { get; set; } = NodeRole.Follower;
        public DateTime LastHeartbeatUtc { get; set; } = DateTime.UtcNow;

        public List<NodeInfo> Peers { get; set; } = new List<NodeInfo>();
    }
}
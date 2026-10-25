using InternetShop.Shared.DTO;
using InternetShop.Shared.Enums;
using InternetShop.Shared.Models;

namespace InternetShop.Server.Consensus
{
    public class HeartbeatDetector
    {
        private readonly NodeRegistry _registry;
        private readonly Dictionary<int, Node> _nodes = new Dictionary<int, Node>();

        public HeartbeatDetector(NodeRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));

            foreach (var info in _registry.GetAll())
                _nodes[info.Id] = new Node(info.Id, _registry);
        }

        public IReadOnlyList<NodeInfo> CheckAll()
        {
            var failed = new List<NodeInfo>();
            var all = _registry.GetAll();

            var sender = FindAnyAvailable(all);
            if (sender == null)
            {
                NodeLogger.Log("Heartbeat check skipped: no AVAILABLE nodes");
                return failed;
            }

            foreach (var target in all)
            {
                if (target.Id == sender.Id) continue; 

                bool ok = SendHeartbeat(sender.Id, target.Id);

                if (!ok)
                {
                    _registry.SetStatus(target.Id, NodeStatus.Failed);
                    NodeLogger.Log($"Node {target.Id} → FAILED");
                    failed.Add(_registry.GetById(target.Id));
                }
            }

            return failed;
        }

        public bool SendHeartbeat(int senderId, int targetId)
        {
            if (!_nodes.TryGetValue(targetId, out var target))
                return false;

            NodeLogger.Log($"Node {senderId} → HEARTBEAT → Node {targetId}");

            var request = new NodeMessage
            {
                Type = NodeMessageType.Heartbeat,
                FromNodeId = senderId,
                ToNodeId = targetId,
                TimestampUtc = DateTime.UtcNow
            };

            var response = target.Receive(request);

            if (response == null || response.Type != NodeMessageType.HeartbeatAck)
            {
                NodeLogger.Log($"Node {targetId} → NO RESPONSE");
                NodeLogger.Log($"Node {targetId} → HEARTBEAT TIMEOUT");
                return false;
            }

            NodeLogger.Log($"Node {targetId} → ALIVE");
            return true;
        }

        private static NodeInfo FindAnyAvailable(IReadOnlyList<NodeInfo> nodes)
        {
            foreach (var n in nodes)
                if (n.Status == NodeStatus.Available) return n;
            return null;
        }
    }
}
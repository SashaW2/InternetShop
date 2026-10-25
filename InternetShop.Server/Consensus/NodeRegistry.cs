using InternetShop.Shared.Enums;
using InternetShop.Shared.Models;

namespace InternetShop.Server.Consensus
{
    public class NodeRegistry
    {
        private readonly object _lock = new object();
        private readonly List<NodeInfo> _nodes = new List<NodeInfo>();

        public NodeRegistry(int count = 5)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));

            for (int i = 1; i <= count; i++)
            {
                _nodes.Add(new NodeInfo
                {
                    Id = i,
                    Status = NodeStatus.Available,
                    Role = NodeRole.Follower
                });
            }

            lock (_lock)
            {
                foreach (var n in _nodes)
                {
                    n.Peers = _nodes
                        .Where(x => x.Id != n.Id)
                        .Select(x => new NodeInfo
                        {
                            Id = x.Id,
                            Status = x.Status,
                            Role = x.Role,
                            LastHeartbeatUtc = x.LastHeartbeatUtc
                        })
                        .ToList();
                }
            }
        }

        public IReadOnlyList<NodeInfo> GetAll()
        {
            lock (_lock) return _nodes.Select(Clone).ToList();
        }

        public NodeInfo GetById(int id)
        {
            lock (_lock)
            {
                var n = _nodes.FirstOrDefault(x => x.Id == id);
                return n == null ? null : Clone(n);
            }
        }

        public void SetStatus(int id, NodeStatus status)
        {
            lock (_lock)
            {
                var n = _nodes.FirstOrDefault(x => x.Id == id);
                if (n == null) return;
                if (n.Status == status) return; 
                n.Status = status;
                Log(n, "STATUS");
            }
        }

        public void SetRole(int id, NodeRole role)
        {
            lock (_lock)
            {
                var n = _nodes.FirstOrDefault(x => x.Id == id);
                if (n == null) return;
                if (n.Role == role) return;
                n.Role = role;
                Log(n, "ROLE");
            }
        }

        private static NodeInfo Clone(NodeInfo n) => new NodeInfo
        {
            Id = n.Id,
            Status = n.Status,
            Role = n.Role,
            LastHeartbeatUtc = n.LastHeartbeatUtc,
            Peers = n.Peers?.Select(Clone).ToList() ?? new List<NodeInfo>()
        };

        public int CountLeaders()
        {
            lock (_lock) return _nodes.Count(x => x.Role == NodeRole.Leader);
        }

        public NodeInfo GetLeader()
        {
            lock (_lock)
            {
                var l = _nodes.FirstOrDefault(x => x.Role == NodeRole.Leader);
                return l == null ? null : Clone(l);
            }
        }

        private static void Log(NodeInfo n, string what)
            => NodeLogger.Log($"Node {n.Id} → {what}: {n.Status}/{n.Role}");
    }
}
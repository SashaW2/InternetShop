using InternetShop.Shared.Enums;
using InternetShop.Shared.Models;

namespace InternetShop.Server.Consensus
{
    public class LeaderElection
    {
        private readonly NodeRegistry _registry;

        public LeaderElection(NodeRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public NodeInfo Elect()
        {
            NodeLogger.Log("Election started");

            var nodes = _registry.GetAll();

            foreach (var n in nodes.OrderBy(x => x.Id))
                NodeLogger.Log($"Node {n.Id} {n.Status.ToString().ToUpperInvariant()}");

            foreach (var n in nodes)
                _registry.SetRole(n.Id, NodeRole.Follower);

            var winner = nodes
                .Where(x => x.Status == NodeStatus.Available)
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();

            if (winner == null)
            {
                NodeLogger.Log("Election finished: no AVAILABLE nodes → Leader = none");
                return null;
            }

            _registry.SetRole(winner.Id, NodeRole.Leader);
            NodeLogger.Log($"Node {winner.Id} elected as LEADER");

            return _registry.GetById(winner.Id);
        }

        public NodeInfo ElectAfterFailure(int failedLeaderId)
        {
            NodeLogger.Log("New election started");

            var nodes = _registry.GetAll();

            var failed = _registry.GetById(failedLeaderId);
            if (failed != null)
            {
                _registry.SetRole(failedLeaderId, NodeRole.Follower);
                NodeLogger.Log($"Node {failedLeaderId} excluded");
            }

            foreach (var n in nodes)
            {
                if (n.Id == failedLeaderId) continue;
                _registry.SetRole(n.Id, NodeRole.Follower);
            }

            var winner = nodes
                .Where(x => x.Status == NodeStatus.Available)
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();

            if (winner == null)
            {
                NodeLogger.Log("Election finished: no AVAILABLE nodes → Leader = none");
                return null;
            }

            _registry.SetRole(winner.Id, NodeRole.Leader);
            NodeLogger.Log($"Node {winner.Id} elected as LEADER");

            return _registry.GetById(winner.Id);
        }

        public bool HasExactlyOneLeader()
        {
            return _registry.CountLeaders() == 1;
        }
    }
}
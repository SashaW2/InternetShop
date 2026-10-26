using InternetShop.Shared.DTO;
using InternetShop.Shared.Enums;
using Newtonsoft.Json;

namespace InternetShop.Server.Consensus
{
    public class ConsensusService
    {
        private readonly NodeRegistry _registry;
        private readonly Dictionary<int, Node> _nodes = new Dictionary<int, Node>();
        private int _nextProposalId = 1;

        public ConsensusService(NodeRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            foreach (var info in _registry.GetAll())
                _nodes[info.Id] = new Node(info.Id, _registry);
        }

        public bool ProposeAndVote(int leaderId, string key, string value)
        {
            var leader = _registry.GetById(leaderId);
            if (leader == null)
            {
                NodeLogger.Log($"Propose failed: Node {leaderId} not found");
                return false;
            }

            if (leader.Role != NodeRole.Leader || leader.Status != NodeStatus.Available)
            {
                NodeLogger.Log($"Propose failed: Node {leaderId} is not an AVAILABLE Leader");
                return false;
            }

            var proposal = new Proposal
            {
                ProposalId = _nextProposalId++,
                LeaderId = leaderId,
                Key = key,
                Value = value,
                CreatedAtUtc = DateTime.UtcNow
            };

            NodeLogger.Log($"Leader Node {leaderId} → PROPOSE {key}={value}");

            var nodes = _registry.GetAll();
            var yes = 0;

            foreach (var target in nodes.Where(n => n.Id != leaderId).OrderBy(n => n.Id))
            {
                if (target.Status != NodeStatus.Available)
                {
                    NodeLogger.Log($"Node {target.Id} → NO RESPONSE");
                    continue;
                }

                bool ok = DeliverProposal(leaderId, target.Id, proposal, out var vote);

                if (!ok || vote == null)
                {
                    NodeLogger.Log($"Node {target.Id} → NO RESPONSE");
                    continue;
                }

                if (vote.IsYes) { yes++; NodeLogger.Log($"Node {target.Id} → YES"); }
                else NodeLogger.Log($"Node {target.Id} → NO");
            }

            yes++;
            NodeLogger.Log($"Node {leaderId} → YES");

            int total = nodes.Count;
            int majority = total / 2 + 1;

            NodeLogger.Log($"YES = {yes}");
            NodeLogger.Log($"Majority = {majority}");

            bool achieved = yes >= majority;
            NodeLogger.Log(achieved ? "CONSENSUS ACHIEVED" : "CONSENSUS NOT ACHIEVED");
            return achieved;
        }

        private bool DeliverProposal(int leaderId, int targetId, Proposal proposal, out Vote vote)
        {
            vote = null;

            if (!_nodes.TryGetValue(targetId, out var target))
                return false;

            var msg = new NodeMessage
            {
                Type = NodeMessageType.Proposal,
                FromNodeId = leaderId,
                ToNodeId = targetId,
                TimestampUtc = DateTime.UtcNow,
                Payload = JsonConvert.SerializeObject(proposal)
            };

            var response = target.Receive(msg);
            if (response == null || response.Type != NodeMessageType.ProposalVote)
                return false;

            vote = JsonConvert.DeserializeObject<Vote>(response.Payload ?? "{}");
            return vote != null;
        }
    }
}
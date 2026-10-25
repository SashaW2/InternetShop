using InternetShop.Shared.DTO;
using InternetShop.Shared.Enums;
using Newtonsoft.Json;

namespace InternetShop.Server.Consensus
{
    public class Node
    {
        private readonly NodeRegistry _registry;

        public int Id { get; }
        public NodeStatus Status => _registry.GetById(Id)?.Status ?? NodeStatus.Failed;
        public NodeRole Role => _registry.GetById(Id)?.Role ?? NodeRole.Follower;

        public Node(int id, NodeRegistry registry)
        {
            Id = id;
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public NodeMessage Receive(NodeMessage msg)
        {
            if (msg == null) return null;

            if (Status == NodeStatus.Failed)
                return null; 

            switch (msg.Type)
            {
                case NodeMessageType.Heartbeat:
                    return new NodeMessage
                    {
                        Type = NodeMessageType.HeartbeatAck,
                        FromNodeId = Id,
                        ToNodeId = msg.FromNodeId,
                        TimestampUtc = DateTime.UtcNow
                    };

                case NodeMessageType.Proposal:
                    {
                        var proposal = JsonConvert.DeserializeObject<Proposal>(msg.Payload ?? "{}");
                        if (proposal == null) return null;

                        bool isYes = !string.IsNullOrWhiteSpace(proposal.Key)
                                  && !string.IsNullOrWhiteSpace(proposal.Value);

                        var vote = new Vote
                        {
                            ProposalId = proposal.ProposalId,
                            VoterId = Id,
                            IsYes = isYes
                        };

                        return new NodeMessage
                        {
                            Type = NodeMessageType.ProposalVote,
                            FromNodeId = Id,
                            ToNodeId = msg.FromNodeId,
                            TimestampUtc = DateTime.UtcNow,
                            Payload = JsonConvert.SerializeObject(vote)
                        };
                    }

                default:
                    return null;
            }
        }
    }
}
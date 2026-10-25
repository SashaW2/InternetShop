using InternetShop.Server.Consensus;
using InternetShop.Shared.Enums;

namespace InternetShop.Tests
{
    public class ConsensusTests
    {
        // ============================================================
        // 1. Создание узлов
        // ============================================================
        [Fact]
        public void Registry_CreatesFiveNodes()
        {
            var registry = new NodeRegistry(5);
            var nodes = registry.GetAll();

            Assert.Equal(5, nodes.Count);
            Assert.All(nodes, n => Assert.Equal(NodeStatus.Available, n.Status));
            Assert.All(nodes, n => Assert.Equal(NodeRole.Follower, n.Role));
        }

        // ============================================================
        // 2. Уникальность ID
        // ============================================================
        [Fact]
        public void Registry_HasUniqueIds()
        {
            var registry = new NodeRegistry(5);
            var ids = registry.GetAll().Select(n => n.Id).ToList();

            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, ids.OrderBy(x => x));
        }

        // ============================================================
        // 3. Определение доступных узлов
        // ============================================================
        [Fact]
        public void Registry_ReportsOnlyAvailableNodes()
        {
            var registry = new NodeRegistry(5);
            registry.SetStatus(5, NodeStatus.Failed);
            registry.SetStatus(4, NodeStatus.Failed);

            var available = registry.GetAll().Where(n => n.Status == NodeStatus.Available).ToList();

            Assert.Equal(3, available.Count);
            Assert.DoesNotContain(available, n => n.Id == 4 || n.Id == 5);
        }

        // ============================================================
        // 4. Выбор лидера (максимальный доступный ID)
        // ============================================================
        [Fact]
        public void Elect_ReturnsHighestAvailableId()
        {
            var registry = new NodeRegistry(5);
            var leader = new LeaderElection(registry).Elect();

            Assert.NotNull(leader);
            Assert.Equal(5, leader.Id);
            Assert.Equal(NodeRole.Leader, registry.GetById(5).Role);
        }

        // ============================================================
        // 5. Невозможность выбрать недоступный узел
        // ============================================================
        [Fact]
        public void Elect_NeverPicksFailedNode()
        {
            var registry = new NodeRegistry(5);
            registry.SetStatus(5, NodeStatus.Failed);
            registry.SetStatus(4, NodeStatus.Failed);
            registry.SetStatus(3, NodeStatus.Failed);

            var leader = new LeaderElection(registry).Elect();

            Assert.NotNull(leader);
            Assert.Equal(NodeStatus.Available, leader.Status);
            Assert.Equal(2, leader.Id);
        }

        [Fact]
        public void Elect_ReturnsNull_WhenAllFailed()
        {
            var registry = new NodeRegistry(5);
            for (int i = 1; i <= 5; i++) registry.SetStatus(i, NodeStatus.Failed);

            var leader = new LeaderElection(registry).Elect();

            Assert.Null(leader);
        }

        // ============================================================
        // 6. Обнаружение отказа лидера
        // ============================================================
        [Fact]
        public void Heartbeat_DetectsFailedLeader()
        {
            var registry = new NodeRegistry(5);
            var detector = new HeartbeatDetector(registry);

            var before = detector.CheckAll();
            Assert.Empty(before);

            registry.SetStatus(5, NodeStatus.Failed);
            var after = detector.CheckAll();

            Assert.Contains(after, n => n.Id == 5);
            Assert.Equal(NodeStatus.Failed, registry.GetById(5).Status);
        }

        // ============================================================
        // 7. Повторный выбор лидера
        // ============================================================
        [Fact]
        public void ElectAfterFailure_PicksNextAvailable()
        {
            var registry = new NodeRegistry(5);
            var election = new LeaderElection(registry);

            var first = election.Elect();
            Assert.Equal(5, first.Id);

            registry.SetStatus(5, NodeStatus.Failed);

            var second = election.ElectAfterFailure(5);

            Assert.NotNull(second);
            Assert.Equal(4, second.Id);
            Assert.Equal(NodeRole.Leader, registry.GetById(4).Role);
            Assert.Equal(NodeRole.Follower, registry.GetById(5).Role); 
        }

        // ============================================================
        // 8. Определение большинства (N/2 + 1)
        // ============================================================
        [Theory]
        [InlineData(5, 3)]
        [InlineData(4, 3)]
        [InlineData(3, 2)]
        [InlineData(1, 1)]
        public void Majority_CalculatedCorrectly(int total, int expected)
        {
            Assert.Equal(expected, total / 2 + 1);
        }

        // ============================================================
        // 9. Положительное голосование → консенсус достигнут
        // ============================================================
        [Fact]
        public void Propose_AllAvailable_ConsensusAchieved()
        {
            var registry = new NodeRegistry(5);
            new LeaderElection(registry).Elect();          
            var consensus = new ConsensusService(registry);

            bool achieved = consensus.ProposeAndVote(5, "OrderStatus", "Paid");

            Assert.True(achieved);
        }

        // ============================================================
        // 10. Отрицательное голосование / отсутствие большинства
        //     → консенсус НЕ достигнут
        // ============================================================
        [Fact]
        public void Propose_OnlyTwoAvailable_ConsensusNotAchieved()
        {
            var registry = new NodeRegistry(5);
            registry.SetStatus(3, NodeStatus.Failed);
            registry.SetStatus(4, NodeStatus.Failed);
            registry.SetStatus(5, NodeStatus.Failed);

            new LeaderElection(registry).Elect();         
            var consensus = new ConsensusService(registry);

            bool achieved = consensus.ProposeAndVote(2, "OrderStatus", "Paid");

            Assert.False(achieved);
        }

        // ============================================================
        // 11. Обработка отказа узла (heartbeat не падает)
        // ============================================================
        [Fact]
        public void Heartbeat_HandlesMultipleFailures_WithoutCrash()
        {
            var registry = new NodeRegistry(5);
            registry.SetStatus(3, NodeStatus.Failed);
            registry.SetStatus(4, NodeStatus.Failed);
            registry.SetStatus(5, NodeStatus.Failed);

            var detector = new HeartbeatDetector(registry);
            var failed = detector.CheckAll();

            Assert.Equal(3, failed.Count);
            Assert.All(failed, n => Assert.Equal(NodeStatus.Failed, n.Status));
        }

        // ============================================================
        // 12. Корректная работа после смены лидера
        // ============================================================
        [Fact]
        public void AfterLeaderChange_NewLeaderCanReachConsensus()
        {
            var registry = new NodeRegistry(5);
            var election = new LeaderElection(registry);

            election.Elect();                           
            registry.SetStatus(5, NodeStatus.Failed);      

            var newLeader = election.ElectAfterFailure(5);  
            Assert.Equal(4, newLeader.Id);

            var consensus = new ConsensusService(registry);
            bool achieved = consensus.ProposeAndVote(4, "OrderStatus", "Paid");

            Assert.True(achieved);                          
            Assert.Equal(1, registry.CountLeaders());     
        }

        // ============================================================
        // 13. Инвариант «одновременно только один Leader»
        // ============================================================
        [Fact]
        public void Invariant_ExactlyOneLeader_AfterElectionAndFailover()
        {
            var registry = new NodeRegistry(5);
            var election = new LeaderElection(registry);

            election.Elect();
            Assert.Equal(1, registry.CountLeaders());

            registry.SetStatus(5, NodeStatus.Failed);
            election.ElectAfterFailure(5);

            Assert.Equal(1, registry.CountLeaders());
            Assert.True(election.HasExactlyOneLeader());
        }
    }
}
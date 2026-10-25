using InternetShop.Server.Consensus;
using InternetShop.Server.Database;
using InternetShop.Shared.Enums;
using System;

namespace InternetShop.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "СЕРВЕР Интернет-магазина";
            Console.WriteLine("========================================");
            Console.WriteLine("   ЗАПУСК СЕРВЕРА ИНТЕРНЕТ-МАГАЗИНА");
            Console.WriteLine("========================================");

            if (!Database.Database.TestPrimaryConnection(out string primaryError))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Не удалось подключиться к Primary: {primaryError}");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Подключение к Primary установлено.");
            Console.ResetColor();

            if (!Database.Database.TestReplicaConnection(out string replicaError))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Не удалось подключиться к Replica: {replicaError}");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Подключение к Replica установлено.");
            Console.ResetColor();

            var rm = new ReplicationManager();
            if (rm.CheckConsistency(out string report))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Проверка начального состояния: {report}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Внимание! {report}");
            }
            Console.ResetColor();

            var registry = new NodeRegistry(5);
            var election = new LeaderElection(registry);
            var detector = new HeartbeatDetector(registry);
            var consensus = new ConsensusService(registry);

            //registry.SetStatus(3, NodeStatus.Failed);
            //registry.SetStatus(4, NodeStatus.Failed);
            //registry.SetStatus(5, NodeStatus.Failed);

            var leader = election.Elect();
            NodeLogger.Log($"Current Leader = " + (leader == null ? "none" : $"Node {leader.Id}"));

            NodeLogger.Log("Heartbeat check");
            var failed = detector.CheckAll();

            if (leader != null && failed.Any(f => f.Id == leader.Id))
            {
                leader = election.ElectAfterFailure(leader.Id);
                NodeLogger.Log($"Current Leader = " + (leader == null ? "none" : $"Node {leader.Id}"));
                NodeLogger.Log($"Leaders count = {registry.CountLeaders()}");
            }

            if (leader != null && leader.Status == NodeStatus.Available)
            {
                NodeLogger.Log("Voting started");
                consensus.ProposeAndVote(leader.Id, "OrderStatus", OrderStatus.Paid.ToString());
            }
            else
            {
                NodeLogger.Log("Voting skipped: no AVAILABLE Leader");
            }

            try
            {
                var server = new Server(3000);
                Database.Database.SetPrimaryAvailable(true);
                Database.Database.SetReplicaAvailable(true);
                //new ReplicationManager().SynchronizeReplicaToPrimary();
                server.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Критическая ошибка: {ex.Message}");
                Console.ReadKey();
            }
        }
    }
}
public class Event
{
    public string EventType { get; set; }
    public int EntityId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();

    public int Attempt { get; set; } = 1;
    public DateTime PublishedAtUtc { get; set; }

    public string ClientId { get; set; } = "-";
}
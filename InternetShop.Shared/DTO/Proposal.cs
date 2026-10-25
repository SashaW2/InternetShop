namespace InternetShop.Shared.DTO
{
    public class Proposal
    {
        public int ProposalId { get; set; }
        public int LeaderId { get; set; }
        public string Key { get; set; }    
        public string Value { get; set; }    
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
namespace InternetShop.Shared.DTO
{
    public class Vote
    {
        public int ProposalId { get; set; }
        public int VoterId { get; set; }
        public bool IsYes { get; set; }
    }
}
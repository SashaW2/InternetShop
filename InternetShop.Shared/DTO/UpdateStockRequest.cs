namespace InternetShop.Shared.DTO
{
    public class UpdateStockRequest
    {
        public int ProductId { get; set; }
        public int NewStock { get; set; }
    }
}
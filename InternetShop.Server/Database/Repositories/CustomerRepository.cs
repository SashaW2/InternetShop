using Microsoft.Data.SqlClient;

namespace InternetShop.Server.Database.Repositories
{
    public class CustomerRepository
    {
        public bool Exists(int customerId)
        {
            using (var conn = Database.CreateConnection())
            using (var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM dbo.Customers WHERE Id = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", customerId);
                return (int)cmd.ExecuteScalar() > 0;
            }
        }
    }
}
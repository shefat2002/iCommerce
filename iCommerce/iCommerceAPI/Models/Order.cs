namespace iCommerceAPI.Models;

public class Order
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime OrderDate { get; set; }
    public List<Product> Products { get; set; } = new List<Product>();
}

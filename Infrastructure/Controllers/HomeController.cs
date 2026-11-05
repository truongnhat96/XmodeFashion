using Microsoft.AspNetCore.Mvc;

namespace Infrastructure.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();
    public IActionResult Privacy() => View();
    public IActionResult Error() => Problem("Không thể xử lý yêu cầu. Vui lòng thử lại.");
    [HttpGet("/Category/{id}")]
    public IActionResult Category(int id) => Redirect($"/xmode/catalog?category={id}");
    [HttpGet("/Product/{id}")]
    public IActionResult Product(int id) => Redirect($"/xmode/product/{id}");
    [HttpGet("/Cart")]
    public IActionResult Cart() => Redirect("/xmode/cart");
    [HttpGet("/Search")]
    public IActionResult Search(string? keyword) => Redirect($"/xmode/catalog?q={Uri.EscapeDataString(keyword ?? "")}");
}

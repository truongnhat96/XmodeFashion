using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Infrastructure.Controllers;

// Presentation only. The existing commerce services and endpoints remain independent.
public class XmodeController : Controller
{
    [HttpGet("/xmode")]
    [HttpGet("/xmode/catalog")]
    [HttpGet("/xmode/product/{id}")]
    [HttpGet("/xmode/cart")]
    [HttpGet("/xmode/checkout")]
    [HttpGet("/xmode/guide")]
    [HttpGet("/xmode/orders")]
    [HttpGet("/xmode/orders/{id}")]
    public IActionResult Store() => View("Index");

    [Authorize(Roles = "Admin")]
    [HttpGet("/xmode/admin")]
    [HttpGet("/xmode/admin/products")]
    [HttpGet("/xmode/admin/categories")]
    [HttpGet("/xmode/admin/orders")]
    [HttpGet("/xmode/admin/content")]
    [HttpGet("/xmode/admin/size-charts")]
    [HttpGet("/xmode/admin/settings")]
    public IActionResult Admin() => View("Index");
}

using DigitalSign.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalSign.Web.Controllers;

public class HomeController : Controller
{
    private readonly IDigitalSignService _signService;

    public HomeController(IDigitalSignService signService)
    {
        _signService = signService;
    }

    public async Task<IActionResult> Index()
    {
        var health = await _signService.GetHealthAsync();
        ViewBag.Health  = health;
        ViewBag.User    = User.Identity?.Name ?? "Unknown";
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}

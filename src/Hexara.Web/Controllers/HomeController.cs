using System.Diagnostics;
using Hexara.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Hexara.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();

    /// <summary>
    /// قوانین بازی.
    ///
    /// بی نیاز به ورود است: کسی که هنوز ثبت‌نام نکرده باید بتواند اول ببیند چه
    /// بازی‌ای است. اعدادش از خودِ موتور می‌آیند نه از متنِ صفحه.
    /// </summary>
    public IActionResult Rules() => View(new RulesViewModel());

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}

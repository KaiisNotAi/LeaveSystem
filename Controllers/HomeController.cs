using LeaveSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace LeaveSystem.Controllers
{
    public class HomeController : Controller
    {
        // 首頁需要登入才能看（這樣未登入者進站會被導到 /Account/Login）
        [Authorize]
        public IActionResult Index()
        {
            return View();
        }

        // 隱私權聲明開放公開
        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

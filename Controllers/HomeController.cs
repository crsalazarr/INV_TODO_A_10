using Microsoft.AspNetCore.Mvc;

namespace INV_TODO_A_10.Controllers
{
    public class HomeController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            // Verificar si está logueado
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("Usuario")))
            {
                return RedirectToAction("Login", "Account");
            }
            
            ViewBag.Usuario = HttpContext.Session.GetString("Usuario");
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre");
            ViewBag.Cargo = HttpContext.Session.GetString("Cargo");
            
            return View();
        }
    }
}
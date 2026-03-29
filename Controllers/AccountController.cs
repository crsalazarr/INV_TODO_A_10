using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using INV_TODO_A_10.Models;
using Microsoft.AspNetCore.Http;

namespace INV_TODO_A_10.Controllers
{
    public class AccountController : Controller
    {
        private readonly ConexionBD _conexion;

        public AccountController(IConfiguration configuration)
        {
            string connectionString = configuration.GetConnectionString("MySQLConnection")
                ?? throw new InvalidOperationException("Connection string 'MySQLConnection' not found.");
            _conexion = new ConexionBD(connectionString);
        }

        // GET: Cargar Login
        [HttpGet]
        public IActionResult Login()
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("Usuario")))
            {
                // Si ya hay sesión, redirigir según el cargo
                string cargo = HttpContext.Session.GetString("Cargo") ?? "";
                if (cargo == "ADMINISTRADOR")
                    return RedirectToAction("GestionarLocal", "Gestion");
                else
                    return RedirectToAction("VentaPrendas", "Ventas");
            }

            if (Request.Cookies.TryGetValue("UsuarioRecordado", out var usuarioRecordado))
                ViewBag.UsuarioRecordado = usuarioRecordado;

            return View("~/Views/Home/Index.cshtml");
        }

        // POST: Procesar Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string username, string password, bool remember = false)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                TempData["ErrorMessage"] = "Usuario y contraseña son requeridos.";
                return View("~/Views/Home/Index.cshtml");
            }

            try
            {
                using var conn = _conexion.ObtenerConexion();

                string query = @"
                    SELECT id, nombre, apellido, cargo, usuario, contraseña, local_id
                    FROM usuarios
                    WHERE usuario = @usuario
                    LIMIT 1";

                using var cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@usuario", username);

                using var reader = cmd.ExecuteReader();

                if (!reader.Read())
                {
                    TempData["ErrorMessage"] = "Usuario no encontrado.";
                    return View("~/Views/Home/Index.cshtml");
                }

                string storedPassword = reader["contraseña"]?.ToString() ?? "";
                if (storedPassword != password)
                {
                    TempData["ErrorMessage"] = "Contraseña incorrecta.";
                    return View("~/Views/Home/Index.cshtml");
                }

                // ✅ LOGIN CORRECTO
                string cargo = reader["cargo"]?.ToString() ?? "";
                string nombre = reader["nombre"]?.ToString() ?? "";
                string apellido = reader["apellido"]?.ToString() ?? "";
                
                HttpContext.Session.SetString("Usuario", username);
                HttpContext.Session.SetString("Cargo", cargo);
                HttpContext.Session.SetString("Nombre", nombre);
                HttpContext.Session.SetString("Apellido", apellido);
                HttpContext.Session.SetInt32("UsuarioId", reader.GetInt32("id"));
                HttpContext.Session.SetInt32("LocalId", reader.GetInt32("local_id"));

                if (remember)
                {
                    Response.Cookies.Append("UsuarioRecordado", username, new CookieOptions
                    {
                        Expires = DateTimeOffset.Now.AddDays(7),
                        HttpOnly = true,
                        Secure = false,
                        SameSite = SameSiteMode.Lax
                    });
                }
                else
                {
                    Response.Cookies.Delete("UsuarioRecordado");
                }

                TempData["SuccessMessage"] = $"¡Bienvenido {nombre} {apellido}!";

                // Redirigir según el cargo del usuario
                if (cargo == "ADMINISTRADOR")
                {
                    return RedirectToAction("GestionarLocal", "Gestion");
                }
                else // CAJERO u otro cargo
                {
                    return RedirectToAction("VentaPrendas", "Ventas");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error en la conexión: " + ex.Message;
                return View("~/Views/Home/Index.cshtml");
            }
        }

        // Cerrar sesión
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete("UsuarioRecordado");
            return RedirectToAction("Login", "Account");
        }

        // Verificar sesión
        public bool IsLoggedIn() => !string.IsNullOrEmpty(HttpContext.Session.GetString("Usuario"));

        // Obtener usuario actual
        public (int? id, string usuario, string nombre, string cargo, int? localId) GetCurrentUser()
        {
            return (
                HttpContext.Session.GetInt32("UsuarioId"),
                HttpContext.Session.GetString("Usuario") ?? "",
                HttpContext.Session.GetString("Nombre") ?? "",
                HttpContext.Session.GetString("Cargo") ?? "",
                HttpContext.Session.GetInt32("LocalId")
            );
        }
    }
}
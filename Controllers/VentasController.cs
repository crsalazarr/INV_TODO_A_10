using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace INV_TODO_A_10.Controllers
{
    public class VentasController : Controller
    {
        private readonly IConfiguration _configuration;

        public VentasController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult VentaPrendas()
        {
            // Validar sesión activa
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("Usuario")))
            {
                return RedirectToAction("Login", "Account");
            }

            // Obtener datos del usuario desde la sesión
            ViewBag.Usuario = HttpContext.Session.GetString("Usuario");
            ViewBag.Nombre = HttpContext.Session.GetString("Nombre");
            ViewBag.Apellido = HttpContext.Session.GetString("Apellido");
            ViewBag.Cargo = HttpContext.Session.GetString("Cargo");

            // Obtener el LocalId desde la sesión
            int? localId = HttpContext.Session.GetInt32("LocalId");

            if (localId.HasValue)
            {
                // Consultar el nombre del local desde la base de datos
                try
                {
                    string connectionString = _configuration.GetConnectionString("MySQLConnection")
                        ?? throw new InvalidOperationException("Connection string 'MySQLConnection' not found.");

                    using var conn = new MySqlConnection(connectionString);
                    conn.Open();

                    string query = "SELECT nombre FROM local WHERE id = @localId LIMIT 1";
                    using var cmd = new MySqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@localId", localId.Value);

                    var result = cmd.ExecuteScalar();
                    ViewBag.LocalNombre = result?.ToString() ?? "LOCAL TODO A 10.000";
                }
                catch (Exception ex)
                {
                    // En caso de error, usar un nombre por defecto
                    ViewBag.LocalNombre = "LOCAL TODO A 10.000";
                    Console.WriteLine($"Error al obtener nombre del local: {ex.Message}");
                }
            }
            else
            {
                ViewBag.LocalNombre = "LOCAL TODO A 10.000";
            }

            return View("~/Views/Home/VentaPrendas.cshtml");
        }
    }
}
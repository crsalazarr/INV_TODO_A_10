using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Text.Json;

namespace INV_TODO_A_10.Controllers
{
    public class GestionController : Controller
    {
        private readonly IConfiguration _configuration;

        public GestionController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // =====================================================
        // HELPERS
        // =====================================================
        private bool IsAuthenticated()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("Usuario"));
        }

        private bool IsAdministrador()
        {
            return HttpContext.Session.GetString("Cargo") == "ADMINISTRADOR";
        }

        private MySqlConnection GetConnection()
        {
            string cs = _configuration.GetConnectionString("MySQLConnection")
                ?? throw new InvalidOperationException("Connection string not found");

            return new MySqlConnection(cs);
        }

        // =====================================================
        // VISTA
        // =====================================================
        public IActionResult GestionarLocal()
        {
            if (!IsAuthenticated())
                return RedirectToAction("Login", "Account");

            if (!IsAdministrador())
                return RedirectToAction("VentaPrendas", "Ventas");

            ViewBag.Nombre = HttpContext.Session.GetString("Nombre");
            ViewBag.Cargo = HttpContext.Session.GetString("Cargo");

            return View("~/Views/Home/gestionarlocal.cshtml");
        }

        // =====================================================
        // LOCALES - ENDPOINTS
        // =====================================================
        
        /// <summary>
        /// GET: Obtener todos los locales
        /// </summary>
        [HttpGet]
        [Route("api/locales")]
        public IActionResult GetLocales()
        {
            if (!IsAuthenticated())
                return Unauthorized();

            try
            {
                var lista = new List<object>();

                using var conn = GetConnection();
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT id, nombre, direccion, ciudad, telefono, estado
                    FROM local
                    ORDER BY nombre", conn);

                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    lista.Add(new
                    {
                        id = rd.GetInt32("id"),
                        nombre = rd.GetString("nombre"),
                        direccion = rd.GetString("direccion"),
                        ciudad = rd.GetString("ciudad"),
                        telefono = rd.IsDBNull(rd.GetOrdinal("telefono"))
                            ? ""
                            : rd.GetString("telefono"),
                        estado = rd.GetString("estado")
                    });
                }

                return Ok(lista);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// POST: Crear un nuevo local
        /// </summary>
        [HttpPost]
        [Route("api/locales")]
        public async Task<IActionResult> CreateLocal([FromBody] JsonElement data)
        {
            if (!IsAuthenticated() || !IsAdministrador())
                return Unauthorized();

            try
            {
                // Validar campos requeridos
                if (!data.TryGetProperty("nombre", out var nombreProp) || string.IsNullOrWhiteSpace(nombreProp.GetString()))
                    return BadRequest(new { error = "El nombre es requerido" });

                if (!data.TryGetProperty("direccion", out var direccionProp) || string.IsNullOrWhiteSpace(direccionProp.GetString()))
                    return BadRequest(new { error = "La dirección es requerida" });

                if (!data.TryGetProperty("ciudad", out var ciudadProp) || string.IsNullOrWhiteSpace(ciudadProp.GetString()))
                    return BadRequest(new { error = "La ciudad es requerida" });

                using var conn = GetConnection();
                await conn.OpenAsync();

                var cmd = new MySqlCommand(@"
                    INSERT INTO local (nombre, direccion, ciudad, telefono, estado)
                    VALUES (@n, @d, @c, @t, @e);
                    SELECT LAST_INSERT_ID();", conn);

                cmd.Parameters.AddWithValue("@n", nombreProp.GetString()!.Trim());
                cmd.Parameters.AddWithValue("@d", direccionProp.GetString()!.Trim());
                cmd.Parameters.AddWithValue("@c", ciudadProp.GetString()!.Trim());
                
                // Manejar teléfono opcional
                string? telefono = null;
                if (data.TryGetProperty("telefono", out var telProp))
                {
                    var telStr = telProp.GetString();
                    telefono = string.IsNullOrWhiteSpace(telStr) ? null : telStr.Trim();
                }
                cmd.Parameters.AddWithValue("@t", (object?)telefono ?? DBNull.Value);

                // Estado con valor por defecto
                string estado = "ACTIVO";
                if (data.TryGetProperty("estado", out var estadoProp))
                {
                    var estadoStr = estadoProp.GetString();
                    if (!string.IsNullOrWhiteSpace(estadoStr))
                        estado = estadoStr;
                }
                cmd.Parameters.AddWithValue("@e", estado);

                var newId = await cmd.ExecuteScalarAsync();

                return Ok(new { success = true, id = Convert.ToInt32(newId) });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// PUT: Actualizar un local existente
        /// </summary>
        [HttpPut]
        [Route("api/locales/{id}")]
        public async Task<IActionResult> UpdateLocal(int id, [FromBody] JsonElement data)
        {
            if (!IsAuthenticated() || !IsAdministrador())
                return Unauthorized();

            try
            {
                // Validar campos requeridos
                if (!data.TryGetProperty("nombre", out var nombreProp) || string.IsNullOrWhiteSpace(nombreProp.GetString()))
                    return BadRequest(new { error = "El nombre es requerido" });

                if (!data.TryGetProperty("direccion", out var direccionProp) || string.IsNullOrWhiteSpace(direccionProp.GetString()))
                    return BadRequest(new { error = "La dirección es requerida" });

                if (!data.TryGetProperty("ciudad", out var ciudadProp) || string.IsNullOrWhiteSpace(ciudadProp.GetString()))
                    return BadRequest(new { error = "La ciudad es requerida" });

                using var conn = GetConnection();
                await conn.OpenAsync();

                var cmd = new MySqlCommand(@"
                    UPDATE local
                    SET nombre = @n,
                        direccion = @d,
                        ciudad = @c,
                        telefono = @t,
                        estado = @e
                    WHERE id = @id", conn);

                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@n", nombreProp.GetString()!.Trim());
                cmd.Parameters.AddWithValue("@d", direccionProp.GetString()!.Trim());
                cmd.Parameters.AddWithValue("@c", ciudadProp.GetString()!.Trim());

                // Manejar teléfono opcional
                string? telefono = null;
                if (data.TryGetProperty("telefono", out var telProp))
                {
                    var telStr = telProp.GetString();
                    telefono = string.IsNullOrWhiteSpace(telStr) ? null : telStr.Trim();
                }
                cmd.Parameters.AddWithValue("@t", (object?)telefono ?? DBNull.Value);

                // Estado
                string estado = "ACTIVO";
                if (data.TryGetProperty("estado", out var estadoProp))
                {
                    var estadoStr = estadoProp.GetString();
                    if (!string.IsNullOrWhiteSpace(estadoStr))
                        estado = estadoStr;
                }
                cmd.Parameters.AddWithValue("@e", estado);

                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                    return NotFound(new { error = "Local no encontrado" });

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// DELETE: Eliminar un local y todos sus registros relacionados (en cascada)
        /// </summary>
        [HttpDelete]
        [Route("api/locales/{id}")]
        public async Task<IActionResult> DeleteLocal(int id)
        {
            if (!IsAuthenticated() || !IsAdministrador())
                return Unauthorized();

            using var conn = GetConnection();
            await conn.OpenAsync();

            // Iniciar una transacción para asegurar que todo se elimine o nada
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // 1. Eliminar todos los inventarios diarios del local
                var deleteInventarioCmd = new MySqlCommand(
                    "DELETE FROM inventario_diario WHERE local_id = @id", conn, transaction);
                deleteInventarioCmd.Parameters.AddWithValue("@id", id);
                int inventariosEliminados = await deleteInventarioCmd.ExecuteNonQueryAsync();

                // 2. Eliminar todos los gastos diarios del local
                var deleteGastosCmd = new MySqlCommand(
                    "DELETE FROM gasto_diario WHERE local_id = @id", conn, transaction);
                deleteGastosCmd.Parameters.AddWithValue("@id", id);
                int gastosEliminados = await deleteGastosCmd.ExecuteNonQueryAsync();

                // 3. Eliminar todos los usuarios asignados al local
                var deleteUsuariosCmd = new MySqlCommand(
                    "DELETE FROM usuarios WHERE local_id = @id", conn, transaction);
                deleteUsuariosCmd.Parameters.AddWithValue("@id", id);
                int usuariosEliminados = await deleteUsuariosCmd.ExecuteNonQueryAsync();

                // 4. Finalmente, eliminar el local
                var deleteLocalCmd = new MySqlCommand(
                    "DELETE FROM local WHERE id = @id", conn, transaction);
                deleteLocalCmd.Parameters.AddWithValue("@id", id);
                int rowsAffected = await deleteLocalCmd.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                {
                    await transaction.RollbackAsync();
                    return NotFound(new { error = "Local no encontrado" });
                }

                // Confirmar la transacción
                await transaction.CommitAsync();

                return Ok(new 
                { 
                    success = true, 
                    message = "Local eliminado correctamente",
                    detalles = new 
                    {
                        inventariosEliminados,
                        gastosEliminados,
                        usuariosEliminados
                    }
                });
            }
            catch (Exception ex)
            {
                // Si algo falla, revertir todos los cambios
                await transaction.RollbackAsync();
                return StatusCode(500, new { error = $"Error al eliminar el local: {ex.Message}" });
            }
        }

        // =====================================================
        // USUARIOS - ENDPOINTS
        // =====================================================

        /// <summary>
        /// GET: Obtener todos los usuarios
        /// </summary>
        [HttpGet]
        [Route("api/usuarios")]
        public IActionResult GetUsuarios()
        {
            if (!IsAuthenticated())
                return Unauthorized();

            try
            {
                var lista = new List<object>();

                using var conn = GetConnection();
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT u.id, u.nombre, u.apellido, u.telefono, u.direccion, 
                           u.cargo, u.usuario, u.local_id, l.nombre as local_nombre
                    FROM usuarios u
                    LEFT JOIN local l ON u.local_id = l.id
                    ORDER BY u.nombre, u.apellido", conn);

                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    lista.Add(new
                    {
                        id = rd.GetInt32("id"),
                        nombre = rd.GetString("nombre"),
                        apellido = rd.GetString("apellido"),
                        telefono = rd.IsDBNull(rd.GetOrdinal("telefono"))
                            ? ""
                            : rd.GetString("telefono"),
                        direccion = rd.IsDBNull(rd.GetOrdinal("direccion"))
                            ? ""
                            : rd.GetString("direccion"),
                        cargo = rd.GetString("cargo"),
                        usuario = rd.GetString("usuario"),
                        local_id = rd.GetInt32("local_id"),
                        local_nombre = rd.IsDBNull(rd.GetOrdinal("local_nombre"))
                            ? ""
                            : rd.GetString("local_nombre")
                    });
                }

                return Ok(lista);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// POST: Crear un nuevo usuario
        /// </summary>
        [HttpPost]
        [Route("api/usuarios")]
        public async Task<IActionResult> CreateUsuario([FromBody] JsonElement data)
        {
            if (!IsAuthenticated() || !IsAdministrador())
                return Unauthorized();

            try
            {
                // Validar campos requeridos
                if (!data.TryGetProperty("nombre", out var nombreProp) || string.IsNullOrWhiteSpace(nombreProp.GetString()))
                    return BadRequest(new { error = "El nombre es requerido" });

                if (!data.TryGetProperty("apellido", out var apellidoProp) || string.IsNullOrWhiteSpace(apellidoProp.GetString()))
                    return BadRequest(new { error = "El apellido es requerido" });

                if (!data.TryGetProperty("cargo", out var cargoProp) || string.IsNullOrWhiteSpace(cargoProp.GetString()))
                    return BadRequest(new { error = "El cargo es requerido" });

                if (!data.TryGetProperty("usuario", out var usuarioProp) || string.IsNullOrWhiteSpace(usuarioProp.GetString()))
                    return BadRequest(new { error = "El usuario es requerido" });

                if (!data.TryGetProperty("contraseña", out var passwordProp) || string.IsNullOrWhiteSpace(passwordProp.GetString()))
                    return BadRequest(new { error = "La contraseña es requerida" });

                if (!data.TryGetProperty("local_id", out var localIdProp))
                    return BadRequest(new { error = "El local es requerido" });

                using var conn = GetConnection();
                await conn.OpenAsync();

                // Verificar si el usuario ya existe
                var checkCmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM usuarios WHERE usuario = @usuario", conn);
                checkCmd.Parameters.AddWithValue("@usuario", usuarioProp.GetString()!.Trim());
                
                var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                if (count > 0)
                    return BadRequest(new { error = "El nombre de usuario ya existe" });

                // Insertar nuevo usuario
                var cmd = new MySqlCommand(@"
                    INSERT INTO usuarios (nombre, apellido, telefono, direccion, cargo, usuario, contraseña, local_id)
                    VALUES (@nombre, @apellido, @telefono, @direccion, @cargo, @usuario, @password, @local_id);
                    SELECT LAST_INSERT_ID();", conn);

                cmd.Parameters.AddWithValue("@nombre", nombreProp.GetString()!.Trim());
                cmd.Parameters.AddWithValue("@apellido", apellidoProp.GetString()!.Trim());

                // Teléfono opcional
                string? telefono = null;
                if (data.TryGetProperty("telefono", out var telProp))
                {
                    var telStr = telProp.GetString();
                    telefono = string.IsNullOrWhiteSpace(telStr) ? null : telStr.Trim();
                }
                cmd.Parameters.AddWithValue("@telefono", (object?)telefono ?? DBNull.Value);

                // Dirección opcional
                string? direccion = null;
                if (data.TryGetProperty("direccion", out var dirProp))
                {
                    var dirStr = dirProp.GetString();
                    direccion = string.IsNullOrWhiteSpace(dirStr) ? null : dirStr.Trim();
                }
                cmd.Parameters.AddWithValue("@direccion", (object?)direccion ?? DBNull.Value);

                cmd.Parameters.AddWithValue("@cargo", cargoProp.GetString()!.Trim());
                cmd.Parameters.AddWithValue("@usuario", usuarioProp.GetString()!.Trim());
                
                // IMPORTANTE: En producción, SIEMPRE hashea las contraseñas con BCrypt o similar
                cmd.Parameters.AddWithValue("@password", passwordProp.GetString()!);
                cmd.Parameters.AddWithValue("@local_id", localIdProp.GetInt32());

                var newId = await cmd.ExecuteScalarAsync();

                return Ok(new { success = true, id = Convert.ToInt32(newId) });
            }
            catch (MySqlException ex) when (ex.Number == 1452)
            {
                return BadRequest(new { error = "El local seleccionado no existe" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// PUT: Actualizar un usuario existente
        /// </summary>
        [HttpPut]
        [Route("api/usuarios/{id}")]
        public async Task<IActionResult> UpdateUsuario(int id, [FromBody] JsonElement data)
        {
            if (!IsAuthenticated() || !IsAdministrador())
                return Unauthorized();

            try
            {
                // Validar campos requeridos
                if (!data.TryGetProperty("nombre", out var nombreProp) || string.IsNullOrWhiteSpace(nombreProp.GetString()))
                    return BadRequest(new { error = "El nombre es requerido" });

                if (!data.TryGetProperty("apellido", out var apellidoProp) || string.IsNullOrWhiteSpace(apellidoProp.GetString()))
                    return BadRequest(new { error = "El apellido es requerido" });

                if (!data.TryGetProperty("cargo", out var cargoProp) || string.IsNullOrWhiteSpace(cargoProp.GetString()))
                    return BadRequest(new { error = "El cargo es requerido" });

                using var conn = GetConnection();
                await conn.OpenAsync();

                var cmd = new MySqlCommand(@"
                    UPDATE usuarios
                    SET nombre = @nombre,
                        apellido = @apellido,
                        telefono = @telefono,
                        direccion = @direccion,
                        cargo = @cargo,
                        local_id = @local_id
                    WHERE id = @id", conn);

                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@nombre", nombreProp.GetString()!.Trim());
                cmd.Parameters.AddWithValue("@apellido", apellidoProp.GetString()!.Trim());

                // Teléfono opcional
                string? telefono = null;
                if (data.TryGetProperty("telefono", out var telProp))
                {
                    var telStr = telProp.GetString();
                    telefono = string.IsNullOrWhiteSpace(telStr) ? null : telStr.Trim();
                }
                cmd.Parameters.AddWithValue("@telefono", (object?)telefono ?? DBNull.Value);

                // Dirección opcional
                string? direccion = null;
                if (data.TryGetProperty("direccion", out var dirProp))
                {
                    var dirStr = dirProp.GetString();
                    direccion = string.IsNullOrWhiteSpace(dirStr) ? null : dirStr.Trim();
                }
                cmd.Parameters.AddWithValue("@direccion", (object?)direccion ?? DBNull.Value);

                cmd.Parameters.AddWithValue("@cargo", cargoProp.GetString()!.Trim());

                // Local ID
                if (data.TryGetProperty("local_id", out var localIdProp))
                {
                    cmd.Parameters.AddWithValue("@local_id", localIdProp.GetInt32());
                }
                else
                {
                    return BadRequest(new { error = "El local es requerido" });
                }

                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                    return NotFound(new { error = "Usuario no encontrado" });

                return Ok(new { success = true });
            }
            catch (MySqlException ex) when (ex.Number == 1452)
            {
                return BadRequest(new { error = "El local seleccionado no existe" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// DELETE: Eliminar un usuario
        /// </summary>
        [HttpDelete]
        [Route("api/usuarios/{id}")]
        public async Task<IActionResult> DeleteUsuario(int id)
        {
            if (!IsAuthenticated() || !IsAdministrador())
                return Unauthorized();

            try
            {
                using var conn = GetConnection();
                await conn.OpenAsync();

                var cmd = new MySqlCommand(
                    "DELETE FROM usuarios WHERE id = @id", conn);

                cmd.Parameters.AddWithValue("@id", id);
                int rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                    return NotFound(new { error = "Usuario no encontrado" });

                return Ok(new { success = true });
            }
            catch (MySqlException ex) when (ex.Number == 1451)
            {
                return BadRequest(new { error = "No se puede eliminar el usuario porque tiene registros asociados" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // =====================================================
        // INVENTARIO - ENDPOINTS
        // =====================================================

        /// <summary>
        /// GET: Obtener todo el inventario
        /// </summary>
        [HttpGet]
        [Route("api/inventario")]
        public IActionResult GetInventario()
        {
            if (!IsAuthenticated())
                return Unauthorized();

            try
            {
                var lista = new List<object>();

                using var conn = GetConnection();
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT id, fecha, local_id, inv_inicial, entrada, salida, inv_final
                    FROM inventario_diario
                    ORDER BY fecha DESC, local_id", conn);

                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    lista.Add(new
                    {
                        id = rd.GetInt32("id"),
                        fecha = rd.GetDateTime("fecha").ToString("yyyy-MM-dd"),
                        local_id = rd.GetInt32("local_id"),
                        inv_inicial = rd.GetInt32("inv_inicial"),
                        entrada = rd.GetInt32("entrada"),
                        salida = rd.GetInt32("salida"),
                        inv_final = rd.GetInt32("inv_final")
                    });
                }

                return Ok(lista);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// GET: Obtener el último inventario de un local
        /// </summary>
        [HttpGet]
        [Route("api/inventario/ultimo/{localId}")]
        public IActionResult GetUltimoInventario(int localId)
        {
            if (!IsAuthenticated())
                return Unauthorized();

            try
            {
                using var conn = GetConnection();
                conn.Open();

                var cmd = new MySqlCommand(@"
                    SELECT id, fecha, local_id, inv_inicial, entrada, salida, inv_final
                    FROM inventario_diario
                    WHERE local_id = @localId
                    ORDER BY id DESC
                    LIMIT 1", conn);

                cmd.Parameters.AddWithValue("@localId", localId);

                using var rd = cmd.ExecuteReader();
                if (rd.Read())
                {
                    var resultado = new
                    {
                        id = rd.GetInt32("id"),
                        fecha = rd.GetDateTime("fecha").ToString("yyyy-MM-dd"),
                        local_id = rd.GetInt32("local_id"),
                        inv_inicial = rd.GetInt32("inv_inicial"),
                        entrada = rd.GetInt32("entrada"),
                        salida = rd.GetInt32("salida"),
                        inv_final = rd.GetInt32("inv_final")
                    };

                    return Ok(resultado);
                }

                return Ok(new { error = "No hay inventario previo" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// POST: Crear un nuevo registro de inventario
        /// </summary>
        [HttpPost]
        [Route("api/inventario")]
        public async Task<IActionResult> CreateInventario([FromBody] JsonElement data)
        {
            if (!IsAuthenticated())
                return Unauthorized();

            try
            {
                // Validar campos requeridos
                if (!data.TryGetProperty("local_id", out var localIdProp))
                    return BadRequest(new { error = "El local es requerido" });

                if (!data.TryGetProperty("fecha", out var fechaProp) || string.IsNullOrWhiteSpace(fechaProp.GetString()))
                    return BadRequest(new { error = "La fecha es requerida" });

                if (!data.TryGetProperty("inv_inicial", out var inicialProp))
                    return BadRequest(new { error = "El inventario inicial es requerido" });

                if (!data.TryGetProperty("inv_final", out var finalProp))
                    return BadRequest(new { error = "El inventario final es requerido" });

                using var conn = GetConnection();
                await conn.OpenAsync();

                var cmd = new MySqlCommand(@"
                    INSERT INTO inventario_diario (fecha, local_id, inv_inicial, entrada, salida, inv_final)
                    VALUES (@fecha, @local_id, @inv_inicial, @entrada, @salida, @inv_final);
                    SELECT LAST_INSERT_ID();", conn);

                cmd.Parameters.AddWithValue("@fecha", fechaProp.GetString());
                cmd.Parameters.AddWithValue("@local_id", localIdProp.GetInt32());
                cmd.Parameters.AddWithValue("@inv_inicial", inicialProp.GetInt32());
                
                // Entrada y salida con valores por defecto
                int entrada = 0;
                if (data.TryGetProperty("entrada", out var entradaProp))
                    entrada = entradaProp.GetInt32();
                cmd.Parameters.AddWithValue("@entrada", entrada);

                int salida = 0;
                if (data.TryGetProperty("salida", out var salidaProp))
                    salida = salidaProp.GetInt32();
                cmd.Parameters.AddWithValue("@salida", salida);

                cmd.Parameters.AddWithValue("@inv_final", finalProp.GetInt32());

                var newId = await cmd.ExecuteScalarAsync();

                return Ok(new { success = true, id = Convert.ToInt32(newId) });
            }
            catch (MySqlException ex) when (ex.Number == 1452)
            {
                return BadRequest(new { error = "El local seleccionado no existe" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
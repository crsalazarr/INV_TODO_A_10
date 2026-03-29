using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Globalization;
using INV_TODO_A_10.Models;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using System.Text;

namespace INV_TODO_A_10.Controllers
{
    public class ReportesController : Controller
    {
        private readonly IConfiguration _configuration;

        public ReportesController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult ReporteDiario()
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
                    ViewBag.Local = result?.ToString() ?? "LOCAL TODO A 10.000";
                }
                catch (Exception ex)
                {
                    // En caso de error, usar un nombre por defecto
                    ViewBag.Local = "LOCAL TODO A 10.000";
                    Console.WriteLine($"Error al obtener nombre del local: {ex.Message}");
                }
            }
            else
            {
                ViewBag.Local = "LOCAL TODO A 10.000";
            }

            return View("~/Views/Home/ReporteDiario.cshtml");
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerReporteHoy()
        {
            try
            {
                // Validar sesión activa
                if (string.IsNullOrEmpty(HttpContext.Session.GetString("Usuario")))
                {
                    return Json(new { success = false, message = "Sesión expirada" });
                }

                int? localId = HttpContext.Session.GetInt32("LocalId");
                if (!localId.HasValue)
                {
                    return Json(new { success = false, message = "Local no identificado" });
                }

                // Obtener fecha actual
                DateTime fechaActual = DateTime.Now;

                string connectionString = _configuration.GetConnectionString("MySQLConnection")
                    ?? throw new InvalidOperationException("Connection string 'MySQLConnection' not found.");

                using var conn = new MySqlConnection(connectionString);
                await conn.OpenAsync();

                string query = @"
                    SELECT id, nomina, arriendo, bolsa, otros, transferencias 
                    FROM gasto_diario 
                    WHERE local_id = @localId 
                    AND DATE(fecha) = DATE(@fecha) 
                    LIMIT 1";

                using var cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@localId", localId.Value);
                cmd.Parameters.AddWithValue("@fecha", fechaActual);

                using var reader = await cmd.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    var gastoDiario = new
                    {
                        id = reader.GetInt32("id"),
                        local_id = localId.Value,
                        nomina = reader.GetDecimal("nomina"),
                        arriendo = reader.GetDecimal("arriendo"),
                        bolsa = reader.GetDecimal("bolsa"),
                        otros = reader.GetDecimal("otros"),
                        transferencias = reader.GetDecimal("transferencias")
                    };

                    return Json(new { 
                        success = true, 
                        data = gastoDiario,
                        message = "Datos cargados correctamente"
                    });
                }
                else
                {
                    return Json(new { 
                        success = true, 
                        data = new { 
                            id = 0,
                            nomina = 0m,
                            arriendo = 0m,
                            bolsa = 0m,
                            otros = 0m,
                            transferencias = 0m
                        },
                        message = "No hay datos para hoy"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener reporte: {ex.Message}");
                return Json(new { 
                    success = false, 
                    message = $"Error al cargar datos: {ex.Message}" 
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> VerificarInventarioInicial()
        {
            try
            {
                // Validar sesión activa
                if (string.IsNullOrEmpty(HttpContext.Session.GetString("Usuario")))
                {
                    return Json(new { success = false, tieneInventario = false, message = "Sesión expirada" });
                }

                int? localId = HttpContext.Session.GetInt32("LocalId");
                if (!localId.HasValue)
                {
                    return Json(new { success = false, tieneInventario = false, message = "Local no identificado" });
                }

                string connectionString = _configuration.GetConnectionString("MySQLConnection")
                    ?? throw new InvalidOperationException("Connection string 'MySQLConnection' not found.");

                using var conn = new MySqlConnection(connectionString);
                await conn.OpenAsync();

                // Verificar si existe algún registro de inventario para este local
                string queryVerificarInventario = @"
                    SELECT COUNT(*) 
                    FROM inventario_diario 
                    WHERE local_id = @localId";

                using var cmdVerificarInventario = new MySqlCommand(queryVerificarInventario, conn);
                cmdVerificarInventario.Parameters.AddWithValue("@localId", localId.Value);

                long countInventario = (long)(await cmdVerificarInventario.ExecuteScalarAsync() ?? 0L);

                bool tieneInventario = countInventario > 0;

                // Si tiene inventario, obtener el último registro
                if (tieneInventario)
                {
                    string queryUltimoInventario = @"
                        SELECT id, fecha, inv_inicial, entrada, salida, inv_final
                        FROM inventario_diario 
                        WHERE local_id = @localId 
                        ORDER BY fecha DESC, id DESC 
                        LIMIT 1";

                    using var cmdUltimoInventario = new MySqlCommand(queryUltimoInventario, conn);
                    cmdUltimoInventario.Parameters.AddWithValue("@localId", localId.Value);

                    using var reader = await cmdUltimoInventario.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        var inventario = new
                        {
                            id = reader.GetInt32("id"),
                            fecha = reader.GetDateTime("fecha").ToString("yyyy-MM-dd"),
                            inv_inicial = reader.GetInt32("inv_inicial"),
                            entrada = reader.GetInt32("entrada"),
                            salida = reader.GetInt32("salida"),
                            inv_final = reader.GetInt32("inv_final")
                        };

                        return Json(new { 
                            success = true, 
                            tieneInventario = true,
                            inventario = inventario,
                            message = "Inventario encontrado"
                        });
                    }
                }

                return Json(new { 
                    success = true, 
                    tieneInventario = tieneInventario,
                    message = tieneInventario ? "Inventario encontrado" : "No existe inventario inicial"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al verificar inventario: {ex.Message}");
                return Json(new { 
                    success = false, 
                    tieneInventario = false,
                    message = $"Error al verificar inventario: {ex.Message}" 
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarReporteDiario(
            decimal nomina, 
            decimal arriendo, 
            decimal bolsa, 
            decimal otros,
            decimal transferencias,
            int totalVentas = 0,
            decimal totalRecaudado = 0)
        {
            try
            {
                // Validar sesión activa
                if (string.IsNullOrEmpty(HttpContext.Session.GetString("Usuario")))
                {
                    return Json(new { success = false, message = "Sesión expirada. Por favor, inicie sesión nuevamente." });
                }

                // Obtener datos de la sesión
                int? localId = HttpContext.Session.GetInt32("LocalId");

                if (!localId.HasValue)
                {
                    return Json(new { success = false, message = "No se pudo identificar el local." });
                }

                // *** VALIDACIÓN CRÍTICA: Verificar inventario antes de registrar salidas ***
                if (totalVentas > 0)
                {
                    bool inventarioValido = await ValidarInventarioParaSalidas(localId.Value, totalVentas);
                    if (!inventarioValido)
                    {
                        return Json(new { 
                            success = false, 
                            message = "❌ No se puede registrar salida de mercancía porque no existe inventario inicial en este local. " +
                                     "Por favor, registre primero el inventario inicial en la sección de Inventario.",
                            bloqueado = true
                        });
                    }
                }

                // Calcular total de gastos
                decimal totalGastos = nomina + arriendo + bolsa + otros + transferencias;

                // Obtener fecha actual
                DateTime fechaActual = DateTime.Now;

                // Conectar a la base de datos
                string connectionString = _configuration.GetConnectionString("MySQLConnection")
                    ?? throw new InvalidOperationException("Connection string 'MySQLConnection' not found.");

                using var conn = new MySqlConnection(connectionString);
                await conn.OpenAsync();

                // TRANSACCIÓN: Guardar gastos Y actualizar inventario
                using var transaction = await conn.BeginTransactionAsync();

                try
                {
                    // 1. VERIFICAR/ACTUALIZAR gasto_diario
                    string queryVerificar = @"
                        SELECT COUNT(*) 
                        FROM gasto_diario 
                        WHERE local_id = @localId 
                        AND DATE(fecha) = DATE(@fecha)";

                    using var cmdVerificar = new MySqlCommand(queryVerificar, conn, transaction);
                    cmdVerificar.Parameters.AddWithValue("@localId", localId.Value);
                    cmdVerificar.Parameters.AddWithValue("@fecha", fechaActual);

                    long count = (long)(await cmdVerificar.ExecuteScalarAsync() ?? 0L);

                    if (count > 0)
                    {
                        // ACTUALIZAR gasto_diario
                        string queryActualizar = @"
                            UPDATE gasto_diario 
                            SET nomina = @nomina,
                                arriendo = @arriendo,
                                bolsa = @bolsa,
                                otros = @otros,
                                transferencias = @transferencias
                            WHERE local_id = @localId 
                            AND DATE(fecha) = DATE(@fecha)";

                        using var cmdActualizar = new MySqlCommand(queryActualizar, conn, transaction);
                        cmdActualizar.Parameters.AddWithValue("@nomina", nomina);
                        cmdActualizar.Parameters.AddWithValue("@arriendo", arriendo);
                        cmdActualizar.Parameters.AddWithValue("@bolsa", bolsa);
                        cmdActualizar.Parameters.AddWithValue("@otros", otros);
                        cmdActualizar.Parameters.AddWithValue("@transferencias", transferencias);
                        cmdActualizar.Parameters.AddWithValue("@localId", localId.Value);
                        cmdActualizar.Parameters.AddWithValue("@fecha", fechaActual);

                        await cmdActualizar.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        // INSERTAR gasto_diario
                        string queryInsertar = @"
                            INSERT INTO gasto_diario 
                            (local_id, fecha, nomina, arriendo, bolsa, otros, transferencias)
                            VALUES 
                            (@localId, @fecha, @nomina, @arriendo, @bolsa, @otros, @transferencias)";

                        using var cmdInsertar = new MySqlCommand(queryInsertar, conn, transaction);
                        cmdInsertar.Parameters.AddWithValue("@localId", localId.Value);
                        cmdInsertar.Parameters.AddWithValue("@fecha", fechaActual);
                        cmdInsertar.Parameters.AddWithValue("@nomina", nomina);
                        cmdInsertar.Parameters.AddWithValue("@arriendo", arriendo);
                        cmdInsertar.Parameters.AddWithValue("@bolsa", bolsa);
                        cmdInsertar.Parameters.AddWithValue("@otros", otros);
                        cmdInsertar.Parameters.AddWithValue("@transferencias", transferencias);

                        await cmdInsertar.ExecuteNonQueryAsync();
                    }

                    // 2. REGISTRAR LAS VENTAS EN INVENTARIO_DIARIO (solo si hay ventas)
                    if (totalVentas > 0)
                    {
                        // Obtener inventario del día anterior o el último disponible
                        var inventarioInicialData = await ObtenerInventarioInicial(conn, transaction, localId.Value, fechaActual);
                        
                        int inventarioInicial = inventarioInicialData.invInicial;
                        DateTime? fechaInventarioAnterior = inventarioInicialData.fechaAnterior;

                        // Verificar si ya existe un registro de inventario para hoy
                        string queryVerificarInventario = @"
                            SELECT COUNT(*), COALESCE(MAX(id), 0) 
                            FROM inventario_diario 
                            WHERE local_id = @localId 
                            AND DATE(fecha) = DATE(@fecha)";

                        using var cmdVerificarInventario = new MySqlCommand(queryVerificarInventario, conn, transaction);
                        cmdVerificarInventario.Parameters.AddWithValue("@localId", localId.Value);
                        cmdVerificarInventario.Parameters.AddWithValue("@fecha", fechaActual);

                        using var readerVerificar = await cmdVerificarInventario.ExecuteReaderAsync();
                        bool existeInventarioHoy = false;
                        int idInventarioHoy = 0;
                        
                        if (await readerVerificar.ReadAsync())
                        {
                            existeInventarioHoy = readerVerificar.GetInt64(0) > 0;
                            idInventarioHoy = readerVerificar.GetInt32(1);
                        }
                        await readerVerificar.CloseAsync();

                        if (existeInventarioHoy && idInventarioHoy > 0)
                        {
                            // Actualizar inventario existente (sumar las ventas)
                            string queryActualizarInventario = @"
                                UPDATE inventario_diario 
                                SET salida = salida + @salida,
                                    inv_final = inv_inicial + entrada - (salida + @salida)
                                WHERE id = @id";

                            using var cmdActualizarInventario = new MySqlCommand(queryActualizarInventario, conn, transaction);
                            cmdActualizarInventario.Parameters.AddWithValue("@salida", totalVentas);
                            cmdActualizarInventario.Parameters.AddWithValue("@id", idInventarioHoy);

                            await cmdActualizarInventario.ExecuteNonQueryAsync();
                        }
                        else
                        {
                            // Insertar nuevo registro de inventario para hoy
                            int inventarioFinal = inventarioInicial - totalVentas;
                            
                            string queryInsertarInventario = @"
                                INSERT INTO inventario_diario 
                                (fecha, local_id, inv_inicial, entrada, salida, inv_final)
                                VALUES 
                                (@fecha, @localId, @inv_inicial, @entrada, @salida, @inv_final)";

                            using var cmdInsertarInventario = new MySqlCommand(queryInsertarInventario, conn, transaction);
                            cmdInsertarInventario.Parameters.AddWithValue("@fecha", fechaActual);
                            cmdInsertarInventario.Parameters.AddWithValue("@localId", localId.Value);
                            cmdInsertarInventario.Parameters.AddWithValue("@inv_inicial", inventarioInicial);
                            cmdInsertarInventario.Parameters.AddWithValue("@entrada", 0);
                            cmdInsertarInventario.Parameters.AddWithValue("@salida", totalVentas);
                            cmdInsertarInventario.Parameters.AddWithValue("@inv_final", inventarioFinal > 0 ? inventarioFinal : 0);

                            await cmdInsertarInventario.ExecuteNonQueryAsync();
                        }
                    }

                    // Confirmar transacción
                    await transaction.CommitAsync();

                    return Json(new { 
                        success = true, 
                        message = "✅ Reporte guardado exitosamente.",
                        totalGastos = totalGastos.ToString("N0", new CultureInfo("es-CO")),
                        totalVentas = totalVentas,
                        totalRecaudado = totalRecaudado.ToString("N0", new CultureInfo("es-CO")),
                        gananciaNeta = (totalRecaudado - totalGastos).ToString("N0", new CultureInfo("es-CO")),
                        clearLocalStorage = true
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al guardar reporte: {ex.Message}");
                return Json(new { 
                    success = false, 
                    message = $"❌ Error al guardar el reporte: {ex.Message}" 
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarVentas([FromBody] JsonElement data)
        {
            try
            {
                if (string.IsNullOrEmpty(HttpContext.Session.GetString("Usuario")))
                {
                    return Json(new { success = false, message = "Sesión expirada" });
                }

                int? localId = HttpContext.Session.GetInt32("LocalId");
                if (!localId.HasValue)
                {
                    return Json(new { success = false, message = "Local no identificado" });
                }

                if (!data.TryGetProperty("totalVentas", out var totalVentasProp) ||
                    !data.TryGetProperty("totalRecaudado", out var totalRecaudadoProp))
                {
                    return Json(new { success = false, message = "Datos de ventas incompletos" });
                }

                int totalVentas = totalVentasProp.GetInt32();
                decimal totalRecaudado = totalRecaudadoProp.GetDecimal();

                // *** VALIDAR INVENTARIO ANTES DE GUARDAR VENTAS ***
                if (totalVentas > 0)
                {
                    bool inventarioValido = await ValidarInventarioParaSalidas(localId.Value, totalVentas);
                    if (!inventarioValido)
                    {
                        return Json(new { 
                            success = false, 
                            message = "❌ No se puede guardar ventas porque no existe inventario inicial. " +
                                     "Registre primero el inventario inicial.",
                            bloqueado = true
                        });
                    }
                }

                DateTime fechaActual = DateTime.Now;
                string connectionString = _configuration.GetConnectionString("MySQLConnection")
                    ?? throw new InvalidOperationException("Connection string 'MySQLConnection' not found.");

                using var conn = new MySqlConnection(connectionString);
                await conn.OpenAsync();

                using var transaction = await conn.BeginTransactionAsync();

                try
                {
                    // Obtener inventario inicial
                    var inventarioInicialData = await ObtenerInventarioInicial(conn, transaction, localId.Value, fechaActual);
                    int inventarioInicial = inventarioInicialData.invInicial;

                    // Verificar si ya existe inventario para hoy
                    string queryVerificarInventario = @"
                        SELECT COUNT(*), COALESCE(MAX(id), 0) 
                        FROM inventario_diario 
                        WHERE local_id = @localId 
                        AND DATE(fecha) = DATE(@fecha)";

                    using var cmdVerificarInventario = new MySqlCommand(queryVerificarInventario, conn, transaction);
                    cmdVerificarInventario.Parameters.AddWithValue("@localId", localId.Value);
                    cmdVerificarInventario.Parameters.AddWithValue("@fecha", fechaActual);

                    using var reader = await cmdVerificarInventario.ExecuteReaderAsync();
                    bool existeInventarioHoy = false;
                    int idInventarioHoy = 0;
                    
                    if (await reader.ReadAsync())
                    {
                        existeInventarioHoy = reader.GetInt64(0) > 0;
                        idInventarioHoy = reader.GetInt32(1);
                    }
                    await reader.CloseAsync();

                    int inventarioFinal = inventarioInicial - totalVentas;
                    
                    if (existeInventarioHoy && idInventarioHoy > 0)
                    {
                        // Actualizar registro existente
                        string queryActualizarInventario = @"
                            UPDATE inventario_diario 
                            SET salida = salida + @salida,
                                inv_final = inv_inicial + entrada - (salida + @salida)
                            WHERE id = @id";

                        using var cmdActualizarInventario = new MySqlCommand(queryActualizarInventario, conn, transaction);
                        cmdActualizarInventario.Parameters.AddWithValue("@salida", totalVentas);
                        cmdActualizarInventario.Parameters.AddWithValue("@id", idInventarioHoy);

                        await cmdActualizarInventario.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        // Insertar nuevo registro
                        string queryInsertarInventario = @"
                            INSERT INTO inventario_diario 
                            (fecha, local_id, inv_inicial, entrada, salida, inv_final)
                            VALUES 
                            (@fecha, @localId, @inv_inicial, @entrada, @salida, @inv_final)";

                        using var cmdInsertarInventario = new MySqlCommand(queryInsertarInventario, conn, transaction);
                        cmdInsertarInventario.Parameters.AddWithValue("@fecha", fechaActual);
                        cmdInsertarInventario.Parameters.AddWithValue("@localId", localId.Value);
                        cmdInsertarInventario.Parameters.AddWithValue("@inv_inicial", inventarioInicial);
                        cmdInsertarInventario.Parameters.AddWithValue("@entrada", 0);
                        cmdInsertarInventario.Parameters.AddWithValue("@salida", totalVentas);
                        cmdInsertarInventario.Parameters.AddWithValue("@inv_final", inventarioFinal > 0 ? inventarioFinal : 0);

                        await cmdInsertarInventario.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();

                    return Json(new { 
                        success = true, 
                        message = "✅ Ventas guardadas en inventario",
                        totalVentas = totalVentas,
                        totalRecaudado = totalRecaudado,
                        inventarioInicial = inventarioInicial,
                        inventarioFinal = inventarioFinal
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = $"❌ Error: {ex.Message}" 
                });
            }
        }

        // *** MÉTODOS AUXILIARES PRIVADOS ***

        private async Task<bool> ValidarInventarioParaSalidas(int localId, int cantidadSalida)
        {
            try
            {
                string connectionString = _configuration.GetConnectionString("MySQLConnection")
                    ?? throw new InvalidOperationException("Connection string 'MySQLConnection' not found.");

                using var conn = new MySqlConnection(connectionString);
                await conn.OpenAsync();

                // Verificar si existe algún registro de inventario para este local
                string queryVerificarInventario = @"
                    SELECT COUNT(*) 
                    FROM inventario_diario 
                    WHERE local_id = @localId";

                using var cmdVerificarInventario = new MySqlCommand(queryVerificarInventario, conn);
                cmdVerificarInventario.Parameters.AddWithValue("@localId", localId);

                long countInventario = (long)(await cmdVerificarInventario.ExecuteScalarAsync() ?? 0L);

                // Si no hay ningún registro de inventario, no permitir salidas
                if (countInventario == 0)
                {
                    return false;
                }

                // Obtener el último inventario para verificar disponibilidad
                string queryUltimoInventario = @"
                    SELECT inv_final 
                    FROM inventario_diario 
                    WHERE local_id = @localId 
                    ORDER BY fecha DESC, id DESC 
                    LIMIT 1";

                using var cmdUltimoInventario = new MySqlCommand(queryUltimoInventario, conn);
                cmdUltimoInventario.Parameters.AddWithValue("@localId", localId);

                var result = await cmdUltimoInventario.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    int inventarioDisponible = Convert.ToInt32(result);
                    
                    // Verificar que haya suficiente inventario disponible
                    if (inventarioDisponible < cantidadSalida)
                    {
                        // Podrías lanzar una excepción específica aquí si quieres
                        Console.WriteLine($"Inventario insuficiente: {inventarioDisponible} disponible, {cantidadSalida} requerida");
                        // Aun así permitimos continuar, pero registramos la advertencia
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ValidarInventarioParaSalidas: {ex.Message}");
                return false;
            }
        }

        private async Task<(int invInicial, DateTime? fechaAnterior)> ObtenerInventarioInicial(
            MySqlConnection conn, 
            MySqlTransaction transaction, 
            int localId, 
            DateTime fechaActual)
        {
            // Buscar el último registro de inventario antes de hoy
            string queryUltimoInventario = @"
                SELECT id, fecha, inv_final
                FROM inventario_diario 
                WHERE local_id = @localId 
                AND fecha < @fechaActual
                ORDER BY fecha DESC, id DESC 
                LIMIT 1";

            using var cmdUltimoInventario = new MySqlCommand(queryUltimoInventario, conn, transaction);
            cmdUltimoInventario.Parameters.AddWithValue("@localId", localId);
            cmdUltimoInventario.Parameters.AddWithValue("@fechaActual", fechaActual);

            using var reader = await cmdUltimoInventario.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                int invFinal = reader.GetInt32("inv_final");
                DateTime fechaAnterior = reader.GetDateTime("fecha");
                await reader.CloseAsync();
                return (invFinal, fechaAnterior);
            }
            await reader.CloseAsync();

            // Si no hay inventario anterior, buscar cualquier inventario (último registro)
            string queryCualquierInventario = @"
                SELECT inv_final
                FROM inventario_diario 
                WHERE local_id = @localId 
                ORDER BY fecha ASC, id ASC 
                LIMIT 1";

            using var cmdCualquierInventario = new MySqlCommand(queryCualquierInventario, conn, transaction);
            cmdCualquierInventario.Parameters.AddWithValue("@localId", localId);

            var result = await cmdCualquierInventario.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
            {
                int invInicial = Convert.ToInt32(result);
                return (invInicial, null);
            }

            // Si no hay ningún registro de inventario (esto no debería pasar si se validó antes)
            // Retornar un valor por defecto
            return (100, null); // Valor por defecto para nuevo local
        }
    }
}
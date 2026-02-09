using ArgusMcp.Interfaces;
using ArgusMcp.Server.Models;
using Azure.Core;
using System.Text.Json;

namespace ArgusMcp.Server;

public class McpServer
{
    private readonly IArgusEngine _dbEngine;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer(IArgusEngine dbEngine)
    {
        _dbEngine = dbEngine;
        _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    }

    public async Task RunAsync()
    {
        // El bucle infinito del DBA: Escuchar, Analizar, Responder
        while (true)
        {
            try
            {
                var line = await Console.In.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var request = JsonSerializer.Deserialize<JsonRpcMessage>(line, _jsonOptions);
                if (request == null || request.Id == null) continue;

                object result = null;

                // Enrutador de Comandos (El Switchboard)
                switch (request.Method)
                {
                    case "initialize":
                        result = HandleInitialize();
                        break;

                    case "tools/list":
                        result = HandleListTools();
                        break;

                    case "tools/call":
                        result = await HandleCallTool(request);
                        break;

                    case "notifications/initialized":
                        // Notificación sin respuesta, solo continuamos
                        continue;

                    default:
                        // 2. CORRECCIÓN: No ignoramos. Respondemos que no entendemos.
                        // Código -32601 es "Method not found" en el estándar JSON-RPC.
                        await SendErrorResponse(request.Id, -32601, $"Método no encontrado: {request.Method}");
                        continue;
                }

                // Enviamos la respuesta
                var response = new JsonRpcResponse { Id = request.Id, Result = result };
                var jsonResponse = JsonSerializer.Serialize(response, _jsonOptions);
                Console.WriteLine(jsonResponse);
            }
            catch (Exception ex)
            {
                // En producción, aquí escribiríamos en un log de error (stderr), nunca en stdout
                Console.Error.WriteLine($"[ERROR FATAL]: {ex.Message}");

            }
        }
    }

    private object HandleInitialize()
    {
        return new
        {
            protocolVersion = "2024-11-05",
            serverInfo = new { name = "ArgusMcp", version = "1.0.0" },
            capabilities = new { tools = new { } }
        };
    }

    private object HandleListTools()
    {
        return new
        {
            tools = new List<McpTool>
            {
            new McpTool
            {
                Name = "list_tables",
                Description = "Lista todas las tablas base de la base de datos para entender el esquema.",
                InputSchema = new { type = "object", properties = new { } } // Sin parámetros
            },
            new McpTool
            {
                Name = "get_table_structure",
                Description = "Obtiene las columnas y tipos de datos de una tabla específica.",
                InputSchema = new
                {
                    type = "object",
                    properties = new { tableName = new { type = "string" } },
                    required = new[] { "tableName" }
                }
            },
            new McpTool
            {
                Name = "execute_safe_query",
                Description = "Ejecuta una consulta SQL SELECT de solo lectura. Útil para ver datos reales. Máximo 50 filas.",
                InputSchema = new
                {
                    type = "object",
                    properties = new { sql = new { type = "string", description = "La consulta SELECT a ejecutar" } },
                    required = new[] { "sql" }
                }
            },
            new McpTool
            {
                Name = "get_object_definition",
                // Actualizamos la descripción para que la IA sepa que puede pedir tablas
                Description = "Obtiene el código SQL (DDL/CREATE) de una Tabla, Vista, Procedimiento Almacenado o Función.",
                InputSchema = new
                {
                    type = "object", 
                    // Le damos una pista fuerte de que use objectName
                    properties = new { objectName = new { type = "string", description = "El nombre del objeto (ej: 'Usuarios' o 'dbo.Ventas')" } },
                    required = new[] { "objectName" }
                }
            },
            new McpTool
            {
                Name = "analyze_indexes",
                Description = "Muestra los índices existentes en una tabla para evaluar rendimiento.",
                InputSchema = new
                {
                    type = "object",
                    properties = new { tableName = new { type = "string" } },
                    required = new[] { "tableName" }
                }
            }
            }
        };
    }

    private async Task<object> HandleCallTool(JsonRpcMessage request)
    {
        var paramsElement = ((JsonElement)request.Params).GetProperty("name").GetString();
        var arguments = ((JsonElement)request.Params).GetProperty("arguments");

        // Log de auditoría (para que veas en consola qué está pidiendo la IA)
        Console.Error.WriteLine($"[TOOL CALL] Herramienta: {paramsElement} | Args: {arguments}");

        if (paramsElement == "list_tables")
        {
            var tables = await _dbEngine.GetTablesAsync();
            return new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(tables) } } };
        }

        if (paramsElement == "get_table_structure")
        {
            // 1. Extracción Robusta: Buscamos el nombre en varios lugares posibles
            string tableName;
            try
            {
                // Intentamos leer 'tableName', si falla probamos 'name', luego 'table', luego 'objectName'
                tableName = GetStringParam(arguments, new[] { "tableName", "name", "table", "objectName" });
            }
            catch (Exception)
            {
                // Si falla todo, lanzamos error claro para que la IA sepa qué pasó
                throw new ArgumentException($"La herramienta get_table_structure requiere el parámetro 'tableName'. Recibido: {arguments}");
            }

            // 2. Log de depuración (Vital para ver qué está pasando)
            Console.Error.WriteLine($"[DEBUG] Analizando estructura de tabla: '{tableName}'");

            var structure = await _dbEngine.GetTableStructureAsync(tableName);

            // 3. Validación de respuesta vacía
            if (structure == null || !structure.Any())
            {
                return new
                {
                    content = new[] {
                    new { type = "text", text = $"No se encontraron columnas para la tabla '{tableName}'. Verifica el nombre o el esquema (ej: 'dbo.Tabla')." }
                }
                };
            }

            return new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(structure) } } };
        }

        if (paramsElement == "execute_safe_query")
        {
            var sql = GetStringParam(arguments, new[] { "sql", "query" });

            // Validación de seguridad
            QueryGuard.Validate(sql);

            // Inyección de TOP/LIMIT si falta
            if (!sql.ToUpper().Contains("TOP ") && !sql.ToUpper().Contains("LIMIT "))
            {
                sql = sql.Replace("SELECT ", "SELECT TOP 50 ", StringComparison.OrdinalIgnoreCase);
            }

            var data = await _dbEngine.ExecuteQueryAsync(sql);
            return new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(data) } } };
        }

        if (paramsElement == "get_object_definition")
        {
            // AQUÍ ESTABA EL ERROR: Ahora aceptamos tableName también porque la IA a veces se confunde
            var objName = GetStringParam(arguments, new[] { "objectName", "tableName", "name" });

            var def = await _dbEngine.GetObjectDefinitionAsync(objName);
            return new { content = new[] { new { type = "text", text = def } } };
        }

        if (paramsElement == "analyze_indexes")
        {
            var tblName = GetStringParam(arguments, new[] { "tableName", "objectName" });
            var idx = await _dbEngine.GetIndexesAsync(tblName);
            return new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(idx) } } };
        }

        throw new Exception($"Herramienta '{paramsElement}' no encontrada.");
    }

    private async Task SendErrorResponse(object id, int code, string message)
    {
        var errorResponse = new
        {
            jsonrpc = "2.0",
            id = id,
            error = new { code = code, message = message }
        };
        var json = JsonSerializer.Serialize(errorResponse, _jsonOptions);
        Console.WriteLine(json);
    }

    // Helper para extraer parámetros de forma segura y flexible
    private string GetStringParam(JsonElement args, string[] possibleNames)
    {
        foreach (var name in possibleNames)
        {
            if (args.TryGetProperty(name, out var element))
            {
                return element.GetString();
            }
        }
        // Si no encontramos nada, lanzamos error controlado
        throw new ArgumentException($"Falta el parámetro requerido. Se esperaba uno de: {string.Join(", ", possibleNames)}");
    }
}
using ArgusMcp.Engine;
using ArgusMcp.Interfaces;
using ArgusMcp.Server;

// 1. Lógica de "Busca la llave"
// Prioridad 1: Argumento de línea de comandos (ej: dotnet run -- "Server=...")
// Prioridad 2: Variable de entorno (ARGUS_CONNECTION_STRING)
var connectionString = args.Length > 0
    ? args[0]
    : Environment.GetEnvironmentVariable("ARGUS_CONNECTION_STRING");

var dbEngineType = Environment.GetEnvironmentVariable("ARGUS_DB_ENGINE") ?? "sqlserver"; // Default a SQL Server

// Validación de seguridad del DBA
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("[ERROR FATAL] No se encontró una cadena de conexión.");
    Console.Error.WriteLine("Uso: Configura la variable de entorno 'ARGUS_CONNECTION_STRING' o pasa la cadena como argumento.");
    Environment.Exit(1); // Salimos con error
    return;
}

// 2. Selección del Motor
IArgusEngine engine;
if (dbEngineType.ToLower() == "postgres")
{
    Console.Error.WriteLine("[ArgusMcp] Iniciando motor PostgreSQL...");
    engine = new PostgresEngine(connectionString);
}
else
{
    Console.Error.WriteLine("[ArgusMcp] Iniciando motor SQL Server...");
    engine = new SqlServerEngine(connectionString);
}

// 3. Inicio del Servidor
Console.Error.WriteLine("[ArgusMcp] Servidor listo. Esperando comandos JSON-RPC...");
var server = new McpServer(engine);
await server.RunAsync();
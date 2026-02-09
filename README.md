# ArgusMcp

Proyecto .NET 8 (C# 12) que expone una pequeña infraestructura para acceso a datos y servidor JSON-RPC. Contiene motores para SQL Server y PostgreSQL, modelos de esquema y un servidor MCP/JSON-RPC.

## Estructura relevante
- ArgusMcp.Engine
  - PostgresEngine.cs
  - SqlServerEngine.cs
- ArgusMcp.Server
  - Program.cs
  - McpServer.cs
  - QueryGuard.cs
- ArgusMcp.Models
  - TableDefinition.cs
  - ColumnDefinition.cs
  - IndexInfo.cs
- ArgusMcp.Interfaces
  - IArgusEngine.cs

## Requisitos
- .NET 8 SDK
- Visual Studio 2022 (o usar la CLI de .NET)

## Compilar y ejecutar (CLI)
Desde la raíz del repositorio:
- Restaurar paquetes y compilar:
  dotnet restore
  dotnet build -c Debug
- Ejecutar el servidor (ajusta la ruta del proyecto si es necesario):
  dotnet run --project ./ArgusMcp.Server/ArgusMcp.Server.csproj

## Ejecutar en Visual Studio 2022
1. Abrir la solución en Visual Studio.
2. En __Solution Explorer__, establecer `ArgusMcp.Server` como proyecto de inicio: clic derecho > __Set as Startup Project__.
3. Ejecutar con __Debug > Start Debugging__ o __Ctrl+F5__ para ejecutar sin depuración.

## Publicar
Publicar en Release:
dotnet publish ./ArgusMcp.Server/ArgusMcp.Server.csproj -c Release -o ./publish

## Notas
- El proyecto utiliza Dapper en modelos (por ejemplo, constructor vacío en ColumnDefinition) — mantener constructores públicos sin parámetros si se usa Dapper.
- Verifica cadenas de conexión y políticas de seguridad antes de exponer el servidor en producción.
- Si necesitas ejemplos de solicitudes JSON-RPC, indica el método y te proporciono ejemplos concretos.

## Contacto
Para ajustes específicos del despliegue o integración con CI/CD, proporcionar el objetivo deseado (contenedor, Windows Service, Linux systemd, Azure, etc.).

## Configuracion MCP
'''
{
    "mcpServers": {
        "ArgusMcp": {
            "command": "C:\\Users\\Alejandro\\Documents\\ArgusMcp\\ArgusMcp\\bin\\Debug\\net8.0\\ArgusMcp.exe",
            "args": [
                "Server=localhost;Database=NombreBaseDeDatos;Trusted_Connection=True;TrustServerCertificate=True;",
                "sqlserver" // "postgres"
            ]
        }
    }
}
'''
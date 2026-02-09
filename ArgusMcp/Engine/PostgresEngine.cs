using ArgusMcp.Interfaces;
using ArgusMcp.Models;
using Dapper;
using Npgsql;
using System.Data;

namespace ArgusMcp.Engine
{
    public class PostgresEngine : IArgusEngine
    {
        private readonly string _connectionString;

        public PostgresEngine(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

        public async Task<IEnumerable<TableDefinition>> GetTablesAsync()
        {
            using var conn = CreateConnection();
            // Postgres: Todo está en minúsculas en information_schema
            var sql = @"
            SELECT table_schema as Schema, table_name as Name 
            FROM information_schema.tables 
            WHERE table_type = 'BASE TABLE' 
            AND table_schema NOT IN ('pg_catalog', 'information_schema')";

            return await conn.QueryAsync<TableDefinition>(sql);
        }

        public async Task<IEnumerable<ColumnDefinition>> GetTableStructureAsync(string tableName)
        {
            using var conn = CreateConnection();
            // En Postgres a veces es necesario castear booleanos explícitamente si Dapper se queja
            var sql = @"
            SELECT column_name as Name, data_type as DataType, 
                   (is_nullable = 'YES') as IsNullable
            FROM information_schema.columns
            WHERE table_name = @tableName";

            return await conn.QueryAsync<ColumnDefinition>(sql, new { tableName });
        }

        // 1. Ejecución Segura
        public async Task<IEnumerable<dynamic>> ExecuteQueryAsync(string sql)
        {
            using var conn = CreateConnection();
            // Dapper maneja dinámicos en Postgres igual de bien
            return await conn.QueryAsync(sql);
        }

        // 2. Ingeniería Inversa (La magia de Postgres)
        public async Task<string> GetObjectDefinitionAsync(string objectName)
        {
            using var conn = CreateConnection();

            // En Postgres, las Vistas están en pg_class y las Funciones en pg_proc.
            // Hacemos una consulta híbrida para buscar en ambos lados.
            var sql = @"
            -- Primero buscamos si es una VISTA
            SELECT 'VIEW' as Type, pg_get_viewdef(c.oid, true) as Definition
            FROM pg_class c 
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relname = @objectName AND n.nspname = 'public' -- Asumimos public por defecto
            
            UNION ALL
            
            -- Si no, buscamos si es una FUNCIÓN/PROCEDIMIENTO
            SELECT 'FUNCTION' as Type, pg_get_functiondef(p.oid) as Definition
            FROM pg_proc p 
            JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE p.proname = @objectName AND n.nspname = 'public'";

            var result = await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { objectName });

            if (result == null)
                return "Objeto no encontrado en el esquema 'public' o no es una Vista/Función.";

            return $"-- Definición de {result.Type} para {objectName}:\n{result.Definition}";
        }

        // 3. Salud de Índices (Consulta de alto nivel)
        public async Task<IEnumerable<IndexInfo>> GetIndexesAsync(string tableName)
        {
            using var conn = CreateConnection();

            // Esta consulta cruza pg_index (metadata) con pg_attribute (columnas)
            // Usamos string_agg para concatenar las columnas del índice en orden.
            var sql = @"
            SELECT 
                i.relname as ""IndexName"",
                string_agg(a.attname, ', ' ORDER BY array_position(ix.indkey, a.attnum)) as ""Columns"",
                ix.indisunique as ""IsUnique"",
                ix.indisprimary as ""IsPrimaryKey""
            FROM pg_index ix
            JOIN pg_class t ON t.oid = ix.indrelid
            JOIN pg_class i ON i.oid = ix.indexrelid
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY(ix.indkey)
            WHERE t.relname = @tableName
            AND t.relkind = 'r' -- Solo tablas reales
            GROUP BY i.relname, ix.indisunique, ix.indisprimary";

            return await conn.QueryAsync<IndexInfo>(sql, new { tableName });
        }
    }
}

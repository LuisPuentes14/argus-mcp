using ArgusMcp.Interfaces;
using ArgusMcp.Models;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;


namespace ArgusMcp.Engine
{
    public class SqlServerEngine : IArgusEngine
    {
        private readonly string _connectionString;

        public SqlServerEngine(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IDbConnection CreateConnection() => new SqlConnection(_connectionString);

        public async Task<IEnumerable<TableDefinition>> GetTablesAsync()
        {
            using var conn = CreateConnection();
            // CORRECCIÓN: Usamos [Schema] y [Name] para escapar las palabras reservadas.
            var sql = @"
        SELECT TABLE_SCHEMA as [Schema], TABLE_NAME as [Name] 
        FROM INFORMATION_SCHEMA.TABLES 
        WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME <> 'sysdiagrams'";

            return await conn.QueryAsync<TableDefinition>(sql);
        }

        public async Task<IEnumerable<ColumnDefinition>> GetTableStructureAsync(string tableName)
        {
            using var conn = CreateConnection();
            // CORRECCIÓN: También escapamos [Name] y [DataType] por seguridad.
            var sql = @"
        SELECT COLUMN_NAME as [Name], DATA_TYPE as [DataType], 
               CASE WHEN IS_NULLABLE = 'YES' THEN 1 ELSE 0 END as IsNullable
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = @tableName";

            return await conn.QueryAsync<ColumnDefinition>(sql, new { tableName });
        }

        public async Task<IEnumerable<dynamic>> ExecuteQueryAsync(string sql)
        {
            using var conn = CreateConnection();
            // Dapper mapea dinámicamente cualquier resultado
            return await conn.QueryAsync(sql);
        }

        public async Task<string> GetObjectDefinitionAsync(string objectName)
        {
            using var conn = CreateConnection();
            // SQL Server tiene una función nativa mágica para esto
            var sql = "SELECT OBJECT_DEFINITION(OBJECT_ID(@objectName))";
            return await conn.QuerySingleOrDefaultAsync<string>(sql, new { objectName })
                   ?? "Objeto no encontrado o no es visible.";
        }

        public async Task<IEnumerable<IndexInfo>> GetIndexesAsync(string tableName)
        {
            using var conn = CreateConnection();
            var sql = @"
        SELECT 
            i.name AS IndexName,
            STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columns,
            i.is_unique AS IsUnique,
            i.is_primary_key AS IsPrimaryKey
        FROM sys.indexes i
        INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
        INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE i.object_id = OBJECT_ID(@tableName)
        GROUP BY i.name, i.is_unique, i.is_primary_key";

            return await conn.QueryAsync<IndexInfo>(sql, new { tableName });
        }
    }
}

using ArgusMcp.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArgusMcp.Interfaces
{
    public interface IArgusEngine
    {
        // 1. Lo Básico: Conectarse
        IDbConnection CreateConnection();

        // 2. Metadatos (La Visión de Argus): ¿Qué tablas existen?
        // Devuelve una lista de nombres de tablas y sus esquemas.
        Task<IEnumerable<TableDefinition>> GetTablesAsync();

        // 3. Estructura (El Detalle): ¿Qué columnas tiene esta tabla?
        Task<IEnumerable<ColumnDefinition>> GetTableStructureAsync(string tableName);

        // 1. Ejecución Segura: Devuelve una lista de diccionarios (filas dinámicas)
        Task<IEnumerable<dynamic>> ExecuteQueryAsync(string sql);

        // 2. Ingeniería Inversa: Obtener el CREATE VIEW/PROCEDURE
        Task<string> GetObjectDefinitionAsync(string objectName);

        // 3. Salud de Índices: Ver si la tabla está optimizada
        Task<IEnumerable<IndexInfo>> GetIndexesAsync(string tableName);
    }
}

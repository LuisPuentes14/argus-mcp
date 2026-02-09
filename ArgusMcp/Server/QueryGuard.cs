using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArgusMcp.Server
{
    public static class QueryGuard
    {
        private static readonly string[] ForbiddenKeywords =
        {
        "DROP", "DELETE", "TRUNCATE", "UPDATE", "INSERT",
        "ALTER", "GRANT", "REVOKE", "EXEC", "MERGE"
    };

        public static void Validate(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("La consulta está vacía.");

            var upperSql = sql.ToUpperInvariant();

            // 1. Solo permitimos SELECT (y CTEs que empiezan con WITH)
            if (!upperSql.TrimStart().StartsWith("SELECT") && !upperSql.TrimStart().StartsWith("WITH"))
                throw new InvalidOperationException("Solo se permiten consultas de lectura (SELECT).");

            // 2. Buscamos palabras peligrosas
            foreach (var keyword in ForbiddenKeywords)
            {
                // Usamos bordes de palabra para evitar falsos positivos (ej: 'UPDATE_DATE' es válido, 'UPDATE' no)
                if (System.Text.RegularExpressions.Regex.IsMatch(upperSql, $@"\b{keyword}\b"))
                {
                    throw new InvalidOperationException($"La consulta contiene comandos prohibidos: {keyword}");
                }
            }
        }
    }
}


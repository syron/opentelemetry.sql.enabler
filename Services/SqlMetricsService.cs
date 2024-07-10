using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Data.SqlClient;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;

public class SqlMetricsService : IOtelMetricsService
{
    private readonly string _connectionString;
    private readonly Meter _meter;
    private Dictionary<string, object> Metrics { get; set;}

    public SqlMetricsService(string meterName, string connectionString)
    {
        _meter = new Meter(meterName);
        _connectionString = connectionString;
    }

    private double ExecuteQueryAndReturn(string key, string query, SqlConnection connection, ref Dictionary<string, object> dict) 
    {
        using var cmd = new SqlCommand(query, connection);
        var queryResult = Convert.ToDouble(cmd.ExecuteScalar());
        dict.Add(key, queryResult);
        return Convert.ToDouble(queryResult);
    }

    public async Task CollectMetricsAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, object>();

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Active connections
        ExecuteQueryAndReturn("sql.active_connections", "SELECT COUNT(*) FROM sys.dm_exec_connections", connection, ref result);
        ExecuteQueryAndReturn("sql.cpu_usage_percentage", "SELECT 100 - SystemIdle FROM (SELECT TOP 1 SystemIdle FROM sys.dm_os_sys_info) AS T", connection, ref result);
        ExecuteQueryAndReturn("sql.database_size_mb", "SELECT SUM(size * 8.0 / 1024) FROM sys.master_files WHERE database_id = DB_ID()", connection, ref result);
        ExecuteQueryAndReturn("sql.buffer_cache_hit_ratio", @"
                SELECT (a.cntr_value * 1.0 / b.cntr_value) * 100.0
                FROM sys.dm_os_performance_counters a
                JOIN sys.dm_os_performance_counters b ON a.object_name = b.object_name
                WHERE a.counter_name = 'Buffer cache hit ratio'
                AND b.counter_name = 'Buffer cache hit ratio base'
                AND a.instance_name = '_Total'", connection, ref result);
        ExecuteQueryAndReturn("sql.page_life_expectancy_seconds", @"
                SELECT cntr_value
                FROM sys.dm_os_performance_counters
                WHERE object_name = 'SQLServer:Buffer Manager'
                AND counter_name = 'Page life expectancy'", connection, ref result);
        ExecuteQueryAndReturn("sql.batch_requests_per_second", @"
                SELECT cntr_value
                FROM sys.dm_os_performance_counters
                WHERE object_name = 'SQLServer:SQL Statistics'
                AND counter_name = 'Batch Requests/sec'", connection, ref result);
        ExecuteQueryAndReturn("sql.user_connections", @"
                SELECT cntr_value
                FROM sys.dm_os_performance_counters
                WHERE object_name = 'SQLServer:General Statistics'
                AND counter_name = 'User Connections'", connection, ref result);
        ExecuteQueryAndReturn("sql.lock_waits_per_second", @"
                SELECT cntr_value
                FROM sys.dm_os_performance_counters
                WHERE object_name = 'SQLServer:Locks'
                AND counter_name = 'Lock Waits/sec'
                AND instance_name = '_Total'", connection, ref result);
        ExecuteQueryAndReturn("sql.full_scans_per_second", @"
                SELECT cntr_value
                FROM sys.dm_os_performance_counters
                WHERE object_name = 'SQLServer:Access Methods'
                AND counter_name = 'Full Scans/sec'", connection, ref result);
        ExecuteQueryAndReturn("sql.compilations_per_second", @"
                SELECT cntr_value
                FROM sys.dm_os_performance_counters
                WHERE object_name = 'SQLServer:SQL Statistics'
                AND counter_name = 'SQL Compilations/sec'", connection, ref result);

        Metrics = result;
    }

    public Dictionary<string, object> Get() => Metrics;
}
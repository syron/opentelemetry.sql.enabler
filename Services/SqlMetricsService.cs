using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Data.SqlClient;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;

public class SqlMetricsService : IOtelMetricsService
{
    private readonly string _connectionString;
    private readonly Meter _meter;
    private Dictionary<string, object> Metrics { get; set;} = [];

    public SqlMetricsService(string meterName, string connectionString)
    {
        _meter = new Meter(meterName);
        _connectionString = connectionString;
    }

    private double ExecuteQueryAndReturn(string key, string query, SqlConnection connection, Dictionary<string, object> dict) 
    {
        using var cmd = new SqlCommand(query, connection);
        var queryResult = Convert.ToDouble(cmd.ExecuteScalar());
        dict.Add(key, queryResult);
        var result = Convert.ToDouble(queryResult);
        _meter.CreateObservableGauge("sql.full_scans_per_second", () => { return result; });
        return result;
    }

    private async Task GetDatabaseSizesAsync(SqlConnection connection, Dictionary<string, object> dict) 
    {
        string commandText = @"SELECT 
    db.name AS [DatabaseName], 
    mf.name AS [LogicalName], 
    mf.type_desc AS [FileType], 
    mf.physical_name AS [Path], 
    CAST(
        (mf.Size * 8
        ) / 1024.0 AS DECIMAL(18, 1)) AS [SizeInMB], 
    'By '+IIF(
            mf.is_percent_growth = 1, CAST(mf.growth AS VARCHAR(10))+'%', CONVERT(VARCHAR(30), CAST(
        (mf.growth * 8
        ) / 1024.0 AS DECIMAL(18, 1)))+' MB') AS [Autogrowth], 
    IIF(mf.max_size = 0, 'No growth is allowed', IIF(mf.max_size = -1, 'Unlimited', CAST(
        (
                CAST(mf.max_size AS BIGINT) * 8
        ) / 1024 AS VARCHAR(30))+' MB')) AS [MaximumSize]
FROM 
     sys.master_files AS mf
     INNER JOIN sys.databases AS db ON
            db.database_id = mf.database_id";

        using (SqlCommand dbCommand = connection.CreateCommand())
        {
                dbCommand.CommandText = commandText;
                using(SqlDataReader reader = await dbCommand.ExecuteReaderAsync())
                {
                        while(reader.Read())
                        {
                                string name = (string)reader["DatabaseName"];
                                string fileType = (string)reader["FileType"];
                                string logicalName = (string)reader["LogicalName"];
                                double size = decimal.ToDouble((decimal)reader["SizeInMB"]);

                                var key = $"sql.database_size_{name}_{logicalName}_{fileType}";
                                _meter.CreateObservableGauge(key, () => { return size; });

                                dict.Add(key, size);

                        }

                        // this advances to the next resultset 
                        reader.NextResult();
                }
        }
    }

    public async Task CollectMetricsAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, object>();

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Active connections
        ExecuteQueryAndReturn("sql.active_connections", "SELECT COUNT(*) FROM sys.dm_exec_connections", connection, result);
        ExecuteQueryAndReturn("sql.database_size_mb", "SELECT SUM(size * 8.0 / 1024) FROM sys.master_files WHERE database_id = DB_ID()", connection, result);

        // get database file sizes
        await GetDatabaseSizesAsync(connection, result);

        ExecuteQueryAndReturn("sql.buffer_cache_hit_ratio", @"
                SELECT (a.cntr_value * 1.0 / b.cntr_value) * 100.0
                FROM sys.dm_os_performance_counters a
                JOIN sys.dm_os_performance_counters b ON a.object_name = b.object_name
                WHERE a.counter_name = 'Buffer cache hit ratio'
                AND b.counter_name = 'Buffer cache hit ratio base'
                AND a.instance_name = '_Total'", connection, result);
        ExecuteQueryAndReturn("sql.page_life_expectancy_seconds", @"
                SELECT cntr_value
                FROM sys.dm_os_performance_counters
                WHERE object_name = 'SQLServer:Buffer Manager'
                AND counter_name = 'Page life expectancy'", connection, result);
        ExecuteQueryAndReturn("sql.batch_requests_per_second", @"
                SELECT cntr_value
                FROM sys.dm_os_performance_counters
                WHERE object_name = 'SQLServer:SQL Statistics'
                AND counter_name = 'Batch Requests/sec'", connection, result);
        ExecuteQueryAndReturn("sql.user_connections", @"
                SELECT cntr_value
                FROM sys.dm_os_performance_counters
                WHERE object_name = 'SQLServer:General Statistics'
                AND counter_name = 'User Connections'", connection, result);
        ExecuteQueryAndReturn("sql.lock_waits_per_second", @"
                SELECT cntr_value
                FROM sys.dm_os_performance_counters
                WHERE object_name = 'SQLServer:Locks'
                AND counter_name = 'Lock Waits/sec'
                AND instance_name = '_Total'", connection, result);
        ExecuteQueryAndReturn("sql.full_scans_per_second", @"
                SELECT cntr_value
                FROM sys.dm_os_performance_counters
                WHERE object_name = 'SQLServer:Access Methods'
                AND counter_name = 'Full Scans/sec'", connection, result);
        ExecuteQueryAndReturn("sql.compilations_per_second", @"
                SELECT cntr_value
                FROM sys.dm_os_performance_counters
                WHERE object_name = 'SQLServer:SQL Statistics'
                AND counter_name = 'SQL Compilations/sec'", connection, result);

        Metrics = result;
    }

    public Dictionary<string, object> Get() => Metrics;
}
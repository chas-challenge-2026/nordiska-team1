namespace Nordiska.Modules.Banking.Infrastructure.DbConfigs;

public class MisshapenConnectionStringException(string connectionString)
    : Exception($"Connection string is missing or invalid. String received: {connectionString}")
{
}
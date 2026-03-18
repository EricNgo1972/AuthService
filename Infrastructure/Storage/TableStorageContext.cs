using AuthService.Application.Interfaces;
using Azure.Data.Tables;

namespace AuthService.Infrastructure.Storage;

public sealed class TableStorageContext
{
    public TableStorageContext(ISecretProvider secretProvider)
    {
        SecretProvider = secretProvider;
    }

    private ISecretProvider SecretProvider { get; }
    private TableServiceClient? Client { get; set; }

    public async Task<TableClient> GetTableAsync(string tableName, CancellationToken cancellationToken = default)
    {
        Client ??= new TableServiceClient(await SecretProvider.GetSecretAsync("STORAGE_CONNECTION_STRING", cancellationToken));
        var table = Client.GetTableClient(tableName);
        await table.CreateIfNotExistsAsync(cancellationToken);
        return table;
    }
}

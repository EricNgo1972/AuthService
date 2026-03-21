using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Storage;
using Azure;

namespace AuthService.Infrastructure.Repositories;

public sealed class UserRepository(TableStorageContext storageContext) : IUserRepository
{
    private const string PartitionKey = "USER";

    public async Task<User?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.Users, cancellationToken);
        try
        {
            var result = await table.GetEntityAsync<UserTableEntity>(PartitionKey, userId, cancellationToken: cancellationToken);
            return Map(result.Value);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.Users, cancellationToken);
        await table.AddEntityAsync(Map(user), cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        var table = await storageContext.GetTableAsync(TableNames.Users, cancellationToken);
        await table.UpsertEntityAsync(Map(user), cancellationToken: cancellationToken);
    }

    private static UserTableEntity Map(User user) => new()
    {
        PartitionKey = PartitionKey,
        RowKey = user.UserId,
        DisplayName = user.DisplayName,
        Email = user.Email,
        NormalizedEmail = user.NormalizedEmail,
        PasswordHash = user.PasswordHash,
        Role = user.Role,
        IsActive = user.IsActive,
        MustChangePassword = user.MustChangePassword,
        FailedLoginCount = user.FailedLoginCount,
        LockoutUntilUtc = user.LockoutUntilUtc,
        PasswordChangedAtUtc = user.PasswordChangedAtUtc,
        CreatedAtUtc = user.CreatedAtUtc,
        UpdatedAtUtc = user.UpdatedAtUtc,
        LastLoginAtUtc = user.LastLoginAtUtc
    };

    private static User Map(UserTableEntity entity) => new()
    {
        TenantId = string.Empty,
        UserId = entity.RowKey,
        DisplayName = string.IsNullOrWhiteSpace(entity.DisplayName) ? entity.Email : entity.DisplayName,
        Email = entity.Email,
        NormalizedEmail = entity.NormalizedEmail,
        PasswordHash = entity.PasswordHash,
        Role = entity.Role,
        IsActive = entity.IsActive,
        MustChangePassword = entity.MustChangePassword,
        FailedLoginCount = entity.FailedLoginCount,
        LockoutUntilUtc = entity.LockoutUntilUtc,
        PasswordChangedAtUtc = entity.PasswordChangedAtUtc,
        CreatedAtUtc = entity.CreatedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc,
        LastLoginAtUtc = entity.LastLoginAtUtc
    };
}

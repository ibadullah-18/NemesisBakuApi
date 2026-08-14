using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;

namespace NemesisBakuApi.Helpers;

public static class DbContextTransactionExtensions
{
    public static async Task<TResult>
        ExecuteResilientTransactionAsync<TResult>(
            this AppDbContext context,
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(operation);

        var strategy =
            context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();

            await using var transaction =
                await context.Database.BeginTransactionAsync(
                    cancellationToken);

            try
            {
                var result = await operation(
                    cancellationToken);

                await transaction.CommitAsync(
                    cancellationToken);

                return result;
            }
            catch
            {
                try
                {
                    await transaction.RollbackAsync(
                        CancellationToken.None);
                }
                catch
                {
                    // Əsas xətanı rollback xətası ilə əvəz etmirik.
                }

                context.ChangeTracker.Clear();
                throw;
            }
        });
    }
}
namespace NemesisBakuApi.Services.Interfaces;

public sealed record DatabaseCleanupResult(
    IReadOnlyDictionary<string, int> DeletedByTable)
{
    public int TotalDeleted =>
        DeletedByTable.Values.Sum();
}

public interface IDatabaseCleanupService
{
    Task<DatabaseCleanupResult> CleanupAsync(
        CancellationToken cancellationToken);
}
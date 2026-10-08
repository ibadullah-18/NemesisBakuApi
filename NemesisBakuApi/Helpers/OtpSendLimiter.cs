using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

namespace NemesisBakuApi.Helpers;

public sealed class OtpSendLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> limiter = PartitionedRateLimiter.Create<string, string>(identity =>
        RateLimitPartition.GetFixedWindowLimiter(identity, _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 1, Window = TimeSpan.FromSeconds(60), QueueLimit = 0, AutoReplenishment = true }));

    public RateLimitLease Acquire(string email, string purpose) => limiter.AttemptAcquire(
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(purpose + ":" + email.Trim().ToLowerInvariant()))));
    public void Dispose() => limiter.Dispose();
}

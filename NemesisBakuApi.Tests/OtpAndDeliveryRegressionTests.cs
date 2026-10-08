using NemesisBakuApi.Helpers;
using NemesisBakuApi.Controllers;
using NemesisBakuApi.Entities;
using System.Reflection;
using Xunit;
namespace NemesisBakuApi.Tests;
public class OtpAndDeliveryRegressionTests
{
    [Fact]
    public void Different_emails_do_not_share_otp_cooldown()
    {
        using var limiter = new OtpSendLimiter();
        using var first = limiter.Acquire("one@example.test", "Register");
        using var duplicate = limiter.Acquire(" ONE@example.test ", "Register");
        using var other = limiter.Acquire("two@example.test", "Register");
        Assert.True(first.IsAcquired); Assert.False(duplicate.IsAcquired); Assert.True(other.IsAcquired);
    }
    [Theory]
    [InlineData("00:00", true)] [InlineData("23:59", true)] [InlineData("14:37", true)]
    [InlineData("24:00", false)] [InlineData("12:00-15:00", false)] [InlineData("9:00", false)]
    public void Exact_delivery_time_is_validated(string time, bool valid) => Assert.Equal(valid, DeliveryTimeRules.IsExactTime(time));
    [Theory]
    [InlineData("metro-pickup", true)] [InlineData("metro-0-1km-road", true)] [InlineData("metro-1-2km-road", true)] [InlineData("store-road", false)]
    public void Only_metro_rules_require_exact_time(string rule, bool expected) => Assert.Equal(expected, DeliveryTimeRules.RequiresExactTime(rule));
    [Fact]
    public void Courier_message_contains_date_and_exact_time()
    {
        var order = new Order { DeliveryDate = new DateTime(2026,10,9), DeliveryTimeRange = "14:37", Items = [] };
        var method = typeof(AdminOrdersController).GetMethod("BuildCourierMessage", BindingFlags.NonPublic | BindingFlags.Static)!;
        var message = (string)method.Invoke(null, [order])!;
        Assert.Contains("09.10.2026", message); Assert.Contains("14:37", message);
    }
}

namespace NemesisBakuApi.Services.Interfaces;

public interface IProductViewTracker
{
    bool TryTrackView(
        Guid productId,
        string viewerKey);
}
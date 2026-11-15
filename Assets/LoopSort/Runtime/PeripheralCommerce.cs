using System;
namespace LoopSort0
{
    // The host SDK must invoke callbacks on Unity's main thread. A purchase
    // callback carries a verified transaction ID, never a button-click result.
    public interface IPeripheralCommerceProvider
    {
        bool RewardedReady {get;}
        bool PurchasesReady {get;}
        string PriceFor(string productId);
        void ShowRewarded(string placement,Action<bool> completed);
        void Purchase(string productId,Action<string> verifiedTransaction);
    }
    public interface IPeripheralAdFeatures
    {
        string AdStatus {get;}
        void RetryAdvertising();
        bool ShowPrivacyOptions(Action<bool> completed);
        void ContinueWithInterstitial(Action completed);
    }
    public interface IPeripheralSettlementAds
    {
        void ShowSettlementInterstitial(Action<bool> completed);
    }
    public interface IPeripheralAdLifecycle
    {
        event Action<string> AdClosed;
    }
}

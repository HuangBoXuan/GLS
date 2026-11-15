using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace LoopSort.ProductIntegration
{
    public sealed partial class ProductAdvertising
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaProxy consentSuccess,consentFailure,consentDismissed;
        // Gather real UMP eligibility before initializing MAX, as in WoolRush.
        void RequestStandalonePrivacy()
        {
            if(privacyBusy)return;
            privacyBusy=true;privacyRetryAt=Time.realtimeSinceStartup+60;
            consentDismissed=new FormDismissed(code=>queue.Post(()=>
            {
                if(disposed)return;
                if(code!=0)Report("privacy form error code="+code);
                RefreshStandaloneEligibility();
            }));
            consentFailure=new ConsentFailure(code=>queue.Post(()=>
            {
                if(disposed)return;
                Report("privacy update error code="+code);
                // A failed update may still have valid cached UMP eligibility.
                RefreshStandaloneEligibility();
            }));
            consentSuccess=new ConsentSuccess(()=>queue.Post(()=>
            {
                if(disposed)return;
                RunAndroidPrivacy(activity=>
                {
                    using(var ump=new AndroidJavaClass("com.google.android.ump.UserMessagingPlatform"))
                        ump.CallStatic("loadAndShowConsentFormIfRequired",activity,consentDismissed);
                });
            }));
            RunAndroidPrivacy(activity=>
            {
                using(var builder=new AndroidJavaObject("com.google.android.ump.ConsentRequestParameters$Builder"))
                using(var parameters=builder.Call<AndroidJavaObject>("build"))
                using(var ump=new AndroidJavaClass("com.google.android.ump.UserMessagingPlatform"))
                using(var info=ump.CallStatic<AndroidJavaObject>("getConsentInformation",activity))
                    info.Call("requestConsentInfoUpdate",activity,parameters,consentSuccess,consentFailure);
            });
        }
        void RunAndroidPrivacy(Action<AndroidJavaObject> operation)
        {
            try
            {
                using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    var activity=unity.GetStatic<AndroidJavaObject>("currentActivity");
                    activity.Call("runOnUiThread",new AndroidJavaRunnable(()=>
                    {
                        try {if(!disposed)operation(activity);}
                        catch {queue.Post(()=>{if(!disposed){Report("privacy native call failed; requests blocked");privacyBusy=false;initialized=false;}});}
                        finally {activity.Dispose();}
                    }));
                }
            }
            catch {Report("privacy dispatcher unavailable; requests blocked");privacyBusy=false;initialized=false;}
        }
        [Preserve] sealed class ConsentSuccess:AndroidJavaProxy
        {
            readonly Action done;
            public ConsentSuccess(Action callback):base("com.google.android.ump.ConsentInformation$OnConsentInfoUpdateSuccessListener"){done=callback;}
            [Preserve] public void onConsentInfoUpdateSuccess()=>done();
        }
        [Preserve] sealed class ConsentFailure:AndroidJavaProxy
        {
            readonly Action<int> done;
            public ConsentFailure(Action<int> callback):base("com.google.android.ump.ConsentInformation$OnConsentInfoUpdateFailureListener"){done=callback;}
            [Preserve] public void onConsentInfoUpdateFailure(AndroidJavaObject error)=>done(error==null?-1:error.Call<int>("getErrorCode"));
        }
        [Preserve] sealed class FormDismissed:AndroidJavaProxy
        {
            readonly Action<int> done;
            public FormDismissed(Action<int> callback):base("com.google.android.ump.ConsentForm$OnConsentFormDismissedListener"){done=callback;}
            [Preserve] public void onConsentFormDismissed(AndroidJavaObject error)=>done(error==null?0:error.Call<int>("getErrorCode"));
        }
#else
        void RequestStandalonePrivacy()=>RefreshStandaloneEligibility();
#endif
    }
}

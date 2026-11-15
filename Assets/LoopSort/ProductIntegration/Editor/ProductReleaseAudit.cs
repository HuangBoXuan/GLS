using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LoopSort.ProductIntegration.Editor
{
    public static class ProductReleaseAudit
    {
        [Serializable] sealed class PrivateKeys{public string reportKey,eventKey,adReviewKey;}
        [Serializable] sealed class Receipt
        {
            public string status,androidPackageName,businessGameId,moduleGameId;
            public string[] missingProductionFields;
            public bool clientInputsApplied,allAdFormatsConfigured,administrativeKeysExcluded,consentGatePassed;
            public bool productionReady,published,apkBuilt;
            public int targetApi;
        }
        public static void PrepareAndCheck()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Run the audit in an isolated batch project.");
            ProductBuildInputs.ApplyLocal();
            var config=ProductConfiguration.LoadBundled();
            if(config.businessGameId!="1006000"||config.gameId!="loopsort0"||config.androidPackageName!="com.falcogames.loopsort")
                throw new Exception("Imported product identity differs from the supplied LoopSort sheet.");
            foreach(var pair in new[]{("daily_gift","rewarded"),("level_complete","interstitial"),("home_banner","banner")})
                if(!ProductConfiguration.Supplied(config.Find(config.gameId,pair.Item1,pair.Item2)?.adUnitId))throw new Exception("Ad format mapping is missing.");
            var args=Environment.GetCommandLineArgs();int pos=Array.IndexOf(args,"-privateBackoffice");
            if(pos<0||pos+1>=args.Length)throw new Exception("Private credential audit input is missing.");
            var keys=JsonUtility.FromJson<PrivateKeys>(File.ReadAllText(args[pos+1]));
            string client=File.ReadAllText("Assets/Resources/LoopSortProduct/configuration.json");
            foreach(string key in new[]{keys.reportKey,keys.eventKey,keys.adReviewKey})
                if(string.IsNullOrWhiteSpace(key)||client.Contains(key))throw new Exception("Administrative credential was not separated from the client.");
            // Configuration/consent guards only: no native SDK, network or fake
            // authentication session is initialized by this fixture.
            var fixture=JsonUtility.FromJson<ProductConfiguration>(JsonUtility.ToJson(config));
            fixture.developmentOnly=false;fixture.registrationConfirmed=true;fixture.productId="integration-test";
            fixture.apiBaseUrl="https://example.com";fixture.cdnBaseUrl="https://example.com";
            fixture.androidPackageName=Application.identifier;
            if(fixture.MissingReleaseFields().Count!=0)throw new Exception("Complete fixture configuration did not pass validation.");
            var context=ProductHostComposition.CreateProductContext(fixture);
            if(!context.TryResolveServerGameId("loopsort0",out var serverId)||serverId!="1006000")throw new Exception("Publisher game ID mapping failed.");
            var go=new GameObject("Product audit fixture");bool blocked=false;
            try
            {
                try{go.AddComponent<ProductAdvertising>().CreateFactory(fixture,new ProductConsent());}
                catch(InvalidOperationException e){blocked=e.Message.Contains("consent");}
                if(!blocked)throw new Exception("Unresolved consent did not block SDK initialization.");
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
            string gradleRoot=Path.GetFullPath("Builds/ProductIntegration/gradle-fixture");
            string library=Path.Combine(gradleRoot,"unityLibrary");
            Directory.CreateDirectory(Path.Combine(library,"src/main"));Directory.CreateDirectory(Path.Combine(gradleRoot,"launcher"));
            File.WriteAllText(Path.Combine(library,"src/main/AndroidManifest.xml"),"<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"><application /></manifest>");
            File.WriteAllText(Path.Combine(gradleRoot,"build.gradle"),"// com.applovin.quality fixture\n");
            File.WriteAllText(Path.Combine(gradleRoot,"launcher/build.gradle"),"// fixture\n");
            new ProductBuildInputs().OnPostGenerateGradleAndroidProject(library);
            if(!File.ReadAllText(Path.Combine(library,"src/main/AndroidManifest.xml")).Contains(config.adMobAndroidAppId)||
               !File.ReadAllText(Path.Combine(gradleRoot,"launcher/build.gradle")).Contains(keys.adReviewKey))
                throw new Exception("Android manifest or private Ad Review build injection failed.");
            var receipt=new Receipt{status="passed",androidPackageName=config.androidPackageName,
                businessGameId=config.businessGameId,moduleGameId=config.gameId,missingProductionFields=config.MissingReleaseFields().ToArray(),
                clientInputsApplied=true,allAdFormatsConfigured=true,administrativeKeysExcluded=true,consentGatePassed=true,
                productionReady=false,published=false,apkBuilt=false,targetApi=(int)PlayerSettings.Android.targetSdkVersion};
            Directory.CreateDirectory("Builds/ProductIntegration");
            File.WriteAllText("Builds/ProductIntegration/config-readiness.json",JsonUtility.ToJson(receipt,true)+"\n");
            AssetDatabase.SaveAssets();
            Debug.Log("Product input audit passed. Release remains blocked by the recorded missing production inputs.");
        }
    }
}

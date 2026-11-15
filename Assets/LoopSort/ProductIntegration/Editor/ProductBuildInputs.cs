using System;
using System.IO;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace LoopSort.ProductIntegration.Editor
{
    public sealed class ProductBuildInputs : IPostGenerateGradleAndroidProject
    {
        const string Input="ProjectSettings/LoopSortProduct.local.json";
        const string Client="Assets/Resources/LoopSortProduct/configuration.json";
        [Serializable] sealed class PrivateBuildInputs{public string adReviewKey;}
        public int callbackOrder=>int.MaxValue-1;
        [MenuItem("LoopSort/产品接入/同步本机客户端参数")]
        public static void ApplyLocal()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before applying product inputs.");
            if(!File.Exists(Input))throw new BuildFailedException("Product parameter input is missing.");
            var config=JsonUtility.FromJson<ProductConfiguration>(File.ReadAllText(Input));
            if(config==null||!ProductConfiguration.Supplied(config.maxSdkKey)||!ProductConfiguration.Https(config.privacyPolicyUrl))
                throw new BuildFailedException("Product SDK input or privacy URL is missing.");
            // Serialize only the allowlisted client type. Report/Event/Ad Review
            // administration credentials are never copied into Resources.
            Directory.CreateDirectory(Path.GetDirectoryName(Client));
            File.WriteAllText(Client,JsonUtility.ToJson(config,true)+"\n",new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(Client,ImportAssetOptions.ForceSynchronousImport);
            AppLovinSettings.Instance.SdkKey=config.maxSdkKey;
            AppLovinSettings.Instance.AdMobAndroidAppId=config.adMobAndroidAppId;
            AppLovinSettings.Instance.QualityServiceEnabled=true;
            AppLovinSettings.Instance.SaveAsync();
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android,config.androidPackageName);
            PlayerSettings.bundleVersion=config.clientVersion;
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion=(AndroidSdkVersions)36;
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android,ScriptingImplementation.IL2CPP);
            AssetDatabase.SaveAssets();
        }
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if(!File.Exists(Input))return;
            var config=JsonUtility.FromJson<ProductConfiguration>(File.ReadAllText(Input));
            if(config==null||!ProductConfiguration.Supplied(config.adMobAndroidAppId))return;
            string manifest=Path.Combine(path,"src/main/AndroidManifest.xml");
            if(!File.Exists(manifest))throw new BuildFailedException("Generated Android manifest is missing.");
            var xml=XDocument.Load(manifest);var app=xml.Root?.Element("application");
            if(app==null)throw new BuildFailedException("Generated Android application element is missing.");
            XNamespace android="http://schemas.android.com/apk/res/android";
            const string key="com.google.android.gms.ads.APPLICATION_ID";
            XElement metadata=null;
            foreach(var item in app.Elements("meta-data"))if((string)item.Attribute(android+"name")==key){metadata=item;break;}
            if(metadata==null){metadata=new XElement("meta-data",new XAttribute(android+"name",key));app.Add(metadata);}
            metadata.SetAttributeValue(android+"value",config.adMobAndroidAppId);xml.Save(manifest);
            // Ad Review is optional and independent of basic ad playback.
            if(!AppLovinSettings.Instance.QualityServiceEnabled)return;
            string privateInput="ProjectSettings/LoopSortProductBuild.local.json";
            if(!File.Exists(privateInput))throw new BuildFailedException("Private Ad Review build input is missing.");
            string reviewKey=JsonUtility.FromJson<PrivateBuildInputs>(File.ReadAllText(privateInput))?.adReviewKey;
            if(!Regex.IsMatch(reviewKey??"",@"^[A-Za-z0-9_-]{16,256}$"))throw new BuildFailedException("Private Ad Review build key is invalid.");
            string launcher=Path.GetFullPath(Path.Combine(path,"../launcher/build.gradle"));
            string root=Path.GetFullPath(Path.Combine(path,"../build.gradle"));
            if(!File.Exists(launcher)||!File.Exists(root))throw new BuildFailedException("Generated Gradle project is incomplete.");
            string rootText=File.ReadAllText(root),text=File.ReadAllText(launcher);
            if(!rootText.Contains("com.applovin.quality"))throw new BuildFailedException("MAX Ad Review root Gradle plugin was not installed.");
            var keyPattern=new Regex(@"(?s)(applovin\s*\{.*?\bapiKey\s+)(['""])[^'""]*\2");
            if(keyPattern.IsMatch(text))text=keyPattern.Replace(text,m=>m.Groups[1].Value+"'"+reviewKey+"'");
            else text+="\napply plugin: 'applovin-quality-service'\napplovin {\n    apiKey '"+reviewKey+"'\n}\n";
            File.WriteAllText(launcher,text);
        }
    }
}

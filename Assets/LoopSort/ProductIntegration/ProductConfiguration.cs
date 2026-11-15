using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace LoopSort.ProductIntegration
{
    [Serializable] public sealed class AdPlacement
    { public string gameId, placement, format, adUnitId; }

    [Serializable] public sealed class ProductConfiguration
    {
        public bool developmentOnly = true, registrationConfirmed, maxEnabled, appsFlyerEnabled;
        public string productId, gameId, androidPackageName, apiBaseUrl, cdnBaseUrl;
        // Publisher's numeric ID is separate from the registered Lua-module ID.
        public string businessGameId, adMobAndroidAppId;
        public string clientVersion = "0.1.0", hostMode = "independent";
        public string maxSdkKey, appsFlyerDevKey, privacyPolicyUrl, termsUrl, accountDeletionUrl;
        public bool adRevenueEnabled;
        public float interstitialCooldownSeconds = 120;
        public AdPlacement[] placements = Array.Empty<AdPlacement>();
        public static ProductConfiguration LoadBundled()
        {
            var asset=Resources.Load<TextAsset>("LoopSortProduct/configuration");
            if(asset==null)throw new InvalidOperationException("Product client configuration was not prepared for this build.");
            return JsonUtility.FromJson<ProductConfiguration>(asset.text)
                ??throw new InvalidOperationException("Product client configuration is invalid.");
        }

        public static bool Supplied(string value) => !string.IsNullOrWhiteSpace(value) &&
            value != "loopsort0-development" && value != "com.local.loopsort0.development" &&
            !value.Contains("REPLACE_") && !value.Contains(".invalid") && !value.Contains("<");
        public static bool Https(string value) => Supplied(value) && Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo);
        public List<string> MissingReleaseFields()
        {
            var missing = new List<string>();
            if (developmentOnly) missing.Add("developmentOnly must be false");
            if (!registrationConfirmed) missing.Add("registrationConfirmed");
            foreach (var pair in new[] { ("gameId", gameId), ("productId", productId), ("androidPackageName", androidPackageName) })
                if (!Supplied(pair.Item2)) missing.Add(pair.Item1);
            if (!Regex.IsMatch(gameId ?? "", "^[a-z][a-z0-9_]{0,63}$")) missing.Add("gameId format");
            if (!Regex.IsMatch(clientVersion ?? "", "^[0-9]+\\.[0-9]+\\.[0-9]+$")) missing.Add("clientVersion format");
            if (hostMode != "independent" && hostMode != "hub") missing.Add("hostMode");
            foreach (var pair in new[] { ("apiBaseUrl", apiBaseUrl), ("cdnBaseUrl", cdnBaseUrl),
                ("privacyPolicyUrl", privacyPolicyUrl), ("termsUrl", termsUrl), ("accountDeletionUrl", accountDeletionUrl) })
                if (!Https(pair.Item2)) missing.Add(pair.Item1);
            if (maxEnabled && !Supplied(maxSdkKey)) missing.Add("maxSdkKey");
            if (appsFlyerEnabled && !Supplied(appsFlyerDevKey)) missing.Add("appsFlyerDevKey");
            if (maxEnabled && !Regex.IsMatch(adMobAndroidAppId??"",@"^ca-app-pub-\d+~\d+$")) missing.Add("adMobAndroidAppId");
            if (adRevenueEnabled && (!maxEnabled || !appsFlyerEnabled)) missing.Add("adRevenueEnabled requires MAX and AppsFlyer");
            if (float.IsNaN(interstitialCooldownSeconds) || float.IsInfinity(interstitialCooldownSeconds) || interstitialCooldownSeconds < 0)
                missing.Add("interstitialCooldownSeconds");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in placements ?? Array.Empty<AdPlacement>())
            {
                if (p == null) { missing.Add("null placement"); continue; }
                string id = p.gameId + "/" + p.placement + "/" + p.format;
                if (!seen.Add(id)) missing.Add("duplicate placement: " + p.placement);
                if (p.gameId != gameId || string.IsNullOrWhiteSpace(p.placement) ||
                    (p.format != "rewarded" && p.format != "interstitial" && p.format != "banner")) missing.Add("invalid placement mapping");
                if (maxEnabled && !Supplied(p.adUnitId)) missing.Add("adUnitId: " + p.placement);
            }
            if (maxEnabled && (placements == null || placements.Length == 0)) missing.Add("placements");
            return missing;
        }

        // Ad integration needs no platform registration, login, API or CDN.
        // Release builds still use the complete production gate above.
        public List<string> MissingAdIntegrationFields()
        {
            var missing=new List<string>();
            if(!maxEnabled||!Supplied(maxSdkKey))missing.Add("MAX SDK configuration");
            if(!Regex.IsMatch(gameId??"","^[a-z][a-z0-9_]{0,63}$"))missing.Add("gameId");
            if(!Regex.IsMatch(androidPackageName??"",@"^[a-zA-Z]\w*(\.[a-zA-Z]\w*){2,}$"))missing.Add("Android package");
            if(!Regex.IsMatch(adMobAndroidAppId??"",@"^ca-app-pub-\d+~\d+$"))missing.Add("AdMob application ID");
            if(!Https(privacyPolicyUrl)||!Https(termsUrl))missing.Add("legal URLs");
            if(float.IsNaN(interstitialCooldownSeconds)||float.IsInfinity(interstitialCooldownSeconds)||interstitialCooldownSeconds<0)missing.Add("interstitial cooldown");
            var aliases=new HashSet<string>(StringComparer.Ordinal);
            var formats=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var p in placements??Array.Empty<AdPlacement>())
            {
                if(p==null||p.gameId!=gameId||string.IsNullOrWhiteSpace(p.placement)||
                   (p.format!="rewarded"&&p.format!="interstitial"&&p.format!="banner")||
                   !Regex.IsMatch(p.adUnitId??"",@"^[a-fA-F0-9]{16}$"))
                {missing.Add("invalid MAX placement");continue;}
                if(!aliases.Add(p.placement+"/"+p.format))missing.Add("duplicate placement");
                if(formats.TryGetValue(p.adUnitId,out string format)&&format!=p.format)missing.Add("ad unit format conflict");
                formats[p.adUnitId]=p.format;
            }
            if(Find(gameId,"daily_gift","rewarded")==null)missing.Add("daily_gift rewarded placement");
            return missing;
        }

        public AdPlacement Find(string game, string placement, string format) =>
            Array.Find(placements ?? Array.Empty<AdPlacement>(), p => p != null && p.gameId == game && p.placement == placement && p.format == format);
    }

    // Values must come from a real CMP/age-policy coordinator, never from the template.
    public sealed class ProductConsent
    {
        public bool Complete, AllowAds, AllowMeasurement, SubjectToGdpr, DataUsage, PersonalizedAds, AdStorage, DoNotSell;
    }
}

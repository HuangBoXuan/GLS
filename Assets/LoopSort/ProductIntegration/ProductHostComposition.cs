using System;
using System.Collections.Generic;
using MiniGamePlatform.HotUpdate;
using MiniGamePlatform.SDK;
using MiniGamePlatform.SDK.Auth;
using UnityEngine;

namespace LoopSort.ProductIntegration
{
    public static class ProductHostComposition
    {
        public static ProductAdvertising Initialize(ModulePlayerBehaviour player,ProductConsent consent,PlatformSession session)
            =>Initialize(player,ProductConfiguration.LoadBundled(),consent,session);
        public static ProductContext CreateProductContext(ProductConfiguration config)
            =>new ProductContext(config.productId,config.apiBaseUrl,config.clientVersion,new[]{config.gameId},
                string.IsNullOrWhiteSpace(config.businessGameId)?null:new Dictionary<string,string>{{config.gameId,config.businessGameId}});
        // The platform supplies the authenticated session and verified catalog.
        // This never fabricates a login or bypasses server version selection.
        public static ProductAdvertising Initialize(ModulePlayerBehaviour player, ProductConfiguration config,
            ProductConsent consent, PlatformSession session)
        {
            if (player == null || config == null || consent == null) throw new ArgumentNullException();
            var missing = config.MissingReleaseFields();
            if (missing.Count != 0) throw new InvalidOperationException("Product configuration incomplete: " + string.Join(", ", missing));
            if (session == null || string.IsNullOrWhiteSpace(session.UserId)) throw new InvalidOperationException("A platform-authenticated session is required.");
            var product = CreateProductContext(config);
            var go = new GameObject("LoopSort product advertising");
            try
            {
                var advertising = go.AddComponent<ProductAdvertising>();
                var factory = advertising.CreateFactory(config, consent, session);
                player.InitializeWithHost(config.hostMode, config.gameId, product, session, factory);
                return advertising;
            }
            catch { UnityEngine.Object.Destroy(go); throw; }
        }
    }
}

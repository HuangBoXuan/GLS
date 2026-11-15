using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MiniGamePlatform.HotUpdate;
using MiniGamePlatform.SDK;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace LoopSort.PlatformLocalHost
{
    [Serializable]
    public sealed class LocalPlayerConfiguration
    {
        public string gameId, moduleVersion, manifestSha256, bundledRoot, profileId;
        public bool localOnly;
    }

    // Product shell only. Game rules, visuals and UI are loaded from the exact
    // pinned module; no LoopSort0.Runtime/Core types are referenced by this shell.
    [DisallowMultipleComponent]
    public sealed class PlatformLocalBootstrap : MonoBehaviour
    {
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        ModulePlayerBehaviour player;
        LocalPlayerConfiguration config;
        GameObject statusPanel;
        Text message;
        Button retry;
        bool activating, quitting;
        string installDirectory;
        string pendingStage;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            CreateShellUI();
            var clear = new GameObject("Local shell background camera");
            clear.transform.SetParent(transform, false);
            var camera = clear.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.285f, .149f, .243f);
            camera.cullingMask = 0; camera.depth = -100;
            clear.AddComponent<AudioListener>();
            if (EventSystem.current == null)
            {
                var events = new GameObject("Host EventSystem");
                events.transform.SetParent(transform, false);
                events.AddComponent<EventSystem>(); events.AddComponent<StandaloneInputModule>();
            }
            player = gameObject.AddComponent<ModulePlayerBehaviour>();
            player.Failed += Failed;
        }

        IEnumerator Start()
        {
            Exception error = null;
            try
            {
                var asset = Resources.Load<TextAsset>("PlatformLocal/configuration");
                if (asset == null) throw new InvalidDataException("Missing local player configuration.");
                config = JsonUtility.FromJson<LocalPlayerConfiguration>(asset.text);
                if (config == null || !config.localOnly || config.profileId != "local-only:default" ||
                    config.bundledRoot != "PlatformLocal/module" || !IsHash(config.manifestSha256))
                    throw new InvalidDataException("Invalid local-only player configuration.");
                // offline.invalid is a reserved non-service domain. No PlatformSession,
                // transport request, ad driver or cloud service is created here.
                var product = new ProductContext("loopsort0-local-only", "https://offline.invalid", "1.0.0", new[] { config.gameId });
                var factory = new OfflineLocalServiceFactory(Path.Combine(Application.persistentDataPath, "LocalOnly", "Saves"), config.profileId);
                player.InitializeWithHost("independent", config.gameId, product, null, factory);
            }
            catch (Exception exception) { error = exception; }
            if (error != null) { Failed(error); yield break; }
            yield return Activate();
        }

        IEnumerator Activate()
        {
            if (activating || quitting) yield break;
            activating = true; retry.interactable = false; statusPanel.SetActive(true);
            Exception failure = null;
            IEnumerator operation = PrepareAndOpen();
            try
            {
                while (true)
                {
                    object yielded = null; bool more = false;
                    try { more = operation.MoveNext(); if (more) yielded = operation.Current; }
                    catch (Exception error) { failure = error; }
                    if (failure != null || !more) break;
                    yield return yielded;
                }
            }
            finally
            {
                try { (operation as IDisposable)?.Dispose(); }
                catch (Exception error) { failure = failure ?? error; Debug.LogException(error); }
                activating = false;
                try { CleanupPendingStage(); }
                catch (Exception error) { failure = failure ?? error; Debug.LogException(error); }
            }
            if (failure != null) Failed(failure);
        }

        IEnumerator PrepareAndOpen()
        {
            if (config == null || player.Player == null) throw new InvalidOperationException("The local shell has not initialized.");
            player.Exit();
            float until = Time.realtimeSinceStartup + 15f;
            while (player.Player.HasPendingExit && Time.realtimeSinceStartup < until)
            { message.text = "正在清理上一次游戏资源…"; yield return null; }
            if (player.Player.HasPendingExit)
                throw new InvalidOperationException("Previous module Exit is pending. The host remains alive for cleanup; retry later.");

            byte[] manifestBytes;
            using (var request = UnityWebRequest.Get(BundledUrl("manifest.json")))
            {
                yield return request.SendWebRequest();
                RequireDownload(request); manifestBytes = request.downloadHandler.data;
            }
            if (Hash(manifestBytes) != config.manifestSha256)
                throw new InvalidDataException("Bundled manifest differs from the pinned local build.");
            var manifest = ModuleManifest.Parse(Utf8.GetString(manifestBytes));
            if (manifest.gameId != config.gameId || manifest.version != config.moduleVersion || manifest.unityVersion != Application.unityVersion)
                throw new InvalidDataException("The bundled module identity or Unity version differs from the local build.");
            string root = Path.Combine(Application.persistentDataPath, "LocalOnly", "Modules");
            Directory.CreateDirectory(root);
            installDirectory = SafeChild(root, config.manifestSha256);
            bool installed = Directory.Exists(installDirectory) && ModuleInstaller.IsValid(manifest, installDirectory, manifestBytes);
            if (!installed)
            {
                string stage = SafeChild(root, "install-" + Guid.NewGuid().ToString("N"));
                pendingStage = stage;
                Directory.CreateDirectory(stage);
                int done = 0;
                foreach (var file in manifest.files)
                {
                    string target = SafeChild(stage, file.path);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    message.text = "正在准备本地游戏资源 " + (++done) + " / " + manifest.files.Length;
                    using (var request = UnityWebRequest.Get(BundledUrl(file.path)))
                    {
                        request.downloadHandler = new DownloadHandlerFile(target) { removeFileOnAbort = true };
                        yield return request.SendWebRequest(); RequireDownload(request);
                    }
                    if (new FileInfo(target).Length != file.size || HashFile(target) != file.sha256.ToLowerInvariant())
                        throw new InvalidDataException("Bundled file verification failed: " + file.path);
                }
                File.WriteAllBytes(SafeChild(stage, "manifest.json"), manifestBytes);
                if (!ModuleInstaller.IsValid(manifest, stage, manifestBytes)) throw new InvalidDataException("Prepared module is invalid.");
                if (Directory.Exists(installDirectory))
                {
                    // Preserve an unexpectedly corrupted older copy for diagnostics.
                    string quarantine = SafeChild(root, "invalid-" + Guid.NewGuid().ToString("N"));
                    Directory.Move(installDirectory, quarantine);
                }
                Directory.Move(stage, installDirectory);
                pendingStage = null;
            }
            message.text = "正在加载本地 Lua 游戏…";
            player.enabled = true;
            player.OpenInstalled(installDirectory, config.manifestSha256, null, value => { if (message != null) message.text = value; });
            statusPanel.SetActive(false);
        }

        void CleanupPendingStage()
        {
            if (pendingStage == null) return;
            string expectedRoot = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "LocalOnly", "Modules"));
            string stage = Path.GetFullPath(pendingStage);
            if (Path.GetDirectoryName(stage) != expectedRoot || !Path.GetFileName(stage).StartsWith("install-", StringComparison.Ordinal))
                throw new InvalidDataException("Refusing to remove an unexpected extraction directory.");
            if (Directory.Exists(stage)) Directory.Delete(stage, true);
            pendingStage = null;
        }

        string BundledUrl(string relative)
        {
            if (!ModuleManifest.IsSafeRelativePath(relative)) throw new InvalidDataException("Unsafe bundled path.");
            string path = Application.streamingAssetsPath.TrimEnd('/') + "/" + config.bundledRoot + "/" + relative;
            return path.Contains("://") ? path : new Uri(Path.GetFullPath(path)).AbsoluteUri;
        }
        static void RequireDownload(UnityWebRequest request)
        { if (request.result != UnityWebRequest.Result.Success) throw new IOException("Bundled content read failed: " + request.error); }
        public static string SafeChild(string parent, string relative)
        {
            if (!ModuleManifest.IsSafeRelativePath(relative)) throw new InvalidDataException("Unsafe local module path.");
            string root = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string child = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!child.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Path escaped local module directory.");
            return child;
        }
        public static string Hash(byte[] bytes)
        { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        public static string HashFile(string path)
        { using var stream = File.OpenRead(path); using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        static bool IsHash(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value) if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
            return true;
        }
        void Failed(Exception exception)
        {
            Debug.LogException(exception); statusPanel.SetActive(true);
            message.text = "本地开发版加载未完成\n" + exception.Message + "\n\n存档已保留，尚未连接任何正式平台服务。";
            retry.interactable = config != null && player != null && player.Player != null;
            if (player != null) { player.Exit(); player.enabled = false; }
        }
        void Retry()
        { if (!activating && !quitting) StartCoroutine(Activate()); }
        void OnApplicationQuit()
        { quitting = true; player?.Exit(); }
        void OnDestroy()
        {
            if (player == null) return;
            player.Failed -= Failed; player.Exit();
            // Never replace/destroy this host to hide a pending Exit. The SDK's
            // shared owner continues cleanup while the application remains alive.
            if (player.Player != null && player.Player.HasPendingExit)
                Debug.LogWarning("Local shell destroyed with cleanup pending; SDK still owns the module VM.");
        }

        void CreateShellUI()
        {
            Font font = Resources.Load<Font>("PlatformLocal/NotoSansSC-Medium") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("Local shell status", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080,1920); scaler.matchWidthOrHeight = .5f;
            RectTransform full = Rectangle(canvasObject.transform, "Startup", Vector2.zero, Vector2.one);
            statusPanel = full.gameObject; var backdrop = statusPanel.AddComponent<Image>(); backdrop.color = new Color(1,.94f,.82f);
            RectTransform textRect = Rectangle(full,"Message",new Vector2(.08f,.3f),new Vector2(.92f,.72f));
            message = textRect.gameObject.AddComponent<Text>(); message.font = font; message.fontSize=42; message.color=Color.black; message.alignment=TextAnchor.MiddleCenter; message.supportRichText=false; message.text="正在准备本地离线游戏…";
            RectTransform retryRect=Rectangle(full,"Retry",new Vector2(.23f,.16f),new Vector2(.77f,.25f));
            var retryImage=retryRect.gameObject.AddComponent<Image>();retryImage.color=new Color(.38f,.78f,.71f);
            retry=retryRect.gameObject.AddComponent<Button>();retry.targetGraphic=retryImage;retry.onClick.AddListener(Retry);retry.interactable=false;
            var retryLabel=Rectangle(retryRect,"Label",Vector2.zero,Vector2.one).gameObject.AddComponent<Text>();retryLabel.font=font;retryLabel.fontSize=42;retryLabel.color=Color.black;retryLabel.alignment=TextAnchor.MiddleCenter;retryLabel.text="重新加载";retryLabel.raycastTarget=false;
            RectTransform badge=Rectangle(canvasObject.transform,"Local-only identity",new Vector2(.05f,.986f),new Vector2(.95f,1));
            var badgeBackground=badge.gameObject.AddComponent<Image>();badgeBackground.color=new Color(1,.94f,.82f,.9f);badgeBackground.raycastTarget=false;
            var badgeLabel=Rectangle(badge,"Label",Vector2.zero,Vector2.one).gameObject.AddComponent<Text>();badgeLabel.font=font;badgeLabel.fontSize=20;badgeLabel.color=Color.black;badgeLabel.alignment=TextAnchor.MiddleCenter;badgeLabel.text="本地离线开发版 · 未接入正式平台服务";badgeLabel.raycastTarget=false;
        }
        static RectTransform Rectangle(Transform parent,string name,Vector2 min,Vector2 max)
        {
            var go=new GameObject(name,typeof(RectTransform));var rect=(RectTransform)go.transform;rect.SetParent(parent,false);
            rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
        }
    }
}

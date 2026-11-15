using System;

namespace LoopSort0.HotUpdate
{
    [Serializable] public sealed class UpdateSettings
    {
        public string manifestUrl="",channel="production";
        public int timeoutSeconds=15,maxDownloadMegabytes=128;
    }
    [Serializable] public sealed class ContentFile
    {
        public string path,sha256;public long size;
    }
    [Serializable] public sealed class ContentManifest
    {
        public int schema=1,bridgeApi=1,versionCode;
        public string version,binaryVersion,bindingId,channel="production",platform="any";
        public ContentFile[] files;
    }
}

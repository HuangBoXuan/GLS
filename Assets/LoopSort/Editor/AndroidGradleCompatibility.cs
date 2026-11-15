using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEditor.Build;

namespace LoopSort0.Editor
{
    // Ads 24.6's WebKit requires AGP >=8.1.1. Keep the SDK intact and use the
    // existing local Android toolchain; don't modify Unity's installation/cache.
    public sealed class AndroidGradleCompatibility:IPostGenerateGradleAndroidProject
    {
        public int callbackOrder=>10000;
        public void OnPostGenerateGradleAndroidProject(string path)=>Prepare(Directory.GetParent(path).FullName);
        public static void Prepare(string project)
        {
            string file=Path.Combine(project,"build.gradle");
            string text=File.ReadAllText(file);
            text=Regex.Replace(text,"(id 'com\\.android\\.(?:application|library)' version )'[^']+'","$1'8.10.1'");
            File.WriteAllText(file,text);
            file=Path.Combine(project,"gradle.properties");text=File.ReadAllText(file);
            text=Regex.Replace(text,@"(?m)^android\.bundle\.enableUncompressedNativeLibs=.*\r?\n?","");
            if(!text.Contains("org.gradle.workers.max="))text+="\norg.gradle.workers.max=4\n";
            File.WriteAllText(file,text);
            // AGP's default NDK differs from Unity's IL2CPP toolchain. Pin the
            // revision that belongs to the exported ndkPath, rather than asking
            // AGP to auto-select or download an incompatible native toolchain.
            file=Path.Combine(project,"unityLibrary/build.gradle");
            text=File.ReadAllText(file);
            var ndk=Regex.Match(text,@"ndkPath\s+""([^""]+)""");
            if(ndk.Success)
            {
                string properties=Path.Combine(ndk.Groups[1].Value,"source.properties");
                if(!File.Exists(properties))throw new BuildFailedException("Unity's configured NDK is missing source.properties.");
                var revision=Regex.Match(File.ReadAllText(properties),@"Pkg\.Revision\s*=\s*([0-9.]+)");
                if(!revision.Success)throw new BuildFailedException("Cannot read the configured Unity NDK revision.");
                string setting="ndkVersion \""+revision.Groups[1].Value+"\"";
                if(Regex.IsMatch(text,@"ndkVersion\s+""[^""]+"""))
                    text=Regex.Replace(text,@"ndkVersion\s+""[^""]+""",setting);
                else text=text.Replace(ndk.Value,ndk.Value+"\n    "+setting);
                File.WriteAllText(file,text);
            }
        }
        public static void Package(string project,string output,bool debug)
        {
            Prepare(project);
            string tools=Environment.GetEnvironmentVariable("GEMLOOPSORT_ANDROID_TOOLCHAIN");
            if(string.IsNullOrEmpty(tools))tools=Path.GetFullPath("../../../BuildTemp/LoopSort-ad-toolchain");
            string jdk=Path.Combine(tools,"jdk");
            string java=Directory.Exists(jdk)?Directory.GetDirectories(jdk).Select(d=>Path.Combine(d,"bin/java.exe")).FirstOrDefault(File.Exists):null;
            string launcher=Path.Combine(tools,"gradle/gradle-8.11.1/lib/gradle-gradle-cli-main-8.11.1.jar");
            if(java==null||!File.Exists(launcher))throw new BuildFailedException("Set GEMLOOPSORT_ANDROID_TOOLCHAIN to a toolchain containing JDK 17 and Gradle 8.11.1.");
            string log=Path.Combine(project,"packaging.log"),variant=debug?"Debug":"Release";
            var start=new System.Diagnostics.ProcessStartInfo(java,"-classpath \""+launcher+"\" org.gradle.launcher.GradleMain --no-daemon --max-workers=4 :launcher:assemble"+variant) {
                WorkingDirectory=project,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true
            };
            using(var stream=File.CreateText(log))using(var process=new System.Diagnostics.Process {StartInfo=start})
            {
                object gate=new object();
                process.OutputDataReceived+=(sender,e)=>{if(e.Data!=null)lock(gate)stream.WriteLine(e.Data);};
                process.ErrorDataReceived+=(sender,e)=>{if(e.Data!=null)lock(gate)stream.WriteLine(e.Data);};
                process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();process.WaitForExit();
                if(process.ExitCode!=0)throw new BuildFailedException("Android packaging failed; see "+log);
            }
            string apk=Path.Combine(project,"launcher/build/outputs/apk/"+variant.ToLowerInvariant()+"/launcher-"+variant.ToLowerInvariant()+".apk");
            File.Copy(apk,output,true);
        }
    }
}

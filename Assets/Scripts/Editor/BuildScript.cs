using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Noctyra.EditorTools
{
    /// <summary>
    /// Windows derlemesi + release zip'i üretir.
    ///   Menü:  Noctyra > Build > Windows + Zip
    ///   CLI :  Unity -batchmode -quit -projectPath . -executeMethod Noctyra.EditorTools.BuildScript.BuildWindows
    /// Çıktı: Builds/Noctyra-win64.zip  (GitHub Release'e bu yüklenir)
    /// </summary>
    public static class BuildScript
    {
        const string OutDir = "Builds/Windows";
        const string ZipPath = "Builds/Noctyra-win64.zip";

        [MenuItem("Noctyra/Build/Windows + Zip")]
        public static void BuildWindows()
        {
            if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
            Directory.CreateDirectory(OutDir);

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(OutDir, "Noctyra.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("Noctyra derlemesi başarısız: " + report.summary.result);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            foreach (var dir in Directory.GetDirectories(OutDir, "*_BurstDebugInformation_DoNotShip"))
                Directory.Delete(dir, true);

            if (File.Exists(ZipPath)) File.Delete(ZipPath);
            ZipFile.CreateFromDirectory(OutDir, ZipPath, System.IO.Compression.CompressionLevel.Optimal, false);
            Debug.Log("Noctyra hazır: " + Path.GetFullPath(ZipPath));
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}

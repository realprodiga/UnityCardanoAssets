using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

public class ProjectContextExporter
{
    private static readonly string[] allowedExtensions = { ".cs", ".js", ".css", ".html", ".json", ".txt", ".jslib", ".md" };
    private const string excludeDirectory = "TextMesh Pro";
    // Generous ceiling for hand-written source/config/docs -- well above anything we've written
    // by hand (a few hundred lines of C# tops out well under this), and well below a bundled/
    // minified library like cardano-mint-sdk.bundle.js. Size-based rather than name-based on
    // purpose: it catches any future oversized generated file too, not just this one.
    private const long maxFileSizeBytes = 500 * 1024; // 500 KB

    [MenuItem("Tools/Export Project Context for AI")]
    public static void ExportProjectInfo()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../"));
        string outputPath = Path.Combine(projectRoot, "ProjectContextForAI.txt");
        
        StringBuilder sb = new StringBuilder();

        try
        {
            EditorUtility.DisplayProgressBar("Exporting Project Context", "Gathering General Info...", 0.1f);
            AppendGeneralAndPlayerSettings(sb);

            EditorUtility.DisplayProgressBar("Exporting Project Context", "Listing Packages...", 0.3f);
            AppendPackagesFolder(sb, projectRoot);

            EditorUtility.DisplayProgressBar("Exporting Project Context", "Generating Asset Tree...", 0.5f);
            AppendProjectStructure(sb, Application.dataPath);

            EditorUtility.DisplayProgressBar("Exporting Project Context", "Reading Source Code...", 0.7f);
            AppendSourceCode(sb, Application.dataPath);

            File.WriteAllText(outputPath, sb.ToString());
            Debug.Log($"[ProjectContextExporter] Successfully exported project context to:\n{outputPath}");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void AppendGeneralAndPlayerSettings(StringBuilder sb)
    {
        sb.AppendLine("=========================================");
        sb.AppendLine("1. PROJECT & PLAYER SETTINGS");
        sb.AppendLine("=========================================");
        
        // General Info
        sb.AppendLine($"Unity Version: {Application.unityVersion}");
        sb.AppendLine($"Active Build Target: {EditorUserBuildSettings.activeBuildTarget}");
        sb.AppendLine($"Company Name: {PlayerSettings.companyName}");
        sb.AppendLine($"Product Name: {PlayerSettings.productName}");
        sb.AppendLine($"Version: {PlayerSettings.bundleVersion}");
        sb.AppendLine($"Color Space: {PlayerSettings.colorSpace}");
        
        BuildTargetGroup currentGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
        sb.AppendLine($"Scripting Backend: {PlayerSettings.GetScriptingBackend(currentGroup)}");
        sb.AppendLine($"API Compatibility Level: {PlayerSettings.GetApiCompatibilityLevel(currentGroup)}");
        sb.AppendLine($"Managed Stripping Level: {PlayerSettings.GetManagedStrippingLevel(currentGroup)}");

        // WebGL Specifics
        sb.AppendLine("\n--- WebGL Specific Settings ---");
        sb.AppendLine($"WebGL Compression Format: {PlayerSettings.WebGL.compressionFormat}");
        sb.AppendLine($"WebGL Decompression Fallback: {PlayerSettings.WebGL.decompressionFallback}");
        sb.AppendLine($"WebGL Data Caching: {PlayerSettings.WebGL.dataCaching}");
        sb.AppendLine($"WebGL Exception Support: {PlayerSettings.WebGL.exceptionSupport}");
        sb.AppendLine($"WebGL Template: {PlayerSettings.WebGL.template}");
        
        // Include active scenes as they are crucial for context
        sb.AppendLine("\n--- Scenes in Build ---");
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                sb.AppendLine(scene.path);
            }
        }
        sb.AppendLine();
    }

    private static void AppendPackagesFolder(StringBuilder sb, string projectRoot)
    {
        sb.AppendLine("=========================================");
        sb.AppendLine("2. PACKAGES FOLDER CONTENTS");
        sb.AppendLine("=========================================");
        
        string packagesPath = Path.Combine(projectRoot, "Packages");
        if (Directory.Exists(packagesPath))
        {
            string[] directories = Directory.GetDirectories(packagesPath);
            foreach (string dir in directories)
            {
                sb.AppendLine($"- {Path.GetFileName(dir)}");
            }
            
            // Also grab manifest.json if it exists, as it's highly relevant to AI
            string manifestPath = Path.Combine(packagesPath, "manifest.json");
            if (File.Exists(manifestPath))
            {
                sb.AppendLine("\n--- manifest.json ---");
                sb.AppendLine(File.ReadAllText(manifestPath));
            }
        }
        else
        {
            sb.AppendLine("Packages folder not found.");
        }
        sb.AppendLine();
    }

    private static void AppendProjectStructure(StringBuilder sb, string assetsPath)
    {
        sb.AppendLine("=========================================");
        sb.AppendLine("3. ASSETS FOLDER STRUCTURE");
        sb.AppendLine("=========================================");
        
        sb.AppendLine("Assets/");
        GenerateTree(assetsPath, "", sb);
        sb.AppendLine();
    }

    private static void GenerateTree(string dirPath, string indent, StringBuilder sb)
    {
        string[] dirs = Directory.GetDirectories(dirPath);
        string[] files = Directory.GetFiles(dirPath);

        for (int i = 0; i < dirs.Length; i++)
        {
            string dirName = Path.GetFileName(dirs[i]);
            sb.AppendLine($"{indent}├── {dirName}/");
            GenerateTree(dirs[i], indent + "│   ", sb);
        }

        for (int i = 0; i < files.Length; i++)
        {
            // Skip meta files in the tree view to keep it readable
            if (files[i].EndsWith(".meta")) continue; 
            sb.AppendLine($"{indent}├── {Path.GetFileName(files[i])}");
        }
    }

    private static void AppendSourceCode(StringBuilder sb, string assetsPath)
    {
        sb.AppendLine("=========================================");
        sb.AppendLine("4. SOURCE CODE & CONFIG FILES");
        sb.AppendLine("=========================================");

        string[] allFiles = Directory.GetFiles(assetsPath, "*.*", SearchOption.AllDirectories);
        int fileCount = 0;
        int skippedCount = 0;

        foreach (string filePath in allFiles)
        {
            // Normalize path for easier comparison
            string normalizedPath = filePath.Replace("\\", "/");
            
            // Exclude TextMesh Pro
            if (normalizedPath.Contains($"/Assets/{excludeDirectory}/") || normalizedPath.Contains($"/Assets/{excludeDirectory} "))
                continue;

            string extension = Path.GetExtension(filePath).ToLower();
            if (!allowedExtensions.Contains(extension))
                continue;

            string relativePath = "Assets" + normalizedPath.Substring(assetsPath.Length);

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > maxFileSizeBytes)
            {
                sb.AppendLine($"\n// --- FILE: {relativePath} (SKIPPED -- {fileInfo.Length / 1024}KB exceeds the {maxFileSizeBytes / 1024}KB export limit; likely a bundled/generated file rather than hand-written source) ---");
                skippedCount++;
                continue;
            }

            sb.AppendLine($"\n// --- FILE: {relativePath} ---");
            try
            {
                sb.AppendLine(File.ReadAllText(filePath));
            }
            catch (System.Exception e)
            {
                sb.AppendLine($"/* Error reading file: {e.Message} */");
            }
            sb.AppendLine("// --- END FILE ---");
            fileCount++;
        }
        
        sb.AppendLine($"\n[Total Processed Files: {fileCount}, Skipped (too large): {skippedCount}]");
    }
}
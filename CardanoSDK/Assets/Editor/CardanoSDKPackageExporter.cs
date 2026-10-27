using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Publisher-only tool (lives outside Assets/CardanoSDK, so it is never shipped).
/// Validates that Assets/CardanoSDK contains no secrets, then exports:
///   - CardanoSDK_v{Version}_AssetStore.unitypackage : no Mesh JS bundle (templates load it from jsDelivr)
///   - CardanoSDK_v{Version}_GitHub.unitypackage     : everything, including the bundle and its license file
/// Output goes to <project>/PackageExports/.
/// </summary>
public static class CardanoSDKPackageExporter
{
    private const string Version = "1.0.0";
    private const string Root = "Assets/CardanoSDK";
    private const string OutDir = "PackageExports";

    [MenuItem("Tools/Cardano SDK/Publisher/Validate Package")]
    public static void ValidateMenu()
    {
        var problems = Validate();
        if (problems.Count == 0) EditorUtility.DisplayDialog("Cardano SDK", "No problems found in " + Root + ".", "OK");
        else EditorUtility.DisplayDialog("Cardano SDK - fix before exporting", string.Join("\n", problems.Take(20)), "OK");
    }

    [MenuItem("Tools/Cardano SDK/Publisher/Export Asset Store + GitHub Packages")]
    public static void ExportMenu()
    {
        var problems = Validate();
        if (problems.Count > 0)
        {
            foreach (var p in problems) Debug.LogError("[Cardano SDK Export] " + p);
            EditorUtility.DisplayDialog("Cardano SDK - export blocked", string.Join("\n", problems.Take(20)), "OK");
            return;
        }

        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(OutDir);

        string[] all = AssetDatabase.FindAssets("", new[] { Root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !AssetDatabase.IsValidFolder(p))
            .Distinct().ToArray();

        string[] assetStore = all.Where(p => !p.Contains("cardano-mint-sdk.bundle")).ToArray();

        string storePath = Path.Combine(OutDir, $"CardanoSDK_v{Version}_AssetStore.unitypackage");
        string githubPath = Path.Combine(OutDir, $"CardanoSDK_v{Version}_GitHub.unitypackage");
        AssetDatabase.ExportPackage(assetStore, storePath, ExportPackageOptions.Default);
        AssetDatabase.ExportPackage(all, githubPath, ExportPackageOptions.Default);

        Debug.Log($"[Cardano SDK Export] {storePath} ({assetStore.Length} files), {githubPath} ({all.Length} files)");
        EditorUtility.RevealInFinder(Path.GetFullPath(githubPath));
    }

    /// <summary>Returns human-readable problems; empty when the package is safe to export.</summary>
    public static List<string> Validate()
    {
        var problems = new List<string>();

        foreach (var guid in AssetDatabase.FindAssets("t:MintingWalletSecret", new[] { Root }))
            problems.Add("Wallet secret inside the package: " + AssetDatabase.GUIDToAssetPath(guid));

        // [ \t]* rather than \s*: an empty field must not match the next line's value.
        var projectId = new Regex(@"^[ \t]*ProjectId:[ \t]*\S+", RegexOptions.Multiline);
        var mnemonic = new Regex(@"^[ \t]*Mnemonic:[ \t]*\S+", RegexOptions.Multiline);
        foreach (var path in Directory.GetFiles(Root, "*.*", SearchOption.AllDirectories))
        {
            if (!(path.EndsWith(".unity") || path.EndsWith(".prefab") || path.EndsWith(".asset"))) continue;
            string text = File.ReadAllText(path);
            if (projectId.IsMatch(text)) problems.Add("Blockfrost ProjectId set in " + path.Replace('\\', '/'));
            if (mnemonic.IsMatch(text)) problems.Add("Mnemonic found in " + path.Replace('\\', '/'));
        }

        foreach (var required in new[] { Root + "/Documentation/CardanoSDK_Documentation.md", Root + "/Third-Party Notices.txt" })
            if (!File.Exists(required)) problems.Add("Missing " + required);

        foreach (var path in Directory.GetFiles(Root, "*.*", SearchOption.AllDirectories))
            if (path.Replace('\\', '/').Length > 140) problems.Add("Path too long (Asset Store limit 150): " + path);

        return problems;
    }
}

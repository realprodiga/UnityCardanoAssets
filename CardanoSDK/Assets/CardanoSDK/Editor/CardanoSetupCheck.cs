using UnityEngine;
using UnityEditor;
using System.IO;

[InitializeOnLoad]
public class CardanoSetupCheck
{
    static CardanoSetupCheck()
    {
        EditorApplication.delayCall += CheckProjectSettings;
    }

    [MenuItem("Cardano SDK/Check Project Settings")]
    public static void CheckProjectSettings()
    {
        bool issuesFound = false;
        string message = "The Cardano SDK has detected the following setup issues:\n\n";

        // 1. Check for TMP Essentials
        string tmpFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        bool missingTMP = !File.Exists(tmpFontPath);
        if (missingTMP)
        {
            message += "- TextMeshPro Essentials are missing.\n";
            issuesFound = true;
        }

        // 2. Check WebGL Compression
        bool wrongCompression = PlayerSettings.WebGL.compressionFormat != WebGLCompressionFormat.Disabled;
        if (wrongCompression)
        {
            message += "- WebGL Compression is NOT set to 'Disabled'.\n";
            issuesFound = true;
        }

        // 3. Check Template Installation (Are files in the right place?)
        // Unity REQUIRES templates to be in Assets/WebGLTemplates to be visible.
        string rootTemplateFolder = Path.Combine(Application.dataPath, "WebGLTemplates");
        bool basicMissing = !Directory.Exists(Path.Combine(rootTemplateFolder, "CardanoBasic"));
        bool responsiveMissing = !Directory.Exists(Path.Combine(rootTemplateFolder, "CardanoResponsive"));
        bool templatesNeedInstall = basicMissing || responsiveMissing;

        if (templatesNeedInstall)
        {
            message += "- Cardano WebGL Templates are not installed in the root Assets folder.\n";
            issuesFound = true;
        }

        // 4. Check Active Template Selection
        // Only relevant if they are already installed, otherwise we fix install first
        bool wrongTemplateSelection = false;
        if (!templatesNeedInstall) 
        {
            string currentTemplate = PlayerSettings.WebGL.template;
            wrongTemplateSelection = !currentTemplate.Contains("CardanoBasic") && !currentTemplate.Contains("CardanoResponsive");
            
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL && wrongTemplateSelection)
            {
                message += "- Active WebGL Template is not set to a Cardano SDK template.\n";
                issuesFound = true;
            }
        }

        if (issuesFound)
        {
            message += "\nWould you like to auto-fix these settings now?";
            
            bool fixNow = EditorUtility.DisplayDialog(
                "Cardano SDK - Project Setup",
                message,
                "Yes, Auto-Fix Everything", 
                "No, Ignore"
            );

            if (fixNow)
            {
                ApplyFixes(missingTMP, wrongCompression, templatesNeedInstall, wrongTemplateSelection);
            }
        }
    }

    static void ApplyFixes(bool fixTMP, bool fixCompression, bool installTemplates, bool fixTemplateSelection)
    {
        // 1. Fix TMP
        if (fixTMP)
        {
            EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources");
        }

        // 2. Fix Compression
        if (fixCompression)
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            Debug.Log("[Cardano SDK] WebGL Compression set to Disabled.");
        }

        // 3. Install Templates (Copy from SDK folder to Root)
        if (installTemplates)
        {
            InstallTemplates();
        }

        // 4. Set Active Template
        if (fixTemplateSelection || installTemplates)
        {
            // We set this after install to ensure Unity sees them
            PlayerSettings.WebGL.template = "PROJECT:CardanoResponsive";
            Debug.Log("[Cardano SDK] WebGL Template set to Cardano Responsive.");
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void InstallTemplates()
    {
        // Source: Where they live in your package
        string sdkPath = "Assets/CardanoSDK/WebGLTemplates";
        
        // Destination: Where Unity demands they be
        string rootPath = "Assets/WebGLTemplates";

        if (!AssetDatabase.IsValidFolder(rootPath))
        {
            AssetDatabase.CreateFolder("Assets", "WebGLTemplates");
        }

        // Copy CardanoBasic
        CopyTemplateFolder(sdkPath, rootPath, "CardanoBasic");
        // Copy CardanoResponsive
        CopyTemplateFolder(sdkPath, rootPath, "CardanoResponsive");

        AssetDatabase.Refresh();
        Debug.Log("[Cardano SDK] WebGL Templates installed to Assets/WebGLTemplates.");
    }

    static void CopyTemplateFolder(string sourceBase, string destBase, string folderName)
    {
        string src = Path.Combine(sourceBase, folderName);
        string dest = Path.Combine(destBase, folderName);

        // Only copy if source exists and dest doesn't
        if (AssetDatabase.IsValidFolder(src) && !AssetDatabase.IsValidFolder(dest))
        {
            AssetDatabase.CopyAsset(src, dest);
        }
    }
}
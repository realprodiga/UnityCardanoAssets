using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Prodiga.CardanoSDK.EditorTools
{
    /// <summary>
    /// Tools > Cardano SDK > Extract Demo Prefabs.
    ///
    /// Converts the reusable functional GameObjects in whichever demo scene is currently open into
    /// real .prefab assets under Assets/CardanoSDK/Prefabs/ -- and reconnects each scene instance to
    /// its new prefab asset (PrefabUtility.SaveAsPrefabAssetAndConnect), so the scene keeps working
    /// exactly as before but now visibly IS built from prefabs.
    ///
    /// v2: before extracting CardanoManager, this now also CONSOLIDATES it -- if BlockfrostManager
    /// and/or KoiosManager currently exist as separate root GameObjects (the shape all three
    /// *DemoSceneBuilder scripts create), they get reparented as children of CardanoManager first.
    /// The result is ONE self-contained CardanoManager.prefab a developer can drag into any scene --
    /// their own, not just a demo scene -- and have wallet-connect fully wired with no assembly
    /// required. This needed no changes to GatedAccessDemoSceneBuilder / MintingDemoSceneBuilder /
    /// AwardingDemoSceneBuilder: GameObject.Find("BlockfrostManager") / .Find("KoiosManager") search
    /// the whole hierarchy, not just root objects, so those builders' existing reuse-detection logic
    /// keeps working unchanged even after this reparents them.
    ///
    /// This exists as a separate, scene-scanning tool rather than baked into the builders on purpose:
    /// those run against an EMPTY scene and construct everything fresh, but by the time a demo scene
    /// is actually finished you've likely hand-tweaked something in it (per the project log:
    /// CheckpointZone's collider was hand-adjusted after the builder ran, and the builder script was
    /// never updated to match). Baking prefab-creation into the builder would only ever capture the
    /// just-built, pre-tweak state. Running THIS tool afterward, against whatever's actually in the
    /// open scene, captures what you really want shipped.
    ///
    /// Looks for these GameObject names and skips any that aren't present in the currently open
    /// scene -- so it's safe to run against any one of the three demo scenes without errors about
    /// the other two's objects being missing:
    ///   - CardanoManager      -> Assets/CardanoSDK/Prefabs/CardanoManager.prefab      (scene root;
    ///                            BlockfrostManager/KoiosManager folded in as children -- see above)
    ///   - GatedGate           -> Assets/CardanoSDK/Prefabs/GatedGate.prefab           (scene root)
    ///   - CheckpointZone      -> Assets/CardanoSDK/Prefabs/CheckpointZone.prefab      (scene root)
    ///   - Minter              -> Assets/CardanoSDK/Prefabs/Minter.prefab              (scene root)
    ///   - Treasury            -> Assets/CardanoSDK/Prefabs/Treasury.prefab            (scene root)
    ///   - WalletStatusPanel   -> Assets/CardanoSDK/Prefabs/WalletStatusPanel.prefab   (under Canvas)
    ///   - MintStatusPanel     -> Assets/CardanoSDK/Prefabs/MintStatusPanel.prefab     (under Canvas)
    ///   - AwardStatusPanel    -> Assets/CardanoSDK/Prefabs/AwardStatusPanel.prefab    (under Canvas)
    ///
    /// Run this once (in any one of the three demo scenes) to produce CardanoManager.prefab, then --
    /// in the OTHER two demo scenes -- delete their separate BlockfrostManager/KoiosManager/
    /// CardanoManager objects and drag in an instance of the resulting prefab instead, so all three
    /// scenes share the exact same infrastructure prefab rather than three independently-built
    /// near-duplicates. Re-running this tool afterward updates the existing prefab asset in place.
    /// </summary>
    public static class CardanoSDKPrefabExtractor
    {
        private const string PrefabFolder = "Assets/CardanoSDK/Prefabs";

        private static readonly string[] RootObjectNames =
        {
            "CardanoManager", "GatedGate", "CheckpointZone", "Minter", "Treasury"
        };

        // These live under Canvas rather than at scene root.
        private static readonly string[] CanvasChildNames =
        {
            "WalletStatusPanel", "MintStatusPanel", "AwardStatusPanel"
        };

        [MenuItem("Tools/Cardano SDK/Extract Demo Prefabs")]
        public static void ExtractFromOpenScene()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/CardanoSDK", "Prefabs");
            }

            ConsolidateWalletConnectHierarchy();

            int extracted = 0;
            var report = new System.Text.StringBuilder();

            foreach (var name in RootObjectNames)
            {
                GameObject obj = GameObject.Find(name);
                if (obj == null) continue;
                ExtractOne(obj, name, report);
                extracted++;
            }

            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                foreach (var name in CanvasChildNames)
                {
                    Transform child = canvas.transform.Find(name);
                    if (child == null) continue;
                    ExtractOne(child.gameObject, name, report);
                    extracted++;
                }
            }

            if (extracted == 0)
            {
                EditorUtility.DisplayDialog("Extract Demo Prefabs",
                    "None of the known demo GameObject names were found in the currently open scene. " +
                    "Open a built demo scene (Gated Access / Minting Tokens / Awarding Tokens) first.",
                    "OK");
                return;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Cardano SDK] Extracted/updated {extracted} prefab(s) in {PrefabFolder}:\n{report}");
        }

        // Folds standalone BlockfrostManager/KoiosManager root objects into CardanoManager as
        // children, if they aren't already. Reparenting doesn't disturb existing serialized
        // references (CardanoWalletManager.blockfrostProvider etc. keep pointing at the same
        // component instances), and GameObject.Find locates children just as well as roots, so this
        // is safe to run against an already-working scene.
        private static void ConsolidateWalletConnectHierarchy()
        {
            GameObject cardanoManager = GameObject.Find("CardanoManager");
            if (cardanoManager == null) return; // nothing to consolidate into

            ReparentIfRoot("BlockfrostManager", cardanoManager.transform);
            ReparentIfRoot("KoiosManager", cardanoManager.transform);
        }

        private static void ReparentIfRoot(string name, Transform newParent)
        {
            GameObject obj = GameObject.Find(name);
            if (obj == null) return; // not present in this scene -- fine, not every scene has both
            if (obj.transform.parent == newParent) return; // already consolidated
            if (obj.transform.parent != null) return; // parented somewhere unexpected -- leave it alone rather than guess

            Undo.SetTransformParent(obj.transform, newParent, "Consolidate " + name + " under CardanoManager");
        }

        private static void ExtractOne(GameObject obj, string name, System.Text.StringBuilder report)
        {
            string path = $"{PrefabFolder}/{name}.prefab";

            // Already connected to a prefab asset at this exact path from a previous run? Apply
            // overrides instead of re-connecting, so running this again after further tweaks updates
            // the existing asset rather than erroring or creating a "(1)" duplicate.
            var existingAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existingAsset != null && PrefabUtility.GetCorrespondingObjectFromSource(obj) == existingAsset)
            {
                PrefabUtility.ApplyPrefabInstance(obj, InteractionMode.AutomatedAction);
                report.AppendLine($"  - {name}: updated existing prefab at {path}");
                return;
            }

            PrefabUtility.SaveAsPrefabAssetAndConnect(obj, path, InteractionMode.AutomatedAction);
            report.AppendLine($"  - {name}: saved new prefab at {path}");
        }
    }
}

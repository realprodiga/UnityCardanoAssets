using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Prodiga.CardanoSDK.Demo;

namespace Prodiga.CardanoSDK.EditorTools
{
    /// <summary>
    /// Editor-only tool that builds the Gated Access demo scene end to end: ground, barrier,
    /// reward area, locked/unlocked/requirement text, a Player (with the combined
    /// PlayerController), a Main Camera, the Blockfrost/Koios/CardanoWalletManager wallet-connect
    /// setup and its UI, a CheckpointZone trigger placed in front of the barrier, and a GatedGate
    /// pre-configured with an example NFT requirement and RequireCheckpointTrigger enabled -- so the
    /// gate only reacts once the player actually walks up to it.
    ///
    /// Run it from Tools > Cardano SDK > Build Gated Access Demo Scene with an empty (or new)
    /// scene open. Everything it creates is grouped under a single Undo step, so Ctrl+Z removes
    /// it all in one go if you want to start over.
    ///
    /// Safe to run on a scene duplicated from CardanoWebGL.unity, too: if a CardanoManager
    /// already exists it's reused (along with its Blockfrost/Koios providers and whatever
    /// Project Id is already configured on it) instead of creating a duplicate, and the existing
    /// wallet-connect UI is left untouched rather than building a second one.
    /// </summary>
    public static class GatedAccessDemoSceneBuilder
    {
        private const string PlayerLayerName = "Player";

        // Fill these in with your own values before running if you want the generated GatedGate
        // to be immediately testable -- these defaults match the GingerBread Squad policy used
        // in earlier testing.
        private const string ExamplePolicyId = "ff1463bdfaf9268cafba9918a4b389723a90a21b3a4068d36b2fe405";
        private const string ExampleAssetNameHex = ""; // empty = match ANY asset under the policy

        [MenuItem("Tools/Cardano SDK/Build Gated Access Demo Scene")]
        public static void BuildScene()
        {
            if (GameObject.Find("Player") != null || GameObject.Find("GatedGate") != null)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Gated Access Demo Scene Builder",
                    "This scene already has a \"Player\" or \"GatedGate\" object. Running this again will create duplicates. Continue anyway?",
                    "Continue",
                    "Cancel");
                if (!proceed) return;
            }

            Undo.SetCurrentGroupName("Build Gated Access Demo Scene");
            int undoGroup = Undo.GetCurrentGroup();

            int playerLayer = GetOrCreateLayer(PlayerLayerName);
            LayerMask excludePlayerMask = playerLayer >= 0 ? (LayerMask)~(1 << playerLayer) : (LayerMask)~0;

            EnsureDirectionalLight();

            GameObject ground = CreateGround();
            GameObject barrier = CreateBarrier();
            CreateRewardArea();
            GameObject lockedText = CreateWorldText("LockedText", "LOCKED", Color.red, new Vector3(0f, 3.7f, 0f));
            GameObject unlockedText = CreateWorldText("UnlockedText", "UNLOCKED", Color.green, new Vector3(0f, 3.7f, 0f));
            unlockedText.SetActive(false);
            GameObject requirementText = CreateWorldText("RequirementText", "", Color.red, new Vector3(0f, 3.0f, 0f), fontSize: 5f);

            GameObject player = CreatePlayer(playerLayer);
            Camera cam = GetOrCreateMainCamera();

            PlayerController playerController = player.GetComponent<PlayerController>();
            playerController.PlayerCamera = cam;
            playerController.CameraCollisionMask = excludePlayerMask;
            playerController.GroundMask = excludePlayerMask;

            bool reusingExistingWalletSetup = GameObject.Find("CardanoManager") != null;

            BlockfrostIntegration blockfrost;
            KoiosIntegration koios;
            CardanoWalletManager walletManager;
            CreateCardanoManagers(out blockfrost, out koios, out walletManager);

            if (reusingExistingWalletSetup)
            {
                Debug.Log("[Cardano SDK] Found an existing CardanoManager -- reused it (and its Blockfrost/Koios " +
                    "providers) instead of creating new ones, and left the existing wallet-connect UI untouched.");
            }
            else
            {
                CreateWalletUI(walletManager, blockfrost);
            }

            GatedAccessController gate = CreateGate(barrier, lockedText, unlockedText, requirementText, blockfrost, koios);
            CreateCheckpoint(gate);

            Undo.CollapseUndoOperations(undoGroup);

            Selection.activeGameObject = player;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("[Cardano SDK] Gated Access demo scene built. The gate only reacts when the " +
                "player walks through CheckpointZone (RequireCheckpointTrigger is on) -- it won't " +
                "open just because the wallet is connected. Remaining manual steps: set " +
                "BlockfrostManager's Project ID, and review GatedGate's Required Policy ID / Asset " +
                "Name (pre-filled with an \"any GingerBread NFT\" example; Advanced Mode is off by " +
                "default, so that's the rule actually in effect).");
        }

        // ---------------------------------------------------------------- Environment

        private static void EnsureDirectionalLight()
        {
            foreach (var light in Object.FindObjectsOfType<Light>())
            {
                if (light.type == LightType.Directional) return;
            }

            GameObject lightObj = new GameObject("Directional Light");
            Light lightComponent = lightObj.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Undo.RegisterCreatedObjectUndo(lightObj, "Create Directional Light");
        }

        private static GameObject CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
            Undo.RegisterCreatedObjectUndo(ground, "Create Ground");
            return ground;
        }

        private static GameObject CreateBarrier()
        {
            GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrier.name = "Barrier";
            barrier.transform.position = new Vector3(0f, 1f, 0f);
            barrier.transform.localScale = new Vector3(20f, 2f, 0.3f);
            Undo.RegisterCreatedObjectUndo(barrier, "Create Barrier");
            return barrier;
        }

        private static GameObject CreateCheckpoint(GatedAccessController gate)
        {
            GameObject checkpoint = new GameObject("CheckpointZone");
            checkpoint.transform.position = new Vector3(0f, 0.01f, -3f);

            // Trigger volume sits low and tight to the pad, so the player actually has to be
            // standing on the pad to trigger it -- not just passing through the space above it.
            BoxCollider trigger = checkpoint.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(0.3f, 0.3f, 0.3f);
            trigger.center = new Vector3(0f, 0.15f, 0f);

            AccessCheckpoint checkpointScript = checkpoint.AddComponent<AccessCheckpoint>();
            checkpointScript.Gate = gate;

            // Visual ground pad, styled like RewardArea (flat, colored, no collider of its own) so the
            // checkpoint is actually visible to the player instead of being an invisible trigger volume.
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "Pad";
            pad.transform.SetParent(checkpoint.transform, false);
            pad.transform.localPosition = Vector3.zero;
            pad.transform.localScale = new Vector3(3f, 0.02f, 3f);
            Object.DestroyImmediate(pad.GetComponent<Collider>());

            Shader shader = Shader.Find("Standard");
            if (shader != null)
            {
                var material = new Material(shader) { color = new Color(0.2f, 0.6f, 1f) }; // light blue, distinct from RewardArea's yellow
                pad.GetComponent<Renderer>().sharedMaterial = material;
            }

            Undo.RegisterCreatedObjectUndo(checkpoint, "Create CheckpointZone");
            return checkpoint;
        }

        private static GameObject CreateRewardArea()
        {
            GameObject rewardArea = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rewardArea.name = "RewardArea";
            rewardArea.transform.position = new Vector3(0f, 0.01f, 3f);
            rewardArea.transform.localScale = new Vector3(3f, 0.02f, 3f);

            Object.DestroyImmediate(rewardArea.GetComponent<Collider>());

            Shader shader = Shader.Find("Standard");
            if (shader != null)
            {
                var material = new Material(shader) { color = new Color(1f, 0.85f, 0.2f) };
                rewardArea.GetComponent<Renderer>().sharedMaterial = material;
            }

            Undo.RegisterCreatedObjectUndo(rewardArea, "Create Reward Area");
            return rewardArea;
        }

        private static GameObject CreateWorldText(string name, string text, Color color, Vector3 position, float fontSize = 8f)
        {
            GameObject obj = new GameObject(name);
            obj.transform.position = position;
            obj.transform.rotation = Quaternion.identity; // faces the same way as the barrier -- readable to a player approaching from -Z

            TextMeshPro tmp = obj.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.color = color;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;

            Undo.RegisterCreatedObjectUndo(obj, "Create " + name);
            return obj;
        }

        // ---------------------------------------------------------------- Player / Camera

        private static GameObject CreatePlayer(int playerLayer)
        {
            GameObject player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 1f, -8f);
            if (playerLayer >= 0) player.layer = playerLayer;

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.zero;

            PlayerController playerController = player.AddComponent<PlayerController>();

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(player.transform, false);
            body.transform.localPosition = Vector3.zero;
            Object.DestroyImmediate(body.GetComponent<Collider>());

            playerController.HideWhenFirstPerson = new[] { body.GetComponent<Renderer>() };

            Undo.RegisterCreatedObjectUndo(player, "Create Player");
            return player;
        }

        private static Camera GetOrCreateMainCamera()
        {
            Camera cam = Camera.main;
            if (cam != null) return cam;

            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            cam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            Undo.RegisterCreatedObjectUndo(camObj, "Create Main Camera");
            return cam;
        }

        // ---------------------------------------------------------------- Cardano wallet setup

        /// <summary>
        /// Finds and reuses BlockfrostManager/KoiosManager/CardanoManager by name if they already
        /// exist in the scene (e.g. a scene duplicated from CardanoWebGL.unity, which may already
        /// have a real Blockfrost Project Id configured) rather than creating duplicates. Only
        /// creates whichever of the three is actually missing.
        /// </summary>
        private static void CreateCardanoManagers(out BlockfrostIntegration blockfrost, out KoiosIntegration koios, out CardanoWalletManager walletManager)
        {
            GameObject blockfrostObj = GameObject.Find("BlockfrostManager");
            if (blockfrostObj != null)
            {
                blockfrost = blockfrostObj.GetComponent<BlockfrostIntegration>();
            }
            else
            {
                blockfrostObj = new GameObject("BlockfrostManager");
                blockfrost = blockfrostObj.AddComponent<BlockfrostIntegration>();
                Undo.RegisterCreatedObjectUndo(blockfrostObj, "Create BlockfrostManager");
            }

            GameObject koiosObj = GameObject.Find("KoiosManager");
            if (koiosObj != null)
            {
                koios = koiosObj.GetComponent<KoiosIntegration>();
            }
            else
            {
                koiosObj = new GameObject("KoiosManager");
                koios = koiosObj.AddComponent<KoiosIntegration>();
                Undo.RegisterCreatedObjectUndo(koiosObj, "Create KoiosManager");
            }

            GameObject cardanoManagerObj = GameObject.Find("CardanoManager");
            if (cardanoManagerObj != null)
            {
                walletManager = cardanoManagerObj.GetComponent<CardanoWalletManager>();
            }
            else
            {
                cardanoManagerObj = new GameObject("CardanoManager");
                walletManager = cardanoManagerObj.AddComponent<CardanoWalletManager>();
                Undo.RegisterCreatedObjectUndo(cardanoManagerObj, "Create CardanoManager");
            }

            if (walletManager.blockfrostProvider == null) walletManager.blockfrostProvider = blockfrost;
            if (walletManager.koiosProvider == null) walletManager.koiosProvider = koios;
        }

        private static void CreateWalletUI(CardanoWalletManager walletManager, BlockfrostIntegration blockfrost)
        {
            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<EventSystem>();
                eventSystemObj.AddComponent<StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(eventSystemObj, "Create EventSystem");
            }

            // Connect button
            GameObject buttonObj = new GameObject("ConnectButton", typeof(RectTransform));
            buttonObj.transform.SetParent(canvasObj.transform, false);
            RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 40f);
            buttonRect.sizeDelta = new Vector2(180f, 50f);
            buttonObj.AddComponent<Image>().color = new Color(0f, 0.2f, 0.68f);
            Button button = buttonObj.AddComponent<Button>();

            GameObject buttonLabelObj = new GameObject("Label", typeof(RectTransform));
            buttonLabelObj.transform.SetParent(buttonObj.transform, false);
            RectTransform labelRect = buttonLabelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            TextMeshProUGUI buttonLabel = buttonLabelObj.AddComponent<TextMeshProUGUI>();
            buttonLabel.text = "Connect Wallet";
            buttonLabel.alignment = TextAlignmentOptions.Center;
            buttonLabel.color = Color.white;

            // Status text
            GameObject statusObj = new GameObject("StatusText", typeof(RectTransform));
            statusObj.transform.SetParent(canvasObj.transform, false);
            RectTransform statusRect = statusObj.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.5f, 0f);
            statusRect.anchorMax = new Vector2(0.5f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.anchoredPosition = new Vector2(0f, 10f);
            statusRect.sizeDelta = new Vector2(300f, 25f);
            TextMeshProUGUI statusText = statusObj.AddComponent<TextMeshProUGUI>();
            statusText.text = "Status: Disconnected";
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.fontSize = 18;

            walletManager.statusText = statusText;

            // Persistent listener so the wiring survives in the Inspector, same as if you'd
            // dragged it on manually via the Button's OnClick() list.
            UnityEventTools.AddPersistentListener(button.onClick, walletManager.RequestConnection);

            if (string.IsNullOrEmpty(blockfrost.ProjectId))
            {
                Debug.LogWarning("[Cardano SDK] BlockfrostManager's Project ID is empty -- set it in the Inspector before testing.");
            }
        }

        // ---------------------------------------------------------------- Gate

        private static GatedAccessController CreateGate(GameObject barrier, GameObject lockedText, GameObject unlockedText, GameObject requirementText, BlockfrostIntegration blockfrost, KoiosIntegration koios)
        {
            GameObject gateObj = new GameObject("GatedGate");
            GatedAccessController gate = gateObj.AddComponent<GatedAccessController>();
            gate.BlockfrostManager = blockfrost;
            gate.KoiosManager = koios;
            gate.Barrier = barrier;
            gate.LockedIndicator = lockedText;
            gate.UnlockedIndicator = unlockedText;
            gate.RequirementText = requirementText.GetComponent<TMP_Text>();
            gate.RequirementDescription = "Requires: any GingerBread Squad NFT";
            gate.RequireCheckpointTrigger = true; // the check happens at CheckpointZone, not the instant the wallet connects

            // Simple mode (Advanced Mode off) is what actually gates the barrier by default -- this
            // is the direct acceptance-criteria path: one required NFT, present or not.
            gate.AdvancedMode = false;
            gate.RequiredPolicyId = ExamplePolicyId;
            gate.RequiredAssetNameHex = ExampleAssetNameHex;

            // Also pre-fill Groups with the equivalent rule so it's a working example the moment
            // Advanced Mode gets switched on -- it's inert (not evaluated) until then.
            gate.Groups = new[]
            {
                new ConditionGroup
                {
                    Conditions = new[]
                    {
                        new AssetCondition
                        {
                            PolicyId = ExamplePolicyId,
                            AssetNameHex = ExampleAssetNameHex,
                            ConditionType = AssetConditionType.AssetPresent
                        }
                    }
                }
            };

            Undo.RegisterCreatedObjectUndo(gateObj, "Create GatedGate");
            return gate;
        }

        // ---------------------------------------------------------------- Layers

        /// <summary>Returns the index of an existing layer with this name, or creates one in the
        /// first free user slot (8-31) and returns that. Returns -1 if none could be created.</summary>
        private static int GetOrCreateLayer(string layerName)
        {
            for (int i = 8; i <= 31; i++)
            {
                if (LayerMask.LayerToName(i) == layerName) return i;
            }

            Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets == null || tagManagerAssets.Length == 0)
            {
                Debug.LogWarning($"[Cardano SDK] Could not access TagManager.asset to create layer '{layerName}'. " +
                    "Add it manually via Edit > Project Settings > Tags and Layers, then set it on the Player and re-run.");
                return -1;
            }

            var tagManager = new SerializedObject(tagManagerAssets[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            for (int i = 8; i <= 31; i++)
            {
                SerializedProperty layerSlot = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layerSlot.stringValue))
                {
                    layerSlot.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return i;
                }
            }

            Debug.LogWarning($"[Cardano SDK] No free layer slots (8-31) available to create '{layerName}'. Add it manually and re-run.");
            return -1;
        }
    }
}

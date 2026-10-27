using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

namespace Prodiga.CardanoSDK
{
    /// <summary>
    /// Gated Access: attach to a "gate" GameObject and set the required NFT in the Inspector to
    /// block passage until the connected wallet holds it.
    ///
    /// Two modes (toggle "Advanced Mode" in the Inspector):
    ///   - Simple (default, Advanced Mode off): the gate opens if the connected wallet holds the
    ///     single NFT specified by RequiredPolicyId / RequiredAssetNameHex.
    ///   - Advanced (Advanced Mode on): the gate is evaluated using Groups instead -- a list of
    ///     ConditionGroups. Every condition inside a group must pass (AND); access is granted if
    ///     ANY group fully passes (OR). Combined with each condition's Invert flag (NOT), this
    ///     covers any boolean combination of:
    ///       - a specific NFT being present
    ///       - any NFT under a policy being present
    ///       - a fungible asset's quantity being within (or outside, via Invert) a min/max range
    ///       - an asset's on-chain metadata matching a specific value
    ///     Groups stays available (and pre-fillable) even while Advanced Mode is off, so flipping
    ///     the toggle later doesn't require reconfiguring from scratch.
    ///
    /// Works with either Blockfrost or Koios -- if both providers are assigned, Blockfrost is
    /// checked first and Koios is used as a fallback, matching WalletDisplay.cs's pattern.
    ///
    /// Advanced Mode's MetadataMatch conditions require the "com.unity.nuget.newtonsoft-json"
    /// package (Window > Package Manager > Add package by name), since on-chain metadata has an
    /// arbitrary, collection-defined shape that Unity's built-in JsonUtility cannot parse.
    ///
    /// By default the gate reacts continuously -- Barrier/Indicators update the instant the
    /// evaluation result changes, regardless of where the player is. Set RequireCheckpointTrigger
    /// to tie that visual reaction to a specific place instead (see AccessCheckpoint), so the gate
    /// only "performs the check" when the player actually reaches it. While RequireCheckpointTrigger
    /// is on, a checkpoint the player is currently standing in also re-checks immediately if the
    /// wallet's data changes (connects, disconnects, or refreshes) -- see AccessCheckpoint -- without
    /// affecting the gate for players who aren't there.
    /// </summary>
    public class GatedAccessController : MonoBehaviour
    {
        [Header("SDK Reference")]
        [Tooltip("If both providers are assigned, Blockfrost is used as the primary source and Koios is used as a fallback.")]
        public BlockfrostIntegration BlockfrostManager;
        public KoiosIntegration KoiosManager;

        [Header("Access Rule Mode")]
        [Tooltip("Off (default): the gate checks for the single NFT specified below. On: use the custom AND/OR/NOT condition-group rules under Groups instead.")]
        public bool AdvancedMode = false;

        [Header("Simple Access Rule (used while Advanced Mode is off)")]
        [Tooltip("56-character policy ID hex of the required NFT.")]
        public string RequiredPolicyId;
        [Tooltip("Asset name in hex. Leave empty to accept ANY asset under RequiredPolicyId.")]
        public string RequiredAssetNameHex;

        [Header("Advanced Rules (used while Advanced Mode is on)")]
        [Tooltip("OR between groups, AND within a group. Ignored while Advanced Mode is off -- see the class summary for examples.")]
        public ConditionGroup[] Groups;

        [Header("Gate Objects")]
        [Tooltip("Disabled while access is granted -- e.g. a blocking Collider, wall mesh, or door.")]
        public GameObject Barrier;
        [Tooltip("Optional: shown/enabled while access is denied.")]
        public GameObject LockedIndicator;
        [Tooltip("Optional: shown/enabled while access is granted.")]
        public GameObject UnlockedIndicator;

        [Header("Requirement Description (optional)")]
        [Tooltip("Human-readable explanation of what's required to unlock the gate, e.g. \"Requires: any GingerBread Squad NFT\". Shown on RequirementText (if assigned) while the gate is locked, hidden once unlocked.")]
        [TextArea(2, 4)]
        public string RequirementDescription;
        [Tooltip("Optional TMP text object (3D TextMeshPro or UI TextMeshProUGUI both work) to display RequirementDescription.")]
        public TMP_Text RequirementText;

        [Header("Checkpoint")]
        [Tooltip("If true, the Barrier/Indicators/RequirementText only update when PerformCheck() is called explicitly (e.g. by an AccessCheckpoint trigger zone), instead of reacting the instant the wallet connects regardless of where the player is standing. HasAccess still stays current every frame either way.")]
        public bool RequireCheckpointTrigger = false;

        public bool HasAccess { get; private set; }

        // Tracks whether we've applied a state at least once, so the very first check always fires --
        // even if it happens to match the scene's default state.
        private bool? _lastAppliedState = null;

        // Metadata is per-asset, not per-wallet, so it's cached by "policyId+assetNameHex" and only
        // ever fetched once per asset regardless of how many times Update() runs. Only relevant to
        // Advanced Mode's MetadataMatch conditions.
        private readonly Dictionary<string, string> _metadataCache = new Dictionary<string, string>();
        private readonly HashSet<string> _metadataFetchesInFlight = new HashSet<string>();

        private bool IsConnected =>
            (BlockfrostManager != null && !string.IsNullOrEmpty(BlockfrostManager.AddressToFetch)) ||
            (KoiosManager != null && !string.IsNullOrEmpty(KoiosManager.AddressToFetch));

        void Start()
        {
            // Initialize visuals to the locked state right away, even when RequireCheckpointTrigger is
            // on. Without this, ApplyState() never runs until the player's first checkpoint crossing,
            // so Barrier/Indicators/RequirementText just sit in whatever raw state the scene left them
            // in (e.g. RequirementText visible but empty) instead of showing "locked" from the start.
            if (BlockfrostManager == null) BlockfrostManager = FindObjectOfType<BlockfrostIntegration>();
            if (KoiosManager == null) KoiosManager = FindObjectOfType<KoiosIntegration>();
            ApplyState();
            _lastAppliedState = HasAccess; // false by default here -- Update()/PerformCheck() will still re-apply once a real evaluation differs
        }

        void Update()
        {
            EnsureMetadataFetched();

            bool granted = Evaluate();
            HasAccess = granted; // stays current every frame regardless of RequireCheckpointTrigger

            if (RequireCheckpointTrigger) return; // visual state only changes via PerformCheck()

            ApplyIfChanged(granted);
        }

        /// <summary>
        /// Re-evaluates the access rule right now and applies the result to the Barrier,
        /// Indicators, and RequirementText, regardless of RequireCheckpointTrigger. Call this from
        /// an AccessCheckpoint trigger zone (or any other gameplay event) to tie the visual
        /// "reveal" to a specific place or moment rather than reacting the instant the wallet
        /// connects. Always re-applies -- even if the result hasn't changed since last time -- so
        /// a checkpoint can play a consistent "scan" reaction on every entry.
        /// </summary>
        public void PerformCheck()
        {
            EnsureMetadataFetched();
            bool granted = Evaluate();
            HasAccess = granted;
            ApplyIfChanged(granted, force: true);
        }

        private void ApplyIfChanged(bool granted, bool force = false)
        {
            if (!force && _lastAppliedState.HasValue && _lastAppliedState.Value == granted) return;
            _lastAppliedState = granted;
            ApplyState();
        }

        private bool Evaluate()
        {
            if (!IsConnected) return false;

            // Simple mode: one NFT, present or not.
            if (!AdvancedMode)
            {
                if (string.IsNullOrEmpty(RequiredPolicyId)) return false;
                return CheckAssetPresent(RequiredPolicyId, RequiredAssetNameHex);
            }

            // Advanced mode: OR-of-ANDs over Groups.
            if (Groups == null) return false;

            foreach (var group in Groups)
            {
                if (group?.Conditions == null || group.Conditions.Length == 0) continue;

                bool groupPasses = true;
                foreach (var condition in group.Conditions)
                {
                    if (!EvaluateCondition(condition))
                    {
                        groupPasses = false;
                        break;
                    }
                }
                if (groupPasses) return true; // OR: the first fully-passing group grants access
            }
            return false;
        }

        private bool EvaluateCondition(AssetCondition c)
        {
            bool result;
            switch (c.ConditionType)
            {
                case AssetConditionType.AssetPresent:
                    result = CheckAssetPresent(c.PolicyId, c.AssetNameHex);
                    break;
                case AssetConditionType.QuantityThreshold:
                    result = CheckQuantityThreshold(c);
                    break;
                case AssetConditionType.MetadataMatch:
                    result = CheckMetadataMatch(c);
                    break;
                default:
                    result = false;
                    break;
            }
            return c.Invert ? !result : result;
        }

        private bool CheckAssetPresent(string policyId, string assetNameHex)
        {
            if (BlockfrostManager != null && !string.IsNullOrEmpty(BlockfrostManager.AddressToFetch))
            {
                return string.IsNullOrEmpty(assetNameHex)
                    ? BlockfrostManager.HasAnyAssetUnderPolicy(policyId)
                    : BlockfrostManager.HasAsset(policyId + assetNameHex, 1);
            }
            if (KoiosManager != null && !string.IsNullOrEmpty(KoiosManager.AddressToFetch))
            {
                return KoiosManager.HasAsset(policyId, assetNameHex, 1);
            }
            return false;
        }

        private bool CheckQuantityThreshold(AssetCondition c)
        {
            long qty = GetQuantity(c.PolicyId, c.AssetNameHex);

            // "Any amount" (neither bound checked) still means the asset must be present, i.e. >= 1 --
            // not "always true regardless of holdings".
            long effectiveMin = c.UseMinQuantity ? c.MinQuantity : 1;
            if (qty < effectiveMin) return false;
            if (c.UseMaxQuantity && qty > c.MaxQuantity) return false;
            return true;
        }

        private long GetQuantity(string policyId, string assetNameHex)
        {
            if (BlockfrostManager != null && !string.IsNullOrEmpty(BlockfrostManager.AddressToFetch))
            {
                return BlockfrostManager.GetAssetQuantity(policyId, assetNameHex);
            }
            if (KoiosManager != null && !string.IsNullOrEmpty(KoiosManager.AddressToFetch))
            {
                return KoiosManager.GetAssetQuantity(policyId, assetNameHex);
            }
            return 0;
        }

        private bool CheckMetadataMatch(AssetCondition c)
        {
            string assetKey = c.PolicyId + c.AssetNameHex;
            if (!_metadataCache.TryGetValue(assetKey, out string rawJson) || string.IsNullOrEmpty(rawJson))
            {
                return false; // not fetched yet, or the fetch failed -- treat as non-matching this frame
            }

            try
            {
                var root = JObject.Parse(rawJson);
                // Blockfrost shape: { ..., "onchain_metadata": { "attributes": { "Hat": "Cop Hat", ... }, ... } }
                JToken metadataRoot = root["onchain_metadata"] ?? root;
                JToken value = metadataRoot?.SelectToken(c.MetadataKey);
                return value != null && string.Equals(value.ToString(), c.MetadataValue, StringComparison.Ordinal);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Cardano SDK] Failed to evaluate metadata condition for '{assetKey}' at path '{c.MetadataKey}': {e.Message}");
                return false;
            }
        }

        private void EnsureMetadataFetched()
        {
            // Only Advanced Mode's MetadataMatch conditions need this -- skip the work (and the API
            // calls) entirely in Simple mode.
            if (!AdvancedMode || Groups == null) return;

            foreach (var group in Groups)
            {
                if (group?.Conditions == null) continue;
                foreach (var c in group.Conditions)
                {
                    if (c.ConditionType != AssetConditionType.MetadataMatch) continue;
                    if (string.IsNullOrEmpty(c.PolicyId) || string.IsNullOrEmpty(c.AssetNameHex)) continue;

                    string assetKey = c.PolicyId + c.AssetNameHex;
                    if (_metadataCache.ContainsKey(assetKey) || _metadataFetchesInFlight.Contains(assetKey)) continue;

                    _metadataFetchesInFlight.Add(assetKey);

                    if (BlockfrostManager != null)
                    {
                        StartCoroutine(BlockfrostManager.FetchAssetMetadataRaw(assetKey, json =>
                        {
                            _metadataCache[assetKey] = json;
                            _metadataFetchesInFlight.Remove(assetKey);
                        }));
                    }
                    else if (KoiosManager != null)
                    {
                        StartCoroutine(KoiosManager.FetchAssetMetadataRaw(c.PolicyId, c.AssetNameHex, json =>
                        {
                            _metadataCache[assetKey] = json;
                            _metadataFetchesInFlight.Remove(assetKey);
                        }));
                    }
                    else
                    {
                        _metadataFetchesInFlight.Remove(assetKey);
                    }
                }
            }
        }

        private void ApplyState()
        {
            if (Barrier != null) Barrier.SetActive(!HasAccess);
            if (LockedIndicator != null) LockedIndicator.SetActive(!HasAccess);
            if (UnlockedIndicator != null) UnlockedIndicator.SetActive(HasAccess);

            if (RequirementText != null)
            {
                RequirementText.text = RequirementDescription;
                RequirementText.gameObject.SetActive(!HasAccess && !string.IsNullOrEmpty(RequirementDescription));
            }
        }
    }
}

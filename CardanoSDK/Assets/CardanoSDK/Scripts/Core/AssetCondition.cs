using System;
using UnityEngine;

namespace Prodiga.CardanoSDK
{
    /// <summary>
    /// Gated Access rule model.
    ///
    /// GatedAccessController evaluates a list of ConditionGroups: every condition inside a
    /// group must pass (AND), and access is granted if ANY group fully passes (OR).
    /// Combined with each condition's Invert flag (NOT), this is a standard
    /// disjunctive-normal-form (OR-of-ANDs-of-possibly-negated-literals) setup, which can
    /// express any boolean combination of the underlying checks without needing an
    /// arbitrarily nested tree -- keeping it something you can actually configure in the
    /// Unity Inspector.
    ///
    /// Example: "(own GingerBread #1653) OR (own any GingerBread NFT AND hold >= 1000 SAELEM)"
    ///   Group 0: [ AssetPresent: GingerBread #1653 ]
    ///   Group 1: [ AssetPresent: any GingerBread (AssetNameHex empty), QuantityThreshold: SAELEM >= 1000 ]
    /// </summary>
    [Serializable]
    public enum AssetConditionType
    {
        /// <summary>Asset must be present (quantity > 0). Leave AssetNameHex empty to match ANY asset under PolicyId.</summary>
        AssetPresent,
        /// <summary>Held quantity of a fungible (or any) asset must satisfy Min/Max bounds.</summary>
        QuantityThreshold,
        /// <summary>The asset's on-chain metadata at MetadataKey must equal MetadataValue. Requires a specific AssetNameHex.</summary>
        MetadataMatch
    }

    [Serializable]
    public class AssetCondition
    {
        [Header("Asset Identity")]
        [Tooltip("56-character policy ID hex. Required for every condition type.")]
        public string PolicyId;

        [Tooltip("Asset name in hex. Leave empty to match ANY asset under the policy " +
            "(valid for AssetPresent and QuantityThreshold; MetadataMatch requires a specific asset).")]
        public string AssetNameHex;

        [Header("Condition")]
        public AssetConditionType ConditionType = AssetConditionType.AssetPresent;

        [Header("Quantity Threshold (ConditionType = QuantityThreshold)")]
        [Tooltip("Check a minimum bound. If both UseMinQuantity and UseMaxQuantity are false, this " +
            "behaves like \"any amount\" -- i.e. the asset just needs to be present.")]
        public bool UseMinQuantity = true;
        public long MinQuantity = 1;
        [Tooltip("Check a maximum bound.")]
        public bool UseMaxQuantity = false;
        public long MaxQuantity = long.MaxValue;

        [Header("Metadata Match (ConditionType = MetadataMatch)")]
        [Tooltip("Dot-separated path into the asset's on-chain metadata, e.g. \"attributes.Hat\" or \"collection\".")]
        public string MetadataKey;
        [Tooltip("Expected value at MetadataKey (case-sensitive exact match).")]
        public string MetadataValue;

        [Header("Logic")]
        [Tooltip("If true, this condition passes when the underlying check FAILS (logical NOT).")]
        public bool Invert = false;
    }

    [Serializable]
    public class ConditionGroup
    {
        [Tooltip("Every condition in this group must pass (AND). The gate opens if ANY group fully passes (OR between groups).")]
        public AssetCondition[] Conditions;
    }
}

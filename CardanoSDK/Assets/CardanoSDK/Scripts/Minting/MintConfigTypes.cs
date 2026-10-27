using System;
using UnityEngine;

namespace Prodiga.CardanoSDK
{
    /// <summary>Whether a mint produces a one-off NFT (quantity locked to 1) or a fungible-style token (any quantity).</summary>
    [Serializable]
    public enum MintableTokenType
    {
        NFT,
        FungibleToken
    }

    /// <summary>One key/value pair for CIP-25 metadata's free-form "attributes" section (e.g. Key="Rarity", Value="Common").</summary>
    [Serializable]
    public class NativeAssetMetadataAttribute
    {
        public string Key;
        public string Value;
    }

    /// <summary>
    /// What gets minted. EnforceUniqueness controls a real distinction that's easy to miss:
    ///
    /// Off (default): every mint uses the exact same asset name, so many players can end up holding
    /// identical copies of "the same" token. It displays and behaves like an NFT (CIP-25 metadata,
    /// picture, name) but isn't strictly unique on-chain -- wallets/explorers that check total supply
    /// per asset name may bucket it as a semi-fungible token rather than a true 1-of-1 NFT. This is
    /// the right shape for things like repeatable achievement badges, where the point is that many
    /// players CAN earn the same one.
    ///
    /// On: each mint gets an auto-numbered suffix appended to the asset name, and a prior-mint check
    /// blocks reusing a slot -- true 1-of-1 NFTs. More bookkeeping (something has to track which
    /// numbers are taken), so treat this as the option to reach for when uniqueness is the actual
    /// point, not the default.
    /// </summary>
    [Serializable]
    public class MintableTokenConfig
    {
        public MintableTokenType TokenType = MintableTokenType.NFT;

        [Tooltip("Base on-chain asset name (ASCII, keep it short -- max 32 bytes per Cardano's limit). With EnforceUniqueness on, a number gets appended per mint (e.g. \"Badge\" -> \"Badge042\").")]
        public string AssetName;

        [Tooltip("See the class summary above -- off means many players can hold identical copies; on means each mint is a true 1-of-1.")]
        public bool EnforceUniqueness = false;

        [Tooltip("Quantity minted per mint. Leave at 1 for NFT. Fungible tokens can mint more than 1 at a time.")]
        public long Quantity = 1;

        [Header("CIP-25 Metadata")]
        public string DisplayName;
        [Tooltip("ipfs://... or ar://... -- hosting the actual image file is outside this SDK's scope; bring your own URI.")]
        public string ImageUri;
        [Tooltip("e.g. \"image/png\", \"image/jpeg\", \"image/gif\". Used as CIP-25's mediaType.")]
        public string ImageMediaType = "image/png";
        [TextArea(2, 4)]
        public string Description;
        public NativeAssetMetadataAttribute[] Attributes;
    }

    /// <summary>
    /// The minting policy is a single-signature native script tied to the minting wallet's own key
    /// hash -- every token minted through it shares one Policy ID, so it reads as one real
    /// collection rather than a pile of unrelated one-off policies.
    /// </summary>
    [Serializable]
    public class MintPolicyConfig
    {
        [Tooltip("NOT YET WIRED IN -- reserved for a future compound native script (signature + time-lock). Tonight's bridge always builds an open-ended ForgeScript.withOneSignature policy regardless of this value. Leave at 0.")]
        public long ExpirySlot = 0;
    }

    /// <summary>
    /// Controls whether the minting wallet covers a given mint's fee (and its ~1-1.5 ADA minimum-UTXO
    /// requirement) instead of the player. Conditions are evaluated against the PLAYER's connected
    /// wallet using the exact same ConditionGroup/AssetCondition engine GatedAccessController uses --
    /// OR between groups, AND within a group, each condition invertible. Note: unlike
    /// GatedAccessController, MetadataMatch conditions aren't supported here (would need async
    /// metadata fetching mid-mint-click); AssetPresent and QuantityThreshold both work.
    ///
    /// The wallet's own balance is always checked regardless of Conditions -- sponsorship never
    /// proceeds if it would drain the wallet below MinimumRemainingLovelace, even if every configured
    /// condition passes.
    /// </summary>
    [Serializable]
    public class MintSponsorshipConfig
    {
        public bool AllowSponsorship = false;

        [Tooltip("The mint only gets sponsored if the minting wallet would still have at least this many lovelace left afterward (1 ADA = 1,000,000 lovelace). Protects against draining the wallet to zero.")]
        public long MinimumRemainingLovelace = 5_000_000;

        [Tooltip("Evaluated against the player's wallet. Leave empty to sponsor unconditionally (the balance check above still applies).")]
        public ConditionGroup[] Conditions;
    }
}

using System;
using UnityEngine;

namespace Prodiga.CardanoSDK
{
    /// <summary>
    /// What AwardingController sends from the treasury wallet to the player -- an EXISTING
    /// on-chain token/NFT the treasury already holds (as opposed to MintableTokenConfig, which
    /// describes something to be minted on demand). PolicyId + AssetNameHex together identify the
    /// exact asset; unlike Gated Access's AssetCondition, there's no "any asset under this policy"
    /// option here -- awarding needs one specific, known token to actually send.
    /// </summary>
    [Serializable]
    public class AwardableTokenConfig
    {
        [Tooltip("56-character policy ID hex of the token the treasury wallet holds.")]
        public string PolicyId;

        [Tooltip("Asset name in hex, exactly as it appears in the treasury wallet's holdings (e.g. from Blockfrost/Koios's asset listing).")]
        public string AssetNameHex;

        [Tooltip("Quantity sent to the player per award -- e.g. 1 for an NFT/achievement badge, or more for a fungible in-game currency token.")]
        public long QuantityPerAward = 1;

        [Header("Display (optional, UI only)")]
        [Tooltip("Not used by the transfer itself -- purely for your own UI/status text if you want to show what's being awarded.")]
        public string DisplayName;
    }
}

using UnityEngine;
using UnityEngine.Events;

namespace Prodiga.CardanoSDK
{
    /// <summary>
    /// Place on a trigger volume positioned wherever the gate check should actually happen --
    /// typically a few units in front of a Barrier. Calls PerformCheck() on the assigned
    /// GatedAccessController, which re-evaluates the wallet's holdings right then and updates the
    /// Barrier/Indicators/RequirementText at that moment, rather than the gate reacting the instant
    /// the wallet connects regardless of where the player is standing.
    ///
    /// The check re-runs at three points:
    ///   - the player enters the volume
    ///   - the player exits the volume
    ///   - the wallet's data changes (connect, disconnect, account switch, or a holdings refresh)
    ///     WHILE the player is still inside the volume -- e.g. they walk up to the checkpoint,
    ///     then connect their wallet while standing on it, without needing to step off and back on
    ///
    /// That third case only applies to players currently inside this checkpoint -- it does not
    /// affect the gate for anyone who isn't. The subscription to the wallet-data events is only
    /// held while the player is inside, specifically so it can't fire for someone standing
    /// elsewhere in the scene.
    ///
    /// Requires a Collider on this GameObject with "Is Trigger" checked (set automatically if you
    /// add this component via the Inspector's Add Component button; verify it manually if you
    /// added the Collider first via script).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AccessCheckpoint : MonoBehaviour
    {
        [Tooltip("The gate this checkpoint performs the check for.")]
        public GatedAccessController Gate;

        [Tooltip("Optional: invoked every time the check runs (entry, exit, or a wallet update while the player is inside), e.g. to play a scan sound or particle effect.")]
        public UnityEvent OnCheckPerformed;

        private bool _playerInside;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;
            _playerInside = true;
            SubscribeToWalletUpdates();
            RunCheck();
        }

        void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other)) return;
            _playerInside = false;
            UnsubscribeFromWalletUpdates();
            RunCheck();
        }

        void OnDisable()
        {
            // Covers the checkpoint being disabled/destroyed while the player happens to be
            // standing inside it, so the subscription doesn't leak.
            if (_playerInside) UnsubscribeFromWalletUpdates();
            _playerInside = false;
        }

        // Accepts the demo PlayerController or any object tagged "Player", so the checkpoint works with your own player.
        private bool IsPlayer(Collider other) => other.CompareTag("Player") || other.GetComponent<Demo.PlayerController>() != null;

        private void SubscribeToWalletUpdates()
        {
            if (Gate == null) return;
            if (Gate.BlockfrostManager != null) Gate.BlockfrostManager.OnDataUpdated += HandleWalletDataUpdated;
            if (Gate.KoiosManager != null) Gate.KoiosManager.OnDataUpdated += HandleWalletDataUpdated;
        }

        private void UnsubscribeFromWalletUpdates()
        {
            if (Gate == null) return;
            if (Gate.BlockfrostManager != null) Gate.BlockfrostManager.OnDataUpdated -= HandleWalletDataUpdated;
            if (Gate.KoiosManager != null) Gate.KoiosManager.OnDataUpdated -= HandleWalletDataUpdated;
        }

        private void HandleWalletDataUpdated()
        {
            // Only subscribed while _playerInside is true, so this is safe to treat as
            // "re-check in place" without checking position again.
            RunCheck();
        }

        private void RunCheck()
        {
            if (Gate == null) return;
            Gate.PerformCheck();
            OnCheckPerformed?.Invoke();
        }
    }
}

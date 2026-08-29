using System;

namespace MurkysManySaves
{
    /// <summary>
    /// A one-time opt-in decision, persisted per save slot: "never decided" (no prompt shown
    /// yet, or the player hasn't answered), "declined", or "accepted" - distinct states, unlike
    /// a plain bool which can't tell "never set" apart from an explicit false.
    ///
    /// Built directly on ParallelFileHandler rather than PersistedValue&lt;T&gt;, so a decision
    /// is written to disk immediately when made, not deferred until the next natural game save -
    /// a player who quits right after answering the prompt won't be asked again next launch.
    /// </summary>
    public class FeatureOptIn
    {
        private readonly string ns;
        private readonly string key;
        private bool askedThisSession;

        public FeatureOptIn(string ns, string key)
        {
            this.ns = ns;
            this.key = key;
            FeatureOptInRegistry.Register(this);
        }

        /// <summary>True once a decision (accept or decline) has been recorded for this save.</summary>
        public bool HasDecision(string saveFile)
        {
            return ParallelFileHandler.KeyExists(saveFile, ns, key);
        }

        /// <summary>True only if the player has explicitly accepted. False if declined or never decided.</summary>
        public bool IsEnabled(string saveFile)
        {
            return ParallelFileHandler.LoadValue(saveFile, ns, key, false);
        }

        /// <summary>
        /// Lets the prompt be shown again even if it was already shown once this session. Called
        /// automatically whenever the game hard-resets (e.g. returning to the main menu) - see
        /// FeatureOptInRegistry/SessionHandler - so most callers won't need to call this
        /// themselves. Exposed publicly for a mod that wants to force an earlier reset.
        /// </summary>
        public void ResetSessionGuard()
        {
            askedThisSession = false;
        }

        /// <summary>
        /// Shows config's prompt if (and only if) the current save has no recorded decision yet
        /// and this hasn't already asked this session. config's own OnAccept/OnDecline still run
        /// as given - this only adds persisting the decision around them. No-ops (and returns
        /// false) if there's no active save, a decision already exists, or already asked this
        /// session.
        /// </summary>
        public bool RequestIfNeeded(PromptDialogConfig config)
        {
            string saveFile = SaveSlotHandler.GetCurrentSaveFile();
            if (saveFile == null || askedThisSession || HasDecision(saveFile))
            {
                return false;
            }

            askedThisSession = true;

            var userOnAccept = config.OnAccept;
            var userOnDecline = config.OnDecline;
            config.OnAccept = () =>
            {
                ParallelFileHandler.SaveValue(saveFile, ns, key, true);
                userOnAccept?.Invoke();
            };
            config.OnDecline = () =>
            {
                ParallelFileHandler.SaveValue(saveFile, ns, key, false);
                userOnDecline?.Invoke();
            };

            PromptDialogHandler.Show(config);
            return true;
        }

        /// <summary>
        /// Wires this decision to be offered automatically: shortly after every load
        /// (delaySeconds gives the scene time to settle first), RequestIfNeeded runs with a
        /// freshly-built config - a no-op if a decision already exists for that save or it was
        /// already asked this session. configFactory is called fresh each time, not cached, so
        /// it can build fully-resolved localized text at call time rather than mod Init time.
        /// Call once, e.g. during mod Init. Use RequestIfNeeded directly instead if you'd rather
        /// trigger the prompt from your own event (e.g. only once a feature becomes relevant)
        /// than automatically after every load.
        /// </summary>
        public void AutoRequestOnLoad(Func<PromptDialogConfig> configFactory, float delaySeconds = 1f)
        {
            SaveSlotHandler.LoadCompleted += (saveFile, slot) =>
            {
                CoroutineRunner.RunDelayed(delaySeconds, () => RequestIfNeeded(configFactory()));
            };
        }
    }
}

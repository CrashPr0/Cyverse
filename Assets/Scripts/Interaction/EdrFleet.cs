using System;
using System.Collections.Generic;
using UnityEngine;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// Owns the endpoint fleet: the task is finished once every compromised
    /// machine has been isolated. Clean machines the player wrongly isolates
    /// don't block completion — they just cost the completion bonus.
    /// </summary>
    public class EdrFleet : MonoBehaviour
    {
        public event Action Completed;
        public bool IsComplete { get; private set; }
        public int completionBonus = 80;

        private readonly List<EndpointStation> endpoints = new List<EndpointStation>();

        public void Register(EndpointStation e)
        {
            if (e != null && !endpoints.Contains(e)) endpoints.Add(e);
        }

        /// <summary>
        /// Re-adopt the endpoints and hand each one its definition back. The
        /// roster is a plain List and EndpointDef isn't [Serializable], so a
        /// scene SAVED with the fleet in it loaded with zero endpoints — which
        /// made Threats 0, so the very first isolation decision instantly
        /// "completed" the task. Matched by the hostname baked into each
        /// endpoint's object name.
        /// </summary>
        public void Rebind(Level2Content.EndpointDef[] defs)
        {
            foreach (var station in FindObjectsOfType<EndpointStation>())
            {
                if (station.def == null && defs != null)
                    foreach (var d in defs)
                        if (d.hostname == station.HostnameFromName) { station.Rebind(d, this); break; }
                if (station.def != null) Register(station);
            }
        }

        public int Contained
        {
            get
            {
                int n = 0;
                foreach (var e in endpoints) if (e.Isolated && e.def.compromised) n++;
                return n;
            }
        }

        public int Threats
        {
            get
            {
                int n = 0;
                foreach (var e in endpoints) if (e.def.compromised) n++;
                return n;
            }
        }

        public int FalseIsolations
        {
            get
            {
                int n = 0;
                foreach (var e in endpoints) if (e.Isolated && !e.def.compromised) n++;
                return n;
            }
        }

        public void NotifyDecision(EndpointStation source)
        {
            // An empty roster would report 0 threats and "complete" the task on
            // the first decision, so an unbound fleet must never finish.
            if (IsComplete || endpoints.Count == 0 || Contained < Threats) return;
            IsComplete = true;

            bool clean = FalseIsolations == 0;
            if (clean) Core.ScoreSystem.Add(completionBonus);
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast(
                    clean
                        ? $"FLEET CONTAINED — no healthy machines disrupted  +{completionBonus}"
                        : "FLEET CONTAINED — but healthy machines were taken offline",
                    new Color(0.90f, 0.66f, 0.14f));
            Completed?.Invoke();
        }
    }
}

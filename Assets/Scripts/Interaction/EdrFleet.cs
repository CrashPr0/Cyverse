using System;
using System.Collections.Generic;
using UnityEngine;
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

        public void Register(EndpointStation e) => endpoints.Add(e);

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
            if (IsComplete || Contained < Threats) return;
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

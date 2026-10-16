using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Hollow.Data
{
    /// <summary>
    /// Owns the live balance profile the game reads, plus a draft the balance panel edits (D44).
    /// Apply validates the draft and makes it live; Undo restores the previous live profile.
    /// Systems that must not change mid-encounter (spawn queues, spawned enemy health, purchase records)
    /// read the live values at their natural moment, so applying never rewrites the past.
    /// </summary>
    public sealed class BalanceService
    {
        public const string BaselineResourcePath = "Balance/Baseline";
        const int MaxUndo = 50;

        readonly Stack<string> undo = new();
        string liveJson;

        public BalanceProfile Baseline { get; }
        public BalanceProfile Live { get; private set; }
        public BalanceProfile Draft { get; private set; }

        /// <summary>When on, every panel edit is applied immediately (live tuning during play).</summary>
        public bool AutoApply { get; set; }

        public List<ValidationIssue> LastIssues { get; private set; } = new();
        public int UndoCount => undo.Count;

        /// <summary>Raised after the live profile changed (apply, undo, load).</summary>
        public event Action<BalanceProfile> Applied;

        public BalanceService(BalanceProfile baseline, BalanceProfile live = null)
        {
            Baseline = BalanceJson.Clone(baseline);
            Live = BalanceJson.Clone(live ?? baseline);
            liveJson = BalanceJson.ToJson(Live);
            Draft = BalanceJson.Clone(Live);
            LastIssues = BalanceValidator.Validate(Draft);
        }

        public static BalanceProfile LoadBaselineFromResources()
        {
            var asset = Resources.Load<TextAsset>(BaselineResourcePath);
            if (asset == null) throw new InvalidOperationException($"Missing Resources/{BaselineResourcePath}.json");
            return BalanceJson.FromJson(asset.text);
        }

        public bool DraftDiffersFromLive() => BalanceJson.ToJson(Draft) != liveJson;

        /// <summary>Validates and applies the draft. Errors block the apply; warnings do not.</summary>
        public bool Apply()
        {
            LastIssues = BalanceValidator.Validate(Draft);
            if (LastIssues.Exists(i => i.Severity == IssueSeverity.Error)) return false;
            string draftJson = BalanceJson.ToJson(Draft);
            if (draftJson == liveJson) return true;

            undo.Push(liveJson);
            if (undo.Count > MaxUndo) TrimUndo();
            Draft.Revision = Live.Revision + 1;
            SetLive(BalanceJson.Clone(Draft));
            return true;
        }

        public void RevertDraft()
        {
            Draft = BalanceJson.Clone(Live);
            LastIssues = BalanceValidator.Validate(Draft);
        }

        public bool Undo()
        {
            if (undo.Count == 0) return false;
            SetLive(BalanceJson.FromJson(undo.Pop()));
            Draft = BalanceJson.Clone(Live);
            return true;
        }

        /// <summary>Copies one panel group (GDD §13) from the baseline into the draft.</summary>
        public void ResetGroupToBaseline(string group)
        {
            var fresh = BalanceJson.Clone(Baseline);
            foreach (var f in typeof(BalanceProfile).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = f.GetCustomAttribute<PanelGroupAttribute>();
                if (attr != null && attr.Group == group) f.SetValue(Draft, f.GetValue(fresh));
            }
            Draft.BuildIndex();
            LastIssues = BalanceValidator.Validate(Draft);
        }

        /// <summary>Puts an imported profile into the draft; it becomes live only on Apply.</summary>
        public void LoadIntoDraft(BalanceProfile imported)
        {
            Draft = BalanceJson.Clone(imported);
            LastIssues = BalanceValidator.Validate(Draft);
        }

        /// <summary>Called by the panel after any field edit.</summary>
        public void NotifyDraftEdited()
        {
            Draft.BuildIndex();
            if (AutoApply) Apply();
            else LastIssues = BalanceValidator.Validate(Draft);
        }

        void SetLive(BalanceProfile p)
        {
            p.BuildIndex();
            Live = p;
            liveJson = BalanceJson.ToJson(p);
            LastIssues = BalanceValidator.Validate(p);
            Applied?.Invoke(Live);
        }

        void TrimUndo()
        {
            var items = undo.ToArray();
            undo.Clear();
            for (int i = MaxUndo - 1; i >= 0; i--) undo.Push(items[i]);
        }
    }
}

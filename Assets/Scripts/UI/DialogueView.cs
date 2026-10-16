using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Hollow.UI
{
    [Serializable]
    public sealed class DialogueSpeaker { public string Id; public string Name; public string Color; }

    [Serializable]
    public sealed class DialogueLine { public string Speaker; public string Text; public float Seconds; }

    [Serializable]
    public sealed class DialogueScene { public string Id; public string Title; public List<DialogueLine> Lines = new(); }

    [Serializable]
    public sealed class DialogueBark { public string Id; public string Speaker; public string Text; public float Seconds; }

    /// <summary>Narrative data (Resources/Narrative/Dialogue.json): text, speakers and durations, editable without code.</summary>
    [Serializable]
    public sealed class DialogueData
    {
        public List<DialogueSpeaker> Speakers = new();
        public List<DialogueScene> Scenes = new();
        public List<DialogueBark> Barks = new();

        public static DialogueData Load()
        {
            var asset = Resources.Load<TextAsset>("Narrative/Dialogue");
            return asset != null ? JsonUtility.FromJson<DialogueData>(asset.text) : new DialogueData();
        }

        public DialogueScene Scene(string id) => Scenes.Find(s => s.Id == id);
        public DialogueBark Bark(string id) => Barks.Find(b => b.Id == id);
        public DialogueSpeaker Speaker(string id) => Speakers.Find(s => s.Id == id);
    }

    /// <summary>
    /// Subtitle box (D33): scenes darken the screen and advance line by line (Enter or click, or by time);
    /// barks are short subtitles during combat that never take input.
    /// </summary>
    public sealed class DialogueView : MonoBehaviour
    {
        UIRoot root;
        DialogueData data;
        RectTransform sceneLayer, box;
        TextMeshProUGUI title, line, hint, bark;
        DialogueScene current;
        int index;
        float lineEnds;
        bool skippable;
        Action onDone;
        float barkEnds;

        public bool Playing => current != null;

        public void Init(UIRoot uiRoot, DialogueData dialogue)
        {
            root = uiRoot;
            data = dialogue;
            var t = root.Theme;
            sceneLayer = UIFactory.Stretch(root.Modal, "Scene");
            UIFactory.Panel(sceneLayer, t, new Color(0, 0, 0, 0.55f), raycast: true);
            var titleRt = UIFactory.Rect(sceneLayer, "Title", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1400, 140));
            title = UIFactory.Text(titleRt, "T", "", t, 72, TextAlignmentOptions.Center);
            box = UIFactory.Rect(sceneLayer, "Box", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 200), new Vector2(1300, 190));
            UIFactory.Panel(box, t);
            line = UIFactory.Text(box, "Line", "", t, 34, TextAlignmentOptions.MidlineLeft);
            line.margin = new Vector4(30, 10, 30, 10);
            var hintRt = UIFactory.Rect(sceneLayer, "Hint", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 318), new Vector2(1300, 40));
            hint = UIFactory.Text(hintRt, "H", "", t, 20, TextAlignmentOptions.Center, t.TextDim);
            var advance = sceneLayer.gameObject.AddComponent<UnityEngine.UI.Button>();
            advance.transition = UnityEngine.UI.Selectable.Transition.None;
            advance.onClick.AddListener(Next);
            sceneLayer.gameObject.SetActive(false);

            var barkRt = UIFactory.Rect(root.Hud, "Bark", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 230), new Vector2(1200, 60));
            bark = UIFactory.Text(barkRt, "B", "", t, 30, TextAlignmentOptions.Center);
        }

        /// <summary>Plays a scene; <paramref name="canSkip"/> when the scene was already seen in this slot.</summary>
        public void PlayScene(string id, bool canSkip, Action done)
        {
            var scene = data.Scene(id);
            if (scene == null || (scene.Lines.Count == 0 && string.IsNullOrEmpty(scene.Title))) { done?.Invoke(); return; }
            current = scene;
            skippable = canSkip;
            onDone = done;
            index = -1;
            title.text = scene.Title ?? "";
            sceneLayer.gameObject.SetActive(true);
            hint.text = canSkip ? "Enter / click: next      Esc: skip" : "Enter / click: next";
            Next();
        }

        public void Next()
        {
            if (current == null) return;
            index++;
            if (index >= current.Lines.Count)
            {
                if (index == 0 && !string.IsNullOrEmpty(current.Title)) { lineEnds = Time.unscaledTime + 2.5f; box.gameObject.SetActive(false); return; }
                Finish();
                return;
            }
            box.gameObject.SetActive(true);
            var l = current.Lines[index];
            line.text = Format(l.Speaker, l.Text);
            lineEnds = Time.unscaledTime + Mathf.Max(0.5f, l.Seconds);
        }

        public void Skip()
        {
            if (current != null && skippable) Finish();
        }

        void Finish()
        {
            current = null;
            sceneLayer.gameObject.SetActive(false);
            var cb = onDone;
            onDone = null;
            cb?.Invoke();
        }

        public void ShowBark(string id)
        {
            var b = data.Bark(id);
            if (b == null) return;
            bark.text = Format(b.Speaker, b.Text);
            barkEnds = Time.unscaledTime + b.Seconds;
        }

        string Format(string speakerId, string text)
        {
            var s = data.Speaker(speakerId);
            string name = s != null && !string.IsNullOrEmpty(s.Name) ? $"<color={s.Color}><b>{s.Name}</b></color>   " : "";
            string body = s != null && string.IsNullOrEmpty(s.Name) ? $"<color={s.Color}><i>{text}</i></color>" : text;
            return name + body;
        }

        void Update()
        {
            if (bark != null && Time.unscaledTime > barkEnds) bark.text = "";
            if (current == null) return;
            if (Time.unscaledTime >= lineEnds) Next();
        }
    }
}

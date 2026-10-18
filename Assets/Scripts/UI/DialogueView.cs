using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollow.UI
{
    /// <summary>Style: Human, Machine, Alarm, Radio or Display (how the line is typed, coloured and heard).</summary>
    [Serializable]
    public sealed class DialogueSpeaker { public string Id; public string Name; public string Color; public string Style; }

    /// <summary>
    /// Seconds: how long the line stays once fully typed. Shot: the picture from this line on (empty keeps the
    /// current one). Delay: a silent pause before the line is typed.
    /// </summary>
    [Serializable]
    public sealed class DialogueLine { public string Speaker; public string Text; public float Seconds; public string Shot; public float Delay; }

    [Serializable]
    public sealed class DialogueScene { public string Id; public List<DialogueLine> Lines = new(); }

    /// <summary>
    /// One still of a scene (Resources/Cutscenes/&lt;Image or Id&gt;): a slow push from ZoomFrom to ZoomTo held on
    /// Focus (0–1 of the picture, from the bottom left), drifting by Drift (share of the width/height).
    /// Flash colours the cut; Punch shakes it (a hit, an alarm).
    /// </summary>
    [Serializable]
    public sealed class DialogueShot
    {
        public string Id;
        public string Image;
        public Vector2 Focus = new(0.5f, 0.5f);
        public float ZoomFrom = 1f;
        public float ZoomTo = 1.1f;
        public Vector2 Drift;
        public string Flash;
        public bool Punch;
    }

    /// <summary>A short line during play. Next chains a reply; Cooldown limits how often it can repeat (seconds).</summary>
    [Serializable]
    public sealed class DialogueBark { public string Id; public string Speaker; public string Text; public float Seconds; public string Next; public float Cooldown; }

    /// <summary>Narrative data (Resources/Narrative/Dialogue.json): text, speakers, pictures and timing, editable without code.</summary>
    [Serializable]
    public sealed class DialogueData
    {
        public List<DialogueSpeaker> Speakers = new();
        public List<DialogueShot> Shots = new();
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
        public DialogueShot Shot(string id) => Shots.Find(s => s.Id == id);
    }

    /// <summary>
    /// Story scenes as illustrated stills (D33, D98): each picture fills the screen above a dark band and slowly
    /// pushes in; cuts cross-fade (some flash and shake). Lines are typed into the band letter by letter, each
    /// speaker in their own colour and voice; E, Enter or a click finishes the line, then moves on. Barks are retired.
    /// </summary>
    public sealed class DialogueView : MonoBehaviour
    {
        const float Band = 0.235f;          // the subtitle band: share of the screen height under the picture
        const float ShotSeconds = 16f;      // one full push-in
        const float CrossFade = 0.55f;
        const float FadeIn = 0.6f, FadeOut = 0.45f, Dim = 0.25f;

        sealed class Layer
        {
            public RectTransform Rect;
            public RawImage Image;
            public DialogueShot Shot;
            public float Started;
        }

        UIRoot root;
        DialogueData data;
        RectTransform sceneLayer, frame, accent, cursor, marker;
        Layer front, back;
        TextMeshProUGUI speakerName, line, hint;
        Image fade, flash, accentImage, cursorImage, markerImage;
        DialogueScene current;
        int index;
        bool skippable, typing, closing;
        string fullText;
        float revealed, nextCharAt, holdUntil, lastTick, punchUntil, fadeTarget, fadeSpeed;
        Color lineColor;
        DialogueSpeaker speaker;
        Action onDone;
        Coroutine lineRoutine;
        readonly Dictionary<string, Texture2D> pictures = new();
        readonly Dictionary<string, string> speakerNames = new();

        /// <summary>v0.19 §15: subtitles can be turned off (spoken lines still play).</summary>
        public bool SubtitlesOn = true;
        /// <summary>A line starts: its id is "&lt;scene&gt;_&lt;n&gt;" (voice file names, docs/VOICE_LINES.md).</summary>
        public event Action<string> LineStarted;
        /// <summary>The scene ended or was skipped: stop any spoken line.</summary>
        public event Action LinesStopped;
        /// <summary>A letter was typed (the speaker's style: Human, Machine, Alarm, Radio); "Cut" for a hard cut.</summary>
        public event Action<string> Typed;

        /// <summary>Overrides a speaker's shown name (the assistant speaks under the chosen AI's name, v0.19 §3).</summary>
        public void SetSpeakerName(string speakerId, string name) => speakerNames[speakerId] = name;

        public bool Playing => current != null;

        public void Init(UIRoot uiRoot, DialogueData dialogue)
        {
            root = uiRoot;
            data = dialogue;
            var t = root.Theme;
            sceneLayer = UIFactory.Stretch(root.Modal, "Scene");
            var backdrop = sceneLayer.gameObject.AddComponent<Image>();
            backdrop.color = Color.black;
            var advance = sceneLayer.gameObject.AddComponent<Button>();
            advance.transition = Selectable.Transition.None;
            advance.onClick.AddListener(Next);

            // The picture: everything above the band, cropped to fill it.
            frame = UIFactory.Stretch(sceneLayer, "Frame");
            frame.anchorMin = new Vector2(0, Band);
            frame.gameObject.AddComponent<RectMask2D>();
            back = NewLayer("ShotB");
            front = NewLayer("ShotA");
            var top = Gradient(frame, "TopShade", new Vector2(0, 1), 90, new Color(0, 0, 0, 0.55f), fromTop: true);
            top.anchorMin = new Vector2(0, 1);
            Gradient(frame, "BottomShade", Vector2.zero, 150, new Color(0.027f, 0.031f, 0.04f, 1f), fromTop: false);
            var flashRt = UIFactory.Stretch(frame, "Flash");
            flash = flashRt.gameObject.AddComponent<Image>();
            flash.raycastTarget = false;
            flash.color = Color.clear;

            // The band: speaker, typed line, a cursor and a "more" marker.
            var band = UIFactory.Stretch(sceneLayer, "Band");
            band.anchorMax = new Vector2(1, Band);
            band.gameObject.AddComponent<Image>().color = new Color(0.027f, 0.031f, 0.04f, 1f);
            band.GetComponent<Image>().raycastTarget = false;
            accent = UIFactory.Rect(band, "Accent", new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 3));
            accentImage = accent.gameObject.AddComponent<Image>();
            accentImage.raycastTarget = false;

            var nameRt = UIFactory.Rect(band, "Speaker", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-150, -22), new Vector2(900, 48));
            speakerName = UIFactory.Text(nameRt, "T", "", t, 30, TextAlignmentOptions.MidlineRight);
            speakerName.fontStyle = FontStyles.Bold;
            speakerName.characterSpacing = 2;
            var lineRt = UIFactory.Stretch(band, "Line");
            lineRt.offsetMin = new Vector2(150, 46);
            lineRt.offsetMax = new Vector2(-150, -80);
            line = UIFactory.Text(lineRt, "T", "", t, 42, TextAlignmentOptions.TopRight);
            line.lineSpacing = 12;

            cursor = UIFactory.Rect(band, "Cursor", new Vector2(0, 0), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4, 40));
            cursorImage = cursor.gameObject.AddComponent<Image>();
            cursorImage.raycastTarget = false;
            marker = UIFactory.Rect(band, "More", new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(110, 48), new Vector2(14, 14));
            marker.localRotation = Quaternion.Euler(0, 0, 45);
            markerImage = marker.gameObject.AddComponent<Image>();
            markerImage.raycastTarget = false;

            var hintRt = UIFactory.Rect(band, "Hint", new Vector2(0, 0), new Vector2(0, 0), new Vector2(150, 12), new Vector2(700, 30));
            hint = UIFactory.Text(hintRt, "H", "", t, 18, TextAlignmentOptions.MidlineLeft, t.TextDim);
            hint.alignment = TextAlignmentOptions.MidlineLeft; // keys sit at the left, out of the line's way
            sceneLayer.gameObject.SetActive(false);

            // Fades over the game too: the scene opens from black and hands back from black.
            var fadeRt = UIFactory.Stretch(root.Modal, "SceneFade");
            fade = fadeRt.gameObject.AddComponent<Image>();
            fade.color = new Color(0, 0, 0, 0);
            fade.raycastTarget = false;
        }

        Layer NewLayer(string name)
        {
            var rt = UIFactory.Rect(frame, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var image = rt.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            image.color = Color.clear;
            return new Layer { Rect = rt, Image = image };
        }

        static RectTransform Gradient(RectTransform parent, string name, Vector2 anchor, float height, Color color, bool fromTop)
        {
            var rt = UIFactory.Rect(parent, name, anchor, new Vector2(0.5f, fromTop ? 1 : 0), Vector2.zero, new Vector2(0, height));
            rt.anchorMin = new Vector2(0, anchor.y);
            rt.anchorMax = new Vector2(1, anchor.y);
            var texture = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 32; y++)
            {
                float k = y / 31f;                        // 0 at the bottom row
                float a = fromTop ? k : 1f - k;            // solid at the picture's edge
                texture.SetPixel(0, y, new Color(1, 1, 1, a * a));
            }
            texture.Apply();
            var image = rt.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.color = color;
            image.raycastTarget = false;
            return rt;
        }

        // ---------------------------------------------------------------- scene flow

        /// <summary>Plays a scene; <paramref name="canSkip"/> when the scene was already seen in this slot.</summary>
        public void PlayScene(string id, bool canSkip, Action done)
        {
            var scene = data.Scene(id);
            if (scene == null || scene.Lines.Count == 0) { done?.Invoke(); return; }
            StopAllCoroutines();
            current = scene;
            skippable = canSkip;
            onDone = done;
            closing = false;
            index = -1;
            front.Shot = back.Shot = null;
            front.Image.color = back.Image.color = Color.clear;
            hint.text = Loc.T(canSkip ? "dialogue.hintSkip" : "dialogue.hint");
            StartCoroutine(Open());
        }

        IEnumerator Open()
        {
            // The game dims to black first, then the first picture rises out of it.
            yield return FadeTo(1f, Dim);
            sceneLayer.gameObject.SetActive(true);
            ShowLine(0);
            yield return FadeTo(0f, FadeIn);
        }

        IEnumerator Close()
        {
            closing = true;
            LinesStopped?.Invoke();
            yield return FadeTo(1f, FadeOut);
            sceneLayer.gameObject.SetActive(false);
            current = null;
            closing = false;
            var cb = onDone;
            onDone = null;
            cb?.Invoke();
            yield return FadeTo(0f, FadeIn);
        }

        IEnumerator FadeTo(float alpha, float seconds)
        {
            float from = fade.color.a;
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                fade.color = new Color(0, 0, 0, Mathf.Lerp(from, alpha, t / seconds));
                yield return null;
            }
            fade.color = new Color(0, 0, 0, alpha);
        }

        /// <summary>E, Enter or a click: finish typing the line, or go to the next one.</summary>
        public void Next()
        {
            if (current == null || closing || index < 0) return;
            if (typing) { CompleteLine(); return; }
            ShowLine(index + 1);
        }

        public void Skip()
        {
            if (current != null && skippable && !closing) StartCoroutine(Close());
        }

        /// <summary>Dev tools: ends the scene at once, without fades.</summary>
        public void FinishNow()
        {
            if (current == null) return;
            StopAllCoroutines();
            LinesStopped?.Invoke();
            sceneLayer.gameObject.SetActive(false);
            fade.color = Color.clear;
            current = null;
            closing = false;
            var cb = onDone;
            onDone = null;
            cb?.Invoke();
        }

        void ShowLine(int i)
        {
            index = i;
            if (index >= current.Lines.Count) { StartCoroutine(Close()); return; }
            var l = current.Lines[index];
            if (!string.IsNullOrEmpty(l.Shot)) Cut(l.Shot);
            if (lineRoutine != null) StopCoroutine(lineRoutine);
            lineRoutine = StartCoroutine(Speak(l));
        }

        IEnumerator Speak(DialogueLine l)
        {
            speaker = data.Speaker(l.Speaker);
            string style = speaker?.Style ?? "";
            bool display = style == "Display";
            fullText = l.Text ?? "";
            revealed = 0;
            typing = true;
            holdUntil = float.MaxValue;
            line.text = "";
            speakerName.text = "";
            accent.sizeDelta = new Vector2(0, 3);
            if (l.Delay > 0) for (float t = 0; t < l.Delay && typing; t += Time.unscaledDeltaTime) yield return null;

            var color = speaker != null && ColorUtility.TryParseHtmlString(speaker.Color, out var c) ? c : root.Theme.Text;
            speakerName.text = display || speaker == null ? "" : speakerNames.TryGetValue(speaker.Id, out var over) ? over : speaker.Name;
            speakerName.color = color;
            lineColor = style switch
            {
                "Machine" => Color.Lerp(Color.white, color, 0.35f),
                "Alarm" => Color.Lerp(Color.white, color, 0.4f),
                "Display" => color,
                _ => Color.Lerp(Color.white, color, 0.15f),
            };
            line.color = lineColor;
            line.fontStyle = style == "Radio" ? FontStyles.Italic : FontStyles.Normal;
            line.alignment = display ? TextAlignmentOptions.Top : TextAlignmentOptions.TopRight;
            line.characterSpacing = display ? 6 : 0;
            line.fontSize = display ? 46 : 42;
            accentImage.color = new Color(color.r, color.g, color.b, 0.7f);
            cursorImage.color = color;
            line.gameObject.SetActive(SubtitlesOn);
            speakerName.gameObject.SetActive(SubtitlesOn);
            LineStarted?.Invoke($"{current.Id}_{index + 1}");

            float perSecond = style switch { "Display" => 15f, "Machine" => 40f, "Alarm" => 36f, "Radio" => 30f, _ => 34f };
            nextCharAt = Time.unscaledTime;
            while (typing && revealed < fullText.Length)
            {
                if (Time.unscaledTime >= nextCharAt)
                {
                    int n = (int)revealed + 1;
                    revealed = n;
                    line.text = fullText.Substring(0, n);
                    char ch = fullText[n - 1];
                    nextCharAt = Time.unscaledTime + 1f / perSecond + Pause(ch, n < fullText.Length ? fullText[n] : ' ');
                    if (!char.IsWhiteSpace(ch) && !display && Time.unscaledTime - lastTick > 0.04f)
                    {
                        lastTick = Time.unscaledTime;
                        Typed?.Invoke(style);
                    }
                }
                yield return null;
            }
            if (typing) CompleteLine();
        }

        /// <summary>Punctuation breathes: a beat after a sentence, half a beat after a comma.</summary>
        static float Pause(char c, char next) => c switch
        {
            '.' or '؟' or '?' or '!' or '…' when next == ' ' || next == '…' => 0.32f,
            '…' => 0.32f,
            '،' or ',' or ':' => 0.16f,
            _ => 0f,
        };

        void CompleteLine()
        {
            typing = false;
            line.text = fullText;
            var l = current.Lines[index];
            holdUntil = Time.unscaledTime + Mathf.Max(0.8f, l.Seconds);
        }

        // ---------------------------------------------------------------- pictures

        void Cut(string shotId)
        {
            var shot = data.Shot(shotId) ?? new DialogueShot { Id = shotId };
            var picture = Picture(string.IsNullOrEmpty(shot.Image) ? shot.Id : shot.Image);
            if (front.Shot == null)
            {
                Place(front, shot, picture, 1f);
            }
            else
            {
                (front, back) = (back, front); // the new picture fades in over the old one, under the shading
                back.Rect.SetSiblingIndex(0);
                front.Rect.SetSiblingIndex(1);
                Place(front, shot, picture, 0f);
            }
            if (!string.IsNullOrEmpty(shot.Flash) && ColorUtility.TryParseHtmlString(shot.Flash, out var fc))
                flash.color = new Color(fc.r, fc.g, fc.b, 0.6f);
            if (shot.Punch) { punchUntil = Time.unscaledTime + 0.55f; Typed?.Invoke("Cut"); }
        }

        void Place(Layer layer, DialogueShot shot, Texture2D picture, float alpha)
        {
            layer.Shot = shot;
            layer.Started = Time.unscaledTime;
            layer.Image.texture = picture;
            layer.Image.color = new Color(1, 1, 1, picture != null ? alpha : 0);
            Frame(layer);
        }

        Texture2D Picture(string id)
        {
            if (!pictures.TryGetValue(id, out var texture))
            {
                texture = Resources.Load<Texture2D>("Cutscenes/" + id);
                pictures[id] = texture;
            }
            return texture;
        }

        /// <summary>Fills the frame with the picture and pushes in on its focus (the focus stays put on screen).</summary>
        void Frame(Layer layer)
        {
            var shot = layer.Shot;
            var texture = layer.Image.texture;
            if (shot == null || texture == null) return;
            Vector2 area = frame.rect.size;
            float aspect = (float)texture.width / texture.height;
            Vector2 size = area.x / area.y > aspect ? new Vector2(area.x, area.x / aspect) : new Vector2(area.y * aspect, area.y);
            float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - layer.Started) / ShotSeconds));
            float zoomFrom = shot.ZoomFrom > 0 ? shot.ZoomFrom : 1f;
            float zoomTo = shot.ZoomTo > 0 ? shot.ZoomTo : 1.1f;
            float punch = Time.unscaledTime < punchUntil && layer == front ? (punchUntil - Time.unscaledTime) / 0.55f : 0f;
            float scale = Mathf.Lerp(zoomFrom, zoomTo, k) + 0.07f * punch * punch;
            Vector2 focus = (shot.Focus - new Vector2(0.5f, 0.5f)) * size;
            Vector2 pos = (1f - scale) * focus + Vector2.Scale(shot.Drift, size) * k;
            if (punch > 0) pos += UnityEngine.Random.insideUnitCircle * 22f * punch * punch;
            Vector2 slack = (size * scale - area) * 0.5f; // never show an edge
            pos = new Vector2(Mathf.Clamp(pos.x, -slack.x, slack.x), Mathf.Clamp(pos.y, -slack.y, slack.y));
            layer.Rect.sizeDelta = size;
            layer.Rect.localScale = new Vector3(scale, scale, 1);
            layer.Rect.anchoredPosition = pos;
        }

        // ---------------------------------------------------------------- frame

        void Update()
        {
            if (current == null || !sceneLayer.gameObject.activeSelf) return;
            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            Frame(back);
            Frame(front);
            if (front.Image.color.a < 1f && front.Image.texture != null)
                front.Image.color = new Color(1, 1, 1, Mathf.MoveTowards(front.Image.color.a, 1f, dt / CrossFade));
            if (flash.color.a > 0) flash.color = new Color(flash.color.r, flash.color.g, flash.color.b, Mathf.MoveTowards(flash.color.a, 0, dt / 0.7f));

            // The accent under the band's edge grows from the speaker's side.
            float width = ((RectTransform)accent.parent).rect.width * 0.42f;
            if (speakerName.text.Length > 0) accent.sizeDelta = new Vector2(Mathf.Lerp(accent.sizeDelta.x, width, 1f - Mathf.Exp(-dt * 9f)), 3);

            if (speaker?.Style == "Radio" && typing)
                line.color = new Color(lineColor.r, lineColor.g, lineColor.b, 0.72f + 0.28f * Mathf.PerlinNoise(now * 18f, 0.3f));
            PlaceCursor(now);

            bool waiting = !typing && !closing && index >= 0;
            marker.gameObject.SetActive(waiting && SubtitlesOn);
            if (waiting)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(now * 5f);
                markerImage.color = new Color(cursorImage.color.r, cursorImage.color.g, cursorImage.color.b, 0.35f + 0.65f * pulse);
                marker.anchoredPosition = new Vector2(110, 48 + 4 * pulse);
                if (now >= holdUntil) ShowLine(index + 1);
            }
        }

        /// <summary>A bar after the last typed letter: Persian runs right to left, so it sits at the left end of the last line.</summary>
        void PlaceCursor(float now)
        {
            bool show = SubtitlesOn && line.text.Length > 0 && (typing || Mathf.Repeat(now, 1f) < 0.55f) && !closing;
            cursor.gameObject.SetActive(show);
            if (!show) return;
            line.ForceMeshUpdate();
            var info = line.textInfo;
            if (info.lineCount == 0) { cursor.gameObject.SetActive(false); return; }
            var last = info.lineInfo[info.lineCount - 1];
            float left = float.MaxValue;
            for (int i = last.firstCharacterIndex; i <= last.lastCharacterIndex && i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (ch.isVisible) left = Mathf.Min(left, ch.bottomLeft.x);
            }
            if (left == float.MaxValue) { cursor.gameObject.SetActive(false); return; }
            Vector3 local = new(left - 10f, (last.ascender + last.descender) * 0.5f, 0);
            Vector3 world = line.rectTransform.TransformPoint(local);
            cursor.position = world;
            cursor.sizeDelta = new Vector2(4, line.fontSize * 0.95f);
        }

        public void ShowBark(string id)
        {
            // Retired bottom-of-screen barks. Authored conversation scenes remain available.
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Noctyra
{
    /// <summary>
    /// Menüler, oyun ekranı ve oyun akışı. Tüm arayüz çalışma anında kodla kurulur,
    /// bu yüzden sahnede (SampleScene) herhangi bir düzenleme gerekmez.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        // ---- genel
        RectTransform canvasRoot;
        Backdrop backdrop;
        RectTransform menuScreen, setupScreen, helpScreen, gameScreen, endScreen;
        Text statsText;

        // ---- kurulum ekranı
        InputField nameInput;
        Text countText, distText;
        Button roleButton, revealButton;
        readonly int[] roleChoices = { -1, 0, 1, 2, 3, 4 };
        int roleChoiceIndex;

        // ---- oyun ekranı
        Text headerText, promptText, logText;
        ScrollRect logScroll;
        RectTransform cardGrid, actionGrid;
        Button speedButton;
        readonly List<Image> cardImages = new List<Image>();
        readonly List<Text> cardTexts = new List<Text>();
        readonly List<string> lines = new List<string>();

        // ---- bitiş ekranı
        Text endTitle, endSub, endRoles;

        // ---- oyun durumu
        GameEngine engine;
        readonly GameSettings settings = new GameSettings();
        float speed = 1f;
        int lastChoice = -1;
        Player pickedPlayer;
        bool deathAnnounced;

        static readonly Color CardNormal = new Color(0.16f, 0.13f, 0.28f, 1f);
        static readonly Color CardHuman = new Color(0.24f, 0.17f, 0.44f, 1f);
        static readonly Color CardDead = new Color(0.09f, 0.09f, 0.11f, 0.9f);
        static readonly Color CardVampire = new Color(0.38f, 0.07f, 0.14f, 1f);
        static readonly Color CardClean = new Color(0.11f, 0.30f, 0.22f, 1f);

        // ====================================================================== Kurulum

        void Awake()
        {
            Application.runInBackground = true;
            EnsureEventSystem();
            BuildCanvas();
            BuildMenu();
            BuildSetup();
            BuildHelp();
            BuildGame();
            BuildEnd();
            backdrop.SetNight(true, true);
            GoMenu();
        }

        void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.transform.SetParent(transform, false);
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        void BuildCanvas()
        {
            var go = new GameObject("NoctyraCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.25f;
            canvasRoot = go.GetComponent<RectTransform>();

            backdrop = gameObject.AddComponent<Backdrop>();
            backdrop.Build(canvasRoot);
        }

        RectTransform NewScreen(string name)
        {
            var rt = UIKit.NewRect(name, canvasRoot);
            UIKit.Stretch(rt);
            return rt;
        }

        void ShowScreen(RectTransform screen)
        {
            menuScreen.gameObject.SetActive(screen == menuScreen);
            setupScreen.gameObject.SetActive(screen == setupScreen);
            helpScreen.gameObject.SetActive(screen == helpScreen);
            gameScreen.gameObject.SetActive(screen == gameScreen);
            endScreen.gameObject.SetActive(false);
        }

        // ====================================================================== Menü

        void BuildMenu()
        {
            menuScreen = NewScreen("Menu");

            var title = UIKit.Label(menuScreen, "NOCTYRA", 170, UIKit.Crimson, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Anchor(title.rectTransform, 0.1f, 0.64f, 0.9f, 0.88f);

            var sub = UIKit.Label(menuScreen, "Vampir  •  Köylü", 44, UIKit.Muted);
            UIKit.Anchor(sub.rectTransform, 0.2f, 0.57f, 0.8f, 0.65f);

            var col = UIKit.NewRect("Buttons", menuScreen);
            col.anchorMin = col.anchorMax = new Vector2(0.5f, 0.34f);
            col.sizeDelta = new Vector2(560f, 400f);
            var vlg = col.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 22f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;

            UIKit.MakeButton(col, "Oyna", UIKit.Crimson, GoSetup, 40);
            UIKit.MakeButton(col, "Nasıl Oynanır?", UIKit.ButtonColor, GoHelp, 36);
            UIKit.MakeButton(col, "Çıkış", UIKit.ButtonColor, QuitGame, 36);

            statsText = UIKit.Label(menuScreen, "", 30, UIKit.Muted, TextAnchor.MiddleLeft);
            UIKit.Anchor(statsText.rectTransform, 0.02f, 0.02f, 0.45f, 0.07f);

            var ver = UIKit.Label(menuScreen, "v" + Application.version, 28, UIKit.Muted, TextAnchor.MiddleRight);
            UIKit.Anchor(ver.rectTransform, 0.75f, 0.02f, 0.98f, 0.07f);
        }

        void RefreshStats()
        {
            statsText.text = "Galibiyet: " + PlayerPrefs.GetInt("wins", 0) +
                             "     Mağlubiyet: " + PlayerPrefs.GetInt("losses", 0);
        }

        void GoMenu()
        {
            StopAllCoroutines();
            if (actionGrid != null) UIKit.ClearChildren(actionGrid);
            backdrop.SetNight(true, false);
            RefreshStats();
            ShowScreen(menuScreen);
        }

        void GoSetup()
        {
            RefreshSetup();
            ShowScreen(setupScreen);
        }

        void GoHelp()
        {
            ShowScreen(helpScreen);
        }

        void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ====================================================================== Kurulum ekranı

        void BuildSetup()
        {
            setupScreen = NewScreen("Setup");
            var panel = UIKit.Panel(setupScreen, "Panel", UIKit.PanelColor);
            UIKit.Anchor(panel.rectTransform, 0.24f, 0.08f, 0.76f, 0.92f);
            var p = panel.transform;

            var title = UIKit.Label(p, "OYUN KURULUMU", 54, UIKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Anchor(title.rectTransform, 0f, 0.88f, 1f, 0.98f);

            // Ad
            Row(p, "Adın", 0.76f);
            var inRt = UIKit.NewRect("NameInput", p);
            UIKit.Anchor(inRt, 0.48f, 0.76f, 0.94f, 0.85f);
            var inImg = inRt.gameObject.AddComponent<Image>();
            inImg.color = new Color(0.14f, 0.12f, 0.25f, 1f);
            var inText = UIKit.Label(inRt, "", 32, UIKit.Ink, TextAnchor.MiddleLeft);
            Pad(inText.rectTransform);
            inText.supportRichText = false;
            var inHolder = UIKit.Label(inRt, "Adını yaz...", 32, UIKit.Muted, TextAnchor.MiddleLeft, FontStyle.Italic);
            Pad(inHolder.rectTransform);
            nameInput = inRt.gameObject.AddComponent<InputField>();
            nameInput.targetGraphic = inImg;
            nameInput.textComponent = inText;
            nameInput.placeholder = inHolder;
            nameInput.characterLimit = 14;
            nameInput.text = settings.HumanName;

            // Oyuncu sayısı
            Row(p, "Oyuncu sayısı", 0.63f);
            var minus = UIKit.MakeButton(p, "-", UIKit.ButtonColor, () => ChangeCount(-1), 44);
            UIKit.Anchor((RectTransform)minus.transform, 0.48f, 0.63f, 0.58f, 0.72f);
            countText = UIKit.Label(p, "8", 44, UIKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Anchor(countText.rectTransform, 0.58f, 0.63f, 0.84f, 0.72f);
            var plus = UIKit.MakeButton(p, "+", UIKit.ButtonColor, () => ChangeCount(1), 44);
            UIKit.Anchor((RectTransform)plus.transform, 0.84f, 0.63f, 0.94f, 0.72f);

            // Rol
            Row(p, "Rolün", 0.50f);
            roleButton = UIKit.MakeButton(p, "Rastgele", UIKit.ButtonColor, CycleRole, 32);
            UIKit.Anchor((RectTransform)roleButton.transform, 0.48f, 0.50f, 0.94f, 0.59f);

            // Rol açıklama
            Row(p, "Ölünce rol açıklansın", 0.37f);
            revealButton = UIKit.MakeButton(p, "Açık", UIKit.ButtonColor, ToggleReveal, 32);
            UIKit.Anchor((RectTransform)revealButton.transform, 0.48f, 0.37f, 0.94f, 0.46f);

            distText = UIKit.Label(p, "", 30, UIKit.Muted);
            UIKit.Anchor(distText.rectTransform, 0.05f, 0.20f, 0.95f, 0.34f);

            var back = UIKit.MakeButton(p, "Geri", UIKit.ButtonColor, GoMenu, 36);
            UIKit.Anchor((RectTransform)back.transform, 0.06f, 0.04f, 0.48f, 0.16f);
            var start = UIKit.MakeButton(p, "Başla", UIKit.Crimson, OnStartPressed, 40);
            UIKit.Anchor((RectTransform)start.transform, 0.52f, 0.04f, 0.94f, 0.16f);
        }

        void Row(Transform parent, string label, float y)
        {
            var t = UIKit.Label(parent, label, 32, UIKit.Ink, TextAnchor.MiddleLeft);
            UIKit.Anchor(t.rectTransform, 0.06f, y, 0.46f, y + 0.09f);
        }

        static void Pad(RectTransform rt)
        {
            UIKit.Stretch(rt);
            rt.offsetMin = new Vector2(16f, 4f);
            rt.offsetMax = new Vector2(-16f, -4f);
        }

        void ChangeCount(int d)
        {
            settings.PlayerCount = Mathf.Clamp(settings.PlayerCount + d, 6, 12);
            RefreshSetup();
        }

        void CycleRole()
        {
            roleChoiceIndex = (roleChoiceIndex + 1) % roleChoices.Length;
            settings.RolePreference = roleChoices[roleChoiceIndex];
            RefreshSetup();
        }

        void ToggleReveal()
        {
            settings.RevealRoles = !settings.RevealRoles;
            RefreshSetup();
        }

        void RefreshSetup()
        {
            countText.text = settings.PlayerCount.ToString();
            UIKit.SetButtonText(roleButton, roleChoiceIndex == 0 ? "Rastgele" : RoleInfo.Name((Role)roleChoices[roleChoiceIndex]));
            UIKit.SetButtonText(revealButton, settings.RevealRoles ? "Açık" : "Kapalı");
            distText.text = "Rol dağılımı: " + GameEngine.Describe(settings.PlayerCount);
        }

        void OnStartPressed()
        {
            string n = (nameInput.text ?? "").Replace("<", "").Replace(">", "").Trim();
            settings.HumanName = n.Length == 0 ? "Sen" : n;
            StartNewGame();
        }

        // ====================================================================== Yardım ekranı

        void BuildHelp()
        {
            helpScreen = NewScreen("Help");
            var panel = UIKit.Panel(helpScreen, "Panel", UIKit.PanelColor);
            UIKit.Anchor(panel.rectTransform, 0.14f, 0.05f, 0.86f, 0.95f);
            var p = panel.transform;

            var title = UIKit.Label(p, "NASIL OYNANIR?", 54, UIKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Anchor(title.rectTransform, 0f, 0.90f, 1f, 0.99f);

            string body =
                "Köy iki gruba ayrılmıştır: <b>Köylüler</b> ve <b>Vampirler</b>.\n" +
                "Köylüler tüm vampirleri bulup asarsa kazanır. Vampirler sayıca köylülere eşitlenirse kazanır.\n\n" +
                "<b>GECE:</b> Vampirler birini avlar. Doktor birini korur (aynı kişiyi üst üste iki gece koruyamaz). Falcı birinin vampir olup olmadığını öğrenir.\n" +
                "<b>GÜN:</b> Köylüler tartışır, birbirini suçlar, rol açıklar. Sonra oylama yapılır; en çok oyu alan asılır (oylar eşitse kimse asılmaz).\n\n" +
                "<b>ROLLER</b>\n" +
                "<b>Köylü</b> – Özel gücü yok; mantığına ve diline güven.\n" +
                "<b>Vampir</b> – Gece avlanır, gündüz masum görünür, diğer vampirleri tanır.\n" +
                "<b>Doktor</b> – Her gece birini korur.\n" +
                "<b>Falcı</b> – Her gece birinin gerçek yüzünü görür.\n" +
                "<b>Avcı</b> – Öldüğünde yanına birini alır.\n\n" +
                "<b>İPUCU:</b> Rolünü açıklarken blöf yapabilirsin, ama botlar da yalan söyleyebilir. Kim kime oy verdi, kim kimi savundu, dikkat et!";

            var text = UIKit.Label(p, body, 30, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Anchor(text.rectTransform, 0.05f, 0.14f, 0.95f, 0.89f);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = 32;

            var back = UIKit.MakeButton(p, "Geri", UIKit.ButtonColor, GoMenu, 36);
            UIKit.Anchor((RectTransform)back.transform, 0.35f, 0.03f, 0.65f, 0.11f);
        }

        // ====================================================================== Oyun ekranı

        void BuildGame()
        {
            gameScreen = NewScreen("Game");

            var logo = UIKit.Label(gameScreen, "NOCTYRA", 36, UIKit.Crimson, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Anchor(logo.rectTransform, 0.02f, 0.92f, 0.22f, 1f);

            headerText = UIKit.Label(gameScreen, "", 54, UIKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Anchor(headerText.rectTransform, 0.30f, 0.92f, 0.70f, 1f);

            speedButton = UIKit.MakeButton(gameScreen, "Hız x1", UIKit.ButtonColor, ToggleSpeed, 26);
            UIKit.Anchor((RectTransform)speedButton.transform, 0.74f, 0.935f, 0.855f, 0.99f);
            var menu = UIKit.MakeButton(gameScreen, "Menü", UIKit.ButtonColor, GoMenu, 26);
            UIKit.Anchor((RectTransform)menu.transform, 0.87f, 0.935f, 0.98f, 0.99f);

            // oyuncu kartları
            var players = UIKit.Panel(gameScreen, "Players", UIKit.PanelColor);
            UIKit.Anchor(players.rectTransform, 0.02f, 0.05f, 0.40f, 0.90f);
            var pTitle = UIKit.Label(players.transform, "KÖYLÜLER", 30, UIKit.Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Anchor(pTitle.rectTransform, 0f, 0.93f, 1f, 1f);
            cardGrid = UIKit.NewRect("Grid", players.transform);
            UIKit.Anchor(cardGrid, 0f, 0f, 1f, 0.93f);
            var grid = cardGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(205f, 125f);
            grid.spacing = new Vector2(12f, 12f);
            grid.padding = new RectOffset(14, 14, 10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperCenter;

            // olay günlüğü
            var logPanel = UIKit.Panel(gameScreen, "Log", UIKit.PanelColor);
            UIKit.Anchor(logPanel.rectTransform, 0.42f, 0.36f, 0.98f, 0.90f);
            var scrollRt = UIKit.NewRect("Scroll", logPanel.transform);
            UIKit.Anchor(scrollRt, 0.01f, 0.02f, 0.99f, 0.98f);
            var hit = scrollRt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            logScroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            var viewport = UIKit.NewRect("Viewport", scrollRt);
            UIKit.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            logText = UIKit.Label(viewport, "", 30, UIKit.Ink, TextAnchor.UpperLeft);
            logText.lineSpacing = 1.1f;
            var lr = logText.rectTransform;
            lr.anchorMin = new Vector2(0.015f, 1f);
            lr.anchorMax = new Vector2(0.985f, 1f);
            lr.pivot = new Vector2(0.5f, 1f);
            lr.anchoredPosition = Vector2.zero;
            lr.sizeDelta = Vector2.zero;
            var fitter = logText.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            logScroll.viewport = viewport;
            logScroll.content = lr;
            logScroll.horizontal = false;
            logScroll.vertical = true;
            logScroll.movementType = ScrollRect.MovementType.Clamped;
            logScroll.scrollSensitivity = 40f;

            // aksiyon paneli
            var action = UIKit.Panel(gameScreen, "Actions", UIKit.PanelColor);
            UIKit.Anchor(action.rectTransform, 0.42f, 0.05f, 0.98f, 0.34f);
            promptText = UIKit.Label(action.transform, "", 32, UIKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Anchor(promptText.rectTransform, 0.02f, 0.78f, 0.98f, 0.99f);
            actionGrid = UIKit.NewRect("Buttons", action.transform);
            UIKit.Anchor(actionGrid, 0.02f, 0.03f, 0.98f, 0.77f);
            var ag = actionGrid.gameObject.AddComponent<GridLayoutGroup>();
            ag.cellSize = new Vector2(220f, 62f);
            ag.spacing = new Vector2(10f, 10f);
            ag.childAlignment = TextAnchor.UpperCenter;
        }

        void ToggleSpeed()
        {
            speed = speed >= 3f ? 1f : speed + 1f;
            UIKit.SetButtonText(speedButton, "Hız x" + (int)speed);
        }

        void BuildCards()
        {
            UIKit.ClearChildren(cardGrid);
            cardImages.Clear();
            cardTexts.Clear();
            foreach (var p in engine.Players)
            {
                var img = UIKit.Panel(cardGrid, "Card_" + p.Name, CardNormal);
                var txt = UIKit.Label(img.transform, "", 28, UIKit.Ink, TextAnchor.MiddleCenter);
                UIKit.Stretch(txt.rectTransform);
                txt.resizeTextForBestFit = true;
                txt.resizeTextMinSize = 14;
                txt.resizeTextMaxSize = 28;
                cardImages.Add(img);
                cardTexts.Add(txt);
            }
        }

        void RefreshPlayers()
        {
            if (engine == null || cardTexts.Count != engine.Players.Count) return;
            bool showAll = !engine.Human.Alive || engine.Winner != null;
            for (int i = 0; i < engine.Players.Count; i++)
            {
                var p = engine.Players[i];
                string role = RoleInfo.Name(p.Role);
                string status;
                Color bg = CardNormal;
                Color fg = UIKit.Ink;

                bool inspected;
                bool inspectedVamp = false;
                inspected = engine.HumanInspections.TryGetValue(p.Id, out inspectedVamp);

                if (!p.Alive)
                {
                    status = (engine.Settings.RevealRoles || showAll) ? "† " + role : "† öldü";
                    bg = CardDead;
                    fg = UIKit.Muted;
                }
                else if (p.IsHuman)
                {
                    status = "Sen • " + role;
                    bg = CardHuman;
                }
                else if (showAll)
                {
                    status = role;
                    if (p.Role == Role.Vampire) bg = CardVampire;
                }
                else if (engine.Human.Role == Role.Vampire && p.Role == Role.Vampire)
                {
                    status = "Vampir (müttefik)";
                    bg = CardVampire;
                }
                else if (inspected)
                {
                    status = inspectedVamp ? "Vampir!" : "Temiz";
                    bg = inspectedVamp ? CardVampire : CardClean;
                }
                else
                {
                    status = "Hayatta";
                }

                cardTexts[i].text = "<b>" + p.Name + "</b>\n" + status;
                cardTexts[i].color = fg;
                cardImages[i].color = bg;
            }
        }

        // ====================================================================== Günlük ve yardımcılar

        static string C(string hex, string s) { return "<color=#" + hex + ">" + s + "</color>"; }

        void AddLine(string rich)
        {
            lines.Add(rich);
            if (lines.Count > 120) lines.RemoveAt(0);
            logText.text = string.Join("\n", lines.ToArray());
            StartCoroutine(ScrollDown());
        }

        IEnumerator ScrollDown()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(logText.rectTransform);
            logScroll.verticalNormalizedPosition = 0f;
        }

        void Narrate(string text) { AddLine(C("A9A1C0", "<i>" + text + "</i>")); }

        void Alert(string text) { AddLine("<b>" + C("F0607A", text) + "</b>"); }

        void Private(string text) { AddLine(C("7FE0A8", "► " + text)); }

        void Say(Player speaker, string text)
        {
            string col = speaker.IsHuman ? "F2C46D" : "C9B8FF";
            AddLine("<b>" + C(col, speaker.Name) + "</b>: " + text);
        }

        void SetHeader(string text, bool night)
        {
            headerText.text = text;
            backdrop.SetNight(night, false);
        }

        WaitForSeconds W(float seconds) { return new WaitForSeconds(seconds / speed); }

        IEnumerator Choose(string prompt, List<string> labels)
        {
            lastChoice = -1;
            promptText.text = prompt;
            UIKit.ClearChildren(actionGrid);
            for (int i = 0; i < labels.Count; i++)
            {
                int idx = i;
                UIKit.MakeButton(actionGrid, labels[i], UIKit.ButtonColor, () => { lastChoice = idx; }, 28);
            }
            while (lastChoice < 0) yield return null;
            UIKit.ClearChildren(actionGrid);
            promptText.text = "";
        }

        IEnumerator PickPlayer(string prompt, List<Player> cands, string skipLabel)
        {
            var labels = cands.Select(c => c.Name).ToList();
            if (skipLabel != null) labels.Add(skipLabel);
            yield return Choose(prompt, labels);
            pickedPlayer = (lastChoice >= 0 && lastChoice < cands.Count) ? cands[lastChoice] : null;
        }

        IEnumerator Play(List<Statement> statements)
        {
            foreach (var st in statements)
            {
                Say(st.Speaker, st.Text);
                yield return W(0.95f);
            }
        }

        string RevealText(Player p)
        {
            return engine.Settings.RevealRoles ? " Rolü: " + RoleInfo.Name(p.Role) + "." : "";
        }

        void CheckHumanDeath()
        {
            if (engine.Human.Alive || deathAnnounced) return;
            deathAnnounced = true;
            Alert("Öldün! Artık izleyicisin; herkesin rolü görünür.");
        }

        // ====================================================================== Oyun akışı

        void StartNewGame()
        {
            StopAllCoroutines();
            backdrop.SetNight(true, true);
            ShowScreen(gameScreen);
            StartCoroutine(RunGame());
        }

        IEnumerator RunGame()
        {
            engine = new GameEngine();
            engine.StartGame(settings, Random.Range(0, int.MaxValue));
            deathAnnounced = false;
            lines.Clear();
            logText.text = "";
            BuildCards();
            RefreshPlayers();
            SetHeader("Hazırlık", true);

            var me = engine.Human;
            Narrate("Noctyra köyüne hoş geldin. Burada herkes bir sır saklıyor...");
            AddLine("<b>" + C(UIKit.Hex(UIKit.Crimson), "Rolün: " + RoleInfo.Name(me.Role)) + "</b>");
            Narrate(RoleInfo.Description(me.Role));
            if (me.Role == Role.Vampire)
            {
                var allies = engine.Players.Where(p => p.Role == Role.Vampire && !p.IsHuman).Select(p => p.Name).ToArray();
                Private(allies.Length > 0 ? "Müttefik vampirler: " + string.Join(", ", allies) : "Köydeki tek vampirsin.");
            }
            Narrate("Köyde " + engine.Players.Count + " kişi var. (" + GameEngine.Describe(engine.Players.Count) + ")");

            yield return Choose("Hazır olduğunda devam et.", new List<string> { "Geceye başla" });

            while (true)
            {
                yield return NightPhase();
                if (engine.Winner != null) break;
                yield return DayPhase();
                if (engine.Winner != null) break;
            }

            RefreshPlayers();
            yield return W(1.2f);
            ShowEnd();
        }

        IEnumerator NightPhase()
        {
            engine.BeginNight();
            var me = engine.Human;
            SetHeader("Gece " + engine.Day, true);
            Narrate("<b>Gece " + engine.Day + "</b> — köy uykuya dalıyor...");
            yield return W(1.0f);

            if (me.Alive && engine.HumanHasNightAction)
            {
                var targets = engine.NightTargets();
                string prompt = me.Role == Role.Vampire ? "Vampir: Bu gece kimi avlayacaksın?"
                              : me.Role == Role.Doctor ? "Doktor: Bu gece kimi koruyacaksın?"
                              : "Falcı: Bu gece kimin kimliğine bakacaksın?";
                if (targets.Count > 0)
                {
                    yield return PickPlayer(prompt, targets, null);
                    string info = engine.SubmitHumanNight(pickedPlayer);
                    if (info != null) Private(info);
                    RefreshPlayers();
                }
                else
                {
                    Narrate("Bu gece yapacak bir şeyin yok.");
                }
            }
            else if (me.Alive)
            {
                Narrate("Rolünün gece yeteneği yok. Gözlerini kapatıp karanlığı dinliyorsun...");
            }
            else
            {
                Narrate("(İzleyici) Gece ilerliyor...");
            }
            yield return W(1.5f);

            var rep = engine.ResolveNight();

            SetHeader("Gün " + engine.Day, false);
            yield return W(1.0f);

            if (rep.Death != null)
            {
                Alert("Sabah oldu. " + rep.Death.Victim.Name + " ölü bulundu. Boynunda iki delik var...");
                if (engine.Settings.RevealRoles) Narrate(RevealText(rep.Death.Victim).Trim());
            }
            else
            {
                Narrate("Sabah oldu. Gece sessiz geçti, kimse ölmedi.");
            }

            if (rep.AttackSaved)
            {
                if (me.Alive && me.Role == Role.Vampire) Private("Doktor, hedefini kurtardı!");
                else if (me.Alive && me.Role == Role.Doctor) Private("Bu gece koruduğun kişi saldırıya uğradı. Başardın!");
                else if (rep.SavedPlayer == me) Private("Gece saldırıya uğradın ama biri seni kurtardı!");
            }

            RefreshPlayers();
            CheckHumanDeath();
            yield return W(1.0f);

            yield return HunterPhase();
            RefreshPlayers();
            CheckHumanDeath();
            engine.CheckWinner();
        }

        IEnumerator DayPhase()
        {
            engine.BeginDay();
            var me = engine.Human;
            Narrate("Köylüler meydanda toplandı. Tartışma başlıyor...");
            yield return W(0.8f);

            yield return Play(engine.OpeningRound());
            if (!me.Alive) yield return Play(engine.OpeningRound());

            int used = 0;
            while (me.Alive && used < 3)
            {
                var labels = new List<string> { "Birini suçla", "Kendimi savun" };
                bool canClaim = !engine.HasClaimed(me);
                if (canClaim) labels.Add("Rolümü açıkla");
                labels.Add("Oylamaya geç");

                yield return Choose("Söz sırası sende. (" + (3 - used) + " hakkın var)", labels);
                string pick = labels[lastChoice];
                if (pick == "Oylamaya geç") break;

                if (pick == "Birini suçla")
                {
                    var cands = engine.AlivePlayers().Where(p => p != me).ToList();
                    yield return PickPlayer("Kimi suçluyorsun?", cands, "Vazgeç");
                    if (pickedPlayer == null) continue;
                    used++;
                    yield return Play(engine.HumanAccuse(pickedPlayer));
                }
                else if (pick == "Kendimi savun")
                {
                    used++;
                    yield return Play(engine.HumanDefend());
                }
                else
                {
                    yield return Choose("Hangi rolü açıklıyorsun? (doğruyu ya da blöf)", new List<string> { "Falcı", "Doktor", "Vazgeç" });
                    if (lastChoice == 2) continue;
                    if (lastChoice == 0)
                    {
                        var cands = engine.AlivePlayers().Where(p => p != me).ToList();
                        yield return PickPlayer("Kimin kimliğine baktığını söyleyeceksin?", cands, "Vazgeç");
                        if (pickedPlayer == null) continue;
                        var target = pickedPlayer;
                        yield return Choose(target.Name + " için ne diyeceksin?", new List<string> { "Vampir", "Temiz" });
                        bool vamp = lastChoice == 0;
                        used++;
                        yield return Play(engine.HumanClaim(Role.Seer, target, vamp));
                    }
                    else
                    {
                        used++;
                        yield return Play(engine.HumanClaim(Role.Doctor, null, false));
                    }
                }
            }

            if (me.Alive && used >= 3) Narrate("Köylüler sabırsızlanıyor, oylamaya geçiliyor.");

            // ---- oylama
            Narrate("<b>Oylama zamanı.</b>");
            yield return W(0.8f);

            Player humanVote = null;
            if (me.Alive)
            {
                var cands = engine.AlivePlayers().Where(p => p != me).ToList();
                yield return PickPlayer("Kimi asmak istiyorsun?", cands, "Çekimser kal");
                humanVote = pickedPlayer;
            }
            else
            {
                yield return W(0.8f);
            }

            var vr = engine.RunVote(humanVote);
            for (int i = 0; i < vr.Voters.Count; i++)
            {
                string tgt = vr.Targets[i] == null ? "çekimser" : vr.Targets[i].Name;
                Narrate(vr.Voters[i].Name + " → " + tgt);
                yield return W(0.35f);
            }

            var death = engine.ApplyLynch(vr);
            if (death != null)
            {
                Alert(death.Victim.Name + " köylüler tarafından asıldı." + RevealText(death.Victim));
            }
            else
            {
                Narrate("Oylar eşit ya da yetersiz. Kimse asılmadı.");
            }
            RefreshPlayers();
            CheckHumanDeath();
            yield return W(1.0f);

            yield return HunterPhase();
            RefreshPlayers();
            CheckHumanDeath();
            engine.CheckWinner();
            yield return W(0.8f);
        }

        IEnumerator HunterPhase()
        {
            var h = engine.PendingHunter;
            if (h == null) yield break;

            Alert(h.Name + " bir Avcıydı! Son nefesinde silahına davranıyor...");
            yield return W(1.0f);

            Player target;
            if (h.IsHuman)
            {
                var cands = engine.AlivePlayers();
                yield return PickPlayer("Avcı: Son kurşununu kime sıkacaksın?", cands, "Ateş etme");
                target = pickedPlayer;
            }
            else
            {
                target = engine.BotHunterTarget();
            }

            var d = engine.ResolveHunter(target);
            if (d != null)
                Alert("Kurşun " + d.Victim.Name + " adlı oyuncuya isabet etti!" + RevealText(d.Victim));
            else
                Narrate("Avcı ateş etmedi.");
            yield return W(1.0f);
        }

        // ====================================================================== Bitiş ekranı

        void BuildEnd()
        {
            endScreen = NewScreen("End");
            var dim = endScreen.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.78f);

            var panel = UIKit.Panel(endScreen, "Panel", new Color(0.08f, 0.06f, 0.16f, 0.97f));
            UIKit.Anchor(panel.rectTransform, 0.26f, 0.08f, 0.74f, 0.92f);
            var p = panel.transform;

            endTitle = UIKit.Label(p, "", 70, UIKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Anchor(endTitle.rectTransform, 0.02f, 0.84f, 0.98f, 0.97f);
            endSub = UIKit.Label(p, "", 38, UIKit.Muted);
            UIKit.Anchor(endSub.rectTransform, 0.02f, 0.76f, 0.98f, 0.84f);

            endRoles = UIKit.Label(p, "", 30, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Anchor(endRoles.rectTransform, 0.1f, 0.20f, 0.9f, 0.75f);
            endRoles.resizeTextForBestFit = true;
            endRoles.resizeTextMinSize = 16;
            endRoles.resizeTextMaxSize = 32;

            var again = UIKit.MakeButton(p, "Tekrar Oyna", UIKit.Crimson, StartNewGame, 36);
            UIKit.Anchor((RectTransform)again.transform, 0.06f, 0.04f, 0.48f, 0.15f);
            var menu = UIKit.MakeButton(p, "Ana Menü", UIKit.ButtonColor, GoMenu, 36);
            UIKit.Anchor((RectTransform)menu.transform, 0.52f, 0.04f, 0.94f, 0.15f);

            endScreen.gameObject.SetActive(false);
        }

        void ShowEnd()
        {
            Team w = engine.Winner.Value;
            bool humanWon = RoleInfo.TeamOf(engine.Human.Role) == w;
            string key = humanWon ? "wins" : "losses";
            PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) + 1);
            PlayerPrefs.Save();

            endTitle.text = w == Team.Villagers ? "KÖYLÜLER KAZANDI" : "VAMPİRLER KAZANDI";
            endTitle.color = w == Team.Villagers ? new Color(0.45f, 0.85f, 0.60f) : UIKit.Crimson;
            endSub.text = humanWon ? "Tebrikler, zafer senin!" : "Bu sefer olmadı... Rövanş?";

            var sb = new System.Text.StringBuilder();
            foreach (var p in engine.Players)
            {
                string col = p.Role == Role.Vampire ? "F0607A" : "CFC8E6";
                string name = p.IsHuman ? p.Name + " (Sen)" : p.Name;
                sb.Append("<color=#").Append(col).Append(">").Append(name).Append("  —  ")
                  .Append(RoleInfo.Name(p.Role)).Append(p.Alive ? "" : "  †").Append("</color>\n");
            }
            endRoles.text = sb.ToString();

            endScreen.gameObject.SetActive(true);
            endScreen.SetAsLastSibling();
        }
    }
}

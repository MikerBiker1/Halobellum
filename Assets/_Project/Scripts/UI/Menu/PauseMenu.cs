// PauseMenu.cs
// Esc in any scene with a battle (the Match, the Sandbox) opens this menu (M10): Resume, Settings, Quit to Main Menu, Quit Game. It adds
// itself to the scene when the scene loads, so no scene file holds it. Pausing is GamePause: time stops, so the battle stops in whatever
// phase it is. While paused, the HUD and everything that takes clicks or keys on the field are switched off, so that nothing behind the
// menu can react; they come back as they were. Leaving asks first, because the match is lost.
// Esc still does its old jobs first: while something is being placed it cancels that, and with a squad selected it clears the selection
// (those scripts see the same key press, so this one must look before they change anything; hence its early execution order).
// A small Menu button in the screen's corner does the same for those who don't know about Esc.
// Depends on: GameFlow, GamePause, BattleController, SquadSelection, DeploymentInput, SpellView, BattleCameraController, UiHitAreas,
//             MenuStyle, SettingsScreen, Unity Input System.

using System;
using System.Collections.Generic;
using Halobellum.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Halobellum.UI
{
    [DefaultExecutionOrder(-300)]
    public sealed class PauseMenu : MonoBehaviour
    {
        /// <summary>The object every HUD of a scene lives on (BattleSceneBuilder).</summary>
        public const string HudObjectName = "HUD";

        private const float PanelWidth = 520f, ButtonHeight = 62f;

        private enum Page { Buttons, Settings, ConfirmMainMenu, ConfirmQuit }

        private readonly SettingsScreen _settings = new SettingsScreen();
        private readonly List<Behaviour> _switchedOff = new List<Behaviour>();
        private BattleController _battle;
        private SquadSelection _selection;
        private Page _page;

        /// <summary>The pause menu of the loaded scene, or null if it has none.</summary>
        public static PauseMenu Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (GameFlow.IsMeasuring(Environment.GetCommandLineArgs())) return;
            var battle = FindAnyObjectByType<BattleController>();
            if (battle == null) return;
            var host = new GameObject("Pause Menu");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<PauseMenu>()._battle = battle;
        }

        private void Awake()
        {
            Current = this;
            useGUILayout = false; // Nothing here uses GUILayout. With the layout pass on, Unity makes new layout groups for this script every frame, which is garbage at every frame rate.
        }

        private void Start() => _selection = _battle != null ? _battle.GetComponent<SquadSelection>() : null;

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            if (GamePause.IsPaused) GamePause.Resume();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
            if (!GamePause.IsPaused)
            {
                if (!EscapeHasOtherWork()) Open();
                return;
            }
            if (_page == Page.Buttons) Close();
            else
            {
                if (_page == Page.Settings) _settings.Close();
                _page = Page.Buttons;
            }
        }

        /// <summary>True if Esc is about to cancel a placement or clear the selection instead.</summary>
        private bool EscapeHasOtherWork()
        {
            if (_selection == null) return false;
            return !_selection.InputEnabled || _selection.SelectedSquadId >= 0 || _selection.SelectedTower != null;
        }

        /// <summary>Pauses the game and shows the menu.</summary>
        public void Open()
        {
            if (GamePause.IsPaused) return;
            _page = Page.Buttons;
            GamePause.Pause();
            SwitchOffWhatTakesInput();
        }

        /// <summary>Closes the menu and lets the game run on.</summary>
        public void Close()
        {
            if (!GamePause.IsPaused) return;
            foreach (Behaviour behaviour in _switchedOff)
            {
                if (behaviour != null) behaviour.enabled = true;
            }
            _switchedOff.Clear();
            GamePause.Resume();
        }

        private void SwitchOffWhatTakesInput()
        {
            _switchedOff.Clear();
            GameObject hud = GameObject.Find(HudObjectName);
            if (hud != null)
            {
                foreach (MonoBehaviour behaviour in hud.GetComponents<MonoBehaviour>()) SwitchOff(behaviour);
            }
            if (_battle != null)
            {
                SwitchOff(_battle.GetComponent<DeploymentInput>());
                SwitchOff(_battle.GetComponent<SquadSelection>());
                SwitchOff(_battle.GetComponent<SpellView>());
            }
            Camera camera = Camera.main;
            if (camera != null) SwitchOff(camera.GetComponent<BattleCameraController>());
        }

        private void SwitchOff(Behaviour behaviour)
        {
            if (behaviour == null || !behaviour.enabled) return;
            behaviour.enabled = false;
            _switchedOff.Add(behaviour);
        }

        private void OnGUI()
        {
            MenuStyle.Ensure();
            GUI.depth = -100; // Over every HUD.
            float scale = HudScale.ApplyFit(SettingsScreen.PanelWidth + 20f, SettingsScreen.PanelHeight + 20f);
            float width = HudScale.ScaledWidth(scale), height = HudScale.ScaledHeight(scale);

            if (!GamePause.IsPaused)
            {
                DrawCornerButton(width, height, scale);
                return;
            }

            UiHitAreas.Add(new Rect(0f, 0f, Screen.width, Screen.height));
            MenuStyle.Fill(new Rect(0f, 0f, width, height), new Color(0.01f, 0.01f, 0.02f, 0.62f));
            switch (_page)
            {
                case Page.Buttons: DrawButtons(width, height); break;
                case Page.Settings:
                    if (_settings.Draw(width, height)) _page = Page.Buttons;
                    break;
                case Page.ConfirmMainMenu: DrawConfirm(width, height, "Leave for the main menu?", GameFlow.OpenMainMenu); break;
                case Page.ConfirmQuit: DrawConfirm(width, height, "Quit the game?", GameFlow.QuitGame); break;
            }
        }

        private void DrawCornerButton(float width, float height, float scale)
        {
            var rect = new Rect(width - 106f, height - 44f, 96f, 34f);
            UiHitAreas.Add(HudScale.ToScreen(rect, scale));
            MenuStyle.Fill(rect, new Color(0.03f, 0.035f, 0.05f, 0.7f));
            if (MenuStyle.SmallFramedButton(rect, "Menu", false)) Open();
        }

        private void DrawButtons(float width, float height)
        {
            string[] labels = { "Resume", "Settings", "Quit to Main Menu", "Quit Game" };
            float panelHeight = 118f + labels.Length * (ButtonHeight + 6f) + 20f;
            var panel = new Rect((width - PanelWidth) * 0.5f, (height - panelHeight) * 0.5f, PanelWidth, panelHeight);
            MenuStyle.Panel(panel);
            GUI.Label(new Rect(panel.x, panel.y + 24f, panel.width, 70f), "PAUSED", MenuStyle.Heading);

            float x = panel.x + 40f, y = panel.y + 118f, buttonWidth = panel.width - 80f;
            if (MenuStyle.MenuButton(new Rect(x, y, buttonWidth, ButtonHeight), labels[0])) Close();
            y += ButtonHeight + 6f;
            if (MenuStyle.MenuButton(new Rect(x, y, buttonWidth, ButtonHeight), labels[1]))
            {
                _settings.Open();
                _page = Page.Settings;
            }
            y += ButtonHeight + 6f;
            if (MenuStyle.MenuButton(new Rect(x, y, buttonWidth, ButtonHeight), labels[2])) _page = Page.ConfirmMainMenu;
            y += ButtonHeight + 6f;
            if (MenuStyle.MenuButton(new Rect(x, y, buttonWidth, ButtonHeight), labels[3])) _page = Page.ConfirmQuit;
        }

        private void DrawConfirm(float width, float height, string question, Action leave)
        {
            var panel = new Rect((width - PanelWidth) * 0.5f, (height - 250f) * 0.5f, PanelWidth, 250f);
            MenuStyle.Panel(panel);
            GUI.Label(new Rect(panel.x + 30f, panel.y + 26f, panel.width - 60f, 56f), question, MenuStyle.SmallButton);
            GUI.Label(new Rect(panel.x + 30f, panel.y + 82f, panel.width - 60f, 40f), _battle != null && _battle.GetComponent<MatchController>() != null ? "This match will be lost." : "This sandbox will be cleared.", MenuStyle.SmallButton);
            float buttonWidth = (panel.width - 100f) * 0.5f;
            if (MenuStyle.SmallFramedButton(new Rect(panel.x + 40f, panel.yMax - 92f, buttonWidth, 58f), "Yes, leave", false)) leave();
            if (MenuStyle.SmallFramedButton(new Rect(panel.xMax - 40f - buttonWidth, panel.yMax - 92f, buttonWidth, 58f), "Stay", false)) _page = Page.Buttons;
        }
    }
}

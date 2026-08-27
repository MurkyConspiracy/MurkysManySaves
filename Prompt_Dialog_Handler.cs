using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MurkysManySaves
{
    /// <summary>Content and styling for a Prompt_Dialog_Handler.Show call. A plain data holder, not a service.</summary>
    public class PromptDialogConfig
    {
        public string Title;
        public string[] BodyLines = Array.Empty<string>();
        public string AcceptLabel = "Accept";
        public string DeclineLabel = "Decline";
        public string SuccessMessage;
        public string[] ExistingPanelTypeNames = Array.Empty<string>();

        public Color OverlayColor = new Color(0f, 0f, 0f, 0.7f);
        public Color PanelColor;
        public Color AccentColor;
        public Color TitleColor;
        public Color BodyTextColor = new Color(0.95f, 0.95f, 0.90f, 1f);
        public Color AcceptButtonColor;
        public Color DeclineButtonColor;

        public Action OnAccept;
        public Action OnDecline;
    }

    /// <summary>
    /// Shows a modal opt-in dialog: tries an existing game confirm-panel type first, falls back
    /// to a custom Canvas built from the config, falls back further to a console-only message.
    /// </summary>
    public static class Prompt_Dialog_Handler
    {
        private static readonly string[] CommonPanelNames = {
            "ConfirmPanelUIManager", "ConfirmPanel", "DialogPanel",
            "MessagePanel", "UIConfirmPanel", "ConfirmPanelUI",
        };

        private static bool isActive = false;
        private static List<GraphicRaycaster> disabledRaycasters = new List<GraphicRaycaster>();

        public static void Show(PromptDialogConfig config)
        {
            if (isActive)
                return;

            isActive = true;
            try
            {
                foreach (var name in FindAvailablePanelTypes().Concat(config.ExistingPanelTypeNames).Concat(CommonPanelNames))
                {
                    if (TryShowExistingPanel(name, config))
                        return;
                }

                CreateCanvasPrompt(config);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MurkysManySaves] Failed to show prompt: {ex.Message}");
                ShowConsoleOnlyPrompt(config);
                isActive = false;
            }
        }

        private static List<string> FindAvailablePanelTypes()
        {
            var results = new List<string>();
            var asmCSharp = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (asmCSharp == null)
                return results;

            var candidates = asmCSharp.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract &&
                    (t.Name.Contains("Panel") || t.Name.Contains("Dialog") || t.Name.Contains("Confirm") || t.Name.Contains("Message")));

            foreach (var type in candidates)
            {
                bool hasInstance = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static) != null;
                bool hasOpenMethod = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Any(m => m.Name.Contains("Open") || m.Name.Contains("Show") || m.Name.Contains("Display"));

                if (hasInstance && hasOpenMethod)
                    results.Add(type.Name);
            }

            return results;
        }

        private static bool TryShowExistingPanel(string typeName, PromptDialogConfig config)
        {
            try
            {
                Type panelType = ES3_Reflection_Handler.FindType(typeName);
                if (panelType == null)
                    return false;

                PropertyInfo instanceProp = panelType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                object instance = instanceProp?.GetValue(null);
                if (instance == null)
                    return false;

                MethodInfo openMethod = panelType.GetMethod("OpenPanel", new[] { typeof(string), typeof(Action), typeof(Action) })
                    ?? panelType.GetMethod("Open", new[] { typeof(string), typeof(Action), typeof(Action) });
                if (openMethod == null)
                    return false;

                string message = BuildMessage(config);
                Action onAccept = () => HandleAccept(config);
                Action onDecline = () => HandleDecline(config);

                openMethod.Invoke(instance, new object[] { message, onAccept, onDecline });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MurkysManySaves] Failed to use panel '{typeName}': {ex.Message}");
                return false;
            }
        }

        private static string BuildMessage(PromptDialogConfig config)
        {
            return config.Title + "\n\n" + string.Join("\n", config.BodyLines);
        }

        private static void HandleAccept(PromptDialogConfig config)
        {
            config.OnAccept?.Invoke();
            isActive = false;
            if (config.SuccessMessage != null)
                ShowSuccessMessage(config.SuccessMessage);
        }

        private static void HandleDecline(PromptDialogConfig config)
        {
            config.OnDecline?.Invoke();
            isActive = false;
        }

        private static void CreateCanvasPrompt(PromptDialogConfig config)
        {
            DisableOtherRaycasters();

            var canvasGO = new GameObject("MurkysManySavesPromptCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            canvasGO.AddComponent<GraphicRaycaster>();

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var overlay = CreateStretchedImage("Overlay", canvasGO.transform, config.OverlayColor, Vector2.zero, Vector2.one);
            var overlayGroup = overlay.gameObject.AddComponent<CanvasGroup>();
            overlayGroup.blocksRaycasts = true;
            overlayGroup.interactable = true;
            overlayGroup.ignoreParentGroups = true;

            var content = CreateStretchedImage("ContentPanel", overlay.transform, config.PanelColor,
                new Vector2(0.15f, 0.10f), new Vector2(0.85f, 0.90f));
            var outline = content.gameObject.AddComponent<Outline>();
            outline.effectColor = config.AccentColor;
            outline.effectDistance = new Vector2(4, 4);

            CreateText("Title", content.transform, config.Title, 48, config.TitleColor,
                new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f), FontStyles.Bold, 24, 48);

            CreateStretchedImage("Separator", content.transform, config.AccentColor,
                new Vector2(0.05f, 0.85f), new Vector2(0.95f, 0.86f));

            string body = string.Join("\n", config.BodyLines);
            CreateText("Description", content.transform, body, 18, config.BodyTextColor,
                new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.82f), FontStyles.Normal, 12, 22, TextAlignmentOptions.TopLeft);

            void CloseAndRun(Action action)
            {
                action?.Invoke();
                EnableOtherRaycasters();
                UnityEngine.Object.Destroy(canvasGO);
            }

            CreateButton("AcceptButton", content.transform, config.AcceptLabel, config.AcceptButtonColor,
                new Vector2(0.05f, 0.03f), new Vector2(0.48f, 0.14f), () => CloseAndRun(() => HandleAccept(config)));

            CreateButton("DeclineButton", content.transform, config.DeclineLabel, config.DeclineButtonColor,
                new Vector2(0.52f, 0.03f), new Vector2(0.95f, 0.14f), () => CloseAndRun(() => HandleDecline(config)));
        }

        private static Image CreateStretchedImage(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        private static void CreateText(string name, Transform parent, string text, int fontSize, Color color,
            Vector2 anchorMin, Vector2 anchorMax, FontStyles style, int minSize, int maxSize,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = minSize;
            tmp.fontSizeMax = maxSize;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void CreateButton(string name, Transform parent, string label, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Action onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.2f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.15f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());

            CreateText("Text", go.transform, label, 32, Color.white,
                Vector2.zero, Vector2.one, FontStyles.Bold, 16, 32);
        }

        private static void DisableOtherRaycasters()
        {
            disabledRaycasters.Clear();
            foreach (var raycaster in UnityEngine.Object.FindObjectsOfType<GraphicRaycaster>())
            {
                if (raycaster.enabled)
                {
                    raycaster.enabled = false;
                    disabledRaycasters.Add(raycaster);
                }
            }
        }

        private static void EnableOtherRaycasters()
        {
            foreach (var raycaster in disabledRaycasters)
            {
                if (raycaster != null)
                    raycaster.enabled = true;
            }
            disabledRaycasters.Clear();
        }

        private static void ShowConsoleOnlyPrompt(PromptDialogConfig config)
        {
            Debug.Log($"[MurkysManySaves] {config.Title}");
            foreach (var line in config.BodyLines)
                Debug.Log($"[MurkysManySaves] {line}");
        }

        private static void ShowSuccessMessage(string message)
        {
            try
            {
                Type tooltipType = ES3_Reflection_Handler.FindType("TooltipUIManager");
                object instance = tooltipType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                MethodInfo showMethod = tooltipType?.GetMethod("Show", new[] { typeof(string) });
                showMethod?.Invoke(instance, new object[] { message });
            }
            catch
            {
                // Optional feedback only - silently skip if no tooltip system is available.
            }
        }
    }
}

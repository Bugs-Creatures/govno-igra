using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(FirstPersonController), typeof(GameAudioSettings))]
public sealed class PauseMenu : MonoBehaviour
{
    private static readonly Color PanelColor = new(0.18f, 0.18f, 0.18f);

    private FirstPersonController player;
    private GameAudioSettings audioSettings;
    private EventSystem ownedEventSystem;
    private GameObject overlay;
    private UnityEngine.UI.Slider volumeSlider;
    private UnityEngine.UI.Slider sensitivitySlider;
    private TMPro.TextMeshProUGUI volumeValue;
    private TMPro.TextMeshProUGUI sensitivityValue;
    private float previousTimeScale;
    private bool previousAudioPause;

    private void Awake()
    {
        player = GetComponent<FirstPersonController>();
        audioSettings = GetComponent<GameAudioSettings>();
        BuildMenu();
    }

    private void Update()
    {
        if (player.IsCinematicLocked || Keyboard.current?.escapeKey.wasPressedThisFrame != true)
            return;

        if (player.IsPaused)
            Close();
        else
            Open();
    }

    private void Open()
    {
        previousTimeScale = Time.timeScale;
        previousAudioPause = AudioListener.pause;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        player.SetPaused(true);

        volumeSlider.SetValueWithoutNotify(Mathf.RoundToInt(audioSettings.MasterVolume * 100f));
        int sensitivity = Mathf.RoundToInt(player.MouseSensitivity * 100f);
        sensitivitySlider.maxValue = Mathf.Max(50, sensitivity);
        sensitivitySlider.SetValueWithoutNotify(sensitivity);
        RefreshValues();
        if (EventSystem.current == null)
        {
            if (ownedEventSystem == null)
            {
                var eventObject = new GameObject("Pause Menu EventSystem", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                eventObject.transform.SetParent(transform, false);
                ownedEventSystem = eventObject.GetComponent<EventSystem>();
            }
            ownedEventSystem.gameObject.SetActive(true);
        }

        overlay.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(volumeSlider.gameObject);
    }

    private void Close()
    {
        if (player == null || !player.IsPaused)
            return;

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.transform.IsChildOf(overlay.transform))
            EventSystem.current.SetSelectedGameObject(null);
        overlay.SetActive(false);
        if (ownedEventSystem != null)
            ownedEventSystem.gameObject.SetActive(false);
        Time.timeScale = previousTimeScale;
        AudioListener.pause = previousAudioPause;
        player.SetPaused(false);
    }

    private void OnDisable() => Close();

    private void OnDestroy()
    {
        if (overlay != null)
            Destroy(overlay);
    }

    private void RefreshValues()
    {
        volumeValue.text = Mathf.RoundToInt(volumeSlider.value).ToString() + "%";
        sensitivityValue.text = (sensitivitySlider.value / 100f).ToString("0.00", CultureInfo.InvariantCulture);
    }

    private void BuildMenu()
    {
        overlay = new GameObject("Pause Menu", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        if (overlay.scene != gameObject.scene)
            SceneManager.MoveGameObjectToScene(overlay, gameObject.scene);
        overlay.SetActive(false);
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = overlay.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0f;

        var scrim = CreateImage("Scrim", overlay.transform, new Color(0f, 0f, 0f, 0.6f));
        scrim.rectTransform.anchorMin = Vector2.zero;
        scrim.rectTransform.anchorMax = Vector2.one;
        scrim.rectTransform.sizeDelta = Vector2.zero;

        var panel = CreateImage("Panel", scrim.transform, PanelColor);
        Place(panel.rectTransform, Vector2.zero, new Vector2(460, 254));
        var title = CreateText("Пауза", panel.transform, 32, TMPro.TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0, 88), new Vector2(400, 44));

        volumeSlider = CreateSlider(panel.transform, "Громкость", 34, 0, 100, out volumeValue);
        sensitivitySlider = CreateSlider(panel.transform, "Чувствительность мыши", -32, 0, 50, out sensitivityValue);
        volumeSlider.onValueChanged.AddListener(value =>
        {
            audioSettings.MasterVolume = value / 100f;
            RefreshValues();
        });
        sensitivitySlider.onValueChanged.AddListener(value =>
        {
            player.MouseSensitivity = value / 100f;
            RefreshValues();
        });

        var menuButton = CreateTextButton(panel.transform, "В меню", new Vector2(-76, -91));
        menuButton.interactable = false;
        var closeButton = CreateTextButton(panel.transform, "Закрыть", new Vector2(76, -91));
        closeButton.onClick.AddListener(Close);
    }

    private static UnityEngine.UI.Button CreateTextButton(Transform parent, string caption, Vector2 position)
    {
        var label = CreateText(caption, parent, 19, TMPro.TextAlignmentOptions.Center);
        label.gameObject.name = caption;
        label.raycastTarget = true;
        Place(label.rectTransform, position, new Vector2(128, 34));

        var button = label.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = label;
        var colors = button.colors;
        colors.normalColor = new Color(0.88f, 0.88f, 0.88f);
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f);
        colors.disabledColor = new Color(0.56f, 0.56f, 0.56f);
        button.colors = colors;
        return button;
    }

    private static UnityEngine.UI.Slider CreateSlider(Transform panel, string caption, float y,
        int min, int max, out TMPro.TextMeshProUGUI value)
    {
        var label = CreateText(caption, panel, 19, TMPro.TextAlignmentOptions.MidlineLeft);
        Place(label.rectTransform, new Vector2(0, y + 16), new Vector2(388, 24));
        var sliderObject = UnityEngine.UI.DefaultControls.CreateSlider(default);
        sliderObject.transform.SetParent(panel, false);
        Place((RectTransform)sliderObject.transform, new Vector2(-36, y - 10), new Vector2(316, 20));
        sliderObject.transform.Find("Background").GetComponent<UnityEngine.UI.Image>().color =
            new Color(0.32f, 0.32f, 0.32f);
        var slider = sliderObject.GetComponent<UnityEngine.UI.Slider>();
        slider.fillRect.GetComponent<UnityEngine.UI.Image>().color = new Color(0.66f, 0.66f, 0.66f);
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = true;

        value = CreateText("", panel, 19, TMPro.TextAlignmentOptions.MidlineRight);
        Place(value.rectTransform, new Vector2(165, y - 10), new Vector2(58, 24));
        return slider;
    }

    private static UnityEngine.UI.Image CreateImage(string name, Transform parent, Color color)
    {
        var element = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        element.transform.SetParent(parent, false);
        var image = element.GetComponent<UnityEngine.UI.Image>();
        image.color = color;
        return image;
    }

    private static TMPro.TextMeshProUGUI CreateText(string content, Transform parent, float size,
        TMPro.TextAlignmentOptions alignment)
    {
        var element = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        element.transform.SetParent(parent, false);
        var label = element.GetComponent<TMPro.TextMeshProUGUI>();
        label.text = content;
        label.fontSize = size;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}

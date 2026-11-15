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
    private static readonly Color PanelColor = new(0.055f, 0.055f, 0.06f, 0.97f);
    private static readonly Color FrameColor = new(1f, 1f, 1f, 0.20f);
    private static readonly Color PaperColor = new(0.93f, 0.91f, 0.87f);
    private static readonly Color FocusColor = new(1f, 0.81f, 0.52f);

    private FirstPersonController player;
    private GameAudioSettings audioSettings;
    private EventSystem ownedEventSystem;
    private GameObject overlay;
    private TMPro.TMP_FontAsset menuFont;
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

        if (overlay != null)
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(overlay.transform))
                EventSystem.current.SetSelectedGameObject(null);
            overlay.SetActive(false);
        }
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
        menuFont = Resources.Load<TMPro.TMP_FontAsset>("NightClock/Menu and Hints");
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
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;

        var scrim = CreateImage("Scrim", overlay.transform, new Color(0f, 0f, 0f, 0.7f));
        scrim.rectTransform.anchorMin = Vector2.zero;
        scrim.rectTransform.anchorMax = Vector2.one;
        scrim.rectTransform.sizeDelta = Vector2.zero;

        var panel = CreateImage("Panel", scrim.transform, PanelColor);
        Place(panel.rectTransform, Vector2.zero, new Vector2(444, 280));
        var outline = panel.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = FrameColor;
        outline.effectDistance = new Vector2(1f, -1f);
        var title = CreateText("Пауза", panel.transform, 29, TMPro.TextAlignmentOptions.Center);
        title.characterSpacing = 4f;
        Place(title.rectTransform, new Vector2(0, 99), new Vector2(370, 42));
        AddDivider(panel.transform, 69f);

        volumeSlider = CreateSlider(panel.transform, "Громкость", 33, 0, 100, out volumeValue);
        sensitivitySlider = CreateSlider(panel.transform, "Чувствительность мыши", -33, 0, 50, out sensitivityValue);
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

        AddDivider(panel.transform, -75f);
        var menuButton = CreateTextButton(panel.transform, "В меню", new Vector2(-92, -105));
        menuButton.interactable = Application.CanStreamedLevelBeLoaded(MainMenu.ScenePath);
        menuButton.onClick.AddListener(() =>
        {
            Close();
            SceneManager.LoadSceneAsync(MainMenu.ScenePath);
        });
        var closeButton = CreateTextButton(panel.transform, "Закрыть", new Vector2(92, -105));
        closeButton.onClick.AddListener(Close);
    }

    private static void AddDivider(Transform parent, float y)
    {
        var divider = CreateImage("Divider", parent, FrameColor);
        divider.raycastTarget = false;
        Place(divider.rectTransform, new Vector2(0f, y), new Vector2(364f, 1f));
    }

    private UnityEngine.UI.Button CreateTextButton(Transform parent, string caption, Vector2 position)
    {
        var label = CreateText(caption, parent, 18, TMPro.TextAlignmentOptions.Center);
        label.gameObject.name = caption;
        label.raycastTarget = true;
        Place(label.rectTransform, position, new Vector2(128, 34));

        var button = label.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = label;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = FocusColor;
        colors.selectedColor = FocusColor;
        colors.pressedColor = new Color(0.85f, 0.64f, 0.40f);
        colors.disabledColor = new Color(0.56f, 0.56f, 0.56f);
        button.colors = colors;
        return button;
    }

    private UnityEngine.UI.Slider CreateSlider(Transform panel, string caption, float y,
        int min, int max, out TMPro.TextMeshProUGUI value)
    {
        var label = CreateText(caption, panel, 17, TMPro.TextAlignmentOptions.MidlineLeft);
        Place(label.rectTransform, new Vector2(-35, y + 11), new Vector2(294, 24));
        var sliderObject = UnityEngine.UI.DefaultControls.CreateSlider(default);
        sliderObject.name = caption + " Slider";
        sliderObject.transform.SetParent(panel, false);
        Place((RectTransform)sliderObject.transform, new Vector2(0, y - 12), new Vector2(364, 20));
        sliderObject.AddComponent<UnityEngine.UI.Image>().color = Color.clear;
        var track = sliderObject.transform.Find("Background").GetComponent<UnityEngine.UI.Image>();
        track.color = FrameColor;
        CenterTrack(track.rectTransform, 4f, 10f);
        var slider = sliderObject.GetComponent<UnityEngine.UI.Slider>();
        CenterTrack((RectTransform)slider.fillRect.parent, 4f, 10f);
        slider.fillRect.sizeDelta = Vector2.zero;
        slider.fillRect.GetComponent<UnityEngine.UI.Image>().color = PaperColor;
        slider.handleRect.sizeDelta = new Vector2(5f, -7f);
        slider.targetGraphic.color = PaperColor;
        var colors = slider.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = FocusColor;
        colors.selectedColor = FocusColor;
        slider.colors = colors;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = true;

        value = CreateText("", panel, 17, TMPro.TextAlignmentOptions.MidlineRight);
        Place(value.rectTransform, new Vector2(151, y + 11), new Vector2(62, 24));
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

    private TMPro.TextMeshProUGUI CreateText(string content, Transform parent, float size,
        TMPro.TextAlignmentOptions alignment)
    {
        var element = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
        element.transform.SetParent(parent, false);
        var label = element.GetComponent<TMPro.TextMeshProUGUI>();
        label.text = content;
        if (menuFont != null)
            label.font = menuFont;
        label.fontSize = size;
        label.color = PaperColor;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    private static void CenterTrack(RectTransform rect, float height, float inset)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(-2f * inset, height);
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}

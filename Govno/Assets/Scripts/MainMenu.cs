using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(MenuAudioSettings))]
public sealed class MainMenu : MonoBehaviour
{
    public const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private const string GameplayScene = "Assets/Scenes/SampleScene.unity";
    private const float StartDuration = 2.6f;
    private static readonly Vector2 WindowFocus = new(0.574f, 0.475f);

    [SerializeField] private UnityEngine.UI.RawImage background;
    [SerializeField] private UnityEngine.UI.Image darkness;
    [SerializeField] private CanvasGroup choices;
    [SerializeField] private UnityEngine.UI.Button playButton;
    [SerializeField] private UnityEngine.UI.Button exitButton;
    [SerializeField] private AudioSource music;
    [SerializeField] private AudioSource effects;
    [SerializeField] private AudioClip startSound;

    private bool starting;
    private float transition;
    private MenuAudioSettings audioSettings;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        audioSettings = GetComponent<MenuAudioSettings>();
        music.volume = audioSettings.MusicVolume;
        playButton.onClick.AddListener(BeginGame);
        exitButton.onClick.AddListener(Quit);
        AddHoverFeedback(playButton);
        AddHoverFeedback(exitButton);
    }

    private void LateUpdate()
    {
        music.volume = audioSettings.MusicVolume * (1f - transition);
        Rect rect = background.rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        float imageAspect = (float)background.texture.width / background.texture.height;
        float canvasAspect = rect.width / rect.height;
        Vector2 cover = new(Mathf.Min(1f, canvasAspect / imageAspect), Mathf.Min(1f, imageAspect / canvasAspect));
        float zoom = Mathf.Lerp(1f, 1.34f, Mathf.SmoothStep(0f, 1f, transition));
        Vector2 size = cover / zoom;
        Vector2 half = size * 0.5f;
        Vector2 center = Vector2.Lerp(new Vector2(0.5f, 0.5f), WindowFocus, transition);
        center.x = Mathf.Clamp(center.x, half.x, 1f - half.x);
        center.y = Mathf.Clamp(center.y, half.y, 1f - half.y);
        background.uvRect = new Rect(center - half, size);
    }

    private void AddHoverFeedback(UnityEngine.UI.Button button)
    {
        var trigger = button.gameObject.AddComponent<EventTrigger>();
        Color selected = button.colors.selectedColor;
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => SetSelectedColor(button, button.colors.highlightedColor));
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => SetSelectedColor(button, selected));
        trigger.triggers.Add(exit);
    }

    private static void SetSelectedColor(UnityEngine.UI.Button button, Color selected)
    {
        // Unity prioritizes the selected state over pointer hover.
        var colors = button.colors;
        colors.selectedColor = selected;
        button.colors = colors;
    }

    public void BeginGame()
    {
        if (!starting)
            StartCoroutine(StartGame());
    }

    private IEnumerator StartGame()
    {
        int buildIndex = SceneUtility.GetBuildIndexByScenePath(GameplayScene);
        if (buildIndex < 0)
        {
            Debug.LogError("The gameplay scene is missing from the build scene list.", this);
            yield break;
        }

        starting = true;
        choices.interactable = false;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        if (startSound != null)
            effects.PlayOneShot(startSound, audioSettings.StartVolume);
        AsyncOperation load = SceneManager.LoadSceneAsync(buildIndex);
        load.allowSceneActivation = false;

        for (float elapsed = 0f; elapsed < StartDuration; elapsed += Time.unscaledDeltaTime)
        {
            transition = elapsed / StartDuration;
            choices.alpha = 1f - Mathf.SmoothStep(0f, 1f, transition * 3f);
            darkness.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.15f, 0.90f, transition)));
            yield return null;
        }

        transition = 1f;
        choices.alpha = 0f;
        darkness.color = Color.black;
        music.Stop();
        load.allowSceneActivation = true;
    }

    public void Quit()
    {
        if (starting)
            return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

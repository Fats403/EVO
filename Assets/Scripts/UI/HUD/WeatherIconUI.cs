using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class WeatherIconUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField]
    private RectTransform root;

    [SerializeField]
    private Image currentIcon;

    [SerializeField]
    private Image incomingIcon;

    [Header("Weather Sprites")]
    [SerializeField]
    private Sprite clearSprite;

    [SerializeField]
    private Sprite droughtSprite;

    [SerializeField]
    private Sprite stormSprite;

    [SerializeField]
    private Sprite wildfireSprite;

    [Header("Tooltip")]
    [SerializeField]
    private TooltipTrigger tooltip;

    [SerializeField]
    private string clearTitle = "Clear Skies";

    [SerializeField, TextArea]
    private string clearBody =
        "Favorable conditions. Food tends to grow and creatures recover between rounds.";

    [SerializeField]
    private string droughtTitle = "Drought";

    [SerializeField, TextArea]
    private string droughtBody =
        "Water and food are scarce. Food piles shrink and starvation becomes more punishing.";

    [SerializeField]
    private string stormTitle = "Storm";

    [SerializeField, TextArea]
    private string stormBody =
        "Violent weather batters the land. Avians become fatigued and food is washed away.";

    [SerializeField]
    private string wildfireTitle = "Wildfire";

    [SerializeField, TextArea]
    private string wildfireBody =
        "Flames sweep across the battlefield, burning all creatures at the end of each round.";

    [Header("Animation")]
    [SerializeField]
    private float duration = 0.3f; // seconds

    private Coroutine animRoutine;
    private bool isSubscribed;

    private void SubscribeIfPossible()
    {
        if (isSubscribed)
            return;

        var wm = WeatherManager.Instance;
        if (wm == null)
            return;

        wm.OnWeatherChanged += HandleWeatherChanged;
        isSubscribed = true;

        // Initialize to current weather immediately
        HandleWeatherChanged(wm.CurrentWeather);
    }

    private void Awake()
    {
        if (root == null)
            root = (RectTransform)transform;

        // Ensure there is a TooltipTrigger attached so hovering the weather icon
        // always shows a tooltip without requiring manual wiring.
        if (tooltip == null)
        {
            tooltip = GetComponent<TooltipTrigger>();
            if (tooltip == null)
                tooltip = gameObject.AddComponent<TooltipTrigger>();
        }
    }

    private void OnEnable()
    {
        // Try once here in case WeatherManager is already initialized
        SubscribeIfPossible();
    }

    private void Start()
    {
        // Start is called after all Awake calls, so WeatherManager.Instance
        // should be valid by now in normal scene setups.
        SubscribeIfPossible();
    }

    private void OnDisable()
    {
        if (WeatherManager.Instance != null && isSubscribed)
        {
            WeatherManager.Instance.OnWeatherChanged -= HandleWeatherChanged;
            isSubscribed = false;
        }
    }

    private void HandleWeatherChanged(WeatherType weather)
    {
        // Hide icon for Extinction
        if (weather == WeatherType.Extinction)
        {
            if (root != null)
                root.gameObject.SetActive(false);
            // No tooltip while hidden
            if (tooltip != null)
            {
                tooltip.title = string.Empty;
                tooltip.body = string.Empty;
                tooltip.icon = null;
            }
            return;
        }

        // Ensure visible for normal weather
        if (root != null && !root.gameObject.activeSelf)
            root.gameObject.SetActive(true);

        Sprite nextSprite = GetSpriteForWeather(weather);
        if (nextSprite == null || currentIcon == null || incomingIcon == null)
            return;

        // Update tooltip content for the new weather
        UpdateTooltip(weather, nextSprite);

        // First time: just set without animation
        if (currentIcon.sprite == null)
        {
            currentIcon.sprite = nextSprite;
            return;
        }

        if (animRoutine != null)
            StopCoroutine(animRoutine);

        animRoutine = StartCoroutine(SlideIcons(nextSprite));
    }

    private void UpdateTooltip(WeatherType weather, Sprite iconSprite)
    {
        if (tooltip == null)
            return;

        tooltip.icon = iconSprite;

        switch (weather)
        {
            case WeatherType.Clear:
                tooltip.title = clearTitle;
                tooltip.body = clearBody;
                break;
            case WeatherType.Drought:
                tooltip.title = droughtTitle;
                tooltip.body = droughtBody;
                break;
            case WeatherType.Storm:
                tooltip.title = stormTitle;
                tooltip.body = stormBody;
                break;
            case WeatherType.Wildfire:
                tooltip.title = wildfireTitle;
                tooltip.body = wildfireBody;
                break;
            default:
                tooltip.title = string.Empty;
                tooltip.body = string.Empty;
                tooltip.icon = null;
                break;
        }
    }

    private Sprite GetSpriteForWeather(WeatherType weather)
    {
        switch (weather)
        {
            case WeatherType.Clear:
                return clearSprite;
            case WeatherType.Drought:
                return droughtSprite;
            case WeatherType.Storm:
                return stormSprite;
            case WeatherType.Wildfire:
                return wildfireSprite;
            default:
                return null;
        }
    }

    private IEnumerator SlideIcons(Sprite nextSprite)
    {
        // Simple cross-fade between current and incoming icons.

        // Ensure both images are at the same position
        currentIcon.rectTransform.anchoredPosition = Vector2.zero;
        incomingIcon.rectTransform.anchoredPosition = Vector2.zero;

        // Set up sprites and initial alphas
        incomingIcon.sprite = nextSprite;

        Color curColor = currentIcon.color;
        Color incColor = incomingIcon.color;

        curColor.a = 1f;
        incColor.a = 0f;
        currentIcon.color = curColor;
        incomingIcon.color = incColor;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            curColor.a = 1f - t;
            incColor.a = t;

            currentIcon.color = curColor;
            incomingIcon.color = incColor;

            yield return null;
        }

        // Finalize: new sprite becomes the current, fully opaque; incoming hidden
        currentIcon.sprite = nextSprite;
        curColor.a = 1f;
        currentIcon.color = curColor;

        incColor.a = 0f;
        incomingIcon.color = incColor;

        animRoutine = null;
    }
}

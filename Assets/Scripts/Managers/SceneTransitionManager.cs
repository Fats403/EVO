using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransitionManager : MonoBehaviour
{
    private static SceneTransitionManager _instance;
    public static SceneTransitionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("SceneTransitionManager");
                _instance = go.AddComponent<SceneTransitionManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [Header("Transition Settings")]
    [SerializeField]
    private float transitionDuration = 1.0f;

    [SerializeField]
    private Color transitionColor = Color.black;

    private Canvas transitionCanvas;
    private Image transitionImage;
    private Material transitionMaterial;
    private static readonly int RadiusID = Shader.PropertyToID("_Radius");
    private static readonly int AspectRatioID = Shader.PropertyToID("_AspectRatio");

    private bool isTransitioning = false;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        SetupCanvas();
    }

    private void SetupCanvas()
    {
        // Create Canvas
        GameObject canvasGo = new GameObject("TransitionCanvas");
        canvasGo.transform.SetParent(transform);
        transitionCanvas = canvasGo.AddComponent<Canvas>();
        transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        transitionCanvas.sortingOrder = 999; // Ensure it's on top

        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        // Create Image
        GameObject imageGo = new GameObject("TransitionImage");
        imageGo.transform.SetParent(canvasGo.transform);
        transitionImage = imageGo.AddComponent<Image>();

        // Make it fill the screen
        RectTransform rect = transitionImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        // Load Shader and Create Material
        Shader shader = Shader.Find("UI/CircleTransition");
        if (shader == null)
        {
            Debug.LogWarning(
                "SceneTransitionManager: Could not find shader 'UI/CircleTransition'. Falling back to simple fade."
            );
            transitionImage.color = new Color(
                transitionColor.r,
                transitionColor.g,
                transitionColor.b,
                0
            );
        }
        else
        {
            transitionMaterial = new Material(shader);
            transitionMaterial.SetColor("_Color", transitionColor);
            transitionMaterial.SetFloat(RadiusID, 2.0f); // Start fully open
            transitionImage.material = transitionMaterial;
            transitionImage.color = Color.white; // Material handles the color
        }

        transitionImage.gameObject.SetActive(false);
    }

    public void LoadScene(string sceneName)
    {
        if (isTransitioning)
            return;
        StartCoroutine(TransitionRoutine(sceneName));
    }

    public void LoadScene(int sceneIndex)
    {
        if (isTransitioning)
            return;
        StartCoroutine(TransitionRoutine(sceneIndex));
    }

    private IEnumerator TransitionRoutine(object sceneIdentifier)
    {
        isTransitioning = true;
        transitionImage.gameObject.SetActive(true);

        // Update Aspect Ratio in case it changed (e.g. window resize)
        if (transitionMaterial != null)
        {
            float aspect = (float)Screen.width / Screen.height;
            transitionMaterial.SetFloat(AspectRatioID, aspect);
        }

        // Fade In (Close circle or fade to black)
        float elapsed = 0;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;

            if (transitionMaterial != null)
            {
                // Radius 2.0 (open) to 0 (closed)
                float radius = Mathf.Lerp(2.0f, 0f, t);
                transitionMaterial.SetFloat(RadiusID, radius);
            }
            else
            {
                // Fallback: simple alpha fade
                Color c = transitionImage.color;
                c.a = t;
                transitionImage.color = c;
            }
            yield return null;
        }

        if (transitionMaterial != null)
            transitionMaterial.SetFloat(RadiusID, 0f);
        else
        {
            Color c = transitionImage.color;
            c.a = 1f;
            transitionImage.color = c;
        }

        // Load Scene
        AsyncOperation asyncLoad = null;
        if (sceneIdentifier is string name)
            asyncLoad = SceneManager.LoadSceneAsync(name);
        else if (sceneIdentifier is int index)
            asyncLoad = SceneManager.LoadSceneAsync(index);

        if (asyncLoad != null)
        {
            while (!asyncLoad.isDone)
            {
                yield return null;
            }
        }

        // Fade Out (Open circle or fade from black)
        elapsed = 0;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transitionDuration;

            if (transitionMaterial != null)
            {
                // Radius 0 (closed) to 2.0 (open)
                float radius = Mathf.Lerp(0f, 2.0f, t);
                transitionMaterial.SetFloat(RadiusID, radius);
            }
            else
            {
                // Fallback: simple alpha fade
                Color c = transitionImage.color;
                c.a = 1f - t;
                transitionImage.color = c;
            }
            yield return null;
        }

        if (transitionMaterial != null)
            transitionMaterial.SetFloat(RadiusID, 2.0f);
        else
        {
            Color c = transitionImage.color;
            c.a = 0f;
            transitionImage.color = c;
        }

        transitionImage.gameObject.SetActive(false);
        isTransitioning = false;
    }
}

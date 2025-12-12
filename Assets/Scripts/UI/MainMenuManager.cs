using UnityEngine;
using UnityEngine.SceneManagement;

namespace EVO.UI
{
    public class MainMenuManager : MonoBehaviour
    {
        [SerializeField]
        private readonly string gameSceneName = "MainScene";

        /// <summary>
        /// Called by UI button to start the game.
        /// </summary>
        public void StartGame()
        {
            if (string.IsNullOrEmpty(gameSceneName))
            {
                Debug.LogError("MainMenuManager: gameSceneName is not set.");
                return;
            }

            // Make sure the scene is added to Build Settings.
            SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }

        /// <summary>
        /// Quits the game. In editor, stops play mode.
        /// </summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

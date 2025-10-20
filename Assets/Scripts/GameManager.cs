using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public enum GameState { Menu, Running, Paused, Resetting }
    public GameState CurrentState = GameState.Menu;

    private void Awake()
    {
        // Simple singleton setup
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        if (CurrentState == GameState.Running)
        {
            // This is your simulation update loop later
        }

        // Quick pause toggle for testing
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TogglePause();
        }
    }

    public void StartSimulation()
    {
        CurrentState = GameState.Running;
        Debug.Log("Simulation Started");
    }

    public void TogglePause()
    {
        if (CurrentState == GameState.Running)
            CurrentState = GameState.Paused;
        else if (CurrentState == GameState.Paused)
            CurrentState = GameState.Running;

        Debug.Log("Simulation state: " + CurrentState);
    }

    public void ResetSimulation()
    {
        CurrentState = GameState.Resetting;
        // For now, you can just reload the scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }
}

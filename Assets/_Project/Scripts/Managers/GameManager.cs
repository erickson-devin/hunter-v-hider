using UnityEngine;

namespace HunterVsHider.Managers
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public enum GameState
        {
            PrepPhase,
            ActionPhase
        }

        public GameState CurrentState { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            ChangeState(GameState.PrepPhase);
        }

        public void ChangeState(GameState newState)
        {
            CurrentState = newState;
            
            // Logic based on state change could be invoked here via events
            switch (CurrentState)
            {
                case GameState.PrepPhase:
                    Debug.Log("Entered Prep Phase");
                    break;
                case GameState.ActionPhase:
                    Debug.Log("Entered Action Phase");
                    break;
            }
        }
    }
}

using System.Collections;
using UnityEngine;

namespace Babel
{
    public class StageFlowController : SingletonBase<StageFlowController>
    {
        private GameManager gameManager;
        private UIManager uiManager;
        private Coroutine stageRoutine;
        private Coroutine resultRoutine;

        public StageState CurrentState { get; private set; } = StageState.Boot;
        public bool IsGameplayBlocked => CurrentState != StageState.Playing;

        public enum StageState
        {
            Boot,
            Tutorial,
            Playing,
            Paused,
            GameOverEffect,
            Result,
            Transition,
        }

        public void Register(GameManager newGameManager, UIManager newUIManager)
        {
            gameManager = newGameManager;
            uiManager = newUIManager != null ? newUIManager : FindAnyObjectByType<UIManager>();

            if (stageRoutine != null)
            {
                StopCoroutine(stageRoutine);
            }

            stageRoutine = StartCoroutine(StageStartRoutine());
        }

        public void HandleGameOver(GameManager targetGameManager)
        {
            if (resultRoutine != null)
            {
                return;
            }

            gameManager = targetGameManager;
            resultRoutine = StartCoroutine(ResultRoutine());
        }

        public bool TryPause()
        {
            if (CurrentState != StageState.Playing)
            {
                return false;
            }

            CurrentState = StageState.Paused;
            Time.timeScale = 0.0f;
            return true;
        }

        public void ResumePause()
        {
            if (CurrentState != StageState.Paused)
            {
                return;
            }

            CurrentState = StageState.Playing;
            Time.timeScale = 1.0f;
        }

        private IEnumerator StageStartRoutine()
        {
            CurrentState = StageState.Boot;
            Time.timeScale = 0.0f;

            yield return null;

            if (uiManager == null)
            {
                uiManager = FindAnyObjectByType<UIManager>();
            }

            if (uiManager != null && uiManager.TryBeginTutorialSplash())
            {
                CurrentState = StageState.Tutorial;

                while (uiManager.IsTutorialSplashActive)
                {
                    yield return null;
                }
            }

            CurrentState = StageState.Playing;
            Time.timeScale = 1.0f;

            if (gameManager != null)
            {
                gameManager.BeginStage();
            }

            stageRoutine = null;
        }

        private IEnumerator ResultRoutine()
        {
            CurrentState = StageState.GameOverEffect;
            Time.timeScale = 1.0f;

            if (gameManager != null && gameManager.IsFailed)
            {
                yield return gameManager.PlayGameOverEffectRoutine();
            }

            CurrentState = StageState.Result;

            if (gameManager != null)
            {
                yield return gameManager.FinalizeGameOverRoutine();
            }

            resultRoutine = null;
        }
    }
}

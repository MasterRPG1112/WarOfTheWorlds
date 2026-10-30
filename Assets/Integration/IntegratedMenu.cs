using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Jain;

namespace Integration
{
    // Added to a copy of the team's original menu. Artwork and panel layout stay intact.
    public class IntegratedMenu : MonoBehaviour
    {
        public Menu menu;
        public Button continueButton;
        public Slider volumeSlider;
        public Toggle fullscreenToggle;
        public Text continueHint;
        void Awake()
        {
            Time.timeScale = 1;
            AudioListener.volume = IntegrationSaveStore.Data.volume;
            if (!Application.isEditor) Screen.fullScreen = IntegrationSaveStore.Data.fullscreen;
        }
        void Start()
        {
            continueButton.interactable = IntegrationSaveStore.HasCheckpoint;
            if (continueHint != null) continueHint.text = IntegrationSaveStore.HasCheckpoint
                ? "스테이지 " + IntegrationSaveStore.Data.checkpoint.stage + " 시작 지점부터 이어합니다."
                : "저장된 진행이 없습니다. 다시하기로 시작하세요.";
            volumeSlider.SetValueWithoutNotify(IntegrationSaveStore.Data.volume);
            fullscreenToggle.SetIsOnWithoutNotify(IntegrationSaveStore.Data.fullscreen);
        }
        public void NewGame()
        {
            IntegrationSaveStore.ContinueRequested = false;
            IntegrationSaveStore.ClearCheckpoint();
            LoadGame();
        }
        public void ContinueGame()
        {
            if (!IntegrationSaveStore.HasCheckpoint) return;
            IntegrationSaveStore.ContinueRequested = true;
            LoadGame();
        }
        static void LoadGame()
        {
            Time.timeScale = 1;
            if (GameDataManager.instance != null) Destroy(GameDataManager.instance.gameObject);
            SceneManager.LoadScene("TaeyangIntegrated");
        }
        public void SetVolume(float value)
        {
            IntegrationSaveStore.Data.volume = Mathf.Clamp01(value);
            AudioListener.volume = IntegrationSaveStore.Data.volume;
            IntegrationSaveStore.Save();
        }
        public void SetFullscreen(bool value)
        {
            IntegrationSaveStore.Data.fullscreen = value;
            if (!Application.isEditor) Screen.fullScreen = value;
            IntegrationSaveStore.Save();
        }
    }
}

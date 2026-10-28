using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jain
{
    public class ExitMenu : MonoBehaviour
    {
        public GameObject exitMenu;

        void Start()
        {
            
        }

        void Update()
        {
            OpenUI();
        }

        public void ContinueBtn()
        {
            exitMenu.SetActive(false);
            Time.timeScale = 1f;
        }

        public void EndBtn()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Menu");
        }

        void OpenUI()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                exitMenu.SetActive(true);
                Time.timeScale = 0f;
            }
        }
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Jain
{
    public class Menu : MonoBehaviour
    {
        public GameObject basicMenu;
        public GameObject settingMenu;

        void Start()
        {

        }

        void Update()
        {

        }

        public void StartBtn()
        {
            SceneManager.LoadScene("Stage1");
        }

        public void SettingBtn()
        {
            Invoke("SettingMenuOpen", 1.5f);
        }

        public void ExitBtn()
        {
            Application.Quit();
        }

        public void BackBtn()
        {
            Invoke("BasicMenuOpen", 1.5f);
        }

        void BasicMenuOpen()
        {
            basicMenu.SetActive(true);
            settingMenu.SetActive(false);
        }

        void SettingMenuOpen()
        {
            settingMenu.SetActive(true);
            basicMenu.SetActive(false);
        }
    }
}
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Jain
{
    public class Menu : MonoBehaviour
    {
        [Header("메뉴 오브젝트 (딱 2개만 사용)")]
        public GameObject basicMenu;
        public GameObject settingMenu;

        [Header("페이드 설정")]
        public float fadeDuration = 0.5f; // 페이드 전환 시간 (초)

        // 내부에서 자동으로 가져올 CanvasGroup 변수
        private CanvasGroup basicGroup;
        private CanvasGroup settingGroup;

        void Start()
        {
            // 각 GameObject에 붙어있는 CanvasGroup을 자동으로 가져옴
            basicGroup = basicMenu != null ? basicMenu.GetComponent<CanvasGroup>() : null;
            settingGroup = settingMenu != null ? settingMenu.GetComponent<CanvasGroup>() : null;

            // 게임 시작 시 기본 메뉴는 켜고, 설정 메뉴는 끄기 초기화
            SetMenuState(basicMenu, basicGroup, true, 1f);
            SetMenuState(settingMenu, settingGroup, false, 0f);
        }

        public void StartBtn()
        {
            SceneManager.LoadScene("Stage1");
        }

        // 설정 버튼 클릭 시: 기본 메뉴 -> 설정 메뉴로 페이드 전환
        public void SettingBtn()
        {
            StopAllCoroutines();
            StartCoroutine(SwitchMenuRoutine(basicGroup, settingGroup, basicMenu, settingMenu));
        }

        // 뒤로 가기 버튼 클릭 시: 설정 메뉴 -> 기본 메뉴로 페이드 전환
        public void BackBtn()
        {
            StopAllCoroutines();
            StartCoroutine(SwitchMenuRoutine(settingGroup, basicGroup, settingMenu, basicMenu));
        }

        public void ExitBtn()
        {
            Application.Quit();
        }

        // 부드럽게 페이드 되면서 두 메뉴를 상호 교체하는 코루틴
        private IEnumerator SwitchMenuRoutine(CanvasGroup fadeOutGroup, CanvasGroup fadeInGroup, GameObject outObj, GameObject inObj)
        {
            if (fadeInGroup == null || fadeOutGroup == null) yield break;

            // 새로 켜질 메뉴 준비
            inObj.SetActive(true);
            fadeInGroup.alpha = 0f;
            fadeInGroup.interactable = false;
            fadeInGroup.blocksRaycasts = false;

            // 기존 메뉴 상호작용 차단
            fadeOutGroup.interactable = false;
            fadeOutGroup.blocksRaycasts = false;

            float elapsed = 0f;

            // 서서히 투명도가 바뀌는 애니메이션
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);

                fadeOutGroup.alpha = Mathf.Lerp(1f, 0f, t);
                fadeInGroup.alpha = Mathf.Lerp(0f, 1f, t);

                yield return null;
            }

            // 마무리 정리
            fadeOutGroup.alpha = 0f;
            outObj.SetActive(false);

            fadeInGroup.alpha = 1f;
            fadeInGroup.interactable = true;
            fadeInGroup.blocksRaycasts = true;
        }

        // 메뉴 상태를 일괄 설정하는 헬퍼 함수
        private void SetMenuState(GameObject obj, CanvasGroup group, bool isActive, float alpha)
        {
            if (obj != null)
            {
                obj.SetActive(isActive);
                if (group != null)
                {
                    group.alpha = alpha;
                    group.interactable = isActive;
                    group.blocksRaycasts = isActive;
                }
            }
        }
    }
}
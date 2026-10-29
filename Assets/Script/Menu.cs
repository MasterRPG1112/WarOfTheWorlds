using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Jain
{
    public class Menu : MonoBehaviour
    {
        [Header("메뉴 오브젝트")]
        public GameObject basicMenu;
        public GameObject settingMenu;
        public GameObject listMenu;
        public GameObject startInvokeMenu;

        [Header("페이드 설정")]
        public float fadeDuration = 0.5f; // 페이드 전환 시간 (초)

        // 내부에서 자동으로 가져올 CanvasGroup 변수
        private CanvasGroup basicGroup;
        private CanvasGroup settingGroup;
        private CanvasGroup listGroup;
        private CanvasGroup startInvokeGroup;

        //랭크 변수
        public Text[] rankTexts;

        void Start()
        {
            // 각 GameObject에 붙어있는 CanvasGroup을 자동으로 가져옴
            basicGroup = basicMenu != null ? basicMenu.GetComponent<CanvasGroup>() : null;
            settingGroup = settingMenu != null ? settingMenu.GetComponent<CanvasGroup>() : null;
            listGroup = listMenu != null ? listMenu.GetComponent<CanvasGroup>() : null;
            startInvokeGroup = startInvokeMenu != null ? startInvokeMenu.GetComponent<CanvasGroup>() : null;

            // 게임 시작 시 기본 메뉴는 켜고, 설정 메뉴는 끄기 초기화
            SetMenuState(basicMenu, basicGroup, true, 1f);
            SetMenuState(settingMenu, settingGroup, false, 0f);
            SetMenuState(listMenu, listGroup, false, 0f);
            SetMenuState(startInvokeMenu, startInvokeGroup, false, 0f);
        }

        public void StartBtn()
        {
            StopAllCoroutines();
            StartCoroutine(SwitchMenuRoutine(basicGroup, startInvokeGroup, basicMenu, startInvokeMenu));
        }

        // 설정 버튼 클릭 시: 기본 메뉴 -> 설정 메뉴로 페이드 전환
        public void SettingBtn()
        {
            StopAllCoroutines();
            StartCoroutine(SwitchMenuRoutine(basicGroup, settingGroup, basicMenu, settingMenu));
        }

        // 뒤로 가기 버튼 클릭 시: 설정 메뉴 -> 기본 메뉴로 페이드 전환
        public void BackBtn1()
        {
            StopAllCoroutines();
            StartCoroutine(SwitchMenuRoutine(settingGroup, basicGroup, settingMenu, basicMenu));
        }

        public void BackBtn2()
        {
            StopAllCoroutines();
            StartCoroutine(SwitchMenuRoutine(listGroup, basicGroup, listMenu, basicMenu));
        }

        public void BackBtn3()
        {
            StopAllCoroutines();
            StartCoroutine(SwitchMenuRoutine(startInvokeGroup, basicGroup, startInvokeMenu, basicMenu));
        }

        public void ExitBtn()
        {
            Application.Quit();
        }

        public void ListBtn()
        {
            OpenRank();
            StopAllCoroutines();
            StartCoroutine(SwitchMenuRoutine(basicGroup,listGroup, basicMenu, listMenu));
        }

        public void ReStartBtn()
        {
            GameDataManager.instance.dataReset = 1;
            GameDataManager.instance.SaveData();
            SceneManager.LoadScene("Stage1");
        }

        public void ContinueBtn()
        {
            GameDataManager.instance.dataReset = 0;
            GameDataManager.instance.SaveData();
            SceneManager.LoadScene("Stage1");
        }

        void OpenRank()
        {
            var rankList = GameDataManager.instance.rank;

            // 1. 우선 모든 텍스트 칸을 초기화 (데이터가 부족할 때 빈칸으로 비우기 위함)
            for (int i = 0; i < rankTexts.Length; i++)
            {
                if (rankTexts[i] != null)
                {
                    rankTexts[i].text = (i + 1) + ". -";
                }
            }

            // 2. 데이터가 없다면 종료
            if (rankList == null || rankList.Count == 0) return;

            // 3. 실제 데이터가 있는 만큼 (최대 10개) 텍스트에 대입
            int count = Mathf.Min(rankList.Count, rankTexts.Length);

            for (int i = 0; i < count; i++)
            {
                if (rankTexts[i] != null)
                {
                    var rankData = rankList[i];

                    // 형식: [순위]. [이름] - [점수]점
                    rankTexts[i].text = (i + 1) + ". " + rankData.playerName + " - " + rankData.stageScore + "점";
                }
            }
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
using Jain;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Score : MonoBehaviour
{
    public int score;

    public Text scoreUI;

    void Start()
    {
        GameDataManager.instance.LoadData();
        if (GameDataManager.instance.dataReset == 1) score = 0;
        else score = GameDataManager.instance.score;
        ReloadUI();
    }

    void Update()
    {
        Cheat();
    }

    void Cheat()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            score += 100;
            ReloadUI();
            GameDataManager.instance.score = score;
            GameDataManager.instance.SaveData();
        }
        if (Input.GetKeyDown(KeyCode.L))
        {
            Debug.Log("<color=yellow>[치트 발동] L키 입력: 스테이지 강제 클리어!</color>");

            // 1. 현재 플레이어의 ID와 점수를 랭킹 매니저(GameDataManager)에 등록
            // (현재 curId 또는 입력받는 이름 변수가 있다면 그에 맞게 수정해주세요)
            string playerName = "CheatUser"; // 테스트용 이름 (혹은 GameDataManager.instance.curId 등)

            if (GameDataManager.instance != null)
            {
                // 아까 만든 AddRank 함수 호출 (점수는 원하는 값이나 현재 점수 대입)
                GameDataManager.instance.AddRank(playerName, score);
            }

            // 2. 클리어 화면(씬)으로 이동하거나 다음 씬으로 이동
            // "ClearScene" 부분은 이동하고자 하는 씬 이름으로 변경해주세요.
            SceneManager.LoadScene("Menu");
        }
    }

    void ReloadUI()
    {
        scoreUI.text = "점수 : " + score + "점";
    }
}

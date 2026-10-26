using Jain;
using UnityEngine;
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
    }

    void ReloadUI()
    {
        scoreUI.text = "Á¡¼ö : " + score + "Á¡";
    }
}

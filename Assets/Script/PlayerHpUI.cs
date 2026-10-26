using UnityEngine;
using UnityEngine.UI;

namespace Jain
{
    public class PlayerHpUI : MonoBehaviour
    {
        public PlayerHp playerHpScript;
        public Image targetImage;
        public float maxHp;

        public Text HpBarInt;

        private void Awake()
        {
            ReloadUI();
            maxHp = playerHpScript.playerHp;
        }

        void Update()
        {
            if (playerHpScript == null)
            {
                if (targetImage != null) targetImage.color = Color.gray;
                return;
            }
            if (playerHpScript.playerHp >= maxHp)
            {
                if (targetImage != null) targetImage.color = Color.green;
            }
            else
            {
                if (targetImage != null) targetImage.color = Color.red;
            }
        }

        public void ReloadUI()
        {
            HpBarInt.text = "" + playerHpScript.playerHp;
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sanghyuk
{
    public class PlayerData : MonoBehaviour
    {
        public int PlayerHp = 5;
        public int Bomb;
        public int Score;
        public float time;
        public int HaveShield;
        public bool Invin;
        private Coroutine invinCoroutine;

        void Start()
        {
            
        }

        void Update()
        {
            
        }
        IEnumerator InvinTimer()
        {

            if (Invin == true)
            {
                yield return new WaitForSeconds(3.0f);
                Invin = false;
                Debug.Log("公利 场");

                invinCoroutine = null;
            }

        }
        public void InvinTimerStart()
        {
            if (invinCoroutine != null)
            {
                StopCoroutine(invinCoroutine);
            }

            Invin = true;
            Debug.Log("公利 矫累");

            invinCoroutine = StartCoroutine(InvinTimer());
        }

    }

}
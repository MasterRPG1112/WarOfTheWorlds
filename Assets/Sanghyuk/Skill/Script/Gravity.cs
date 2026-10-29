using UnityEngine;

namespace Sanghyuk
{
    public class Gravity : MonoBehaviour
    {
        void Start()
        {
            Destroy(gameObject, 5f);
        }

        void Update()
        {
            GameObject PlayerPos = GameObject.Find("Player");
            transform.position = PlayerPos.transform.position;
        }
        private void OnCollisionEnter2D(Collision2D other)
        {
            if (other.gameObject.CompareTag("Bullet"))
            {
                //총알 속도 데이터 가져와서 속도 줄이는거 여기 만들기
            }
        }
    }
}

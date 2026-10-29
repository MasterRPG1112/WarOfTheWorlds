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
                //other 써서 속도를 느리게 하는 코드 가져오기
            }
        }
    }
}

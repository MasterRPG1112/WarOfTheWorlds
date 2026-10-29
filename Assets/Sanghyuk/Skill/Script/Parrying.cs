using UnityEngine;

namespace Sanghyuk
{
    public class Parrying : MonoBehaviour
    {
        void Start()
        {
            Destroy(gameObject, 0.125f);
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
                Destroy(other.gameObject);
            }
        }
    }
}

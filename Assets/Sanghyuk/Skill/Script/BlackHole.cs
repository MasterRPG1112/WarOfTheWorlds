
using UnityEngine;

namespace Sanghyuk
{
    public class BlackHole : MonoBehaviour
    {
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            Destroy(gameObject, 3f);
        }

        // Update is called once per frame
        void Update()
        {

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

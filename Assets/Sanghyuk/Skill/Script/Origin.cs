using UnityEngine;

namespace Sanghyuk
{
    public class Origin : MonoBehaviour
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
            transform.position += new Vector3(0f, 3f, 0f) * 2f * Time.deltaTime;
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

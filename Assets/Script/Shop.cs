using UnityEngine;

namespace Jain
{
    public class Shop : MonoBehaviour
    {
        //돈
        public int coin;

        //우주선 파츠 소유 여부
        public int part1;
        public int part2;
        public int part3;

        //스테이지 클리어 시 파츠 구매 권한
        public int partLicense;

        //특수 아이템 소유 여부
        public int provocation; //강제 유도
        public int stop; //시간 정지
        public int reflex; //공격 반사
        public int pull; //블랙홀
        public int slow; //감속장

        void Start()
        {

        }

        void Update()
        {

        }
    }

}
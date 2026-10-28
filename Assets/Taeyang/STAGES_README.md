# Taeyang 스테이지 사용 안내

Unity 6000.5.0f1. 기존 TaeyangStage1 및 EnemyTest 씬은 유지됩니다.

## 실행
Assets/Taeyang/Scenes/TaeyangCampaign1.unity를 열고 Play 합니다.
TaeyangCampaign2/3 씬에서 해당 지역을 바로 테스트할 수도 있습니다.
이동: WASD/방향키, Space: 적 탄환 제거(기존 테스트 플레이어 기능).
테스트 플레이어는 사망하면 체력을 복구하므로 실제 게임 오버/밸런스 검증용은 아닙니다.

각 지역은 300초 생존 + 잔여 적 정리 + 보스 대기/전투 순서입니다.
따라서 보스전까지 포함한 전체 시간은 5분을 넘습니다.
60초마다 웨이브가 상승하며 체력·발사 빈도·편대 규모가 증가합니다.
등장 간격은 편대 기준 3/4/5초, 첫 등장도 해당 간격 뒤입니다.
300초 시점에는 새 편대를 생성하지 않습니다(총 99/74/59회).

- 1: 갈색 별빛, 낙하 소행성, 정찰기/전투기/요격기, 후반 폭격기, 후반 2대 편대.
- 2: 어두운 보라 별빛, 옆으로 흐르는 잔해, 중장갑함 포함 2~3대 편대.
- 3: 푸른 얼음 별빛, 좌우로 흔들리는 얼음 운석, 적 5종 3~4대 편대.
- 정찰기: 유도탄. 요격기: 속도 기반 예측탄. 폭격기: 목표 위치를 고정한 곡선탄.
- 중장갑함: 확산탄과 Stage 2 이상 추가 유도탄.
배경과 장애물은 코드에서 생성한 도형이며 외부 이미지 에셋을 사용하지 않습니다.

## 보스 담당 연결
각 씬의 Stage N Director > StageDirector > Boss Prefab에 팀원 보스를 연결합니다.
한 씬에서 순서대로 진행하려면 Campaign Boss Prefabs 배열의 0/1/2에 각 보스를 연결하세요.
Boss Prefab은 현재 스테이지 단독 테스트용이며 다음 지역으로 갈 때 초기화됩니다.
Boss Position은 기본 (0,3,0)입니다. 300초 뒤 일반 적 정리가 끝나면 한 번 생성됩니다.
외부 스포너를 사용할 때는 Boss Requested 이벤트에서 생성하고 AttachBoss(instance)를 호출합니다.
보스 오브젝트 Destroy는 클리어로 인식합니다. 풀링/비활성화 방식이면 실제 처치 순간
NotifyBossDefeated()를 호출해야 합니다. 단순 비활성화는 클리어로 처리하지 않습니다.
보스 미연결 시 자동 클리어하지 않으며 TEST ONLY 버튼만 대기를 건너뜁니다.
클리어 뒤 NEXT STAGE는 씬 교체 없이 다음 지역을 시작합니다. Stage 3은 최종 클리어 표시입니다.
Stage Cleared 이벤트로 팀원의 보상/메뉴 로직을 연결할 수 있습니다.
보스 프리팹을 연결한 뒤 씬 생성 메뉴를 다시 실행하면 연결이 초기화되므로 주의하세요.

## 팀 통합 범위
씬에는 기존 DemoPlayer가 사용됩니다. 다른 팀원 플레이어, 저장, 메뉴, 상점은 수정하지 않았습니다.
장애물은 DemoPlayer에 직접 피해를 줍니다. 다른 Player 태그 오브젝트에는 TakeDamage(int)를
SendMessage로 전달합니다. 해당 메서드가 없는 팀원 플레이어/부위별 HP 체계에는 별도 어댑터가 필요합니다.
기존 탄환의 Bullet 태그 기반 팀원 HP 처리는 유지됩니다.
오디오는 추가하지 않았습니다. Build Settings/Packages/ProjectSettings도 변경하지 않았습니다.

## 재검증
배치 실행: -executeMethod Taeyang.Editor.ThreeStageBuilder.Validate -quit
Play Mode 실행: -executeMethod Taeyang.Editor.ThreeStagePlayCheck.Run (-quit 제외)
StageValidation.txt / StagePlayValidation.txt에 결과가 기록됩니다.
Play 검사는 12배속으로 각 스테이지 전체 시간을 진행하며 테스트 보스 오브젝트 연결과
처치 콜백을 검증합니다. 실제 보스전, 수동 조작, 시각적 품질과 난이도 평가는 별도입니다.


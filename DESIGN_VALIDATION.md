# 기획 전투 수정 — 구현 및 검증 결과

2026-10-30, Unity 6000.5.0f1, Windows D3D11. 기반은 PR #3을 포함한 PR #5 head `4db7c58`이다. 새 브랜치 `feature/design-missile-lure-20261030`에서 작업했고 변경 전 Git bundle을 보관했다.

## 구현

플레이어 자동 공격·유도탄·폭탄·시작 스킬 선택을 비활성화했다. 적 미사일로 다른 적을 처치하며 실제 점수·재화를 얻는다. 부드러운 8방향 이동, 공통 HP8, 날개 피격 공유, 접촉 피해·넉백·피격 무적, 아이템 4종 패치·2슬롯·개별 쿨타임, 하트·무적·보호막, 스테이지 후 상점, 장비 효과, v1→v2 저장 호환을 연결했다.

기존 적 5종과 공격 패턴, 확대된 고정 전장·8방향 스폰, 3개 300초 스테이지·특수 장애물·웨이브를 유지했다. 보스 모델·패턴 대신 일반 탄 면역·몸통박치기·HP·사망 콜백 어댑터만 제공한다. 원본 그래픽을 사용하며 배경·오프닝·엔딩·랭킹·치트 개발을 추가하지 않았다. 원본 씬·팀원 프리팹·메뉴·배경 스크립트의 diff가 없는 것을 확인했다.

## 실제 통과한 검사

- Unity 씬 생성·프리팹/스크립트 참조 검사 성공, Windows Development 빌드 성공(`Succeeded errors=0`, 약 194.7MB).
- 실제 Play Mode 51개 PASS, 게임 런타임 오류 0건. Unity Search 내부 예외 1건을 별도 기록했다.
- 실제 Windows 실행 8개 PASS, 게임 런타임 오류 0건. 기존 메뉴→새 게임→전투→메뉴→저장 이어하기를 실행했다.
- Windows GPU 캡처 6장. 1920×1080·1280×720의 HP8, HP4 반폭 게이지, 패치 쿨타임·장착 창을 확인했다.
- 3×300초 캠페인은 20배 시간 가속·플레이어 무적으로 진행했다. 적을 검사 코드로 전부 죽여 진행시키지 않았으며 기존 퇴각 규칙을 거쳤다. 상점 두 번과 다음 스테이지 전환을 확인했다. 사람의 무적 없는 난이도·완주 검사와는 다르다.
- 조작 검사는 실제 컨트롤러와 FixedUpdate를 호출해 가속·회전·감속·마지막 방향을 확인했다. 운영체제 키 입력이나 사람의 조작감 평가는 하지 않았다.
- 검증 시작 전 사용자 저장과 분리했다. 일반 Windows 실행에는 검증용 무적·자동 진행이 없다.

## 남은 항목

1. **기획서 사진 7장 미제공**: 텍스트는 반영했지만 사진에만 있는 정확한 아이템 획득 조건·밸런스·스테이지별 특수 규칙·재시작 규칙은 확인할 수 없다. 기존 구현과 명시된 텍스트를 사용했다. 기존 26초 퇴각 규칙의 기획 일치 여부도 확인하지 못했다.
2. **실제 팀원 보스 미연결**: 테스트용 어댑터의 몸통박치기·탄 면역·사망 이벤트만 검증했다. 현재 빌드는 보스 미등록 테스트 진행 경로를 사용한다. 실보스 3종을 Inspector에 등록하고 재검증해야 한다.
3. **Windows 종료 충돌 미해결**: 기능 검사 완료 후 `Application.Quit`에서 exit `-1073741819` (`0xC0000005`). 마지막 로그는 ComputeBuffer 해제 경고와 `PlayerConnection::Cleanup`이다. 기존 [이슈 #4](https://github.com/MasterRPG1112/WarOfTheWorlds/issues/4)와 같은 현상이다. 기능 성공과 정상 프로세스 종료를 구분한다.
4. **사람의 조작·난이도·밸런스 평가 미실시**. 스테이지 체크포인트 이어하기이며 전투 도중 정밀 재개는 지원하지 않는다. 실제 효과·쿨타임은 이어하기 시 초기화한다.

자동 Unity 실행·검증은 사용자가 명시적으로 허용한 범위에서 수행했다. 실제 팀 보스 등록과 최종 제출 전 사람의 플레이 검토가 필요하다. 계정 잔여 사용량을 읽는 도구가 없어 사용자 주간·5시간 한도 안의 소비량을 수치로 보증할 수는 없다.

## 전달 및 실행

`WarOfTheWorlds-Design-Unity.zip`을 압축 해제해 Unity Hub에서 6000.5.0f1로 연 뒤 `Assets/Integration/MainMenu.unity`를 실행한다. `WarOfTheWorlds-Design-Windows.zip`은 전체를 압축 해제하고 `WarOfTheWorlds.exe`를 실행한다. EXE만 따로 옮기면 안 된다. 조작·보스 연결·검사 진입점은 `INTEGRATION.md`에 있다.

기존 PR #3·#5와 main은 유지한다. 새 PR은 #5 브랜치를 기준으로 이번 설계 변경만 보여주며 head에는 이전 통합 내용도 모두 포함한다. PR 주소와 최종 커밋은 외부 `design-publication.json`에 기록한다.

## 변경 파일

- `Assets/Integration/DesignHud.cs`
- `Assets/Integration/DesignHud.cs.meta`
- `Assets/Integration/DesignRuntimeSmoke.cs`
- `Assets/Integration/DesignRuntimeSmoke.cs.meta`
- `Assets/Integration/DesignValidationMode.cs`
- `Assets/Integration/DesignValidationMode.cs.meta`
- `Assets/Integration/Editor/DesignPlayCheck.cs`
- `Assets/Integration/Editor/DesignPlayCheck.cs.meta`
- `Assets/Integration/Editor/IntegrationBuilder.cs`
- `Assets/Integration/IntegratedBossTarget.cs`
- `Assets/Integration/IntegratedBossTarget.cs.meta`
- `Assets/Integration/IntegratedCombat.cs`
- `Assets/Integration/IntegratedGame.cs`
- `Assets/Integration/IntegratedSkillEffect.cs`
- `Assets/Integration/IntegrationSaveStore.cs`
- `Assets/Integration/PatchPickup.cs`
- `Assets/Integration/PatchPickup.cs.meta`
- `Assets/Integration/Prefabs.meta`
- `Assets/Integration/Prefabs/Patch_0.prefab`
- `Assets/Integration/Prefabs/Patch_0.prefab.meta`
- `Assets/Integration/Prefabs/Patch_1.prefab`
- `Assets/Integration/Prefabs/Patch_1.prefab.meta`
- `Assets/Integration/Prefabs/Patch_2.prefab`
- `Assets/Integration/Prefabs/Patch_2.prefab.meta`
- `Assets/Integration/Prefabs/Patch_3.prefab`
- `Assets/Integration/Prefabs/Patch_3.prefab.meta`
- `Assets/Integration/TaeyangIntegrated.unity`
- `Assets/Script/HealItem.cs`
- `Assets/Script/Invincibility.cs`
- `Assets/Script/Player.cs`
- `Assets/Script/Shield.cs`
- `Assets/Taeyang/Scripts/EnemyProjectile.cs`
- `Assets/Taeyang/Scripts/EnemyShip.cs`
- `Assets/Taeyang/Scripts/StageDirector.cs`
- `DESIGN_VALIDATION.md`
- `INTEGRATION.md`

## Play Mode 원문

```text
PASS Immediate battle start without skill selection
PASS Shared HP8 and no initial skill ownership
PASS Fixed-step acceleration and turn begin smoothly without snapping
PASS Movement converges rapidly to the requested direction and speed
PASS Release applies smooth deceleration
PASS Rest preserves the last direction
PASS No automatic/basic/guided player gun
PASS Unowned patch blocked 0
PASS Unowned patch blocked 1
PASS Unowned patch blocked 2
PASS Unowned patch blocked 3
PASS Legacy save migrates to one HP pool without free equipped skill
PASS Missile ignores its owner and physically kills another enemy
PASS Lured kill gives score and currency once
PASS Enemy attack pattern creates real projectiles 0
PASS Enemy attack pattern creates real projectiles 1
PASS Enemy attack pattern creates real projectiles 2
PASS Enemy attack pattern creates real projectiles 3
PASS Enemy attack pattern creates real projectiles 4
PASS Enemy body contact damages the shared HP once
PASS Contact knockback separates enemy and player
PASS Heart pickup and maximum healing clamp
PASS Shield absorbs one hit and consumes one charge
PASS Timed invulnerability blocks damage
PASS Timed invulnerability expires
PASS Physical patch pickups grant matching stock
PASS Only two patches equip and unmounted use is blocked
PASS Black hole consumes stock only on successful cooldown start
PASS Black hole absorbs real enemy missiles
PASS Patch cooldowns are independent
PASS Slow field modifies real projectile speed
PASS Ending slow effect restores original speed
PASS Owned equipped mirror activates
PASS Mirror reflects the existing missile
PASS Reflected missile causes actual enemy damage without player bullets
PASS Pause freezes patch cooldowns
PASS Owned time stop freezes enemies without global player pause
PASS Time stop halts enemy and missile physics
PASS Time stop expires normally
PASS Existing shop purchase price and usable patch stock
PASS Purchased engine upgrade affects actual movement speed
PASS Version2 save retains shared HP and patch slots
PASS Stage clear opens shop intermission 1
PASS Stage clear opens shop intermission 2
PASS Three 300-second stages finish through explicit missing-boss test path
PASS No player basic shots throughout campaign
PASS Restart resets shared health and stock without selection
PASS Real player body contact damages boss adapter
PASS Ordinary enemy missiles cannot damage boss
PASS Boss adapter defeat calls stage completion interface
LIMIT: synthetic boss adapter only; actual teammate boss, human controls/balance and seven absent design photos are not validated. Accelerated campaign uses invulnerability.
PASS No game runtime errors
Runtime errors=0; Search exceptions=1
```

## Windows 실행 원문

```text
PASS Existing menu entry remains available
PASS New game begins immediately without selection
PASS Shared HP8 and unowned initial skills
PASS Insufficient currency blocks shop purchase
PASS Owned equipped patch consumes stock and starts cooldown
PASS No player basic shots during real-time combat
PASS Menu return and saved checkpoint continuation
PASS Continue resumes directly without skill popup
Runtime errors=0; captures=6
Human input/balance and real teammate boss not tested. Invulnerability used only for visual capture.
FUNCTIONAL PASS; process exit evaluated separately
```

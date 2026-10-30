# Taeyang 중심 보스 제외 통합

Unity **6000.5.0f1**. 기반 main: `1efd7c3`.
로컬 브랜치: `integration/taeyang-no-boss-20261030`.

## 실행과 메뉴

1. Unity Hub에 이 프로젝트 폴더를 추가하고 6000.5.0f1로 연다.
2. **`Assets/Integration/MainMenu.unity`**를 열고 Play를 누른다. 빌드도 이 씬에서 시작한다.
3. 팀원이 만든 기존 메뉴의 시작 버튼을 누른 뒤 다시하기 또는 이어하기를 선택한다.
4. `Assets/Integration/TaeyangIntegrated.unity`로 이동해 스킬을 선택하면 전투가 시작된다.
5. Esc의 MAIN MENU 또는 결과 화면의 MAIN MENU로 기존 메뉴에 돌아온다. 재시작은 새 게임으로 초기화한다.

기존 `Assets/Scenes/Menu.unity`를 복사해 메뉴 화면과 패널 구성을 유지했다. `Jain.Menu`의 기존 시작·설정·랭킹·뒤로·종료 버튼 콜백을 복사본에 다시 연결했다. 원본 메뉴 씬은 덮어쓰지 않았다. 비어 있던 설정 패널에는 음량과 전체화면 조작을 연결했다. 새 오디오나 그래픽 개선은 추가하지 않았다.

## 이어하기·랭킹·설정 저장

별도 `Application.persistentDataPath/TaeyangIntegration-v1.json`에 저장한다. 기존 PlayerPrefs는 읽거나 덮어쓰지 않는다.

- 이어하기는 **저장된 스테이지의 시작 지점**으로 돌아간다. 스테이지, 세 부위 체력, 점수·돈, 장치·소모품, 선택 스킬을 복원한다. 진행 중 탄환·적·웨이브를 그대로 재개하는 기능은 아니다.
- 스테이지 시작 때 체크포인트를 저장한다. 저장이 없으면 이어하기를 비활성화한다. 새 게임과 게임 결과 확정 시 이전 체크포인트를 지운다.
- 클리어·게임 오버 결과를 중복 없이 등록하고 상위 10개를 기존 랭킹 화면에 표시한다. 이름 입력 기능이 없으므로 기록 이름은 `Player`다.
- 음량과 전체화면 설정을 저장한다. 음량은 `AudioListener.volume`에 적용하며, 실제 전체화면 전환은 Windows 실행판에서 적용한다.
- 잘못된 저장은 기본값으로 복구한다. 다시 저장할 때 이전 파일과 손상 파일의 백업을 남긴다.

## 전투 조작

WASD/방향키 이동, 자동 발사, Space/Shift 선택 스킬, Tab 스킬 변경, Esc 일시정지, X는 스테이지당 1회 Origin 폭탄이다. 일시정지의 SHOP으로 기존 상점을 연다. 상점 뒤로 가기 후 Esc로 계속한다.

소모품은 B(블랙홀), V(감속장), R(패링), T(3초 적 정지)로 사용한다. 선택 스킬은 재사용 대기시간으로 사용하며 소모품과 별개다. 장치는 비상 동력원(코어 파괴 1회 방지), 터보 엔진(이동 속도 증가), 철갑탄(공격력 증가)으로 연결했다. 이 추가 효과의 밸런스는 팀 검토가 필요하다.

게임은 한 전투 씬에서 300초 생존과 잔여 적 정리를 거쳐 3스테이지를 진행한다. 기존 적의 26초 후 퇴각 규칙을 유지한다. 보스는 요청에 따라 제외했다.

## 구성과 재생성

기존 Menu/Stage1 및 Taeyang 데모·캠페인 씬, 프리팹과 그래픽 파일을 보존했다. TaeyangCampaign1을 바탕으로 기존 Stage1의 플레이어·체력 UI·상점·점수 UI를 복사했다. 비어 있던 기본 공격과 미완성 충돌 연결은 `Assets/Integration`의 어댑터로 추가했으며, 필요한 공통 스크립트의 라우팅과 종속성을 수정했다.

에디터 메뉴 `Tools > WarOfTheWorlds > Build Integrated No Boss Campaign`은 메뉴와 전투 통합 씬을 재생성한다. 통합 씬에 수동으로 추가한 변경은 덮어쓰므로 먼저 보관한다.

- `Integration.Editor.IntegrationBuilder.Build`: 두 씬 생성·참조 검사
- `Integration.Editor.MenuPlayCheck.Run`: 실제 메뉴 콜백·씬 전환·이어하기·랭킹·설정·별도 파일 저장 검사
- `Integration.Editor.IntegrationPlayCheck.Run`: 실제 전투 충돌·3스테이지 진행 검사
- `Integration.Editor.IntegrationBuilder.BuildWindows`: MainMenu → TaeyangIntegrated 순서로 Windows Development 빌드

Play Mode 검사 진입점은 `-quit` 없이 실행한다. 메뉴 검사는 사용자 저장을 보존하며, 디스크 검증은 별도 작업 파일에서 수행한다. 검증 결과와 미검증 항목은 프로젝트 외부의 `통합-검증-보고서.md`에서 구분한다.

## 보스 연결

`StageDirector.bossRequested`, `AttachBoss`, `NotifyBossDefeated`, `stageCleared` 계약을 유지했다. 실제 보스를 붙일 때 통합 씬의 `noBossCampaign`을 끄고 프리팹과 종료 이벤트를 연결한 뒤 별도 실행 검증이 필요하다.

## 확인된 실행 제한

메뉴 36개·전투 30개 자동 검사는 통과했고 Windows 메뉴/전투 렌더링과 복귀를 확인했다. 다만 Windows 프로그램 종료 단계의 네이티브 오류 0xC0000005는 미해결이다. 상세 로그와 범위는 외부 검증 보고서에 기록했다.

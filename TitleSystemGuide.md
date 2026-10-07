# 타이틀 시스템 연결 가이드

이 문서는 타이틀 씬에서 쓰는 스크립트와, 각 스크립트에 연결해야 하는 오브젝트/주의사항을 정리한 문서입니다.

## 1. TitleManager

### 역할
- 타이틀 씬의 메인 버튼 처리
- 게임 시작
- 업적 패널 열기/닫기
- 설정 패널 열기/닫기
- 종료 처리

### 연결할 오브젝트
- `Game Start Button`
- `Achievement Button`
- `Settings Button`
- `Quit Button`
- `Close Achievement Button`
- `Close Settings Button`
- `Achievement Panel`
- `Settings Panel`
- `AchievementPanelView`

### 주의사항
- `gameScene`(enum)은 실제 게임 씬 이름과 정확히 일치하는 enum 멤버를 사용해야 합니다. enum 값의 이름은 빌드에 등록된 씬 파일명과 일치하도록 유지하세요.
- 문자열 대신 enum을 사용하므로 컴파일 타임 안전성은 향상되지만, 씬을 리네임하거나 새 씬을 추가할 때는 `SceneName` enum을 갱신해야 합니다.
- `Achievement Panel`과 `Settings Panel`은 시작 시 비활성화 상태로 두는 것이 좋습니다.
- `Settings Panel`은 현재 UI 열고 닫기만 동작하며, 실제 옵션값 변경 기능은 아직 붙이지 않았습니다.
- 업적 패널과 설정 패널은 동시에 열리지 않도록 구성했습니다.
- `Game Start Button`은 시작 시 런타임 데이터 초기화를 먼저 수행한 뒤 씬 전환합니다.
- `selectedMode`는 현재 저장만 하고 있고, 실제 모드 분기는 아직 연결하지 않았습니다.

## 2. AchievementManager

### 역할
- 업적 정의 데이터 로드
- 업적 진행도 저장/복원
- 업적 해금 상태 유지
- 타이틀 씬과 게임 씬 모두에서 사용 가능

### 연결할 오브젝트
- 씬 어딘가에 한 번만 존재하는 `AchievementManager` 오브젝트
- 업적 데이터 에셋들

### 주의사항
- `AchievementManager`는 `DontDestroyOnLoad`로 유지됩니다.
- 업적 정의는 `AchievementData` ScriptableObject로 만들어야 합니다.
- 저장 파일은 `Application.persistentDataPath` 아래 JSON으로 생성됩니다.
- 업적 ID는 반드시 중복되지 않아야 합니다.

## 3. AchievementPanelView

### 역할
- 업적 목록 UI를 다시 그리는 역할

### 연결할 오브젝트
- `Content Root`
- `Entry Prefab`

### 주의사항
- `Entry Prefab`은 `AchievementEntryView`가 붙어 있어야 합니다.
- `Content Root`는 Scroll View의 Content 같은 부모 오브젝트를 넣는 것이 일반적입니다.
- `Refresh()`를 호출해야 목록이 다시 생성됩니다.

## 4. AchievementEntryView

### 역할
- 업적 한 줄의 표시를 담당

### 연결할 오브젝트
- `Title Text`
- `Description Text`
- `Progress Text`
- `Difficulty Text`
- `Unlocked Badge`

### 주의사항
- 진행도는 `3/4` 형식으로 표시됩니다.
- 달성 표시 배지는 선택 사항이지만 있으면 가독성이 좋습니다.

## 5. GameManager

### 역할
- 게임 런타임 상태 관리
- 타이틀에서 새 게임 시작 시 초기화 대상 일부 제공

### 주의사항
- `ResetRunData()`는 타이틀의 시작 버튼에서 호출됩니다.
- 현재 구현은 코인, 스테이지, 처치 수만 초기화합니다.
- 업적 데이터는 초기화하지 않습니다.

## 6. ArtifactManager

### 역할
- 유물 소유 목록 관리

### 주의사항
- `ClearArtifacts()`는 타이틀의 새 게임 시작 시 호출됩니다.
- 유물은 런 중 데이터이므로 새 게임에서 비워주는 편이 안전합니다.

## 추가로 넣을 만한 기능
- 설정 패널 안에 BGM / 효과음 슬라이더
- 해상도 / 전체화면 토글
- 크레딧 화면
- 튜토리얼 / 조작법 화면
- 이어하기 기능
- 게임 모드 선택 버튼
- 버전 표시

## 추천 배치 순서
1. TitleManager 오브젝트 1개 생성
2. 버튼과 패널 연결
3. AchievementManager를 초기 씬 또는 부트스트랩 씬에 배치
4. 업적 데이터 에셋 생성
5. 업적 목록 UI 프리팹 연결
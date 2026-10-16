# Chess-Loglike

체스 기물의 이동 규칙에 로그라이크식 성장, 상점, 인장(Seal), 유물(Artifact)을 결합한 Unity 게임 프로젝트입니다.
이 문서는 현재 저장소의 씬, ScriptableObject 데이터, 런타임 매니저와 UI 코드를 기준으로 작성한 개발자용 안내서입니다.

## 한눈에 보기

- **타이틀**: `Assets/Scenes/TitleScene.unity`
- **게임**: `Assets/Scenes/GameScene.unity`
- **게임 루프**: 전투 → 보상/정리 → 상점·이벤트·공방 → 다음 전투
- **핵심 성장 자원**
  - 기물: 이동 규칙과 전투력을 결정하는 전투 유닛
  - 인장: 개별 기물에 장착되어 이동/생존/전투 후속 행동을 바꾸는 모듈
  - 유물: 런 전체에 적용되는 패시브 또는 전투 중 사용하는 액티브 효과
  - 코인: 상점 구매, 유물 강화, 상점 리롤에 사용
- **데이터 중심 설계**: 기물·인장·유물은 대부분 `ScriptableObject`로 정의하고, 실제 동작은 C# 컴포넌트/효과 클래스가 담당합니다.

## 프로젝트 구조

```text
Assets/
  Scenes/                 타이틀 및 게임 씬
  Scripts/
    Manager/              런 상태, 스테이지, 턴, 사운드
    Piece/                기물 생성/이동/인벤토리/승급
      Seal/               인장 데이터, 장착 관리자, 인장 효과
    Artifact/             유물 데이터, 인벤토리, 효과 레지스트리
    Shop/                 상점 생성·구매·리롤
    WorkShop/             기물 합성·유물 강화
    Reward/               전투 보상
    Event/                이벤트 및 인장 해결
    Grid/                 보드 좌표와 셀
    UI/                   툴팁, 인벤토리, 연출
  Data/
    Piece/                PieceData 및 시작/상점 기물 목록
    Seal/                 인장 프리팹
    Artifact/             ArtifactData 에셋
    Achievement/          업적 데이터
  Prefabs/                UI 및 게임 오브젝트 프리팹
```

## 실행 전 확인

1. Unity에서 프로젝트를 엽니다.
2. 빌드 씬에 `TitleScene`과 `GameScene`이 등록되어 있는지 확인합니다.
3. `GameScene`에 `GameManager`, `GameStateManager`, `PieceManager`, `PieceSpawner`, `ShopManager`, `ArtifactManager`, `SealInventory` 등의 씬 오브젝트가 한 번씩 존재하는지 확인합니다.
4. `PieceInventory`의 `InitialPieceData`, `InitialSealData` 참조를 확인합니다.
5. 보드와 UI의 프리팹 참조가 비어 있지 않은지 확인합니다.

현재 매니저 다수는 싱글턴이며, 일부는 `DontDestroyOnLoad`로 유지되고 일부는 씬에 종속됩니다. 씬을 복제하거나 전환 구조를 바꿀 때 중복 매니저와 오래된 UI 참조가 생기지 않도록 주의해야 합니다.

## 런 구조

`GameManager`의 상위 흐름은 다음과 같습니다.

```text
None
  └─ Battle
       ├─ Prepare       기물 생성 및 배치
       ├─ GamePlay      턴제 전투
       ├─ Win/GameOver
       └─ Reward/Cleanup
  ├─ Shop              상점
  ├─ Event             이벤트
  └─ WorkShop          공방
```

- 전투에 진입하면 `GameStateManager.Prepare`가 시작되고 `PieceSpawner`가 플레이어/적 기물을 생성합니다.
- 시작 버튼을 누르면 배치 스냅샷을 저장하고 적 기물을 공개한 뒤 `GamePlay`로 넘어갑니다.
- 전투를 벗어날 때 플레이어 기물의 다음 스테이지용 스냅샷을 저장합니다.
- 스테이지는 `ClearedStage + 1`이 5의 배수일 때 보스 스테이지로 취급됩니다.
- 일반 스테이지는 `Resources.LoadAll<StageData>("Stages")`로 읽은 후보 중 진행도에 맞는 난이도를 우선 선택합니다.

## 핵심 데이터 처리

### 기물

- `PieceData`는 ID, `PieceType`, 표시명, 아이콘, 희귀도, 설명을 보유합니다.
- 실제 보드 기물은 `PieceController`가 담당합니다.
- `PieceSpawner.PieceSpawnInfo`는 기물 종류, 보드 좌표, 시작 인장을 함께 가질 수 있습니다.
- `PieceInventory`는 다음 스테이지에 남길 기물 종류를 보유합니다. 현재 저장 구조는 기물의 종류 중심이며, 일반적인 전투 종료 경로에서는 장착 인장을 `SealInventory`로 반환합니다.
- `PieceController`는 이동 가능 범위 계산, 드래그/드롭, 위협 표시, 승급, 버프 스택, 기물 파괴와 캡처 후속 처리를 담당합니다.

### 인장

인장은 두 층으로 나뉩니다.

1. `SealData`: 이름, 희귀도, 설명, 아이콘, 상점 판매 여부, 호환 기물, 동작 프리팹을 정의합니다.
2. `SealBase` 파생 컴포넌트: 실제 효과를 실행합니다.

장착 과정은 다음과 같습니다.

```text
SealData
  → SealManager.TryAttachSealToPiece
  → 호환 PieceType 검사
  → PieceController.EquipSeal
  → sealPrefab 인스턴스화
  → SealBase.Initialize(data, owner)
  → OnEquip
  → PieceController.equippedSeals에 등록
```

`SealBase`가 제공하는 주요 훅은 다음과 같습니다.

- `ModifyMoves`: 실제 이동 범위 수정
- `GetPreviewMovementOffsets`: 툴팁 이동 범위 표시
- `OnBeforeDestroy`: 파괴 거부 또는 생존 효과
- `OnBeforeMove`: 이동 직전 경로/상태 처리
- `OnAfterMove`: 이동 완료 후 추가 행동
- `OnAfterCapture`: 캡처 후 효과
- `OnOwnerDestroyed`: 소유 기물 파괴 시 효과

장착 해제는 준비 단계에서만 허용되며, 성공하면 `SealInventory`로 반환됩니다. 인벤토리는 빈 슬롯을 `null`로 유지하면서 이동·교환·삽입을 지원합니다.

### 유물

- `ArtifactData`는 ID, 이름, 설명, 희귀도, 가격, 타입, 최대 레벨을 보유합니다.
- `ArtifactType.Passive`는 보유 중 효과가 적용되고, `Active`는 전투 중 내 턴에 클릭해 1개를 소모합니다.
- `ArtifactManager.AddArtifact`는 런타임 복제본을 만들어 레벨/스택을 초기화합니다.
- 패시브 유물은 동일 ID 중복 획득을 막고, 액티브 유물은 동일 슬롯의 스택을 증가시킵니다.
- 효과 구현은 `ArtifactEffectRegistry`가 ID별 효과 객체를 제공하고, 각 효과 클래스가 실제 동작을 담당합니다.
- 스테이지 제한 게이지는 `ArtifactEffectHandlers.ResetStageLimitedEffects`를 통해 새 스테이지마다 초기화됩니다.
- 패시브 유물은 `ArtifactInhanceManager`를 통해 코인을 지불하고 레벨을 올릴 수 있으며, 액티브 유물은 강화 대상이 아닙니다.

## 상점

`ShopManager`는 기본적으로 3개의 슬롯을 만들고 각 슬롯을 다음과 같이 생성합니다.

- 기물 후보: `ShopPieceData`
- 독립 인장: 설정된 `standaloneSealSlotChance`에 따라 별도 슬롯으로 등장
- 기물 부착 인장: 진행도에 따라 등장 확률을 굴린 뒤 호환 가능한 인장 중 선택
- 유물: 별도 `ShopArtifactSlot`에 판매 가능한 유물 1개를 표시

기물에 인장이 붙으면 희귀도별 가격 배율이 적용됩니다.

| 인장 희귀도 | 가격 배율 |
|---|---:|
| Common | 1.5x |
| Rare | 1.7x |
| Epic | 2.0x |
| Legendary | 2.5x |

기물 부착 인장 확률은 `15% × 진행도`가 기본이며, A003 유물 레벨마다 보너스가 더해집니다. 희귀도 비율은 코드상 Legendary/Epic/Rare/Common 순으로 `0.5 : 2.5 : 4 : 8`에 해당합니다. 상점 리롤 비용은 기본 5이며 A002 효과와 연결됩니다.

## 개발 시 주의할 점

- 에디터에서는 일부 데이터 목록을 `AssetDatabase`로 찾지만, 빌드에서는 `Resources.LoadAll` 경로를 사용합니다. 실제 빌드에 포함할 데이터는 해당 런타임 로딩 경로와 일치하는지 확인해야 합니다.
- `ArtifactData.GetDescription`의 동적 수치는 유물 ID 문자열(`A001`~`A005`)과 효과 클래스에 결합되어 있습니다. ID 변경 시 설명과 실제 효과가 함께 깨질 수 있습니다.
- 인장 효과는 프리팹에 `SealBase` 파생 컴포넌트가 반드시 있어야 합니다. 프리팹 누락 또는 컴포넌트 누락은 장착 실패로 이어집니다.
- `PieceInventory`는 동종 기물을 타입으로 저장하므로 개별 기물 인스턴스의 상태를 영속화하지 않습니다.
- `GameManager`, `GameStateManager`, `PieceSpawner` 등은 싱글턴 초기화 순서의 영향을 받습니다. 새 매니저를 추가할 때 `Awake`, `Start`, 씬 로드 이벤트의 순서를 확인하십시오.

## 관련 문서

- [역기획서](REVERSE_GAME_DESIGN.md)
- [타이틀 시스템 연결 가이드](TitleSystemGuide.md)
- [기물 합성 디버깅 가이드](pieces-synth.agent.md)

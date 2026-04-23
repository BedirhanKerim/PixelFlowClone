# CLAUDE.md

Bu dosya Claude Code (claude.ai/code) için proje rehberidir.

## Proje

**Heron Games Hybrid Casual Developer Case** — "Pixel Flow" klonu. Grid'deki pikseller hedef resmin pikselleridir. Oyuncu shelf ve queue'daki domuzlara tıklayarak grid perimetresine yollar, domuzlar kendi rengine uyan hücrelere ateş eder, resim açığa çıkar.

## Tech Stack

- **Unity 6000.0 LTS** (URP)
- **VContainer** `1.16.8` — DI, composition root `GameLifetimeScope`
- **GenericEventBus** — struct event'ler, `GameEventBus : GenericEventBus<object>`
- **UniTask** — pig lap async loop, level transitions
- **DOTween Free** — tween'ler, reveal sequence (manuel `AwaitCompletion` extension'ı `Scripts/Core/TweenExtensions.cs`)
- **Lean Pool** — import edilmiş ama pool için `UnityEngine.Pool.ObjectPool<T>` kullanılıyor
- **TextMeshPro** — pig ammo 3D text
- **GPU Instancing** — grid cube'ları tek material + per-instance `MaterialPropertyBlock`

## Mimari

### Composition Root

`GameLifetimeScope` tüm servisleri register eder:
- Pure C# singletons: `GameEventBus`, `GridModel`, `LevelProgress`, `ShelfService`, `QueueService`
- Entry points: `WinConditionService`, `FailConditionService` (IStartable + IDisposable)
- SO instances: `GameConfig`, `LevelLibrary`
- Scene components: `LevelLoader`, `GridRenderer`, `PigFactory`, `PigPathService`, `BulletFactory`, `ShootingService`, `InputService`, `GameFlowController`, `CameraController`, `VfxService`, `RevealChoreography`

### Event Flow

Tüm sistem iletişimi struct event'leri üzerinden. Event'ler `Scripts/Events/GameEvents.cs`'te tanımlı.

MonoBehaviour pattern:
```csharp
[Inject] private GameEventBus _eventBus;

private void OnEnable() { _eventBus.SubscribeTo<Foo>(OnFoo); }
private void OnDisable() { _eventBus.UnsubscribeFrom<Foo>(OnFoo); }
private void OnFoo(ref Foo e) { ... }
```

### Phase machine (lightweight)

`GameFlowController` bir `Phase` enum'u tutar (`Loading | Playing | Won | Lost | Transitioning`), transition'larda `PhaseChanged` raise eder. Formal FSM yerine event-driven tercih edildi — servisler hangi phase'de ne yapacaklarını kendileri bilir.

### Klasör

```
Assets/
├── Scripts/
│   ├── Core/       GameLifetimeScope, GameFlowController, GameConfig, Phase, TweenExtensions
│   ├── Events/     GameEvents (tüm struct event'ler + GameEventBus)
│   ├── Level/      LevelData SO, LevelLibrary SO, LevelLoader, LevelProgress, FailReason
│   ├── Grid/       GridModel, GridRenderer, CellType, CellAddress
│   ├── Pigs/       PigEntity, PigFactory, PerimeterTrack, PigPathService, ShelfService, QueueService, PigConfig, PigState, PigOrigin
│   ├── Gameplay/   InputService, ShootingService, BulletController, BulletFactory, WinConditionService, FailConditionService
│   └── Visuals/    CameraController, VfxService, RevealChoreography
├── Editor/
│   └── LevelBuilderWindow (image → LevelData SO)
├── Prefabs/        Pig, Bullet, PaintSpark (elle oluşturulur, factory slot'larına bağlanır)
├── Materials/      Elle oluşturulur
└── ScriptableObjects/
    ├── GameConfig.asset
    ├── LevelLibrary.asset
    └── Levels/*.asset
```

Asmdef kullanılmıyor — tek `Assembly-CSharp` + `Editor/` magic folder.

## Gameplay Kuralları (mekanik referansı)

### Shelf & Queue

- Shelf: sabit 5 slot, aynı anda max 5 pig perimetrede.
- Queue: dinamik uzunluk, yalnızca öndeki 3'ü seçilebilir.
- **ShelfOrigin** pig tıklandığında → slot **reserve** kalır, lap sonunda mermisi varsa aynı slota döner.
- **QueueOrigin** pig tıklandığında → slot yok, queue bir kayar. Lap sonunda mermisi varsa shelf'te boş slot arar; yoksa **LevelFailed (ShelfOverflow)**.

### Firing

Domuz perimetre waypoint'lerinde (4 köşe) saat yönünde tur atar. Her anlık pozisyonda grid'e **dik** görüş hattı vardır (`PerimeterTrack.GetLineOfSight`). `GridModel.TryFindFirstMatch` şu kuralla çalışır:

```
line boyunca:
  Stone        → görüş kapanır, ateş yok, return false
  Painted      → üzerinden geçer, skip
  Normal + hedef rengi eşleşiyor + unpainted → FIRE, return true
  else         → skip
```

### Cell types

- **Empty**: cube hiç oluşturulmaz (PNG transparency → auto empty)
- **Normal**: boyanabilir, win sayımına dahil
- **Stone**: boyanmaz, görüş hattını kapatır, win sayımına dahil değil (obstacle)

## Convention'lar

- Event struct'ları PascalCase public field'lar, namespace yok.
- `[Inject] private T _field;` field injection, hem scene component'ler hem pure C# constructor inject.
- Factory'ler prefab referansını `[SerializeField]` ile alır, `Instantiate` eder.
- Runtime'da oluşturulan cube'lar için `GameObject.CreatePrimitive` + shared instanced material, per-instance color `MaterialPropertyBlock` üzerinden.
- Commit mesajları: `tag : lowercase açıklama` formatı (`edit :`, `bugfix :`, `feat :`, `refactor :`, `hotfix :`).

## Editor Tools

- **PixelFlow → Level Builder** — `LevelBuilderWindow`, image → LevelData SO pipeline:
  - Image drop + grid size (6-40)
  - Downscale (Point/Bilinear)
  - Alpha threshold → Empty cell mask (transparent pikseller cube almaz)
  - Palette quantization (merge threshold-based, max 12 color)
  - Auto-fill pigs (renk başına hücre sayısı ≥15 ise 20 ammo, değilse 10 ammo)

## Build & Run

Unity 6000.0 LTS'te açılır. `Assets/Scenes/GameplayScene.unity`'i aç → Play. Level Builder ile oluşturulan level'lar `LevelLibrary.asset`'e eklenir.

Android APK hedefli; input touch veya mouse.

## Case Constraint'leri (Heron Games brief)

- Clean code + coding standards + performance + özgün katkılar değerlendirilir
- Tek obstacle: **Stone** (infrastructure hazır, editor paint tool henüz yok)
- "No menus, no ads, no IAP, no leaderboard, no boosters, no SFX, no challenges, no pause, no rating" — hepsinden vazgeçildi
- Unique addition: end-level reveal choreography (cube'lar luminance'a göre Y yükselir, kamera orbit)

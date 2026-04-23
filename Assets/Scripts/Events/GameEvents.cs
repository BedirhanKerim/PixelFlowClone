using GenericEventBus;
using UnityEngine;

public class GameEventBus : GenericEventBus<object> { }

public struct LevelLoadRequested { public int LevelIndex; }
public struct LevelLoaded { public LevelData Data; }
public struct LevelCompleted { public int LevelIndex; }
public struct LevelFailed { public FailReason Reason; }
public struct PhaseChanged { public Phase From; public Phase To; }

public struct PigTappedFromShelf { public int PigId; public int SlotIndex; }
public struct PigTappedFromQueue { public int PigId; }

public struct PigEnteredPath { public int PigId; public Vector3 Entry; }
public struct PigLapCompleted { public int PigId; public int AmmoLeft; }
public struct PigDepleted { public int PigId; }

public struct ShelfSlotReserved { public int SlotIndex; public int PigId; }
public struct PigReturnedToShelf { public int PigId; public int SlotIndex; }
public struct ShelfOverflowFail { public int PigId; }

public struct ShotFired { public int PigId; public CellAddress Target; public byte ColorIndex; }
public struct CellPainted { public CellAddress Cell; public byte ColorIndex; }

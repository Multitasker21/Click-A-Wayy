using Godot;
using System;
using System.Collections.Generic;

public partial class TileHoverScene : Node2D
{
    [Export] private TileMapLayer TileMapLayer;
    [Export] private TileMapLayer TileMapLayerNav; // Navigation layer
private Dictionary<Vector2I, (int srcId, Vector2I atlas)> _backupNavTiles = new();

    [Export] private TileMap TileMap;
    [Export] private Sprite2D HoverSpriteTemplate;
    [Export] private Sprite2D TargetSpriteTemplate;
    [Export] private Sprite2D BlockedSpriteTemplate;
    [Export] private Sprite2D Goal;
    private bool isDraggingGoal = false;
    private Vector2I goalOriginCell;
    private Vector2 goalStartGlobalPosition;

    
    private Node2D _placableobstacle;
    private Sprite2D _originalTemplate;
    private Sprite2D _activeDrum;
    private bool isPlacingDrum = false;
    private List<Vector2I> _activeFootprint = new(); // Relative tile offsets from origin (hovered tile)
    private enum UndoType { TileMove, ObjectPlace }
    private Stack<(UndoType type, object data)> undoHistory = new();
    private Stack<List<(Vector2I from, Vector2I to, int srcId, Vector2I atlas)>> undoStack = new();
    private Stack<Sprite2D> objectUndoStack = new();
    private Stack<(UndoType type, object data)> redoHistory = new();

    private HashSet<Vector2I> _occupiedObjectCells = new();

    private List<Vector2I> selectedCells = new();
    private Vector2I currentOffset = Vector2I.Zero;
    private Vector2I _lastHovered = new(int.MinValue, int.MinValue);
    private Vector2 _tileSize = new(8, 8);

    private List<Sprite2D> hoverSprites = new();
    private List<Sprite2D> targetSprites = new();

    private bool isDragging = false;
    private bool isRightMouseHeld = false;
    private bool dragSelectionStarted = false;

    public override void _Ready()
    {
        _placableobstacle = GetNode<Node2D>("PlacableObstacle");
        _placableobstacle.Visible = false;
        HoverSpriteTemplate.Visible = false;
        TargetSpriteTemplate.Visible = false;
        BlockedSpriteTemplate.Visible = false;
        ScanAndBlockNavFromTileMapLayer();
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            if (keyEvent.Keycode == Key.Z && Input.IsKeyPressed(Key.Ctrl))
            {
                UndoLastMove();
                return;
            }
            if (keyEvent.Keycode == Key.Y && Input.IsKeyPressed(Key.Ctrl))
            {
                RedoLastMove();
                return;
            }

        }        

        if (e is InputEventMouseButton mouseBtn)
        {
            Vector2I cell = TileMapLayer.LocalToMap(TileMapLayer.GetLocalMousePosition());
            if (mouseBtn.ButtonIndex == MouseButton.Left && mouseBtn.Pressed && !isPlacingDrum)
        {
            // If clicked on Goal, start dragging it
            Vector2 globalClick = GetGlobalMousePosition();
            if (Goal.GetRect().HasPoint(Goal.ToLocal(globalClick)))
            {
                isDraggingGoal = true;
                goalOriginCell = TileMapLayer.LocalToMap(TileMap.ToLocal(Goal.GlobalPosition));
                goalStartGlobalPosition = Goal.GlobalPosition;
                GD.Print("🎯 Goal picked up");
                return;
            }
        }

        if (mouseBtn.ButtonIndex == MouseButton.Left && !mouseBtn.Pressed && isDraggingGoal)
        {    
            // Check if placement is blocked
            if (_occupiedObjectCells.Contains(cell) || TileMapLayer.GetCellSourceId(cell) != -1)
            {
                GD.Print("❌ Goal cannot be placed here — blocked.");
                Goal.GlobalPosition = goalStartGlobalPosition;
            }
            else
            {
                Vector2 worldPos = TileMap.ToGlobal(TileMapLayer.MapToLocal(cell));
                Goal.GlobalPosition = worldPos;

                undoHistory.Push((UndoType.ObjectPlace, new GoalMoveData
                {
                    FromCell = goalOriginCell,
                    ToCell = cell,
                    FromGlobalPos = goalStartGlobalPosition,
                    ToGlobalPos = worldPos
                }));

                redoHistory.Clear();

                GD.Print("✅ Goal placed.");
            }

            isDraggingGoal = false;
        }

        if (isPlacingDrum && _activeDrum != null)
            {
                if (mouseBtn.ButtonIndex == MouseButton.Left && mouseBtn.Pressed)
                {
                    // Check if any cell is blocked
                    bool blocked = false;
                    foreach (Vector2I offset in _activeFootprint)
                    {
                        Vector2I checkCell = cell + offset;

                        bool blockedByTile = TileMapLayer.GetCellSourceId(checkCell) != -1;
                        bool blockedByObject = _occupiedObjectCells.Contains(checkCell);

                        if (blockedByTile || blockedByObject)
                        {
                            blocked = true;
                            break;
                        }
                    }

                    if (blocked)
                    {
                        GD.Print("❌ Cannot place drum: space occupied.");
                        return;
                    }

                    // ✅ Safe to place: now update nav tiles + occupied cells
                    foreach (Vector2I offset in _activeFootprint)
                    {
                        Vector2I blockedCell = cell + offset;

                        if (TileMapLayerNav.GetCellSourceId(blockedCell) != -1)
                        {
                            if (!_backupNavTiles.ContainsKey(blockedCell))
                            {
                                int navSrc = TileMapLayerNav.GetCellSourceId(blockedCell);
                                Vector2I navAtlas = TileMapLayerNav.GetCellAtlasCoords(blockedCell);
                                _backupNavTiles[blockedCell] = (navSrc, navAtlas);
                            }

                            TileMapLayerNav.EraseCell(blockedCell);
                            GD.Print($"🧱 Nav tile removed at {blockedCell} due to object placement.");
                        }

                        _occupiedObjectCells.Add(blockedCell);
                    }

                    // Finish placement
                    Vector2 avgOffset = Vector2.Zero;
                    foreach (var offset in _activeFootprint)
                        avgOffset += offset;
                    avgOffset /= _activeFootprint.Count;

                    Vector2 worldPos = TileMapLayer.MapToLocal(cell) + (_tileSize * avgOffset);
                    _activeDrum.GlobalPosition = TileMap.ToGlobal(worldPos);

                    Vector2I originCell = TileMapLayer.LocalToMap(TileMap.ToLocal(worldPos) - (_tileSize * avgOffset));

                    undoHistory.Push((UndoType.ObjectPlace, new ObjectPlaceData
                    {
                        Template = _originalTemplate,
                        GlobalPosition = _activeDrum.GlobalPosition,
                        Footprint = new List<Vector2I>(_activeFootprint),
                        OriginCell = cell
                    }));

                    _activeDrum = null;
                    isPlacingDrum = false;
                    redoHistory.Clear();
                    targetSprites.ForEach(s => s.QueueFree());
                    targetSprites.Clear();

                    GD.Print("✅ Drum placed.");
                    return;
                }
            }


            // LEFT CLICK → Single select or place
            if (mouseBtn.ButtonIndex == MouseButton.Left)
            {
                if (mouseBtn.Pressed)
                {
                    if (selectedCells.Count > 0 && isDragging)
                    {
                        GD.Print("Placing tiles...");
                        PlaceTiles();
                        return;
                    }
                    // Restrict to only visible tiles (non-empty)
                    int srcId = TileMapLayer.GetCellSourceId(cell);
                    if (srcId == -1)
                    {
                        GD.Print($"❌ Skipped invisible tile on single click: {cell}");
                        return;
                    }
                    // Clear and select a new single cell
                    selectedCells.Clear();
                    selectedCells.Add(cell);
                    isDragging = true;
                    UpdateHoverSprites();
                    GD.Print($"Single-click selected: {cell}");
                }
            }

            // RIGHT CLICK → Start or stop drag-selection
            if (mouseBtn.ButtonIndex == MouseButton.Right)
{
    isRightMouseHeld = mouseBtn.Pressed;

    if (mouseBtn.Pressed)
    {
        int srcId = TileMapLayer.GetCellSourceId(cell);

        if (srcId != -1 && !selectedCells.Contains(cell))
        {
            selectedCells.Add(cell);
            dragSelectionStarted = true;
            UpdateHoverSprites();
            GD.Print($"Started drag-selection with: {cell}");
        }
        else
        {
            GD.Print($"❌ Ignored ghost tile on right-click: {cell}");
        }
    }
    else // Right released
    {
        if (dragSelectionStarted)
        {
            isDragging = true;
            dragSelectionStarted = false;
            GD.Print("Multi-select drag started");
        }
    }
}

        }
    }
    public static List<Vector2I> GetFootprintFromTexture(Texture2D texture, Vector2 tileSize, float alphaThreshold = 0.1f)
    {
        List<Vector2I> footprint = new();

        if (texture == null || texture.GetImage() == null)
            return footprint;

        Image img = texture.GetImage();

        int width = img.GetWidth();
        int height = img.GetHeight();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color color = img.GetPixel(x, y);
                if (color.A > alphaThreshold)
                {
                    Vector2I cell = new Vector2I(
                        Mathf.FloorToInt(x / tileSize.X),
                        Mathf.FloorToInt(y / tileSize.Y)
                    );
                    if (!footprint.Contains(cell))
                        footprint.Add(cell);
                }
            }
        }

        return footprint;
    }

    public void BeginPlacingObject(Sprite2D template, List<Vector2I>? footprintOverride = null)
    {
        if (_activeDrum != null)
            _activeDrum.QueueFree();

        _originalTemplate = template; // store original reference
        _activeDrum = _originalTemplate.Duplicate() as Sprite2D;

        _activeDrum.Visible = true;
        _activeDrum.Centered = true;
        AddChild(_activeDrum);

        // If footprintOverride is not provided, auto-generate from sprite
        if (footprintOverride != null)
        {
            _activeFootprint = new List<Vector2I>(footprintOverride);
        }
        else
        {
            _activeFootprint = GetFootprintFromTexture(_activeDrum.Texture, _tileSize);
        }

        isPlacingDrum = true;
        GD.Print($"🟣 Object placement started with {_activeFootprint.Count} footprint tiles.");
    }

    public override void _Process(double delta)
    {
        Vector2I hovered = TileMapLayer.LocalToMap(TileMapLayer.GetLocalMousePosition());
        if (isPlacingDrum && _activeDrum != null && _activeFootprint.Count > 0)
        {
            // Calculate center of the footprint
            Vector2 avg = Vector2.Zero;
            foreach (var offset in _activeFootprint)
                avg += offset;
            avg /= _activeFootprint.Count;

            Vector2 snappedPos = TileMapLayer.MapToLocal(hovered) + (_tileSize * avg);
            _activeDrum.GlobalPosition = TileMap.ToGlobal(snappedPos);

            UpdateFootprintPreview(hovered);
        }

        // Drag-select with right hold
        if (isRightMouseHeld && !selectedCells.Contains(hovered))
        {
            int srcId = TileMapLayer.GetCellSourceId(hovered);
            if (srcId != -1) // Only select visible/placed tiles
            {
                selectedCells.Add(hovered);
                GD.Print($"✅ Drag-selected: {hovered}");
                UpdateHoverSprites();
            }
            else
            {
                GD.Print($"❌ Skipped invisible tile: {hovered}");
            }
        }


        // ✅ Update drag target
        if (isDragging && selectedCells.Count > 0 && hovered != _lastHovered)
        {
            currentOffset = hovered - selectedCells[0];
            _lastHovered = hovered;
            UpdateTargetSprites();
        }
        if (isDraggingGoal)
        {
            //Vector2I hovered = TileMapLayer.LocalToMap(TileMapLayer.GetLocalMousePosition());
            Vector2 snappedPos = TileMap.ToGlobal(TileMapLayer.MapToLocal(hovered));
            Goal.GlobalPosition = snappedPos;
        }

    }

    private void UpdateHoverSprites()
    {
        foreach (var s in hoverSprites)
            s.QueueFree();
        hoverSprites.Clear();

        foreach (var cell in selectedCells)
        {
            Vector2 pos = TileMapLayer.MapToLocal(cell);
            var sprite = HoverSpriteTemplate.Duplicate() as Sprite2D;
            sprite.GlobalPosition = TileMap.ToGlobal(pos);
            sprite.Centered = true;
            sprite.Visible = true;
            AddChild(sprite);
            hoverSprites.Add(sprite);
        }
    }

    private void UpdateTargetSprites()
    {
        foreach (var s in targetSprites)
            s.QueueFree();
        targetSprites.Clear();

        foreach (var cell in selectedCells)
        {
            Vector2I target = cell + currentOffset;
            Vector2 pos = TileMapLayer.MapToLocal(target);
            Sprite2D sprite;

            bool targetOccupied = TileMapLayer.GetCellSourceId(target) != -1 || _occupiedObjectCells.Contains(target);

            if (targetOccupied)
            {
                // ❌ Show blocked placement sprite
                sprite = BlockedSpriteTemplate.Duplicate() as Sprite2D;
            }
            else
            {
                // ✅ Show normal placement sprite
                sprite = TargetSpriteTemplate.Duplicate() as Sprite2D;
            }

            sprite.GlobalPosition = TileMap.ToGlobal(pos);
            sprite.Centered = true;
            sprite.Visible = true;
            AddChild(sprite);
            targetSprites.Add(sprite);
        }
    }

    private void PlaceTiles()
    {
        List<(Vector2I from, Vector2I to, int srcId, Vector2I atlas)> moveHistory = new();

        foreach (Vector2I cell in selectedCells)
        {
            Vector2I target = cell + currentOffset;

            bool blockedByTile = TileMapLayer.GetCellSourceId(target) != -1;
            bool blockedByObject = _occupiedObjectCells.Contains(target);

            if (blockedByTile || blockedByObject)
            {
                GD.Print($"❌ Placement blocked at {target}. Aborting placement.");
                return; // Abort entire placement
            }

        }

        foreach (Vector2I cell in selectedCells)
{
    Vector2I target = cell + currentOffset;

    int srcId = TileMapLayer.GetCellSourceId(cell);
    Vector2I atlas = TileMapLayer.GetCellAtlasCoords(cell);

    GD.Print($"✅ Moving {cell} -> {target} (srcId: {srcId}, atlas: {atlas})");

    if (srcId != -1)
    {
        /// REMEMBER NOW IT WILL NOT CHAIN REMOVE WALL MOVEMENTS IT WILL DIRECTLY UNDO TO ORIGIN
        // Check if cell was previously a target — this means we’re remapping the same tile
        // Build a proper move chain
        var lastMove = undoHistory.Count > 0 && undoHistory.Peek().type == UndoType.TileMove
            ? undoHistory.Peek().data as List<(Vector2I from, Vector2I to, int srcId, Vector2I atlas)>
            : null;

        if (lastMove != null)
        {
            for (int i = 0; i < lastMove.Count; i++)
            {
                if (lastMove[i].to == cell)
                {
                    // We're moving a tile that was already moved in the previous step — update the chain
                    var chainedMove = lastMove[i];
                    chainedMove.to = target;
                    lastMove[i] = chainedMove;
                    goto SkipNewMoveAdd; // Skip adding a duplicate
                }
            }
        }

        moveHistory.Add((cell, target, srcId, atlas));

    SkipNewMoveAdd:
        TileMapLayer.EraseCell(cell);
        TileMapLayer.SetCell(target, srcId, atlas);
        TryRestoreNavTile(cell);
    }

    // Block nav tile at new target
    if (TileMapLayerNav.GetCellSourceId(target) != -1)
    {
        if (!_backupNavTiles.ContainsKey(target))
        {
            int navSrc = TileMapLayerNav.GetCellSourceId(target);
            Vector2I navAtlas = TileMapLayerNav.GetCellAtlasCoords(target);
            _backupNavTiles[target] = (navSrc, navAtlas);
        }

        TileMapLayerNav.EraseCell(target);
        GD.Print($"🧱 Removed nav tile at {target} due to new obstacle.");
    }
}


        // Save move to undo stack
        if (moveHistory.Count > 0)
        undoHistory.Push((UndoType.TileMove, moveHistory));

        selectedCells.Clear();
        hoverSprites.ForEach(s => s.QueueFree());
        targetSprites.ForEach(s => s.QueueFree());

        hoverSprites.Clear();
        targetSprites.Clear();

        isDragging = false;
        currentOffset = Vector2I.Zero;
    }
    private void UndoLastMove()
    {
        if (undoHistory.Count == 0)
        {
            GD.Print("❌ Nothing to undo.");
            return;
        }

        var (type, data) = undoHistory.Pop();
        redoHistory.Push((type, data)); 

        switch (type)
        {
            case UndoType.TileMove:
                var moves = data as List<(Vector2I from, Vector2I to, int srcId, Vector2I atlas)>;
                if (moves != null)
                {
                    foreach (var move in moves)
                    {
                        TileMapLayer.EraseCell(move.to);
                        TileMapLayer.SetCell(move.from, move.srcId, move.atlas);
                        GD.Print($"↩️ Undo: {move.to} → {move.from} (srcId: {move.srcId}, atlas: {move.atlas})");
                        if (_backupNavTiles.TryGetValue(move.to, out var navData))
                        {
                            TileMapLayerNav.SetCell(move.to, navData.srcId, navData.atlas);
                            //_backupNavTiles.Remove(move.to);
                            GD.Print($"🧭 Restored nav tile at {move.to} after undo.");
                        }

                    }
                    
                    UpdateHoverSprites();
                    UpdateTargetSprites();
                }
                break;

            case UndoType.ObjectPlace:
                ObjectPlaceData undoData = data as ObjectPlaceData;
                if (undoData != null)
                {
                    GD.Print("↩️ Undo: Last placed object sprite");


                    GD.Print($"Undo Global Position: {undoData.GlobalPosition}");
                    Vector2I undoOrigin = undoData.OriginCell;
                    GD.Print($"Calculated Undo Origin: {undoOrigin}");

                    foreach (Vector2I offset in undoData.Footprint)
                    {
                        Vector2I targetCell = undoOrigin + offset;
                        GD.Print($"Removing cell: {targetCell}");
                        _occupiedObjectCells.Remove(targetCell);
                    }
                    

                    // Remove placed sprite from scene (based on position match)
                    foreach (Node child in GetChildren())
                    {
                        if (child is Sprite2D sprite && sprite.GlobalPosition == undoData.GlobalPosition)
                        {
                            sprite.QueueFree();
                            break;
                        }
                    }
                    foreach (Vector2I offset in undoData.Footprint)
                    {
                        Vector2I cell = undoData.OriginCell + offset;

                        if (_backupNavTiles.TryGetValue(cell, out var navData))
                        {
                            TileMapLayerNav.SetCell(cell, navData.srcId, navData.atlas);
                            //_backupNavTiles.Remove(cell);
                            GD.Print($"🧭 Restored nav tile at {cell} after undo.");
                        }
                    }

                }
                break;
        }
    }
    private void RedoLastMove()
    {
        if (redoHistory.Count == 0)
        {
            GD.Print("❌ Nothing to redo.");
            return;
        }

        var (type, data) = redoHistory.Pop();

        switch (type)
        {
            case UndoType.TileMove:
                var moves = data as List<(Vector2I from, Vector2I to, int srcId, Vector2I atlas)>;
                if (moves != null)
                {
                    foreach (var move in moves)
                    {
                        TileMapLayer.EraseCell(move.from);
                        TileMapLayer.SetCell(move.to, move.srcId, move.atlas);
                        GD.Print($"🔁 Redo: {move.from} → {move.to} (srcId: {move.srcId}, atlas: {move.atlas})");

                        // Remove nav tile if needed
                        if (TileMapLayerNav.GetCellSourceId(move.to) != -1)
                        {
                            if (!_backupNavTiles.ContainsKey(move.to))
                            {
                                int navSrc = TileMapLayerNav.GetCellSourceId(move.to);
                                Vector2I navAtlas = TileMapLayerNav.GetCellAtlasCoords(move.to);
                                _backupNavTiles[move.to] = (navSrc, navAtlas);
                            }

                            TileMapLayerNav.EraseCell(move.to);
                            GD.Print($"🔁 Redo: Removed nav tile at {move.to} due to tile move.");
                        }
                    }

                    undoHistory.Push((UndoType.TileMove, data)); // Push back to undo
                    UpdateHoverSprites();
                    UpdateTargetSprites();
                }
                break;

            case UndoType.ObjectPlace:
                ObjectPlaceData objData = data as ObjectPlaceData;
                if (objData != null)
                {
                    Vector2I redoOrigin = objData.OriginCell;
                    bool blocked = false;

                    foreach (Vector2I offset in objData.Footprint)
                    {
                        Vector2I cell = redoOrigin + offset;

                        bool blockedByTile = TileMapLayer.GetCellSourceId(cell) != -1;
                        bool blockedByObject = _occupiedObjectCells.Contains(cell);

                        if (blockedByTile || blockedByObject)
                        {
                            GD.Print($"❌ Redo blocked: cell {cell} is already occupied.");
                            blocked = true;
                            break;
                        }
                    }

                    if (blocked)
                    {
                        GD.Print("❌ Skipping redo object placement due to collision.");
                        return;
                    }

                    // No blockage, proceed with placing
                    Sprite2D placed = objData.Template.Duplicate() as Sprite2D;
                    placed.Visible = true;
                    placed.Centered = true;
                    placed.GlobalPosition = objData.GlobalPosition;
                    AddChild(placed);

                    foreach (Vector2I offset in objData.Footprint)
                    {
                        Vector2I cell = redoOrigin + offset;

                        if (TileMapLayerNav.GetCellSourceId(cell) != -1)
                        {
                            if (!_backupNavTiles.ContainsKey(cell))
                            {
                                int navSrc = TileMapLayerNav.GetCellSourceId(cell);
                                Vector2I navAtlas = TileMapLayerNav.GetCellAtlasCoords(cell);
                                _backupNavTiles[cell] = (navSrc, navAtlas);
                            }

                            TileMapLayerNav.EraseCell(cell);
                            GD.Print($"🔁 Redo: Removed nav tile at {cell} due to object.");
                        }

                        _occupiedObjectCells.Add(cell); // ✅ Only added once here
                    }

                    undoHistory.Push((UndoType.ObjectPlace, objData)); // Push back for undo again
                    GD.Print("🔁 Redo: Recreated object sprite.");
                }
                break;
        }
    }


    public void SelectDrumTiles()
    {
        selectedCells.Clear();

        // You can also export this or calculate dynamically.
        Rect2I region = TileMapLayer.GetUsedRect(); // Covers all placed + nearby cells
        int targetSourceId = 3;

        for (int x = region.Position.X; x < region.Position.X + region.Size.X; x++)
        {
            for (int y = region.Position.Y; y < region.Position.Y + region.Size.Y; y++)
            {
                Vector2I cell = new Vector2I(x, y);
                int srcId = TileMapLayer.GetCellSourceId(cell);

                // Select if it has the desired source id OR it’s not placed but intended to be
                if (srcId == targetSourceId)
                {
                    selectedCells.Add(cell);
                }
            }
        }

        if (selectedCells.Count > 0)
        {
            isDragging = true;
            currentOffset = Vector2I.Zero;
            _lastHovered = new(int.MinValue, int.MinValue);
            UpdateHoverSprites();
            UpdateTargetSprites();

            GD.Print($"✅ Drum: Selected {selectedCells.Count} tiles with source ID {targetSourceId}.");
        }
        else
        {
            GD.Print($"❌ Drum: No tiles found with source ID {targetSourceId}.");
        }
    }
    private void UpdateFootprintPreview(Vector2I baseCell)
    {
        foreach (var s in targetSprites)
            s.QueueFree();
        targetSprites.Clear();

        foreach (Vector2I offset in _activeFootprint)
        {
            Vector2I cell = baseCell + offset;
            Vector2 pos = TileMap.ToGlobal(TileMapLayer.MapToLocal(cell));

            bool isOccupied = TileMapLayer.GetCellSourceId(cell) != -1 || _occupiedObjectCells.Contains(cell);


            Sprite2D sprite = (isOccupied ? BlockedSpriteTemplate : TargetSpriteTemplate).Duplicate() as Sprite2D;
            sprite.GlobalPosition = pos;
            sprite.Visible = true;
            sprite.Centered = true;

            AddChild(sprite);
            targetSprites.Add(sprite);
        }
    }
    private void ScanAndBlockNavFromTileMapLayer()
    {
        Rect2I region = TileMapLayer.GetUsedRect();
        for (int x = region.Position.X; x < region.Position.X + region.Size.X; x++)
        {
            for (int y = region.Position.Y; y < region.Position.Y + region.Size.Y; y++)
            {
                Vector2I cell = new Vector2I(x, y);
                int srcId = TileMapLayer.GetCellSourceId(cell);

                if (srcId != -1 && TileMapLayerNav.GetCellSourceId(cell) != -1)
                {
                    if (!_backupNavTiles.ContainsKey(cell))
                    {
                        int navSrc = TileMapLayerNav.GetCellSourceId(cell);
                        Vector2I navAtlas = TileMapLayerNav.GetCellAtlasCoords(cell);
                        _backupNavTiles[cell] = (navSrc, navAtlas);
                    }

                    TileMapLayerNav.EraseCell(cell);
                    GD.Print($"🧱 Startup: Removed nav tile at {cell} under obstacle.");
                }
            }
        }
    }
    private void TryRestoreNavTile(Vector2I cell)
    {
        if (_backupNavTiles.TryGetValue(cell, out var navData))
        {
            TileMapLayerNav.SetCell(cell, navData.srcId, navData.atlas);
            _backupNavTiles.Remove(cell);
            GD.Print($"🧭 Restored nav tile at {cell} (after tile moved away).");
        }
    }    
    private class ObjectPlaceData
    {
        public Sprite2D Template;
        public Vector2 GlobalPosition;
        public List<Vector2I> Footprint;
        public Vector2I OriginCell; 
    }
    private class GoalMoveData
    {
        public Vector2I FromCell;
        public Vector2I ToCell;
        public Vector2 FromGlobalPos;
        public Vector2 ToGlobalPos;
    }

}

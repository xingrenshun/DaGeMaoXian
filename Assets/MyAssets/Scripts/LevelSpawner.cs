using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class LevelSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject rollPrefab;
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private GameObject obstcalePrefab;
    [SerializeField] private GameObject knotsHolderPrefab;
    [SerializeField] private GameObject boundaryPrefab;
    [SerializeField] private GameObject pipePrefab;
    [SerializeField] private GameObject rollContainerPrefab;
    [SerializeField] private GameObject hideKnotsHolderPrefab;
    [SerializeField] private GameObject rollsMergeRingPrefab;
    [SerializeField] private GameObject knotPrefab;

    [Header("Transforms")]
    [HideInInspector] public List<Transform> columnTransforms;
    [SerializeField] private Transform gridCenter;
    [SerializeField] private Transform columnCenter;
    [SerializeField] private Transform rollContainerCenter;

    [Header("Spacing")]
    public float tileSpacing;
    public float knotSpacing;
    public float boundaryWallSpacing; // Extra space between walls and grid
    public float columnSpacing; // How much walls extend backward to cover empty space
    public float rollContainerSpacing;

    private GameObject[,] tileGrid;
    [HideInInspector] public LevelData levelData;

    [Header("Knots")]
    private int rows = 6;
    private int columns = 4;
    private float spacingX = .3f;
    private float spacingZ = .3f;

    [Header("Colors")]
    public List<Color> colors;

    [Header(" ")]
    [SerializeField] private SpriteRenderer beltRend;
    public List<TextAsset> levelsJson; // JSON files with levels

    [Header("Dynamic Spawning")]
    [SerializeField] private int initialKnotsHoldersPerColumn; // Number of knot holders to spawn initially per column
    
    // Track column data for dynamic spawning
    private Dictionary<Transform, List<QueueItem>> remainingColumnItems = new();
    private Dictionary<Transform, int> currentColumnSpawnIndex = new();

    public void SpawnLevel(int levelIndex)
    {
        TextAsset jsonFile = levelsJson[levelIndex];

        // Parse JSON - handle the "1|" prefix if present
        string jsonContent = jsonFile.text;
        if (string.IsNullOrEmpty(jsonContent))
        {
            Debug.LogError("JSON file is empty!");
            return;
        }

        if (jsonContent.StartsWith("1|"))
            jsonContent = jsonContent.Substring(2); // Remove "1|" prefix

        // 每关都把整份 JSON（15KB+）打进日志，真机上会造成明显卡顿与日志膨胀，仅在编辑器下保留
#if UNITY_EDITOR
        Debug.Log($"JSON Content: {jsonContent}");
#endif

        levelData = JsonUtility.FromJson<LevelData>(jsonContent);

        if (levelData == null)
        {
            Debug.LogError("Failed to parse JSON data!");
            return;
        }

        // Check if required data exists
        if (levelData.gridSize == null)
        {
            Debug.LogError("Grid size is null!");
            return;
        }

        if (levelData.blocks == null)
        {
            Debug.LogError("Blocks data is null!");
            return;
        }

        if (levelData.blindQueues == null)
        {
            Debug.LogError("Blind queues data is null!");
            return;
        }

        Debug.Log($"Loaded level data: Grid {levelData.gridSize.x}x{levelData.gridSize.y}, {levelData.blocks.Count} blocks, {levelData.blindQueues.Count} columns");

        FindFirstObjectByType<Camera>().backgroundColor = colors[Random.Range(0, colors.Count)];

        if (levelIndex < 15)
            beltRend.size = new Vector2(7.1f, beltRend.size.y);
        else
            beltRend.size = new Vector2(8, beltRend.size.y);

        // Generate level components
        SpawnRollContainers(levelIndex);
        GenerateGrid();
        GenerateBoundaries();
        SpawnBlocks();
        SpawnKnotsHolders();        
    }

    private void SpawnRollContainers(int levelIndex)
    {
        RollsManager rollsManager = FindFirstObjectByType<RollsManager>();

        // Decide how many to spawn based on levelIndex
        int spawnCount;
        if (levelIndex >= 0 && levelIndex < 4)
            spawnCount = 4;
        else if (levelIndex >= 4 && levelIndex < 12)
            spawnCount = 5;
        else if (levelIndex >= 12 && levelIndex < 24)
            spawnCount = 6;
        else
            spawnCount = 7;

        // Total width of all containers including spacing
        float totalWidth = (spawnCount - 1) * rollContainerSpacing;

        for (int i = 0; i < spawnCount; i++)
        {
            // Calculate centered position
            float xPos = rollContainerCenter.position.x - (totalWidth / 2f) + (i * rollContainerSpacing);

            GameObject rollContainer = Instantiate(
                rollContainerPrefab,
                new Vector3(xPos, rollContainerCenter.position.y, rollContainerCenter.position.z),
                Quaternion.identity,
                rollContainerCenter // keep grouped
            );

            rollsManager.rollContainers.Add(rollContainer.GetComponent<RollContainer>());
        }
    }

    void GenerateGrid()
    {
        if (levelData == null)
        {
            Debug.LogError("Level data is null!");
            return;
        }

        if (levelData.gridSize == null)
        {
            Debug.LogError("Grid size data is missing!");
            return;
        }

        int gridWidth = levelData.gridSize.x;
        int gridHeight = levelData.gridSize.y;

        Debug.Log($"Creating grid: {gridWidth}x{gridHeight}");

        tileGrid = new GameObject[gridWidth, gridHeight];

        // Calculate starting position to center the grid
        Vector3 startPos = gridCenter.position;
        startPos.x -= (gridWidth - 1) * tileSpacing * 0.5f;
        startPos.z -= (gridHeight - 1) * tileSpacing * 0.5f;

        // Create grid of tiles
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector3 tilePosition = startPos + new Vector3(x * tileSpacing, 0, y * tileSpacing);
                GameObject tile = Instantiate(tilePrefab, tilePosition, Quaternion.identity, transform);
                tile.name = $"Tile_{x}_{y}";
                tileGrid[x, y] = tile;
            }
        }

        Debug.Log($"Generated grid: {gridWidth}x{gridHeight}");
    }

    void GenerateBoundaries()
    {
        if (levelData == null || levelData.gridSize == null)
        {
            Debug.LogError("Level data or grid size is null!");
            return;
        }

        int gridWidth = levelData.gridSize.x;
        int gridHeight = levelData.gridSize.y;

        if (gridWidth <= 0 || gridHeight <= 0)
        {
            Debug.LogError($"Invalid grid size: {gridWidth}x{gridHeight}");
            return;
        }

        try
        {
            // Calculate grid bounds for consistent wall placement
            float leftEdgeX = tileGrid[0, 0].transform.position.x - (tileSpacing * 0.5f);
            float rightEdgeX = tileGrid[gridWidth - 1, 0].transform.position.x + (tileSpacing * 0.5f);
            float backEdgeZ = tileGrid[0, 0].transform.position.z - (tileSpacing * 0.5f);
            float frontEdgeZ = tileGrid[0, gridHeight - 1].transform.position.z + (tileSpacing * 0.5f);
            float centerY = tileGrid[0, 0].transform.position.y;

            // Calculate wall positions with IDENTICAL spacing from grid edges
            Vector3 leftWallPos = new(
                leftEdgeX - (boundaryWallSpacing - .3f),
                centerY,
                gridCenter.transform.position.z - .1f
            );

            Vector3 rightWallPos = new(
                rightEdgeX + (boundaryWallSpacing - .3f),
                centerY,
                gridCenter.transform.position.z - .1f
            );

            Vector3 backWallPos = new(
                (leftEdgeX + rightEdgeX) * 0.5f,
                centerY,
                backEdgeZ - (boundaryWallSpacing - .2f)
            );

            // Calculate dimensions for wall coverage
            float gridDepth = frontEdgeZ - backEdgeZ;
            float gridWidth_f = rightEdgeX - leftEdgeX;
            float extraCoverage = tileSpacing * 2;

            // Create left boundary wall
            GameObject leftBoundary = Instantiate(boundaryPrefab, leftWallPos, Quaternion.Euler(0, 90, 0), transform);
            leftBoundary.transform.localScale = new(
                levelData.gridSize.y,
                leftBoundary.transform.localScale.y,
                leftBoundary.transform.localScale.z
            );

            // Create right boundary wall
            GameObject rightBoundary = Instantiate(boundaryPrefab, rightWallPos, Quaternion.Euler(0, -90, 0), transform);
            rightBoundary.transform.localScale = new(
                levelData.gridSize.y,
                rightBoundary.transform.localScale.y,
                rightBoundary.transform.localScale.z
            );

            // Create back boundary wall
            GameObject backBoundary = Instantiate(boundaryPrefab, backWallPos, Quaternion.Euler(0, 0, 0), transform);
            backBoundary.transform.localScale = new(
                backBoundary.transform.localScale.x + 7,
                backBoundary.transform.localScale.y,
                gridWidth_f + extraCoverage
            );

            Debug.Log($"Generated 3 boundary walls with consistent {boundaryWallSpacing} spacing from grid");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error generating boundaries: {e.Message}");
        }
    }

    void SpawnBlocks()
    {
        if (levelData.blocks == null || levelData.blocks.Count == 0)
        {
            Debug.LogError("No blocks data found!");
            return;
        }

        foreach (BlockData blockData in levelData.blocks)
        {
            if (!IsValidBlock(blockData)) continue;

            int x = blockData.position.x;
            int y = blockData.position.y;
            Vector3 blockPosition = tileGrid[x, y].transform.position;

            switch (blockData.type)
            {
                case 3: // Pipe
                    SpawnPipe(blockData, x, y, blockPosition);
                    break;

                default:
                    if (blockData.state.color == 0)
                        SpawnObstacle(x, y, blockPosition);
                    else
                        SpawnRoll(blockData, x, y, blockPosition);
                    break;
            }
        }
        Debug.Log($"Spawned {levelData.blocks.Count} blocks");
    }

    bool IsValidBlock(BlockData blockData) // Remove This Later
    {
        if (blockData?.position == null)
        {
            Debug.LogError("Block position is null, skipping...");
            return false;
        }

        int x = blockData.position.x;
        int y = blockData.position.y;

        if (x < 0 || x >= levelData.gridSize.x || y < 0 || y >= levelData.gridSize.y)
        {
            Debug.LogError($"Block position ({x}, {y}) is out of grid bounds!");
            return false;
        }

        return true;
    }
    void SpawnPipe(BlockData blockData, int x, int y, Vector3 pos)
    {
        Quaternion rotation = blockData.targetDirection != null
            ? GetRotation(new Vector2(blockData.targetDirection.x, blockData.targetDirection.y))
            : Quaternion.identity;

        GameObject pipe = Instantiate(pipePrefab, pos, rotation, transform);
        pipe.name = $"Pipe_{x}_{y}";

        Pipe pipeComponent = pipe.GetComponent<Pipe>();
        if (blockData.attachments != null)
        {
            pipeComponent.x = x;
            pipeComponent.y = y;
            pipeComponent.rollsToSpawnColorIds = blockData.attachments
                .Select(a => a.color).ToList();
        }
    }

    void SpawnObstacle(int x, int y, Vector3 pos)
    {
        pos.y = obstcalePrefab.transform.position.y;
        GameObject obstacle = Instantiate(obstcalePrefab, pos, Quaternion.identity, transform);
        obstacle.name = $"Obstacle_{x}_{y}";
        obstacle.tag = "Obstacle";
    }

    void SpawnRoll(BlockData blockData, int x, int y, Vector3 pos)
    {
        GameObject roll = Instantiate(rollPrefab, pos, Quaternion.identity, transform);
        roll.name = $"Roll_{x}_{y}";

        Roll rollComponent = roll.GetComponent<Roll>();
        SetupRollState(rollComponent, blockData, x, y);

        if (blockData.type == 1 && blockData.linkType == 0)
            SetupMergeRing(roll, rollComponent, blockData, pos);

        if (y == levelData.gridSize.y - 1)
            rollComponent.CanMove();
    }

    void SetupRollState(Roll roll, BlockData blockData, int x, int y)
    {
        if (blockData.state == null)
        {
            Debug.LogError($"Roll ({x}, {y}) state is null!");
            return;
        }

        roll.colorId = blockData.state.color;
        roll.rollModel.material.color = GetColor(blockData.state.color);

        if (blockData.state.isHidden)
        {
            roll.hideModelObj.SetActive(true);
            roll.notMovableModel.gameObject.SetActive(false);
        }
        else
            roll.notMovableModel.material.color = GetColor(blockData.state.color);

        if (blockData.state.isFrozen)
        {
            roll.frozenObj.SetActive(true);
            roll.isFrozen = true;
        }
     
        if (blockData.state.isKey)
            roll.keyObj.SetActive(true);
        else if(blockData.state.isLocked)
            roll.lockObj.SetActive(true);
    }

    void SetupMergeRing(GameObject roll, Roll rollComponent, BlockData blockData, Vector3 pos)
    {
        if (blockData.targetDirection == null) return;
        GameObject foundObj = FindRollFromDir(roll.name, blockData.targetDirection.x, blockData.targetDirection.y);
        if (foundObj == null || !foundObj.GetComponent<Roll>())
        {
            Quaternion rotation = GetRotation(new Vector2(blockData.targetDirection.x, blockData.targetDirection.y));
            Instantiate(rollsMergeRingPrefab, pos, rotation, roll.transform);
        }

        rollComponent.targetRollDirection = new Vector2(blockData.targetDirection.x, blockData.targetDirection.y);
        rollComponent.isConnectedRoll = true;
    }

    void SpawnKnotsHolders()
    {
        if (levelData.blindQueues == null || levelData.blindQueues.Count == 0)
        {
            Debug.LogError("No blind queues data found!");
            return;
        }

        // Total width of the row
        float totalWidth = (levelData.blindQueues.Count - 1) * columnSpacing;

        for (int i = 0; i < levelData.blindQueues.Count; i++)
        {
            // Offset from the center
            float xPos = columnCenter.position.x - (totalWidth / 2f) + (i * columnSpacing);

            // Spawn object
            Vector3 spawnPos = new Vector3(xPos, columnCenter.position.y, columnCenter.position.z);
            GameObject column = new GameObject("Column " + i);
            column.transform.position = spawnPos;
            columnTransforms.Add(column.transform);
            column.AddComponent<Column>();

            BlindQueueColumn queueColumn = levelData.blindQueues[i];
            if (queueColumn.column == null || queueColumn.column.Count == 0)
            {
                Debug.LogError($"Column {i + 1} has no items!");
                continue;
            }

            SpawnKnotsHoldersInColumn(queueColumn.column, column.transform, $"Column{i + 1}");
        }
    }

    void SpawnKnotsHoldersInColumn(List<QueueItem> queueItems, Transform columnTransform, string columnName)
    {
        if (queueItems == null || queueItems.Count == 0 || columnTransform == null)
        {
            Debug.LogError($"Invalid data for {columnName}");
            return;
        }

        // Initialize tracking for this column
        remainingColumnItems[columnTransform] = new List<QueueItem>(queueItems);
        currentColumnSpawnIndex[columnTransform] = 0;

        Vector3 startPosition = columnTransform.position;

        // Determine how many knot holders to spawn initially (limited by available items)
        int itemsToSpawn = Mathf.Min(initialKnotsHoldersPerColumn, queueItems.Count);

        // Spawn initial batch of KnotsHolder prefabs
        for (int i = 0; i < itemsToSpawn; i++)
            SpawnSingleKnotHolder(columnTransform, i, queueItems[i], columnName);

        // Update current spawn index
        currentColumnSpawnIndex[columnTransform] = itemsToSpawn;

        //Debug.Log($"Spawned initial {itemsToSpawn} knot holders in {columnName} (out of {queueItems.Count} total)");
    }

    private void SpawnSingleKnotHolder(Transform columnTransform, int index, QueueItem queueItem, string columnName)
    {
        Vector3 startPosition = columnTransform.position;
        
        // Calculate position (first knot at column position, rest forward)
        Vector3 knotPosition = startPosition + columnTransform.forward * (index * knotSpacing);

        // If this is the first knot (index == 0), spawn it exactly at the column transform position
        if (index == 0)
            knotPosition = startPosition;

        // Spawn knots holder
        GameObject knotHolder = Instantiate(knotsHolderPrefab, knotPosition, Quaternion.identity, columnTransform);
        //knotHolder.name = $"{columnName}_Knot_{index}";
        // Set color using KnotsHolder script colorId field
        KnotsHolder knotsHolderComponent = knotHolder.GetComponent<KnotsHolder>();
        knotsHolderComponent.colorId = queueItem.color;
                
        if(queueItem.isHidden)
        {
            GameObject hidePanel = Instantiate(hideKnotsHolderPrefab, knotPosition, Quaternion.identity, knotHolder.transform);
            knotsHolderComponent.hidePanel = hidePanel;
        }

        //Debug.Log($"Set {columnName} knot {index} color to: {queueItem.color}");

        SpawnKnotsInHolder(knotHolder);
    }

    // Spawns the next KnotsHolder in the specified column if there are remaining items.
    public void SpawnNextKnotsHolder(Transform columnTransform)
    {
        // Check if we have tracking data for this column // Remove this Later
        if (!remainingColumnItems.ContainsKey(columnTransform) || !currentColumnSpawnIndex.ContainsKey(columnTransform)) 
        {
            Debug.LogError($"No tracking data found for column {columnTransform.name}");
            return;
        }

        List<QueueItem> columnItems = remainingColumnItems[columnTransform];
        int currentIndex = currentColumnSpawnIndex[columnTransform];

        // Check if there are more items to spawn // Remove this Later
        if (currentIndex >= columnItems.Count)
        {
            Debug.Log($"No more knot holders to spawn in column {columnTransform.name}");
            return;
        }

        // Get column name for naming
        string columnName = columnTransform.name;
        
        // Spawn the next knot holder
        SpawnSingleKnotHolder(columnTransform, currentIndex, columnItems[currentIndex], columnName);
        
        // Update spawn index
        currentColumnSpawnIndex[columnTransform] = currentIndex + 1;
        
        //Debug.Log($"Spawned next knot holder ({currentIndex}) in {columnName}. Remaining: {columnItems.Count - currentIndex - 1}");
    }

    public void SpawnKnotsInHolder(GameObject knotHolder)
    {
        KnotsHolder knotsHolder = knotHolder.GetComponent<KnotsHolder>();

        Vector3 startPos = knotsHolder.knotsParent.position;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                // Snake (zigzag) pattern
                int spawnCol = (row % 2 == 0) ? col : (columns - 1 - col);

                // Calculate position based on object's orientation
                Vector3 spawnPos =
                    startPos +
                    (-knotsHolder.knotsParent.right * spawnCol * spacingX) +   // column direction
                    (knotsHolder.knotsParent.forward * row * spacingZ);      // row direction

                GameObject knot = Instantiate(knotPrefab, spawnPos, Quaternion.identity, knotsHolder.knotsParent);
                knot.GetComponentInChildren<SpriteRenderer>().color = GetColor(knotsHolder.colorId);
            }
        }
    }

    private GameObject FindRollFromDir(string name, int dx, int dy)
    {
        // Split the name, works for both "Roll_x_y" and "Obstacle_x_y" and "Pipe_x_y"
        string[] parts = name.Split('_');
        int x = int.Parse(parts[1]);
        int y = int.Parse(parts[2]);

        // Apply offset
        int newX = x + dx;
        int newY = y + dy;

        // First try Roll
        string rollName = $"Roll_{newX}_{newY}";
        GameObject roll = GameObject.Find(rollName);
        if (roll != null) return roll;

        // If no Roll, try Pipe
        string pipeName = $"Pipe_{newX}_{newY}";
        GameObject pipe = GameObject.Find(pipeName);
        if (pipe != null) return pipe;

        // Nothing found
        return null;
    }

    // Helper method to get grid position world coordinates (useful for debugging)
    public Vector3 GetGridWorldPosition(int x, int y)
    {
        if (tileGrid != null && x >= 0 && x < levelData.gridSize.x && y >= 0 && y < levelData.gridSize.y)
            return tileGrid[x, y].transform.position;

        return Vector3.zero;
    }

    Quaternion GetRotation(Vector2 dir)
    {
        if (dir.x == 0 && dir.y == 1) return Quaternion.Euler(0, 0, 0);   // Up
        if (dir.x == 0 && dir.y == -1) return Quaternion.Euler(0, 180, 0); // Down
        if (dir.x == 1 && dir.y == 0) return Quaternion.Euler(0, 90, 0);  // Right
        if (dir.x == -1 && dir.y == 0) return Quaternion.Euler(0, -90, 0); // Left

        return Quaternion.identity; // default
    }

    public Color GetColor(int index)
    {
        switch (index)
        {
            case 8:
                return colors[0];
            case 512:
                return colors[1];
            case 4:
                return colors[2];
            case 256:
                return colors[3];
            case 2:
                return colors[4];
            case 64:
                return colors[5];
            case 16:
                return colors[6];
            case 32:
                return colors[7];
            case 128:
                return colors[8];
            default:
                return colors[3];
        }
    }
}
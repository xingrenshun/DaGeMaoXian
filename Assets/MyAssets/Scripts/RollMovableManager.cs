using UnityEngine;

public class RollMovableManager : MonoBehaviour
{
    LevelSpawner levelSpawner;

    private void Start()
    {
        levelSpawner = FindFirstObjectByType<LevelSpawner>();
    }

    public void CheckAllRollsMovable()
    {
        foreach (Roll roll in FindObjectsByType<Roll>(FindObjectsSortMode.None))
        {
            if (roll.isMoved || roll.isMovable) continue;

            string[] parts = roll.gameObject.name.Split('_');

            if (parts.Length >= 3)
            {
                int x = int.Parse(parts[1]);
                int y = int.Parse(parts[2]);

                if (!IsRollPathBlocked(x, y))
                {
                    //Debug.Log("UnBlocked " + roll.gameObject.name);
                    roll.CanMove();
                }
            }
            else
            {
                //Debug.LogError("Invalid name format! Expected Roll_x_y");
            }
        }
    }

    private bool IsRollPathBlocked(int x, int y)
    {
        int gridSizeX = levelSpawner.levelData.gridSize.x;
        int gridSizeY = levelSpawner.levelData.gridSize.y;

        // Directions: Right, Left, Forward, Back
        Vector2Int[] directions =
        {
        new Vector2Int(1, 0),   // Right
        new Vector2Int(-1, 0),  // Left
        new Vector2Int(0, 1),   // Forward
        new Vector2Int(0, -1)   // Back
    };

        foreach (var dir in directions)
        {
            int nx = x + dir.x;
            int ny = y + dir.y;

            // Skip out of grid
            if (nx < 0 || nx >= gridSizeX || ny < 0 || ny >= gridSizeY)
                continue;

            // --- Check Roll ---
            if (GameObject.Find($"Roll_{nx}_{ny}") != null)
                continue; // blocked

            // --- Check Obstacle ---
            if (GameObject.Find($"Obstacle_{nx}_{ny}") != null)
                continue; // blocked

            // --- Check Pipe ---
            if (GameObject.Find($"Pipe_{nx}_{ny}") != null)
                continue; // blocked

            // If none exist → path clear
            return false;
        }

        // If all directions blocked
        return true;
    }
}
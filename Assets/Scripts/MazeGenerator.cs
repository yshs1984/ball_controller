using System.Collections.Generic;
using UnityEngine;

// 起動時に穴掘り法(recursive backtracker)で迷路を生成し、
// 床のサイズ・ボールの開始位置・ゴールの位置もあわせて調整する
public class MazeGenerator : MonoBehaviour
{
    [SerializeField] private int width = 6;
    [SerializeField] private int height = 6;
    [SerializeField] private float cellSize = 4f;
    [SerializeField] private float wallHeight = 2f;
    [SerializeField] private float wallThickness = 0.3f;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Transform floor;
    [SerializeField] private Transform ball;
    [SerializeField] private Transform goal;

    private struct Cell
    {
        public bool visited;
        public bool north;
        public bool south;
        public bool east;
        public bool west;
    }

    private const int MaxSize = 12;

    private Cell[,] cells;

    private void Start()
    {
        Generate();
    }

    // ステージ開始・進行の両方から呼ばれる生成処理
    public void Generate()
    {
        ClearWalls();
        GenerateMaze();
        BuildWalls();
        PlaceFloor();
        PlaceBallAndGoal();
    }

    // ステージが進むごとに迷路を一回り大きくして再生成する(上限MaxSizeで頭打ち)
    public void GenerateNewStage(int stage)
    {
        width = Mathf.Min(MaxSize, width + 1);
        height = Mathf.Min(MaxSize, height + 1);
        Generate();
    }

    private void ClearWalls()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    private void GenerateMaze()
    {
        cells = new Cell[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                cells[x, z] = new Cell { visited = false, north = true, south = true, east = true, west = true };
            }
        }

        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        Vector2Int start = new Vector2Int(0, 0);
        cells[start.x, start.y].visited = true;
        stack.Push(start);

        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();
            List<Vector2Int> unvisitedNeighbors = GetUnvisitedNeighbors(current);

            if (unvisitedNeighbors.Count == 0)
            {
                stack.Pop();
                continue;
            }

            Vector2Int next = unvisitedNeighbors[Random.Range(0, unvisitedNeighbors.Count)];
            RemoveWallBetween(current, next);
            cells[next.x, next.y].visited = true;
            stack.Push(next);
        }
    }

    private List<Vector2Int> GetUnvisitedNeighbors(Vector2Int cell)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();

        if (cell.y < height - 1 && !cells[cell.x, cell.y + 1].visited)
        {
            neighbors.Add(new Vector2Int(cell.x, cell.y + 1));
        }
        if (cell.y > 0 && !cells[cell.x, cell.y - 1].visited)
        {
            neighbors.Add(new Vector2Int(cell.x, cell.y - 1));
        }
        if (cell.x < width - 1 && !cells[cell.x + 1, cell.y].visited)
        {
            neighbors.Add(new Vector2Int(cell.x + 1, cell.y));
        }
        if (cell.x > 0 && !cells[cell.x - 1, cell.y].visited)
        {
            neighbors.Add(new Vector2Int(cell.x - 1, cell.y));
        }

        return neighbors;
    }

    private void RemoveWallBetween(Vector2Int a, Vector2Int b)
    {
        if (b.x == a.x + 1)
        {
            cells[a.x, a.y].east = false;
            cells[b.x, b.y].west = false;
        }
        else if (b.x == a.x - 1)
        {
            cells[a.x, a.y].west = false;
            cells[b.x, b.y].east = false;
        }
        else if (b.y == a.y + 1)
        {
            cells[a.x, a.y].north = false;
            cells[b.x, b.y].south = false;
        }
        else if (b.y == a.y - 1)
        {
            cells[a.x, a.y].south = false;
            cells[b.x, b.y].north = false;
        }
    }

    private void BuildWalls()
    {
        float originX = -width * cellSize / 2f;
        float originZ = -height * cellSize / 2f;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                Vector3 center = new Vector3(
                    originX + x * cellSize + cellSize / 2f,
                    0f,
                    originZ + z * cellSize + cellSize / 2f);

                if (cells[x, z].north)
                {
                    CreateWall(center + new Vector3(0f, 0f, cellSize / 2f), cellSize, wallThickness);
                }
                if (cells[x, z].south)
                {
                    CreateWall(center + new Vector3(0f, 0f, -cellSize / 2f), cellSize, wallThickness);
                }
                if (cells[x, z].east)
                {
                    CreateWall(center + new Vector3(cellSize / 2f, 0f, 0f), wallThickness, cellSize);
                }
                if (cells[x, z].west)
                {
                    CreateWall(center + new Vector3(-cellSize / 2f, 0f, 0f), wallThickness, cellSize);
                }
            }
        }
    }

    private void CreateWall(Vector3 center, float sizeX, float sizeZ)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.SetParent(transform);
        wall.transform.position = new Vector3(center.x, wallHeight / 2f, center.z);
        wall.transform.localScale = new Vector3(sizeX, wallHeight, sizeZ);

        if (wallMaterial != null)
        {
            wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
        }
    }

    private void PlaceFloor()
    {
        if (floor == null)
        {
            return;
        }

        float floorSizeX = width * cellSize;
        float floorSizeZ = height * cellSize;
        floor.position = new Vector3(0f, floor.position.y, 0f);
        floor.localScale = new Vector3(floorSizeX, floor.localScale.y, floorSizeZ);
    }

    private void PlaceBallAndGoal()
    {
        float originX = -width * cellSize / 2f;
        float originZ = -height * cellSize / 2f;

        if (ball != null)
        {
            Vector3 startPosition = new Vector3(
                originX + cellSize / 2f,
                ball.position.y,
                originZ + cellSize / 2f);
            ball.position = startPosition;

            Rigidbody ballRigidbody = ball.GetComponent<Rigidbody>();
            if (ballRigidbody != null)
            {
                ballRigidbody.linearVelocity = Vector3.zero;
                ballRigidbody.angularVelocity = Vector3.zero;
            }
        }

        if (goal != null)
        {
            Vector3 goalPosition = new Vector3(
                originX + (width - 1) * cellSize + cellSize / 2f,
                goal.position.y,
                originZ + (height - 1) * cellSize + cellSize / 2f);
            goal.position = goalPosition;
        }
    }
}

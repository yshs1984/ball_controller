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
    private float ballStartHeight;
    private Mesh cubeMesh;

    private void Start()
    {
        // 落下でball.position.yがマイナスになった後でも正しい高さに戻せるよう、
        // シーン上で設定された本来の高さをここで一度だけ記録しておく
        if (ball != null)
        {
            ballStartHeight = ball.position.y;
        }

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

    // 落下時のリトライで呼ばれる。迷路は再生成せず、ボールの位置だけ戻す
    public void ResetBall()
    {
        PlaceBallAndGoal();
    }

    private void ClearWalls()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);

            // 動的生成した結合メッシュはGameObjectを消しても自動では解放されないため、
            // 明示的にDestroyしてメモリリークを防ぐ
            MeshFilter meshFilter = child.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Destroy(meshFilter.sharedMesh);
            }

            Destroy(child.gameObject);
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
        List<CombineInstance> wallInstances = new List<CombineInstance>();
        float originX = -width * cellSize / 2f;
        float originZ = -height * cellSize / 2f;

        // 内部の壁だけを、区切り線ごとに1枚ずつ集める(隣接する2セルの両側から
        // 重複して集めないよう、列/行の境界を基準にループする)。
        // 迷路外周(x=0, x=width, z=0, z=height)の境界線はループ範囲に含めないため、
        // 外周には壁が作られない(ボールが端まで転がると落下できるようにするため)。

        // 縦方向の壁(列と列の間。東西を区切る)
        for (int x = 1; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                if (cells[x - 1, z].east)
                {
                    Vector3 position = new Vector3(
                        originX + x * cellSize,
                        wallHeight / 2f,
                        originZ + z * cellSize + cellSize / 2f);
                    AddWallInstance(wallInstances, position, wallThickness, cellSize);
                }
            }
        }

        // 横方向の壁(行と行の間。南北を区切る)
        for (int z = 1; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                if (cells[x, z - 1].north)
                {
                    Vector3 position = new Vector3(
                        originX + x * cellSize + cellSize / 2f,
                        wallHeight / 2f,
                        originZ + z * cellSize);
                    AddWallInstance(wallInstances, position, cellSize, wallThickness);
                }
            }
        }

        if (wallInstances.Count == 0)
        {
            return;
        }

        // 全ての壁を1つのメッシュに結合し、描画(MeshRenderer)と当たり判定(MeshCollider)を
        // それぞれ1つのGameObjectにまとめる(壁の数だけGameObjectを作らないようにするため)。
        // 壁は動かない静的オブジェクトなので、MeshColliderは非convex(既定値)のままでよい
        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(wallInstances.ToArray());

        GameObject walls = new GameObject("Walls");
        walls.transform.SetParent(transform);
        walls.transform.localPosition = Vector3.zero;
        walls.transform.localRotation = Quaternion.identity;
        walls.transform.localScale = Vector3.one;

        MeshFilter meshFilter = walls.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = combinedMesh;

        MeshRenderer meshRenderer = walls.AddComponent<MeshRenderer>();
        if (wallMaterial != null)
        {
            meshRenderer.sharedMaterial = wallMaterial;
        }

        MeshCollider meshCollider = walls.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = combinedMesh;
    }

    private void AddWallInstance(List<CombineInstance> wallInstances, Vector3 position, float sizeX, float sizeZ)
    {
        CombineInstance instance = new CombineInstance
        {
            mesh = GetCubeMesh(),
            transform = Matrix4x4.TRS(position, Quaternion.identity, new Vector3(sizeX, wallHeight, sizeZ))
        };
        wallInstances.Add(instance);
    }

    // 壁の結合元となる、Unity標準のCubeメッシュを1度だけ取得してキャッシュする
    private Mesh GetCubeMesh()
    {
        if (cubeMesh == null)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
        }

        return cubeMesh;
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
                ballStartHeight,
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

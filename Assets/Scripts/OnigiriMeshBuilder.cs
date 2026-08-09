using System.Collections.Generic;
using UnityEngine;

// ボールを「おむすび」(三角柱)の見た目・当たり判定にする。
// Unityには三角柱の標準プリミティブが無いため、MazeGeneratorの壁生成と同様に
// 実行時にコードでメッシュを組み立てる。
// そのため、Editor上の非再生時のシーンビューでは古いメッシュのまま表示される
[RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
public class OnigiriMeshBuilder : MonoBehaviour
{
    // 正三角形の外接円半径(おむすびの大きさ)
    [SerializeField] private float radius = 0.6f;

    // Y方向の厚み(おむすびの奥行き)。
    // 薄くすると底面が安定しすぎて転ばず、床を滑るだけの動きになる。
    // 半径0.6に対して0.8程度にすると、角を軸にゴロゴロと転がるようになる
    [SerializeField] private float thickness = 0.8f;

    // 正三角形の頂点数
    private const int CornerCount = 3;

    private void Awake()
    {
        Mesh mesh = BuildPrismMesh();

        GetComponent<MeshFilter>().sharedMesh = mesh;

        // 非kinematicなRigidbodyと組み合わせるMeshColliderはconvexである必要がある
        // (三角柱は凸形状なのでそのまま設定できる)
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
        meshCollider.convex = true;
    }

    // Y軸方向に押し出した三角柱メッシュを組み立てる。
    // 上から見て正三角形になる向きにすることで、X-Z平面へ加わる力に対して
    // どの方向からも対称に反応する(進行方向によって転がり方が変わらない)
    private Mesh BuildPrismMesh()
    {
        Vector3[] topCorners = new Vector3[CornerCount];
        Vector3[] bottomCorners = new Vector3[CornerCount];

        for (int i = 0; i < CornerCount; i++)
        {
            // 90度を起点に120度ずつ回した位置に頂点を置く
            float angle = (90f + i * 120f) * Mathf.Deg2Rad;
            float x = radius * Mathf.Cos(angle);
            float z = radius * Mathf.Sin(angle);

            topCorners[i] = new Vector3(x, thickness / 2f, z);
            bottomCorners[i] = new Vector3(x, -thickness / 2f, z);
        }

        // 面ごとに法線を変えたい(フラットシェーディング)ため、頂点は面ごとに分離する。
        // 上下のフタが各3頂点、側面3枚が各4頂点の計18頂点
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> triangles = new List<int>();

        // 上のフタ。頂点を 0→2→1 の順に並べると法線が+Y(上向き)になる
        AddTriangle(vertices, normals, triangles, topCorners[0], topCorners[2], topCorners[1]);

        // 下のフタ。頂点を 0→1→2 の順に並べると法線が-Y(下向き)になる
        AddTriangle(vertices, normals, triangles, bottomCorners[0], bottomCorners[1], bottomCorners[2]);

        // 側面3枚。各辺を下→上→隣の上→隣の下の順に並べると法線が外向きになる
        for (int i = 0; i < CornerCount; i++)
        {
            int next = (i + 1) % CornerCount;
            AddQuad(vertices, normals, triangles,
                bottomCorners[i], topCorners[i], topCorners[next], bottomCorners[next]);
        }

        Mesh mesh = new Mesh { name = "Onigiri" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();

        return mesh;
    }

    // 三角形を1枚追加する。法線はUnityの巻き順(Cross(b-a, c-a)が表向き)から求める
    private void AddTriangle(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<int> triangles,
        Vector3 a, Vector3 b, Vector3 c)
    {
        int baseIndex = vertices.Count;
        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);

        for (int i = 0; i < 3; i++)
        {
            normals.Add(normal);
        }

        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 2);
    }

    // 四角形を三角形2枚として追加する
    private void AddQuad(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<int> triangles,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int baseIndex = vertices.Count;
        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);

        for (int i = 0; i < 4; i++)
        {
            normals.Add(normal);
        }

        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 2);

        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 3);
    }
}

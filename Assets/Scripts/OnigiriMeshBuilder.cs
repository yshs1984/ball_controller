using System.Collections.Generic;
using UnityEngine;

// ボールを「おむすび」(角を丸めた三角柱。下半分に海苔の帯)の見た目・当たり判定にする。
// Unityには三角柱の標準プリミティブが無いため、MazeGeneratorの壁生成と同様に
// 実行時にコードでメッシュを組み立てる。
// そのため、Editor上の非再生時のシーンビューでは古いメッシュのまま表示される
[RequireComponent(typeof(MeshFilter), typeof(MeshCollider), typeof(MeshRenderer))]
public class OnigiriMeshBuilder : MonoBehaviour
{
    // 正三角形の外接円半径(おむすびの大きさ)
    [SerializeField] private float radius = 0.6f;

    // Y方向の厚み(おむすびの奥行き)。
    // 薄くすると底面が安定しすぎて転ばず、床を滑るだけの動きになる。
    [SerializeField] private float thickness = 0.8f;

    // 角の丸め半径。正三角形の内角は60度なので、半径radiusの三角形では
    // 辺同士が radius/2 未満でないと隣り合う角の丸めが重なってしまう
    [SerializeField] private float cornerRadius = 0.18f;

    // 角1つあたりの丸めの分割数(多いほど滑らかになる)
    [SerializeField] private int cornerSegments = 6;

    // 海苔(黒)の帯を描画する材質。MeshRendererの1番目の材質(既存のBallMaterial=米)と
    // 組み合わせ、メッシュを2つのサブメッシュ(米/海苔)に分けて描画する
    [SerializeField] private Material noriMaterial;

    private const int CornerCount = 3;

    private void Awake()
    {
        List<Vector3> profile = BuildRoundedTriangleProfile();
        Mesh mesh = BuildPrismMesh(profile);

        GetComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterials = new Material[] { meshRenderer.sharedMaterial, noriMaterial };

        // 非kinematicなRigidbodyと組み合わせるMeshColliderはconvexである必要がある
        // (角を丸めた三角柱も凸形状なのでそのまま設定できる)
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
        meshCollider.convex = true;
    }

    // 正三角形の角を丸めた輪郭線(XZ平面)を作る。
    // 正三角形はどの角も内角60度で合同なので、接点までの距離・フィレット円の中心までの
    // 距離は三角関数から求まる同じ式を3つの角すべてに使い回せる
    private List<Vector3> BuildRoundedTriangleProfile()
    {
        Vector3[] corners = new Vector3[CornerCount];
        for (int i = 0; i < CornerCount; i++)
        {
            float angle = (90f + i * 120f) * Mathf.Deg2Rad;
            corners[i] = new Vector3(radius * Mathf.Cos(angle), 0f, radius * Mathf.Sin(angle));
        }

        // 接点までの距離: t = cornerRadius / tan(内角/2)
        // フィレット円の中心までの距離: d = cornerRadius / sin(内角/2)
        float halfInteriorAngle = 30f * Mathf.Deg2Rad;
        float tangentDistance = cornerRadius / Mathf.Tan(halfInteriorAngle);
        float centerDistance = cornerRadius / Mathf.Sin(halfInteriorAngle);

        List<Vector3> profile = new List<Vector3>();

        for (int i = 0; i < CornerCount; i++)
        {
            Vector3 current = corners[i];
            Vector3 prev = corners[(i + CornerCount - 1) % CornerCount];
            Vector3 next = corners[(i + 1) % CornerCount];

            Vector3 dirToPrev = (prev - current).normalized;
            Vector3 dirToNext = (next - current).normalized;
            Vector3 bisector = (dirToPrev + dirToNext).normalized;

            Vector3 arcCenter = current + bisector * centerDistance;
            Vector3 startSpoke = current + dirToPrev * tangentDistance - arcCenter;
            Vector3 endSpoke = current + dirToNext * tangentDistance - arcCenter;

            // 2つの接点の間(=角そのもの)を丸めの分割数で刻む
            float sweepAngle = Vector3.SignedAngle(startSpoke, endSpoke, Vector3.up);

            for (int s = 0; s <= cornerSegments; s++)
            {
                float t = (float)s / cornerSegments;
                Vector3 spoke = Quaternion.Euler(0f, sweepAngle * t, 0f) * startSpoke;
                profile.Add(arcCenter + spoke);
            }
        }

        return profile;
    }

    // 丸めた三角形の輪郭をY軸方向に押し出して三角柱メッシュを組み立てる。
    // 側面は高さの中間で上下2段に分け、下段を海苔(サブメッシュ1)、
    // 上段と上下のフタを米(サブメッシュ0)にする
    private Mesh BuildPrismMesh(List<Vector3> profile)
    {
        float topY = thickness / 2f;
        float bottomY = -thickness / 2f;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> riceTriangles = new List<int>();
        List<int> noriTriangles = new List<int>();

        // 上のフタ(米)。下のフタとは逆順に頂点を並べて法線を+Yにする
        AddCap(vertices, normals, riceTriangles, profile, topY, Vector3.up, naturalOrder: false);

        // 下のフタ(海苔)。地面に接する面
        AddCap(vertices, normals, noriTriangles, profile, bottomY, Vector3.down, naturalOrder: true);

        // 側面。各辺を下(海苔)→中間→上(米)の2段の四角形として積む
        int count = profile.Count;
        for (int i = 0; i < count; i++)
        {
            Vector3 a = profile[i];
            Vector3 b = profile[(i + 1) % count];

            Vector3 aBottom = new Vector3(a.x, bottomY, a.z);
            Vector3 aMiddle = new Vector3(a.x, 0f, a.z);
            Vector3 aTop = new Vector3(a.x, topY, a.z);
            Vector3 bBottom = new Vector3(b.x, bottomY, b.z);
            Vector3 bMiddle = new Vector3(b.x, 0f, b.z);
            Vector3 bTop = new Vector3(b.x, topY, b.z);

            AddQuad(vertices, normals, noriTriangles, aBottom, aMiddle, bMiddle, bBottom);
            AddQuad(vertices, normals, riceTriangles, aMiddle, aTop, bTop, bMiddle);
        }

        Mesh mesh = new Mesh { name = "Onigiri" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(riceTriangles, 0);
        mesh.SetTriangles(noriTriangles, 1);
        mesh.RecalculateBounds();

        return mesh;
    }

    // フタ(上または下)を中心から輪郭へ扇状に三角形分割して追加する
    private void AddCap(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<int> triangles,
        List<Vector3> profile,
        float y,
        Vector3 normal,
        bool naturalOrder)
    {
        int centerIndex = vertices.Count;
        vertices.Add(new Vector3(0f, y, 0f));
        normals.Add(normal);

        int firstOuterIndex = vertices.Count;
        foreach (Vector3 p in profile)
        {
            vertices.Add(new Vector3(p.x, y, p.z));
            normals.Add(normal);
        }

        int count = profile.Count;
        for (int i = 0; i < count; i++)
        {
            int a = firstOuterIndex + i;
            int b = firstOuterIndex + (i + 1) % count;

            triangles.Add(centerIndex);
            if (naturalOrder)
            {
                triangles.Add(a);
                triangles.Add(b);
            }
            else
            {
                triangles.Add(b);
                triangles.Add(a);
            }
        }
    }

    // 四角形を三角形2枚として追加する。法線はUnityの巻き順(Cross(b-a, c-a)が表向き)から求める
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

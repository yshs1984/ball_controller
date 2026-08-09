using System.Collections.Generic;
using UnityEngine;

// ボールを「おむすび」(角を丸めた三角柱に、海苔の帯を巻いたもの)の見た目・当たり判定にする。
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

    // 角の丸め半径。正三角形の内角は60度なので、辺の長さに対して大きくしすぎると
    // 隣り合う角の丸めどうしが重なってしまう
    [SerializeField] private float cornerRadius = 0.18f;

    // 角1つあたりの丸めの分割数(多いほど滑らかになる)
    [SerializeField] private int cornerSegments = 6;

    // 海苔(黒)の帯を描画する材質。MeshRendererの1番目の材質(既存のBallMaterial=米)と
    // 組み合わせ、メッシュを2つのサブメッシュ(米/海苔)に分けて描画する
    [SerializeField] private Material noriMaterial;

    // 海苔の帯が覆う範囲。おむすびのZ方向の広がり(底辺=0、頂点=1)に対する割合で指定する。
    // 帯はZ軸に垂直な2枚の平面で切り出すため、三角形の断面ではなく
    // 「三角柱に四角柱を埋め込んだ」ような直線的な境界になる
    [SerializeField, Range(0f, 1f)] private float noriStartRatio = 0f;
    [SerializeField, Range(0f, 1f)] private float noriEndRatio = 0.4f;

    private const int CornerCount = 3;

    // 頂点が切断面のちょうど上に乗っているかを判定する許容誤差
    private const float SplitEpsilon = 1e-5f;

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
    // 海苔の帯はZ軸に垂直な2枚の平面で切り出し、厚み方向(Y)には全体を貫く。
    // これにより海苔は三角形の断面をなぞる形ではなく、直線的な境界を持つ板になる
    private Mesh BuildPrismMesh(List<Vector3> profile)
    {
        float topY = thickness / 2f;
        float bottomY = -thickness / 2f;

        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        foreach (Vector3 p in profile)
        {
            minZ = Mathf.Min(minZ, p.z);
            maxZ = Mathf.Max(maxZ, p.z);
        }

        float noriMinZ = Mathf.Lerp(minZ, maxZ, Mathf.Min(noriStartRatio, noriEndRatio));
        float noriMaxZ = Mathf.Lerp(minZ, maxZ, Mathf.Max(noriStartRatio, noriEndRatio));

        // 切断面と交わる位置に頂点を挿し込み、輪郭のどの辺も1つの領域に収まるようにする
        List<Vector3> outline = InsertSplitPoints(profile, noriMinZ);
        outline = InsertSplitPoints(outline, noriMaxZ);

        // 凸多角形をZ方向の2平面で切ると、3つの領域はいずれも凸多角形になる。
        // 巡回順を保ったまま抽出すれば、そのまま扇状に三角形分割できる
        List<Vector3> riceNear = SelectRange(outline, minZ - 1f, noriMinZ);
        List<Vector3> nori = SelectRange(outline, noriMinZ, noriMaxZ);
        List<Vector3> riceFar = SelectRange(outline, noriMaxZ, maxZ + 1f);

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> riceTriangles = new List<int>();
        List<int> noriTriangles = new List<int>();

        // 上下のフタ。下フタは輪郭の並び順そのままで法線が-Yになるので、上フタは並びを反転させる
        AddCapPolygon(vertices, normals, riceTriangles, riceNear, topY, Vector3.up, reverse: true);
        AddCapPolygon(vertices, normals, riceTriangles, riceFar, topY, Vector3.up, reverse: true);
        AddCapPolygon(vertices, normals, noriTriangles, nori, topY, Vector3.up, reverse: true);

        AddCapPolygon(vertices, normals, riceTriangles, riceNear, bottomY, Vector3.down, reverse: false);
        AddCapPolygon(vertices, normals, riceTriangles, riceFar, bottomY, Vector3.down, reverse: false);
        AddCapPolygon(vertices, normals, noriTriangles, nori, bottomY, Vector3.down, reverse: false);

        // 側面。切断点を挿し込んであるので、各辺は米か海苔のどちらか一方に属する
        int count = outline.Count;
        for (int i = 0; i < count; i++)
        {
            Vector3 a = outline[i];
            Vector3 b = outline[(i + 1) % count];

            float midZ = (a.z + b.z) / 2f;
            List<int> target = (midZ >= noriMinZ && midZ <= noriMaxZ) ? noriTriangles : riceTriangles;

            AddQuad(vertices, normals, target,
                new Vector3(a.x, bottomY, a.z),
                new Vector3(a.x, topY, a.z),
                new Vector3(b.x, topY, b.z),
                new Vector3(b.x, bottomY, b.z));
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

    // 輪郭がZ=splitZの平面をまたぐ辺に、交点の頂点を挿し込む
    private List<Vector3> InsertSplitPoints(List<Vector3> outline, float splitZ)
    {
        List<Vector3> result = new List<Vector3>();
        int count = outline.Count;

        for (int i = 0; i < count; i++)
        {
            Vector3 a = outline[i];
            Vector3 b = outline[(i + 1) % count];
            result.Add(a);

            bool aBelow = a.z < splitZ - SplitEpsilon;
            bool bBelow = b.z < splitZ - SplitEpsilon;
            bool aAbove = a.z > splitZ + SplitEpsilon;
            bool bAbove = b.z > splitZ + SplitEpsilon;

            if ((aBelow && bAbove) || (aAbove && bBelow))
            {
                float t = (splitZ - a.z) / (b.z - a.z);
                result.Add(Vector3.Lerp(a, b, t));
            }
        }

        return result;
    }

    // Zが指定範囲に収まる頂点だけを、元の巡回順を保ったまま取り出す。
    // 凸多角形をZ方向の帯で切り取った領域は凸なので、この並びのまま扇状分割してよい
    private List<Vector3> SelectRange(List<Vector3> outline, float fromZ, float toZ)
    {
        List<Vector3> result = new List<Vector3>();

        foreach (Vector3 p in outline)
        {
            if (p.z >= fromZ - SplitEpsilon && p.z <= toZ + SplitEpsilon)
            {
                result.Add(p);
            }
        }

        return result;
    }

    // フタ(上または下)を、先頭の頂点から扇状に三角形分割して追加する
    private void AddCapPolygon(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<int> triangles,
        List<Vector3> polygon,
        float y,
        Vector3 normal,
        bool reverse)
    {
        if (polygon.Count < 3)
        {
            return;
        }

        int baseIndex = vertices.Count;

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector3 p = polygon[reverse ? polygon.Count - 1 - i : i];
            vertices.Add(new Vector3(p.x, y, p.z));
            normals.Add(normal);
        }

        for (int i = 1; i < polygon.Count - 1; i++)
        {
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + i);
            triangles.Add(baseIndex + i + 1);
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

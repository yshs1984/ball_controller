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

    // 海苔は「三角柱を貫く四角柱」として切り出す。
    // 底辺側からどこまで覆うかをZ方向の広がり(底辺=0、頂点=1)に対する割合で、
    // 帯の幅をX方向の広がりに対する割合で指定する。
    // 幅を1未満にすることで、上下の面には三角形の輪郭に沿わない長方形として現れる
    [SerializeField, Range(0f, 1f)] private float noriStartRatio = 0f;
    [SerializeField, Range(0f, 1f)] private float noriEndRatio = 0.45f;
    [SerializeField, Range(0f, 1f)] private float noriWidthRatio = 0.45f;

    private const int CornerCount = 3;

    // 頂点が切断面のちょうど上に乗っているかを判定する許容誤差
    private const float SplitEpsilon = 1e-5f;

    // 3点が一直線に並んでいるとみなす閾値(外積の大きさの2乗で比較する)。
    // 実際の角は外積の2乗が1e-6以上あるため、正しい角を削ってしまう心配はない
    private const float CollinearEpsilonSqr = 1e-12f;

    private enum Axis
    {
        X,
        Z
    }

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
    // 海苔はX・Zの両方向に区切った「四角柱」で切り出し、厚み方向(Y)には全体を貫く
    private Mesh BuildPrismMesh(List<Vector3> profile)
    {
        float topY = thickness / 2f;
        float bottomY = -thickness / 2f;

        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        float maxAbsX = 0f;
        foreach (Vector3 p in profile)
        {
            minZ = Mathf.Min(minZ, p.z);
            maxZ = Mathf.Max(maxZ, p.z);
            maxAbsX = Mathf.Max(maxAbsX, Mathf.Abs(p.x));
        }

        float noriMinZ = Mathf.Lerp(minZ, maxZ, Mathf.Min(noriStartRatio, noriEndRatio));
        float noriMaxZ = Mathf.Lerp(minZ, maxZ, Mathf.Max(noriStartRatio, noriEndRatio));
        float noriHalfX = maxAbsX * noriWidthRatio;

        // 側面用に、4枚の切断面すべての交点を輪郭へ挿し込んでおく。
        // こうすると各辺は海苔か米のどちらか一方に収まる
        List<Vector3> outline = InsertCrossings(profile, Axis.Z, noriMinZ);
        outline = InsertCrossings(outline, Axis.Z, noriMaxZ);
        outline = InsertCrossings(outline, Axis.X, -noriHalfX);
        outline = InsertCrossings(outline, Axis.X, noriHalfX);

        // フタは切断面ごとに切り分ける。凸多角形を平面で切った断片はどちらも凸なので、
        // 巡回順を保ったまま振り分けるだけで、そのまま扇状に三角形分割できる
        List<Vector3> nearBase, aboveBase, withinBand, beyondBand, leftOfNori, fromLeftEdge, nori, rightOfNori;
        SplitConvex(outline, Axis.Z, noriMinZ, out nearBase, out aboveBase);
        SplitConvex(aboveBase, Axis.Z, noriMaxZ, out withinBand, out beyondBand);
        SplitConvex(withinBand, Axis.X, -noriHalfX, out leftOfNori, out fromLeftEdge);
        SplitConvex(fromLeftEdge, Axis.X, noriHalfX, out nori, out rightOfNori);

        List<List<Vector3>> ricePieces = new List<List<Vector3>>
        {
            nearBase, beyondBand, leftOfNori, rightOfNori
        };

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> riceTriangles = new List<int>();
        List<int> noriTriangles = new List<int>();

        // 上下のフタ。下フタは輪郭の並び順そのままで法線が-Yになるので、上フタは並びを反転させる。
        // 切り分けの端には一直線に並んだ頂点が残るため、先に間引いてから三角形にする
        foreach (List<Vector3> piece in ricePieces)
        {
            List<Vector3> cleaned = RemoveRedundantVertices(piece);
            AddCapPolygon(vertices, normals, riceTriangles, cleaned, topY, Vector3.up, reverse: true);
            AddCapPolygon(vertices, normals, riceTriangles, cleaned, bottomY, Vector3.down, reverse: false);
        }

        List<Vector3> cleanedNori = RemoveRedundantVertices(nori);
        AddCapPolygon(vertices, normals, noriTriangles, cleanedNori, topY, Vector3.up, reverse: true);
        AddCapPolygon(vertices, normals, noriTriangles, cleanedNori, bottomY, Vector3.down, reverse: false);

        // 側面
        int count = outline.Count;
        for (int i = 0; i < count; i++)
        {
            Vector3 a = outline[i];
            Vector3 b = outline[(i + 1) % count];

            // 切断面が既存の頂点とほぼ重なった場合、幅ゼロの側面ができるので飛ばす
            if ((b - a).sqrMagnitude <= SplitEpsilon * SplitEpsilon)
            {
                continue;
            }

            Vector3 mid = (a + b) / 2f;
            bool isNori = mid.z >= noriMinZ - SplitEpsilon
                && mid.z <= noriMaxZ + SplitEpsilon
                && Mathf.Abs(mid.x) <= noriHalfX + SplitEpsilon;

            AddQuad(vertices, normals, isNori ? noriTriangles : riceTriangles,
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

    private static float Coordinate(Vector3 point, Axis axis)
    {
        return axis == Axis.X ? point.x : point.z;
    }

    // 輪郭が切断面をまたぐ辺に、交点の頂点を挿し込む
    private List<Vector3> InsertCrossings(List<Vector3> outline, Axis axis, float value)
    {
        List<Vector3> result = new List<Vector3>();
        int count = outline.Count;

        for (int i = 0; i < count; i++)
        {
            Vector3 a = outline[i];
            Vector3 b = outline[(i + 1) % count];
            result.Add(a);

            float coordA = Coordinate(a, axis);
            float coordB = Coordinate(b, axis);

            bool aBelow = coordA < value - SplitEpsilon;
            bool bBelow = coordB < value - SplitEpsilon;
            bool aAbove = coordA > value + SplitEpsilon;
            bool bAbove = coordB > value + SplitEpsilon;

            if ((aBelow && bAbove) || (aAbove && bBelow))
            {
                result.Add(Vector3.Lerp(a, b, (value - coordA) / (coordB - coordA)));
            }
        }

        return result;
    }

    // 凸多角形を軸に垂直な平面で2つに切る。
    // 切り口の頂点を挿し込んでから座標で振り分けるだけでよく、
    // 巡回順が保たれるため断片はそのまま凸多角形として扱える
    private void SplitConvex(
        List<Vector3> polygon,
        Axis axis,
        float value,
        out List<Vector3> below,
        out List<Vector3> above)
    {
        List<Vector3> withCrossings = InsertCrossings(polygon, axis, value);

        below = new List<Vector3>();
        above = new List<Vector3>();

        foreach (Vector3 p in withCrossings)
        {
            float coord = Coordinate(p, axis);
            if (coord <= value + SplitEpsilon)
            {
                below.Add(p);
            }
            if (coord >= value - SplitEpsilon)
            {
                above.Add(p);
            }
        }
    }

    // 重複した頂点と、一直線に並んでいて角になっていない頂点を取り除く。
    // 切り分けた断片の端にはこうした頂点が残り、そのまま扇状分割すると
    // 面積ゼロの三角形がメッシュに紛れ込んでしまう
    private List<Vector3> RemoveRedundantVertices(List<Vector3> polygon)
    {
        List<Vector3> unique = new List<Vector3>();

        foreach (Vector3 p in polygon)
        {
            if (unique.Count == 0 || (p - unique[unique.Count - 1]).sqrMagnitude > SplitEpsilon * SplitEpsilon)
            {
                unique.Add(p);
            }
        }

        // 先頭と末尾が重なっている場合も1つにまとめる
        if (unique.Count > 1 && (unique[0] - unique[unique.Count - 1]).sqrMagnitude <= SplitEpsilon * SplitEpsilon)
        {
            unique.RemoveAt(unique.Count - 1);
        }

        List<Vector3> result = new List<Vector3>();
        int count = unique.Count;

        for (int i = 0; i < count; i++)
        {
            Vector3 prev = unique[(i + count - 1) % count];
            Vector3 current = unique[i];
            Vector3 next = unique[(i + 1) % count];

            if (Vector3.Cross(current - prev, next - prev).sqrMagnitude > CollinearEpsilonSqr)
            {
                result.Add(current);
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

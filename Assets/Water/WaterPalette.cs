using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
Refrence
海洋学统计模型
https://www.keithlantz.net/2011/10/ocean-simulation-part-one-using-the-discrete-fourier-transform/
https://zhuanlan.zhihu.com/p/31670275
http://www.cnblogs.com/wubugui/p/4541812.html?utm_source=tuicool&utm_medium=referral
https://blog.csdn.net/toughbro/article/details/5687590
http://www.ceeger.com/forum/read.php?tid=24976&fid=2

FT/FFT
https://www.bilibili.com/video/av25840793/?p=24&t=1173
https://zhuanlan.zhihu.com/p/31584464

其他
https://zhuanlan.zhihu.com/p/21573239
https://developer.nvidia.com/gpugems/GPUGems/gpugems_ch01.html
*/

/*
k: Vector
K: magnitude
*/

/*
Coordinate, Domain and range
k:(kx, kz) = (2πn / Lx, 2πm / Lz)
n,m ∈ (-N / 2, N / 2)
Lx, Lz = N
kx, kz ∈ (-πN / L, πN / L)
kx, kz ∈ (-π, π)

k:(kx, kz) = (2π * (2 * resolution - n) / resolution, 2π * (2 * resolution - m) / resolution)
n,m ∈ (0, resolution)
kx, kz ∈ (-π, π)
*/

/*
Complex(Vector2)
exp(ix) = cosx + i sinx => (cosx, sinx)

multiply
A * B = (Ax * Bx - Ay * By, Ax * By + Ay * Bx)
*/
public class WaterPalette : MonoBehaviour
{
    public Material mat;
    public int resolution;
    public float unitWidth;
    public float length;
    public float amplitude;
    public Vector2 wind;

    // play的时候上面的值不能更改

    public float choppiness;
    public bool play;
    public float time;

    private MeshFilter filter;
    private Mesh mesh;

    private Vector3[] vertices;
    private Vector3[] normals;
    private Vector2[] uvs;
    private int[] indices;

    // htilde0
    private Vector2[] verttilde;
    // htilde0 conjugate
    private Vector2[] vertConj;
    // temp
    private Vector3[] newVertices;

    private const float G = 9.81f;
    private const float EPSILON = 0.0001f;

    void Start()
    {
        GameObject water = new GameObject("Water");

        filter = water.AddComponent<MeshFilter>();
        mesh = new Mesh();
        filter.mesh = mesh;

        water.AddComponent<MeshRenderer>().material = mat;

        SetParams();
        GenerateMesh();
    }

    void SetParams()
    {
        vertices = new Vector3[resolution * resolution];
        indices = new int[(resolution - 1) * (resolution - 1) * 6];
        normals = new Vector3[resolution * resolution];
        uvs = new Vector2[resolution * resolution];

        vertConj = new Vector2[resolution * resolution];
        verttilde = new Vector2[resolution * resolution];
        newVertices = new Vector3[resolution * resolution];
    }

    void GenerateMesh()
    {
        Random.InitState(0);

        int indiceCount = 0;
        float halfResolution = resolution / 2f;

        for (int i = 0; i < resolution; i++)
        {
            float horizontalPosition = (i - halfResolution) * unitWidth;

            for (int j = 0; j < resolution; j++)
            {
                int currentIdx = i * (resolution) + j;
                float verticalPosition = (j - halfResolution) * unitWidth;
                vertices[currentIdx] = new Vector3(horizontalPosition + unitWidth / 2f, 0f, verticalPosition + unitWidth / 2f);
                normals[currentIdx] = new Vector3(0f, 1f, 0f);
                uvs[currentIdx] = new Vector2(i * 1.0f / (resolution - 1), j * 1.0f / (resolution - 1));

                if (i != resolution - 1 && j != resolution - 1)
                {
                    indices[indiceCount++] = currentIdx;
                    indices[indiceCount++] = currentIdx + 1;
                    indices[indiceCount++] = currentIdx + resolution + 1;

                    indices[indiceCount++] = currentIdx;
                    indices[indiceCount++] = currentIdx + resolution + 1;
                    indices[indiceCount++] = currentIdx + resolution;
                }

                verttilde[currentIdx] = htilde0(i * 2 - resolution, j * 2 - resolution);
                Vector2 temp = htilde0(resolution - 2 * i, resolution - 2 * j);
                vertConj[currentIdx] = new Vector2(temp.x, -temp.y);
            }
        }

        mesh.vertices = vertices;
        mesh.SetIndices(indices, MeshTopology.Triangles, 0);
        mesh.normals = normals;
        mesh.uv = uvs;
        filter.mesh = mesh;
    }

    void Update()
    {
        if (play)
        {
            time += Time.deltaTime;
            EvaluateWaves();
        }
    }

    // h(k, t) = h0 * exp{i * w(k) * t} + h0mk_Conj * exp{-i * w(k) * t}
    // 返回值是一个复数(参考Gerstner Wave,猜测x是高度(sinθ),y是位移量(cosθ))
    private Vector2 htilde(int index, Vector2 k)
    {
        Vector2 h0 = verttilde[index];
        Vector2 h0mk_Conj = vertConj[index];
        // w(k) * t
        float omegat = Dispersion(k) * time;
        // exp{i * omegat} = (cos(omegat), sin(omegat))
        // exp{i * omegat} = (cos(omegat), -sin(omegat))
        float _cos = Mathf.Cos(omegat);
        float _sin = Mathf.Sin(omegat);
        Vector2 c0 = new Vector2(_cos, _sin);
        Vector2 c1 = new Vector2(_cos, -_sin);
        // res = htilde0 * c0 + htilde0mkconj * c1
        // htilde0 * c0 = (h0.x * c0.x - h0.y * c0.y, h0.x * c0.y + h0.y * c0.x)
        // htilde0mkconj * c1 = (h0conj.x * c1.x - h0conj.y * c1.y, h0conj.x * c1.y + h0conj.y * c1.x)
        Vector2 res = new Vector2(h0.x * c0.x - h0.y * c0.y + h0mk_Conj.x * c1.x - h0mk_Conj.y * c1.y, h0.x * c0.y + h0.y * c0.x + h0mk_Conj.x * c1.y + h0mk_Conj.y * c1.x);
        return res;
    }

    /*
    Dispersion Relation 色散关系
    水的频率与水的几个特性的关系

    w^2 = gk 描述的是拥有无限深度且不可压缩的流体表面的波形(大概)

    w^2 = gk * tanh(kD) d是水深,在浅水区,频率和这些参数有关,越浅w越小

    w^2 = gk * (1 + k^2 * L^2) 对于很小的水波,需要考虑到水的张力(tenssor),也就是L

    这里用的是第一种 w = ±√gk, 取正值 w = √gk
    */
    private float Dispersion(Vector2 k)
    {
        float w = 2 * Mathf.PI / length;

        /*
        // k:(kx, kz) = (2PI * n / Lx, 2PI * n / Lz)
        // kx, kz ∈ (-PI * N / L, PI * N / L)
        float kx = Mathf.PI * (2 * n - resolution) / length;
        float kz = Mathf.PI * (2 * m - resolution) / length;

        // |k| = Mathf.Sqrt(kx * kx + kz * kz)
        // √gk  = Mathf.Sqrt(G * Mathf.Sqrt(kx * kx + kz * kz))
        */

        // Floor(w' / w) * w 是为了取正整数倍的w
        // 应该是为了正确的离散傅里叶变换
        return Mathf.Floor(Mathf.Sqrt(G * k.magnitude) / w) * w;
    }

    /*
    h0 = 1 / √2  * (ξ1 + i * ξ2) * √P(k)
    小优化 h0 = (ξ1 + i* ξ2) * √(P(k) / 2)

    原公式 h0mk_Conj = htilde0(-n, -m).Conj()
    n, m ∈(-N / 2, N / 2)
    这里用 h0mk_Conj = htilde0(N - n, N - m).Conj()
    n, m ∈(0, N)
    Phillips(N - n, N - m) 等价于 Phillips(-n, -m)
    注: 现在用的原公式
    */
    private Vector2 htilde0(int n, int m)
    {
        // r : Circular gaussian distribution
        // 要求平均值是0，方差小于1的分布
        Vector2 r = new Vector2();
        float u = Random.value;
        float v = Random.value;
        r.x = Mathf.Sqrt(-2f * Mathf.Log(u)) * Mathf.Cos(2 * Mathf.PI * v);
        r.y = Mathf.Sqrt(-2f * Mathf.Log(u)) * Mathf.Sin(2 * Mathf.PI * v);

        return r * Mathf.Sqrt(Phillips(n, m) / 2f);
    }

    /* 
    Phillips spectrum Phillips频谱
    Phillips(k) = A ( exp(-1/((K * L)^2)) / K^4 ) * |dot(k, w)|^2

    这个经验公式在波长比较小的时候,即波数很大的时候收敛性很差
    因此当波长远远小于L的时候,可以将该公式乘一个修正因子来修正

    Phillips(k) = A ( exp(-1/((K * L)^2)) / K^4 ) * |dot(k, w)|^2 * exp(-k^2 * l^2)

    L = V^2 / g 是持续维v的风在海面上产生最大可能性的海浪的波长
    w 是风力的方向

    下面我还没懂?????????????????????????????????
    |dot(k, w)|^2 消除波在风向的垂直方向的移动
    为了提高频谱的收敛性,通过乘来取消长度w<<l的波
    */
    private float Phillips(int n, int m)
    {
        // k:(kx, kz) = (2π* n / Lx, 2π* n / Lz)
        Vector2 k = new Vector2(n * Mathf.PI / length, m * Mathf.PI / length);
        float k_length2 = k.sqrMagnitude;
        if (k_length2 < EPSILON * EPSILON)
            return 0.0f;
        float k_length4 = k_length2 * k_length2;

        float kDotW = Vector2.Dot(k.normalized, wind.normalized);
        float kDotW2 = kDotW * kDotW;
        float w_length = wind.magnitude;
        float L = w_length * w_length / G;
        float L2 = L * L;

        // l是修正系数,这里damping = 0.001
        float damping = 0.001f;
        // l^2要乘以两次damping
        float l2 = L2 * damping * damping;

        return amplitude * Mathf.Exp(-1f / (k_length2 * L2)) / k_length4 * kDotW2 * Mathf.Exp(-k_length2 * l2);
    }

    private void EvaluateWaves()
    {
        Vector2[] hds = new Vector2[resolution * resolution];

        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                int index = i * resolution + j;
                newVertices[index] = vertices[index];
            }
        }

        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                int index = i * resolution + j;
                Vector3 nor = new Vector3(0f, 0f, 0f);
                Vector3 hd = Displacement(new Vector2(newVertices[index].x, newVertices[index].z), out nor);
                newVertices[index].y = hd.y;
                newVertices[index].z = vertices[index].z - hd.z * choppiness;
                newVertices[index].x = vertices[index].x - hd.x * choppiness;
                normals[index] = nor;
                hds[index] = new Vector2(hd.x, hd.z);
            }
        }
        
        mesh.vertices = newVertices;
        mesh.normals = normals;

        /*
        雅可比行列式
        https://pic4.zhimg.com/80/v2-2cab501515edca3248af7c3f64251361_hd.jpg
        x = x + choppiness * D(x, t)
        D(x, t) = ∑ -i * k.normalized * htilde(k,t) * exp(i * k·x))
        choppiness是控制水平位移的常数
        choppiness * D(x, t) = 0时,雅可比行列式的值为1
        J(x) = JxxJyy - JxyJyx
        Jxx(x) = 1 + choppiness(Dx(x + 1) - Dx(x))
        Jyy(x) = 1 + choppiness(Dy(x + 1) - Dy(x))
        Jxy(x) = choppiness(Dxy(x + 1) - Dxy(x))
        Jyx(x) = choppiness(Dxy(x + 1) - Dyy(x)) = Jxy(x)
        */

        Color[] colors = new Color[resolution * resolution];

        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                int index = i * resolution + j;
                Vector2 dDdx = Vector2.zero;
                Vector2 dDdy = Vector2.zero;
                if (i != resolution - 1)
                {
                    dDdx = choppiness * (hds[index] - hds[index + resolution]);
                }
                if (j != resolution - 1)
                {
                    dDdy = choppiness * (hds[index] - hds[index + 1]);
                }
                float jacobian = (1 + dDdx.x) * (1 + dDdy.y) - dDdx.y * dDdy.x;

                // 下面是一些优化,不知道为什么?????????????????????????????????
                Vector2 noise = new Vector2(Mathf.Abs(normals[index].x), Mathf.Abs(normals[index].z)) * 0.3f;
                float turb = Mathf.Max(1f - jacobian + noise.magnitude, 0f);
                float xx = 1f + 3f * Mathf.SmoothStep(1.2f, 1.8f, turb);
                xx = Mathf.Min(turb, 1.0f);
                xx = Mathf.SmoothStep(0f, 1f, turb);
                colors[index] = new Color(xx, xx, xx, xx);
            }
        }

        mesh.colors = colors;
    }

    /*
    Displacement vector

    (猜测)参考Gerstner Wave,h(x, t)的值是一个复数(Vecter2),x是高度(sinθ),y是位移量(cosθ)

    FT 傅里叶变换
    下面公式第一个 * 实际代表复数乘法
    h(x, t) = ∑ htilde(k,t) * exp(i * k·x))
    h(x, t) = ∑ htilde(k,t) * exp(i * Dot(k, x))
    exp(i * Dot(k, x)) = (cos(Dot(k, x), i * sin(Dot(k, x))
    k∈(2πn / Lx, 2πn / Lz)
    x = (postion.x, postion.z)

    h:
    h(x, t).x
    x,z: 
    参考Gerstner Wave,对xz进行位移
    ∑ -i * k.normalized * htilde(k,t) * exp(i * k·x))
    - i * i = 1
    用参数choppiness来控制偏移程度
    */

    /*
    normal
    应该是up(0, 1, 0) - 切线向量
    切线向量用求导来求出
    h'(x, t) = ∑ i * k * htilde(k,t) * exp(i * k·x))
    i * i = -1
    */
    private Vector3 Displacement(Vector2 x, out Vector3 nor)
    {
        float height = 0;
        Vector2 displacement = new Vector2(0f, 0f);
        Vector3 n = Vector3.zero;
        Vector2 c, htilde_C, k;
        float kx, kz, k_length, kDotX;

        for (int i = 0; i < resolution; i++)
        {
            kx = 2 * Mathf.PI * (i - resolution / 2.0f) / length;

            for (int j = 0; j < resolution; j++)
            {
                kz = 2 * Mathf.PI * (j - resolution / 2.0f) / length;
                k = new Vector2(kx, kz);

                // htilde(k,t) * exp(i * k·x))
                k_length = k.magnitude;
                if (k_length < EPSILON)
                    continue;

                kDotX = Vector2.Dot(k, x);
                c = new Vector2(Mathf.Cos(kDotX), Mathf.Sin(kDotX));
                Vector2 temp = htilde(i * resolution + j, k);
                htilde_C = new Vector2(temp.x * c.x - temp.y * c.y, temp.x * c.y + temp.y * c.x);

                height += htilde_C.x;

                // i * k * htilde_C
                // 为什么是和复部相乘?????????????????????????????????
                n += new Vector3(-kx * htilde_C.y, 0f, -kz * htilde_C.y);

                // -i * k.direction * htilde_C
                // 原代码有负号,应该是他写错了,修改后效果明显是对的
                //displacement += new Vector2(kx / k_length * htilde_C.y, -kz / k_length * htilde_C.y);
                displacement += new Vector2(kx / k_length * htilde_C.y, kz / k_length * htilde_C.y);
            }
        }

        // (0, 1, 0) - ϵ(x, 0, z)
        // 减出来的向量不是单位向量
        // 但我理解中有的时候应该是加n?????????????????????????????????
        nor = Vector3.Normalize(Vector3.up - n);
        
        return new Vector3(displacement.x, height, displacement.y);
    }
}
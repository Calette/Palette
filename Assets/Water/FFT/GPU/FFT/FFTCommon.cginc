#include "UnityCG.cginc"

#define tenssor2 0.0000073046
#define PI 3.1415926536
#define EPSILON2 0.0000001
#define G 9.81f

struct FFTVertexInput
{
	float4 vertex : POSITION;
	float4 texcoord : TEXCOORD0;
};

struct FFTVertexOutput
{
	float4 pos : SV_POSITION;
	float4 uv : TEXCOORD0;
	float4 screenPos : TEXCOORD1;
};

FFTVertexOutput vert_quad(FFTVertexInput v)
{
	FFTVertexOutput o;

	o.pos = UnityObjectToClipPos(v.vertex);
	o.uv = v.texcoord;
	o.screenPos = ComputeScreenPos(o.pos);

	return o;
}

// 随机数
inline float UVRandom(float2 uv, float salt, float random)
{
	uv += float2(salt, random);
	return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
}

// 乘法
inline float2 MultComplex(float2 a, float2 b)
{
	return float2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x);
}

// 乘以i
inline float2 MultByI(float2 a)
{
	return float2(-a.y, a.x);
}

// 共轭
inline float2 Conj(float2 a)
{
	return float2(a.x, -a.y);
}

/*
返回k
n,m ∈ (0, Resolution)

*/
inline float2 GetWave(float n, float m, float len, float res)
{
	//return PI * float2(2 * n - res, 2 * m - res) / len;

	n -= 0.5;
	m -= 0.5;
	n = ((n < res * 0.5) ? n : n - res);
	m = ((m < res * 0.5) ? m : m - res);
	return 2 * PI * float2(n, m) / len;
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
inline float Phillips(float n, float m, float amp, float2 wind, float res, float len)
{
	float2 k = GetWave(n, m, len, res);
	float klen2 = k.x * k.x + k.y * k.y;
	float klen4 = klen2 * klen2;
	if(klen2 < EPSILON2)
		return 0;

	float kDotW = dot(normalize(k), normalize(wind));
	float kDotW2 = kDotW * kDotW;
	float L = (wind.x * wind.x + wind.y * wind.y) / G;
	float L2 = L * L;

	// l是修正系数,这里damping = 0.001
	float damping = 0.001;
	float l2 = L2 * damping * damping;
	return amp * exp(-1 / (klen2 * L2)) / klen4 * kDotW2 * exp(-klen2 * l2);
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

/*
r1,r2都是随机数种子
*/
inline float2 hTilde0(float2 uv, float r1, float r2, float phi)
{
	// r : Circular gaussian distribution
	// 要求平均值是0，方差小于1的分布
	float2 r;

	// 获取随机数
	float rand1 = UVRandom(uv, 10.612, r1);
	float rand2 = UVRandom(uv, 11.899, r2);

	// log(0) = -∞
	rand1 = clamp(rand1, 0.001, 1);
	rand2 = clamp(rand2, 0.001, 1);

	float x = sqrt(-2 * log(rand1));
	float y = 2 * PI * rand2;
	r.x = x * cos(y);
	r.y = x * sin(y);

	return r * sqrt(phi / 2); 
}

/*
Dispersion Relation 色散关系
水的频率与水的几个特性的关系

w^2 = gk 描述的是拥有无限深度且不可压缩的流体表面的波形(大概)

w^2 = gk * tanh(kD) d是水深,在浅水区,频率和这些参数有关,越浅w越小

w^2 = gk * (1 + k^2 * L^2) 对于很小的水波,需要考虑到水的张力(tenssor),也就是L

这里用的是第一种 w = ±√gk, 取正值 w = √gk
*/

/*
这里用了第三种 w^2 = gk * (1 + k^2 * L^2)
L = 370
*/
inline float CalcDispersion(float n, float m, float len, float dt, float res)
{
	float2 wave = GetWave(n, m, len, res);
	float w = 2 * PI / len;
	float wlen2 = wave.x * wave.x + wave.y * wave.y;

	//return sqrt(G * length(wave) * (1 + wlen * wlen / 370 / 370)) * dt;
	return sqrt(G * length(wave) * (1 + wlen2 * tenssor2)) * dt;

	// return floor(sqrt(G * length(wave) / w)) * w * dt;
}
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Diagnostics;
using System.Text;

namespace RayTracing
{
    #region 辅助类

    public class CubeMap
    {
        private static CubeMap skybox;

        public static CubeMap Skybox
        {
            get
            {
                if (skybox == null)
                    skybox = new CubeMap(@"D:\Unity\Palette\Assets\Wispy Sky\Textures\SkyBox020001.tif",
                        @"D:\Unity\Palette\Assets\Wispy Sky\Textures\SkyBox020005.tif",
                        @"D:\Unity\Palette\Assets\Wispy Sky\Textures\SkyBox020004.tif",
                        @"D:\Unity\Palette\Assets\Wispy Sky\Textures\SkyBox020003.tif",
                        @"D:\Unity\Palette\Assets\Wispy Sky\Textures\SkyBox020002.tif",
                        @"D:\Unity\Palette\Assets\Wispy Sky\Textures\SkyBox020006.tif"
                        );
                return skybox;
            }

            set
            {
                skybox = value;
            }
        }

        private readonly ImageTexture[] buffer = new ImageTexture[6];

        public CubeMap(params string[] files)
        {
            for (int i = 0; i < 6; i++) buffer[i] = new ImageTexture(files[i]);
        }

        public Color value(Vector3 dir)
        {
            int index = GetFace(dir);
            Vector2 uv = GetUV(index, dir);
            return buffer[index].value(uv.x, uv.y, Vector3.zero);
        }

        private Vector2 GetUV(int index, Vector3 dir)
        {
            float u = 0, v = 0, factor;
            switch (index)
            {
                case 0: //前
                    factor = 1 / dir[0];
                    u = 1 + dir[2] * factor;
                    v = 1 + dir[1] * factor;
                    break;
                case 1://上
                    factor = 1 / dir[1];
                    u = 1 + dir[2] * factor;
                    v = 1 + dir[0] * factor;
                    v = 2 - v;
                    break;
                case 2://右
                    factor = 1 / dir[2];
                    u = 1 + dir[0] * factor;
                    u = 2 - u;
                    v = 1 + dir[1] * factor;
                    break;
                case 3: //后
                    factor = 1f / dir.x;
                    u = 1 + dir[2] * factor;
                    v = 1 + dir[1] * factor;
                    v = 2 - v;
                    break;
                case 4: //底
                    factor = 1 / dir.y;
                    u = 1 + dir[2] * factor;
                    u = 2 - u;
                    v = 1 + dir[0] * factor;
                    v = 2 - v;
                    break;
                case 5: //左
                    factor = 1f / dir.z;
                    u = 1 + dir[0] * factor;
                    u = 2 - u;
                    v = 1 + dir[1] * factor;
                    v = 2 - v;
                    break;
            }
            return new Vector2(u / 2, v / 2);
        }

        private static int GetFace(Vector3 dir)
        {
            var MAX = 0;
            for (var i = 0; i < 3; i++) if (Mathf.Abs(dir[i]) > Mathf.Abs(dir[MAX])) MAX = i;
            return MAX + (dir[MAX] < 0 ? 3 : 0);
        }
    }

    public class AABB
    {
        public Vector3 min, max;

        public AABB() { }

        public AABB(Vector3 a, Vector3 b)
        {
            min = a;
            max = b;
        }

        public bool Hit(Ray r, float tmin, float tmax)
        {
            for (int a = 0; a < 3; a++)
            {
                float t0;
                float t1;

                /*
                补充normalDirection[a] = 0时的做法
                =0时表示这个轴的值不会随着t动,只要检测是否在min,max中间就行了
                */
                if (r.normalDirection[a] == 0)
                {
                    if (r.original[a] > min[a] && r.original[a] < max[a])
                        continue;
                    return false;
                }
                else
                {
                    /*
                    tx0 = (x0 - Ax) / Bx
                    tx1 = (x1 - Ax) / Bx
                    为了保证t0<t1,取两个值小的为t0,大的为t1
                    */
                    float tx0 = (min[a] - r.original[a]) / r.normalDirection[a];
                    float tx1 = (max[a] - r.original[a]) / r.normalDirection[a];
                    t0 = tx0 < tx1 ? tx0 : tx1;
                    t1 = tx0 > tx1 ? tx0 : tx1;
                }

                /*
                normalDirection[a]可能是0,这时t0和ti的值是Infinity
                tmin = infinity, tman = tmax, 将返回false
                现在不会出现以上情况

                当x0 - Ax和Bx同时为0时,tx0 = NaN,与tx0的所有比大小都将返回NaN和false
                这种情况下将视为false
                现在也不会出现以上情况
                
                进行3次对比, 只要xyz中t0大于任何一个xyz中t1就表示不在包围盒里
                */
                tmin = Mathf.Max(t0, tmin);
                tmax = Mathf.Min(t1, tmax);

                if (tmax <= tmin)
                    return false;
            }
            return true;
        }
    }

    public class Camera
    {
        public Vector3 position;
        public Vector3 lowLeftCorner;
        public Vector3 horizontal;
        public Vector3 vertical;
        public Vector3 u, v, w;
        public float radius;
        public float time0, time1;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lookFrom"></param>
        /// <param name="lookat"></param>
        /// <param name="vup">这个参数不是说摄像机的v是向上而是让v处在vup和w的平面,基本就是Vector3.up</param>
        /// <param name="vfov"></param>
        /// <param name="aspect"></param>
        /// <param name="r"></param>
        /// <param name="focus_dist">聚焦的距离</param>
        /// <param name="t0"></param>
        /// <param name="t1"></param>
        public Camera(Vector3 lookFrom, Vector3 lookat, Vector3 vup, float vfov, float aspect, float r = 0, float focus_dist = 1, float t0 = 0, float t1 = 0)
        {
            radius = r * 0.5f;
            time0 = t0;
            time1 = t1;
            float unitAngle = Mathf.PI / 180f * vfov;
            float halfHeight = Mathf.Tan(unitAngle * 0.5f);
            float halfWidth = aspect * halfHeight;
            position = lookFrom;
            w = (lookat - lookFrom).normalized;
            u = Vector3.Cross(vup, w).normalized;
            v = Vector3.Cross(w, u).normalized;
            lowLeftCorner = lookFrom + w * focus_dist - halfWidth * u * focus_dist - halfHeight * v * focus_dist;
            horizontal = 2 * halfWidth * focus_dist * u;
            vertical = 2 * halfHeight * focus_dist * v;
        }

        /// <summary>
        /// x,y是焦距平面的坐标
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public Ray CreateRay(float x, float y)
        {
            if (radius == 0f)
                return new Ray(position, lowLeftCorner + x * horizontal + y * vertical - position, time0 + Random.value * (time1 - time0));
            else
            {
                Vector3 rd = radius * Math.GetRandomPointInUnitDisk();
                //射线远点在uv这个平面上偏移
                Vector3 offset = rd.x * u + rd.y * v;
                //微偏移后的位置到那个平面上的点
                return new Ray(position + offset, lowLeftCorner + x * horizontal + y * vertical - position - offset, time0 + Random.value * (time1 - time0));
            }
        }
    }

    public class Ray
    {
        // 原点
        public Vector3 original;
        // 方向
        public Vector3 direction;
        // normalized方向
        public Vector3 normalDirection;
        // 时间
        public float time;

        public Ray(Vector3 o, Vector3 d, float t = 0)
        {
            time = t;
            original = o;
            direction = d;
            normalDirection = d.normalized;
        }

        public Vector3 GetPoint(float t)
        {
            return original + t * normalDirection;
        }
    }

    /// <summary>
    /// hit点的信息
    /// </summary>
    public struct HitRecord
    {
        // 这里t是距离
        public float t;
        public Vector3 postion;
        // normal是单位向量
        public Vector3 normal;
        public Material material;
        // uv
        public float u, v;
    }

    #endregion

    #region 工具类

    public static class Math
    {
        public static Vector3 GetRandomPointInUnitSphere()
        {
            // 2 * (0, 1) - 1 => (-1, 1)
            Vector3 p = 2f * new Vector3(Random.value, Random.value, Random.value) - Vector3.one;
            // 限定p的长度不超过单位圆
            p = p.normalized * Random.value;
            return p;
        }

        /// <summary>
        /// 单位反射方向向量
        /// </summary>
        /// <param name="vin"></param>
        /// <param name="normal"></param>
        /// <returns></returns>
        public static Vector3 Reflect(Vector3 vin, Vector3 normal)
        {
            // vin、normal都是单位向量,dot = cos
            return vin - 2 * Vector3.Dot(vin, normal) * normal;
        }

        // https://zhuanlan.zhihu.com/p/31127076 公式推导
        /// <summary>
        /// 折射
        /// </summary>
        /// <param name="vin"></param>
        /// <param name="normal"></param>
        /// <param name="ni_no"></param>
        /// <param name="refracted"></param>
        /// <returns></returns>
        public static bool Refract(Vector3 vin, Vector3 normal, float ni_no, ref Vector3 refracted)
        {
            // 其实传入的vin就是单位向量不用normalize
            // Vector3 uvin = vin.normalized;
            Vector3 uvin = vin;
            float dt = Vector3.Dot(uvin, normal);
            float discrimination = 1 - ni_no * ni_no * (1 - dt * dt);
            // discrimination的值为负数时
            // 表示ni_no > 1(比如由玻璃(n = 1.5)进入空气(n = 1))入射向量与平面接近平行(dt值太小 => 1 - dt * dt 的值太大)
            if (discrimination > 0)
            {
                refracted = ni_no * (uvin - normal * dt) - normal * Mathf.Sqrt(discrimination);
                return true;
            }
            return false;
        }

        // https://zhuanlan.zhihu.com/p/31534769 公式推导
        /// <summary>
        /// Schlick近似,是菲涅耳方程的近似,精确公式见公式推导
        /// 比如玻璃材质,反射比在射线与平面垂直时的值是0.04,平行(边缘)时是1
        /// </summary>
        /// <param name="cos"></param>
        /// <param name="ref_idx"></param>
        /// <returns></returns>
        public static float Schlick(float cos, float ref_idx)
        {
            // 相当于 (ni - no) / (ni + no)
            float r0 = (1 - ref_idx) / (1 + ref_idx);
            r0 *= r0;
            return r0 + (1 - r0) * Mathf.Pow((1 - cos), 5);
        }

        /// <summary>
        /// 这个取的是圆而不是球
        /// </summary>
        /// <returns></returns>
        public static Vector3 GetRandomPointInUnitDisk()
        {
            Vector3 p = 2f * new Vector3(Random.value, Random.value, 0) - new Vector3(1, 1, 0);
            p = p.normalized * Random.value;
            return p;
        }
    }

    // 参考：https://zhuanlan.zhihu.com/p/20197323
    public static class LowDiscrepancySequence
    {
        public static int seed = 0;

        public static float value
        {
            get
            {
                return (RadicalInverse(2, seed++));
            }
        }

        //Vector3: return Vector3(RadicalInverse(2, seed), RadicalInverse(3, seed), RadicalInverse(4, seed++)); Halton序列/或者用Hammersley

        public static float RadicalInverse(int Base, int i)
        {
            float Digit, Radical, Inverse;
            Digit = Radical = 1.0f / Base;
            Inverse = 0.0f;
            while (i != 0)
            {
                // 求余相当于算出i在"Base"进制下的最低位的数
                // 除以Base相当于移除i在"Base"进制下的最低位的数
                // Digit是小数点右边"Base"进制下每位数的单位
                // 如Base = 2时,Digit代表1/2,1/4,1/8...
                Inverse += Digit * (i % Base);
                i /= Base;

                Digit *= Radical;
            }
            return Inverse;
        }
    }

    #endregion

    #region SDF类

    public abstract class Hitable
    {
        public abstract bool Hit(Ray ray, float t_min, float t_max, ref HitRecord rec);

        // Hitable为什么不能有个AABB属性来存包围盒而要每次算是因为这里有time变量
        public abstract bool BoundingBox(float t0, float t1,ref AABB box);

        // 包含box0和box1的包围盒
        public AABB GetBox(AABB box0, AABB box1)
        {
            var small = new Vector3(
                Mathf.Min(box0.min.x, box1.min.x),
                Mathf.Min(box0.min.y, box1.min.y),
                Mathf.Min(box0.min.z, box1.min.z));
            var big = new Vector3(
                 Mathf.Max(box0.max.x, box1.max.x),
                 Mathf.Max(box0.max.y, box1.max.y),
                 Mathf.Max(box0.max.z, box1.max.z));
            return new AABB(small, big);
        }
    }

    public class Sphere : Hitable
    {
        public Vector3 center;
        public float radius;
        public Material material;
        // 因为这个球不会动可以存一个包围盒不用每次算
        private readonly AABB box;

        public Sphere(Vector3 cen, float rad, Material mat)
        {
            center = cen;
            radius = rad;
            material = mat;
            box = new AABB(center - new Vector3(radius, radius, radius), center + new Vector3(radius, radius, radius));
        }
        
        /*
        A:original
        B:direction
        C:center
        切点离圆心距离是R
        dot((A + t *B - C), (A + t * B - C)) = R * R
        展开是t的二元一次方程：
        t * t * dot(B, B) + 2 * t * dot(B, A - C) + dot(A - C, A - C) - R * R = 0 
        
        这里direction是normalDirection,单位向量,所以a = dot(B, B) = 1
        */
        public override bool Hit(Ray ray, float t_min, float t_max, ref HitRecord rec)
        {
            /*
            根据t的二元一次方程
            oc = A - C;
            a = dot(B, B);
            b = 2 * dot(B, oc);
            c = dot(oc, oc) - R * R;

            我的Ray中有normalDirection值,它的长度一定是1,所以以下a被省略了
            */
            Vector3 oc = ray.original - center;

            //float a = Vector3.Dot(ray.normalDirection, ray.normalDirection);
            float b = 2f * Vector3.Dot(oc, ray.normalDirection);
            float c = Vector3.Dot(oc, oc) - radius * radius;
            // 实际上是判断这个方程有没有根,如果有2个根就是击中
            //float discriminant = b * b - 4 * a * c;
            float discriminant = b * b - 4 * c;

            if (discriminant > 0)
            {
                //带入并计算出最靠近射线源的点
                //float temp = (-b - Mathf.Sqrt(discriminant)) / a * 0.5f;
                float temp = (-b - Mathf.Sqrt(discriminant)) * 0.5f;

                if (temp < t_max && temp >= t_min)
                {
                    rec.t = temp;
                    rec.postion = ray.GetPoint(rec.t);
                    rec.normal = (rec.postion - center).normalized;
                    rec.material = material;
                    //if (Vector3.Dot(rec.normal, ray.normalDirection) > 0)
                    //    c = 0;
                    // 计算UV
                    GetSphereUV(ref rec);
                    return true;
                }
                // 如果靠近射线源的点的距离不在范围内,就计算远离射线源的点
                //temp = (-b + Mathf.Sqrt(discriminant)) / a * 0.5f;
                temp = (-b + Mathf.Sqrt(discriminant)) * 0.5f;
                if (temp < t_max && temp >= t_min)
                {
                    rec.t = temp;
                    rec.postion = ray.GetPoint(rec.t);
                    rec.normal = (rec.postion - center).normalized;
                    rec.material = material;
                    //if (Vector3.Dot(rec.normal, ray.normalDirection) > 0)
                    //{
                    //    c = temp;
                    //    temp = (-b - Mathf.Sqrt(discriminant)) * 0.5f;
                    //}
                    GetSphereUV(ref rec);
                    return true;
                }
                // 远近点都不在范围内
            }
            return false;
        }

        public override bool BoundingBox(float t0, float t1, ref AABB box)
        {
            //box = new AABB(center - new Vector3(radius, radius, radius), center + new Vector3(radius, radius, radius));
            box = this.box;
            return true;
        }

        void GetSphereUV(ref HitRecord record)
        {
            // Atan2 : 返回的是相对x(这里是record.postion.x)轴的角度,范围(-pi, pi)
            float phi = Mathf.Atan2(record.postion.z - center.z, record.postion.x - center.x);
            
            // 范围(-pi / 2, pi / 2)
            float theta = Mathf.Asin((record.postion.y - center.y) / radius);
            // (-pi, pi) => (0, 1)
            record.u = (phi + Mathf.PI) / (2 * Mathf.PI);
            // (-pi / 2, pi / 2) => ()
            record.v = (theta + Mathf.PI / 2) / Mathf.PI;

            /*
            float x = record.postion.x - center.x;
            float y = record.postion.y - center.y;
            float z = record.postion.z - center.z;

            File.AppendAllText(@"C:\Users\Administrator\Desktop\1.txt",
                    string.Format("record: ({0}, {1}, {2}), x: {3}, y: {4}, z: {5}, r2: {6} \r\n phi: {7}, theta: {8} \r\n", record.postion.x, record.postion.y, record.postion.z, x, y, z, x * x + y * y + z * z, phi / Mathf.PI, theta / Mathf.PI)
                    , Encoding.Default);
            //*/
        }
    }

    public class MovingSphere : Hitable
    {
        public float radius;
        public Material material;
        public Vector3 startPosition, endPosition;
        public float startTime, endTime;

        public MovingSphere(Vector3 startPos, Vector3 endPos, float t0, float t1, float rad, Material mat)
        {
            radius = rad;
            startPosition = startPos;
            endPosition = endPos;
            startTime = t0;
            endTime = t1;
            material = mat;
        }

        public Vector3 Center(float time)
        {
            return startPosition + time * (endPosition - startPosition);
        }

        public override bool Hit(Ray ray, float t_min, float t_max, ref HitRecord rec)
        {
            Vector3 oc = ray.original - Center(ray.time);

            float b = 2f * Vector3.Dot(oc, ray.normalDirection);
            float c = Vector3.Dot(oc, oc) - radius * radius;
            float discriminant = b * b - 4 * c;

            if (discriminant > 0)
            {
                float temp = (-b - Mathf.Sqrt(discriminant)) * 0.5f;

                if (temp < t_max && temp >= t_min)
                {
                    rec.t = temp;
                    rec.postion = ray.GetPoint(rec.t);
                    rec.normal = (rec.postion - Center(ray.time)).normalized;
                    rec.material = material;
                    return true;
                }
                temp = (-b + Mathf.Sqrt(discriminant)) * 0.5f;
                if (temp < t_max && temp >= t_min)
                {
                    rec.t = temp;
                    rec.postion = ray.GetPoint(rec.t);
                    rec.normal = (rec.postion - Center(ray.time)).normalized;
                    rec.material = material;
                    return true;
                }
            }
            return false;
        }

        // 包含t0到t1时间所有包围盒的包围盒(其实只是t0和t1两个时间,不是直线一个方向运动可能就不行了)
        public override bool BoundingBox(float t0, float t1, ref AABB box)
        {
            box = GetBox(
                new AABB(Center(t0) - new Vector3(radius, radius, radius),
                    Center(t0) + new Vector3(radius, radius, radius)),
                new AABB(Center(t1) - new Vector3(radius, radius, radius),
                    Center(t1) + new Vector3(radius, radius, radius)));
            return true;
        }
    }

    class PlaneXY : Hitable
    {
        // k = z
        private float x0, x1, y0, y1, z;

        private Material material;

        public PlaneXY(float _x0, float _x1, float _y0, float _y1, float _z, Material mat)
        {
            material = mat;

            if (_x0 < _x1)
            {
                x0 = _x0;
                x1 = _x1;
            }
            else
            {
                x0 = _x1;
                x1 = _x0;
            }

            if (_y0 < _y1)
            {
                y0 = _y0;
                y1 = _y1;
            }
            else
            {
                y0 = _y1;
                y1 = _y0;
            }
           
            z = _z;
        }

        public override bool Hit(Ray ray, float t_min, float t_max, ref HitRecord rec)
        {
            var t = (z - ray.original.z) / ray.normalDirection.z;
            if (t < t_min || t > t_max) return false;
            var x = ray.original.x + t * ray.normalDirection.x;
            if (x < x0 || x > x1) return false;
            var y = ray.original.y + t * ray.normalDirection.y;
            if (y < y0 || y > y1) return false;

            rec.u = (x - x0) / (x1 - x0);
            rec.v = (y - y0) / (y1 - y0);
            rec.t = t;
            rec.material = material;
            rec.normal = new Vector3(0, 0, 1);
            rec.postion = ray.GetPoint(t);
            return true;
        }

        public override bool BoundingBox(float t0, float t1, ref AABB box)
        {
            box = new AABB(new Vector3(x0, y0, z - 0.0001f), new Vector3(x1, y1, z + 0.0001f));
            return true;
        }
    }

    class PlaneXZ : Hitable
    {
        private float x0, x1, z0, z1, y;

        private Material material;

        public PlaneXZ(float _x0, float _x1, float _z0, float _z1, float _y, Material mat)
        {
            material = mat;

            if (_x0 < _x1)
            {
                x0 = _x0;
                x1 = _x1;
            }
            else
            {
                x0 = _x1;
                x1 = _x0;
            }

            if (_z0 < _z1)
            {
                z0 = _z0;
                z1 = _z1;
            }
            else
            {
                z0 = _z1;
                z1 = _z0;
            }

            y = _y;
        }
 
        public override bool Hit(Ray ray, float t_min, float t_max, ref HitRecord rec)
        {
            // ray射到射到z = k时
            float t = (y - ray.original.y) / ray.normalDirection.y;
            if (t < t_min || t > t_max)
                return false;

            float x = ray.original.x + t * ray.normalDirection.x;
            if (x < x0 || x > x1)
                return false;

            float z = ray.original.z + t * ray.normalDirection.z;
            if (z < z0 || z > z1)
                return false;

            rec.u = (x - x0) / (x1 - x0);
            rec.v = (z - z0) / (z1 - z0);
            rec.t = t;
            rec.material = material;
            rec.normal = new Vector3(0, 1, 0);
            rec.postion = ray.GetPoint(t);
            return true;
        }

        public override bool BoundingBox(float t0, float t1, ref AABB box)
        {
            box = new AABB(new Vector3(x0, y - 0.0001f, z0), new Vector3(x1, y + 0.0001f, z1));
            return true;
        }
    }

    class PlaneYZ : Hitable
    {
        private float z0, z1, y0, y1, x;

        private Material material;

        public PlaneYZ(float _y0, float _y1, float _z0, float _z1, float _x, Material mat)
        {
            material = mat;

            if (_y0 < _y1)
            {
                y0 = _y0;
                y1 = _y1;
            }
            else
            {
                y0 = _y1;
                y1 = _y0;
            }

            if (_z0 < _z1)
            {
                z0 = _z0;
                z1 = _z1;
            }
            else
            {
                z0 = _z1;
                z1 = _z0;
            }

            x = _x;
        }

        public override bool Hit(Ray ray, float t_min, float t_max, ref HitRecord rec)
        {
            var t = (x - ray.original.x) / ray.normalDirection.x;
            if (t < t_min || t > t_max) return false;
            var z = ray.original.z + t * ray.normalDirection.z;
            if (z < z0 || z > z1) return false;
            var y = ray.original.y + t * ray.normalDirection.y;
            if (y < y0 || y > y1) return false;

            rec.v = (z - z0) / (z1 - z0);
            rec.u = (y - y0) / (y1 - y0);
            rec.t = t;
            rec.material = material;
            rec.normal = new Vector3(1, 0, 0);
            rec.postion = ray.GetPoint(t);
            return true;
        }

        public override bool BoundingBox(float t0, float t1, ref AABB box)
        {
            box = new AABB(new Vector3(x - 0.0001f, y0, z0), new Vector3(x + 0.0001f, y1, z1));
            return true;
        }
    }

    public class FilpNormals : Hitable
    {
        private readonly Hitable hitable;

        public FilpNormals(Hitable p)
        {
            hitable = p;
        }

        public override bool Hit(Ray ray, float t_min, float t_max, ref HitRecord rec)
        {
            if (!hitable.Hit(ray, t_min, t_max, ref rec)) return false;
            rec.normal = -rec.normal;
            return true;
        }

        public override bool BoundingBox(float t0, float t1, ref AABB box)
        {
            return hitable.BoundingBox(t0, t1, ref box);
        }
    }

    class Cube : Hitable
    {
        public Vector3 pmin, pmax;

        public HitableList surfaces;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p0"></param>
        /// <param name="p1"></param>
        /// <param name="m1">back</param>
        /// <param name="m2">front</param>
        /// <param name="m3">bottom</param>
        /// <param name="m4">up</param>
        /// <param name="m5">left</param>
        /// <param name="m6">right</param>
        public Cube(Vector3 p0, Vector3 p1, Material m1, Material m2 = null, Material m3 = null, Material m4 = null, Material m5 = null, Material m6 = null)
        {
            pmin = p0;
            pmax = p1;

            if (m2 == null)
                m2 = m1;

            if (m3 == null)
                m3 = m2;

            if (m4 == null)
                m4 = m3;

            if (m5 == null)
                m5 = m4;

            if (m6 == null)
                m6 = m5;

            surfaces = new HitableList();
            // back
            surfaces.list.Add(new PlaneXY(p0.x, p1.x, p0.y, p1.y, p1.z, m1));
            // front
            surfaces.list.Add(new FilpNormals(new PlaneXY(p0.x, p1.x, p0.y, p1.y, p0.z, m2)));
            // up
            surfaces.list.Add(new PlaneXZ(p0.x, p1.x, p0.z, p1.z, p1.y, m3));
            // bottom
            surfaces.list.Add(new FilpNormals(new PlaneXZ(p0.x, p1.x, p0.z, p1.z, p0.y, m4)));
            // left
            surfaces.list.Add(new PlaneYZ(p0.y, p1.y, p0.z, p1.z, p1.x, m5));
            // right
            surfaces.list.Add(new FilpNormals(new PlaneYZ(p0.y, p1.y, p0.z, p1.z, p0.x, m6)));
        }

        public override bool BoundingBox(float t0, float t1, ref AABB box)
        {
            box = new AABB(pmin, pmax);
            return true;
        }

        public override bool Hit(Ray ray, float t_min, float t_max, ref HitRecord rec)
        {
            return surfaces.Hit(ray, t_min, t_max, ref rec);
        }
    }

    public class Translate : Hitable
    {
        public Hitable Object;
        private Vector3 offset;

        public Translate(Hitable p, Vector3 displace)
        {
            offset = displace;
            Object = p;
        }

        public override bool BoundingBox(float t0, float t1, ref AABB box)
        {
            if (!Object.BoundingBox(t0, t1, ref box)) return false;
            box = new AABB(box.min + offset, box.max + offset);
            return true;
        }

        public override bool Hit(Ray ray, float t_min, float t_max, ref HitRecord rec)
        {
            var moved = new Ray(ray.original - offset, ray.direction);
            if (!Object.Hit(moved, t_min, t_max, ref rec)) return false;
            rec.postion += offset;
            return true;
        }
    }

    //Y轴旋转类
    public class RotateY : Hitable
    {
        public AABB bbox = new AABB();
        public bool hasbox;
        public Hitable Object;
        private float sin_theta, cos_theta;
        public override bool BoundingBox(float t0, float t1, ref AABB box)
        {
            box = bbox;
            return hasbox;
        }

        public RotateY(Hitable p, float angle)
        {
            Object = p;
            float radians = (Mathf.PI / 180f) * angle;
            sin_theta = Mathf.Sin(radians);
            cos_theta = Mathf.Cos(radians);
            hasbox = Object.BoundingBox(0, 1, ref bbox);
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(-float.MaxValue, -float.MaxValue, -float.MaxValue);
            for (var i = 0; i < 2; i++)
                for (var j = 0; j < 2; j++)
                    for (var k = 0; k < 2; k++)
                    {
                        float x = i * bbox.max.x + (1 - i) * bbox.min.x;
                        float y = j * bbox.max.y + (1 - j) * bbox.min.y;
                        float z = k * bbox.max.z + (1 - k) * bbox.min.z;
                        float newx = cos_theta * x + sin_theta * z;
                        float newz = -sin_theta * x + cos_theta * z;
                        var tester = new Vector3(newx, y, newz);
                        for (int c = 0; c < 3; c++)
                        {
                            if (tester[c] > max[c]) max[c] = tester[c];
                            if (tester[c] < min[c]) min[c] = tester[c];
                        }
                    }


            bbox = new AABB(min, max);
        }

        public override bool Hit(Ray ray, float t_min, float t_max, ref HitRecord rec)
        {
            Vector3 origin = ray.original;
            Vector3 direction = ray.direction;
            origin[0] = cos_theta * ray.original[0] - sin_theta * ray.original[2];
            origin[2] = sin_theta * ray.original[0] + cos_theta * ray.original[2];
            direction[0] = cos_theta * ray.direction[0] - sin_theta * ray.direction[2];
            direction[2] = sin_theta * ray.direction[0] + cos_theta * ray.direction[2];
            var rotatedR = new Ray(origin, direction, ray.time);
            var r = rec;
            if (Object.Hit(rotatedR, t_min, t_max, ref rec))
            {
                Vector3 p = rec.postion;
                Vector3 normal = rec.normal;
                p[0] = cos_theta * rec.postion[0] + sin_theta * rec.postion[2];
                p[2] = -sin_theta * rec.postion[0] + cos_theta * rec.postion[2];
                normal[0] = cos_theta * rec.normal[0] + sin_theta * rec.normal[2];
                normal[2] = -sin_theta * rec.normal[0] + cos_theta * rec.normal[2];
                rec.postion = p;
                rec.normal = normal;
                return true;
            }
            rec = r;
            return false;
        }
    }

    public class HitableList : Hitable
    {
        public List<Hitable> list;
        public HitableList() { list = new List<Hitable>(); }

        /// <summary>
        /// 返回所有Hitable中最靠近射线源的命中信息
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="t_min"></param>
        /// <param name="t_max"></param>
        /// <param name="rec"></param>
        /// <returns></returns>
        public override bool Hit(Ray ray, float t_min, float t_max, ref HitRecord rec)
        {
            HitRecord tempRecord = new HitRecord();
            bool hitAnything = false;
            float closest = t_max;

            foreach (Hitable h in list)
            {
                // 如果在(min, closest)的范围内击中,则更新rec
                if (h.Hit(ray, t_min, closest, ref tempRecord))
                {
                    hitAnything = true;
                    closest = tempRecord.t;
                    rec = tempRecord;
                }
            }

            return hitAnything;
        }

        // 包含list中所有包围盒的包围盒
        public override bool BoundingBox(float t0, float t1, ref AABB box)
        {
            if (list.Count == 0)
                return false;
            AABB tempBox = new AABB();

            // 目前不会出现这种情况
            if (!list[0].BoundingBox(t0, t1, ref tempBox))
                return false;

            box = tempBox;

            //遍历list,把所有包围盒包围了
            foreach (Hitable t in list)
            {
                if (t.BoundingBox(t0, t1, ref tempBox))
                    box = GetBox(box, tempBox);
                else return false;
            }

            return true;
        }
    }

    public class BVHNode : Hitable
    {
        public Hitable left, right;
        public AABB box;
        public BVHNode() { }

        public BVHNode(Hitable[] p, int n, float time0, float time1)
        {
            //随机一个轴。 x轴:0 y轴:1 z轴:2
            int method = (int)(3 * Random.value);
            if (method == 4)
                method = 3;
            //转换为List然后使用排序,最后再转换回Array
            //排序规则使用lambda表达式转向比较函数,并加入轴向参数
            List<Hitable> temp_list = new List<Hitable>(p);
            temp_list.Sort((a, b) => Compare(a, b, method));
            p = temp_list.ToArray();

            //检测当前子节点数量,如果大于2则继续分割。
            switch (n)
            {
                case 1:
                    left = right = p[0];
                    break;
                case 2:
                    left = p[0];
                    right = p[1];
                    break;
                default: //拆分
                    left = new BVHNode(SplitArray(p, 0, n / 2 - 1), n / 2, time0, time1);
                    right = new BVHNode(SplitArray(p, n / 2, n - 1), n - n / 2, time0, time1);
                    break;
            }

            // 生成子节点的包围盒
            AABB box_left = new AABB(), box_right = new AABB();
            if (!left.BoundingBox(time0, time1, ref box_left) || !right.BoundingBox(time0, time1, ref box_right))
                throw new System.Exception("no bounding box in bvh_node constructor");

            //根据子节点生成当前节点的包围盒
            box = GetBox(box_left, box_right);
        }

        //根据a.b随机一个轴包围盒最小值大小来排序的嵌套函数。
        private int Compare(Hitable a, Hitable b, int i)
        {
            AABB l = new AABB(), r = new AABB();
            // 目前不会出现这种情况
            if (!a.BoundingBox(0, 0, ref l) || !b.BoundingBox(0, 0, ref r))
                throw new System.Exception("NULL");

            return l.min[i] - r.min[i] < 0 ? -1 : 1;
        }

        //用来排序的分割数组的嵌套函数,取StartIndex到EndIndex来生成新的数组
        private Hitable[] SplitArray(Hitable[] Source, int StartIndex, int EndIndex)
        {
            var result = new Hitable[EndIndex - StartIndex + 1];
            for (var i = 0; i <= EndIndex - StartIndex; i++)
                result[i] = Source[i + StartIndex];
            return result;
        }

        public override bool BoundingBox(float t0, float t1, ref AABB b)
        {
            b = box;
            return true;
        }

        public override bool Hit(Ray ray, float t_min, float t_max, ref HitRecord rec)
        {
            //检测包围和碰撞,返回碰撞的子树的信息
            if (box.Hit(ray, t_min, t_max))
            {
                HitRecord left_rec = new HitRecord(), right_rec = new HitRecord();
                var hit_left = left.Hit(ray, t_min, t_max, ref left_rec);
                var hit_right = right.Hit(ray, t_min, t_max, ref right_rec);

                //都撞到了返回先撞到的子树
                if (hit_left && hit_right)
                {
                    rec = left_rec.t < right_rec.t ? left_rec : right_rec;
                    return true;
                }
                if (hit_left)
                {
                    rec = left_rec;
                    return true;
                }
                if (hit_right)
                {
                    rec = right_rec;
                    return true;
                }
                return false;
            }
            return false;
        }

    }

    #endregion

    #region 散射模型

    public abstract class Material
    {
        // 没有重载则不会发光,任何材质都可以重载，拥有发光效果
        public virtual Color Emitted(float u, float v, Vector3 p)
        {
            return new Color(0, 0, 0);
        }
        /// <summary>
        /// 材质表面发生的光线变化过程
        /// </summary>
        /// <param name="rayIn"></param>
        /// <param name="record"></param>
        /// <param name="attenuation">衰减</param>
        /// <param name="scattered"></param>
        /// <returns>是否发生了光线变化</returns>
        public abstract bool Scatter(Ray rayIn, HitRecord record, ref Color attenuation, ref Ray scattered);
    }

    /// <summary>
    /// 简单的发光材质
    /// </summary>
    public class DiffuseLight : Material
    {
        private readonly Texture texture;
        private readonly float intensity;
        public DiffuseLight(Texture t, float i)
        {
            texture = t;
            intensity = i;
        }

        // 不会继续散射,也不会返回颜色和Ray
        public override bool Scatter(Ray rayIn, HitRecord record, ref Color attenuation, ref Ray scattered)
        {
            return false;
        }

        // 返回颜色
        public override Color Emitted(float u, float v, Vector3 p)
        {
            return texture.value(u, v, p) * intensity;
        }
    }

    /// <summary>
    /// 理想的漫反射模型
    /// </summary>
    public class Lambertian : Material
    {
        //Color albedo;
        public Texture texture;

        //public Lambertian(Color a) { albedo = a; }
        public Lambertian(Texture tex)
        {
            texture = tex;
        }

        public override bool Scatter(Ray rayIn, HitRecord record, ref Color attenuation, ref Ray scattered)
        {
            Vector3 target = record.postion + record.normal + Math.GetRandomPointInUnitSphere();
            scattered = new Ray(record.postion, target - record.postion, rayIn.time);
            attenuation = texture.value(record.u, record.v, record.postion);
            return true;
        }
    }

    /// <summary>
    /// 理想的镜面反射模型
    /// </summary>
    public class Metal : Material
    {
        //private Color albedo;
        public Texture texture;

        // 镜面反射程度
        private float fuzz;

        //public Metal(Color a, float f) { albedo = a; fuzz = f; }
        public Metal(Texture tex, float f)
        {
            texture = tex;
            fuzz = f;
        }

        public override bool Scatter(Ray rayIn, HitRecord record, ref Color attenuation, ref Ray scattered)
        {
            //attenuation = albedo;
            attenuation = texture.value(record.u, record.v, record.postion);

            Vector3 reflected = Math.Reflect(rayIn.normalDirection, record.normal);
            // 其实unity有自带
            //Vector3 reflected = Vector3.Reflect(rayIn.normalDirection, record.normal);

            // 可能是因为float计算误差！！！有时候会传入从物体内部的射来的射线
            // 这里是如果是从内部传过来就直接当作是切线
            if (Vector3.Dot(rayIn.normalDirection, record.normal) >= 0)
            {
                scattered = rayIn;
                return true;
            }

            // 注释部分都是测试时找bug代码
            //int i = 0;

            // 如果不是法向量的半球面,则再随机(因为float的计算误差)
            do
            {
                scattered = new Ray(record.postion, reflected + fuzz * Math.GetRandomPointInUnitSphere(), rayIn.time);
                /*
                if (++i == 30)
                {
                    File.AppendAllText(@"C:\Users\Administrator\Desktop\1.txt",
                        "in: " + rayIn.normalDirection.x.ToString() + "," + rayIn.normalDirection.y.ToString() + "," + rayIn.normalDirection.z.ToString() + "\r\n" +
                        "normal: " + record.normal.x.ToString() + "," + record.normal.y.ToString() + "," + record.normal.z.ToString() + "\r\n" +
                        "dot: " + Vector3.Dot(rayIn.normalDirection, record.normal).ToString() + "\r\n" +
                        "reflected: " + reflected.x.ToString() + "," + reflected.y.ToString() + "," + reflected.z.ToString() + "\r\n" +
                        "scattered: " + scattered.direction.ToString() + scattered.normalDirection.ToString() + "\r\n" +
                        "dot: " + Vector3.Dot(scattered.normalDirection, record.normal).ToString() + "\r\n"
                        , Encoding.Default);
                    return false;
                }
                //*/
            }
            // 如果因为float计算误差新的scattered还是出现是从物体内部传过来的,则再重新算scattered
            while (Vector3.Dot(scattered.normalDirection, record.normal) < 0);

            return true;
            //*/

            /*
            // 另一种方法：直接返回false,但在球边缘(容易出现bug的地方)的效果很差
            scattered = new Ray(record.postion, reflected + fuzz * Math.GetRandomPointInUnitSphere());
            // 如果不是法向量的半球面(方向弹回球里面了)
            return Vector3.Dot(scattered.normalDirection, record.normal) > 0;
            //*/    
        }
        
    }

    /// <summary>
    /// 透明折射模型
    /// </summary>
    public class Dielectirc : Material
    {
        // 相对空气的折射率(一般用于介质内向介质外传播)
        private float ref_idx;

        public Dielectirc(float ri) { ref_idx = ri; }

        public override bool Scatter(Ray rayIn, HitRecord record, ref Color attenuation, ref Ray scattered)
        {
            Vector3 outNormal;
            Vector3 reflected = Math.Reflect(rayIn.direction, record.normal);
            // 透明的物体当然不会吸收任何光
            attenuation = Color.white;
            // ni_no(入射光eta/出射光eta)：入射光从介质内传入价值外eta1, eta2的比值(相对空气的折射率)
            float ni_no;
            Vector3 refracted = Vector3.zero;

            // 需要的cos值,要使用折射率较高那一边的theta(这里就是介质那边的theta)
            // 因为公式的原因,cos这里应该用入射角,上面说明是错误的
            float cos = 0;
            // 反射比
            float reflect_prob;

            // 如果光线是从介质内向介质外传播,那么法线就要反转一下
            if (Vector3.Dot(rayIn.direction, record.normal) > 0)
            {
                outNormal = -record.normal;
                ni_no = ref_idx;
                //cos值应该是入射角的cos,应该不需要ni_no
                //cos = ni_no * Vector3.Dot(rayIn.normalDirection, record.normal);
                cos = Vector3.Dot(rayIn.normalDirection, record.normal);
            }
            // 是由空气进入介质
            else
            {
                outNormal = record.normal;
                ni_no = 1f / ref_idx;
                //cos值是出射角的cos(介质的折射率比较大),原来这个是入射角
                cos = -Vector3.Dot(rayIn.normalDirection, record.normal);
                // 这里的公式用入射角
                //float sin = Vector3.Cross(rayIn.normalDirection, record.normal).magnitude / ref_idx;
                //cos = Mathf.Sqrt(1 - sin * sin);
            }

            // 如果没发生折射,就用反射
            if (Math.Refract(rayIn.normalDirection, outNormal, ni_no, ref refracted))
            {
                //应该是ni/no而不是固定的介质/空气
                //reflect_prob = Math.Schlick(cos, ref_idx);
                reflect_prob = Math.Schlick(cos, ni_no);
            }
            // 比如比如由玻璃进入空气时没发生折射,在玻璃内部反射
            // 可能会一直弹,按理来说空气进入玻璃下一个折射一定会折射出来,如果没折射出来(float误差引起计算错误),在玻璃内部反射了就应该是一直反射折射不出来了(float误差折射出来)
            else
            {
                //此时反射比为100%, 直接返回reflected方向
                //reflect_prob = 1;
                scattered = new Ray(record.postion, reflected, rayIn.time);
                return true;
            }

            // 根据reflect_prob来决定reflect和refract的比例
            if (Random.value <= reflect_prob)
            {
                scattered = new Ray(record.postion, reflected, rayIn.time);
            }
            else
            {
                scattered = new Ray(record.postion, refracted, rayIn.time);
            }

            return true;
        }
    }

    #endregion

    #region Texture

    public abstract class Texture
    {
        public abstract Color value(float u, float v, Vector3 p);
    }

    // 纯色纹理
    public class ConstantColor : Texture
    {
        private Color color;

        public ConstantColor(Color c)
        {
            color = c;
        }

        public override Color value(float u, float v, Vector3 p)
        {
            return color;
        }
    }

    // 棋盘格
    public class CheckerTexture : Texture
    {
        public Texture odd, even;

        public CheckerTexture(Texture t0, Texture t1)
        {
            even = t0; odd = t1;
        }

        public override Color value(float u, float v, Vector3 p)
        {
            return Mathf.Sin(10 * p.x - 0.01f) * Mathf.Sin(10 * p.y - 0.01f) * Mathf.Sin(10 * p.z -0.01f) <= 0 ? odd.value(u, v, p) : even.value(u, v, p);
        }
    }

    public class ImageTexture : Texture
    {
        private readonly byte[] data;
        private readonly int width, height;

        // 可以看作是unity中Texture的Tiling
        public float scaleU = 1, scaleV = 1;
        public float offsetU = 1, offsetV = 1;

        //构造函数直接读取图片
        public ImageTexture(string file, float scaU = 1, float scaV = 1, float offU = 0, float offV = 0)
        {
            scaleU = scaU;
            scaleV = scaV;
            offsetU = offU;
            offsetV = offV;
            var bitmap = new System.Drawing.Bitmap(System.Drawing.Image.FromFile(file));
            data = new byte[bitmap.Width * bitmap.Height * 3];
            width = bitmap.Width;
            height = bitmap.Height;
            for (var i = 0; i < bitmap.Height; i++)
            {
                for (var j = 0; j < bitmap.Width; j++)
                {
                    // 获得j, i处的Color
                    var c = bitmap.GetPixel(j, i);
                    // 把RGB存入data
                    // System.Drawing.Color的RGB值用byte(8位,范围(0,255))储存
                    data[3 * j + 3 * width * i] = c.R;
                    data[3 * j + 3 * width * i + 1] = c.G;
                    data[3 * j + 3 * width * i + 2] = c.B;
                }
            }
        }

        //构造函数赋予RGB缓冲
        public ImageTexture(byte[] p, int x, int y)
        {
            data = p;
            width = x;
            height = y;
        }

        //取得某UV的颜色值。
        public override Color value(float u, float v, Vector3 p)
        {
            // 范围在(0, 1)
            u = (u * scaleU + offsetU) % 1;
            v = (v * scaleV + offsetV) % 1;
            // 范围在(0, width - 1或height - 1)
            var i = Mathf.Clamp((int)(u * width), 0, width - 1);
            // 图的0到1是从上到下,uv是从下到上,所以翻转一下,减0.001的目的是范围?
            var j = Mathf.Clamp((int)((1 - v) * height - 0.001f), 0, height - 1);
            
            return new Color(
                data[3 * i + 3 * width * j] / 255f,
                data[3 * i + 3 * width * j + 1] / 255f,
                data[3 * i + 3 * width * j + 2] / 255f
                );
        }
    }

    #endregion

    #region unity序列化测试

    public class MyClass
    {
        public string s;
    }

    [System.Serializable]
    public class MyClassSerializable
    {
        public float f1;
        [System.NonSerialized] public float f2;
        private int i1;
        [SerializeField] private int i2;
    }

    public class SerializationRuleWindow : EditorWindow
    {
        public MyClass m1;
        public MyClassSerializable s1;
        private MyClassSerializable s2;
    }

    #endregion
    
    public class RayTracing : EditorWindow
    {
        public string IMG_PATH = @"C:\Users\Administrator\Desktop\Ray Tracing";

        public static int width;
        public static int height;
        public static int sample;
        public static float sample_weight;
        public static int max_scatter_time;
        public static float radius;
        public int i;
        
        Stopwatch sw;

        [MenuItem("Ray Tracing/Ray Tracing")]
        public static void OnClick()
        {
            //*
            if (!PlayerPrefs.HasKey("width"))
            {
                width = 800;
                PlayerPrefs.SetInt("width", width);
            }
            else
            {
                width = PlayerPrefs.GetInt("width");
            }

            if (!PlayerPrefs.HasKey("height"))
            {
                height = 450;
                PlayerPrefs.SetInt("height", height);
            }
            else
            {
                height = PlayerPrefs.GetInt("height");
            }

            if (!PlayerPrefs.HasKey("sample"))
            {
                sample = 20;
                PlayerPrefs.SetInt("sample", sample);
            }
            else
            {
                sample = PlayerPrefs.GetInt("sample");
            }

            if (!PlayerPrefs.HasKey("max_scatter_time"))
            {
                max_scatter_time = 50;
                PlayerPrefs.SetInt("max_scatter_time", max_scatter_time);
            }
            else
            {
                max_scatter_time = PlayerPrefs.GetInt("max_scatter_time");
            }

            if (!PlayerPrefs.HasKey("radius"))
            {
                radius = 0.8f;
                PlayerPrefs.SetFloat("radius", radius);
            }
            else
            {
                radius = PlayerPrefs.GetFloat("radius");
            }
            //*/

            RayTracing window = new RayTracing();
            window.Show();
        }

        void OnGUI()
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("width", GUILayout.Width(130));
            width = int.Parse(EditorGUILayout.TextField(width.ToString(), GUILayout.Width(100)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("height", GUILayout.Width(130));
            height = int.Parse(EditorGUILayout.TextField(height.ToString(), GUILayout.Width(100)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("sample", GUILayout.Width(130));
            sample = int.Parse(EditorGUILayout.TextField(sample.ToString(), GUILayout.Width(100)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("max_scatter_time", GUILayout.Width(130));
            max_scatter_time = int.Parse(EditorGUILayout.TextField(max_scatter_time.ToString(), GUILayout.Width(100)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("radius", GUILayout.Width(130));
            radius = float.Parse(EditorGUILayout.TextField(radius.ToString(), GUILayout.Width(100)));
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Create PNG", GUILayout.Height(30)))
            {
                //// float误差
                //if (sample * sample_weight >= 1.01f || sample * sample_weight < 0.99f)
                //{
                //    EditorUtility.DisplayDialog("ERROR", "sample与sample_weight相乘应等于1！" + " " + sample * sample_weight, "ok");
                //    return;
                //}
                sample_weight = 1f / sample;

                //*
                PlayerPrefs.SetInt("width", width);
                PlayerPrefs.SetInt("height", height);
                PlayerPrefs.SetInt("sample", sample);
                PlayerPrefs.SetInt("max_scatter_time", max_scatter_time);
                PlayerPrefs.SetFloat("radius", radius);
                //*/
                sw = new Stopwatch();
                sw.Start();
                Color[] colors = new Color[width * height];
                if (CreateColorForTestMetal(width, height, ref colors))
                    CreatePng(width, height, colors);
            }
        }

        #region 图像生成
        void CreatePng(int width, int height, Color[] colors)
        {
            if (width * height != colors.Length)
            {
                EditorUtility.DisplayDialog("ERROR", "长宽与数组长度无法对应！", "ok");
                return;
            }
            Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
            tex.SetPixels(colors);
            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            FileStream fs = new FileStream(IMG_PATH + "_sample_" + sample.ToString() + "_time_" + (int)(sw.ElapsedTicks / (decimal)Stopwatch.Frequency) + "_radius" + radius + ".png", FileMode.Create);
            sw.Stop();
            BinaryWriter bw = new BinaryWriter(fs);
            bw.Write(bytes);
            fs.Close();
            bw.Close();
        }
        #endregion

        /// <summary>
        /// 根据射线获得颜色
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="hitableList"></param>
        /// <param name="depth">当前迭代次数,大于max_scatter_time时停止迭代</param>
        /// <returns></returns>
        Color GetColorForTestMetal(Ray ray, HitableList hitableList, int depth)
        {
            HitRecord record = new HitRecord();

            // 第一步先得到击中的HitRecord信息,如果击中,得到record(位置,法向量,材质),没击中则是天空盒
            if (hitableList.Hit(ray, 0.0001f, float.MaxValue, ref record))
            {
                Ray r = new Ray(Vector3.zero, Vector3.zero, ray.time);
                Color attenuation = Color.black;
                // 第二步根据材质得到散射的光线方向迭代,完成迭代后得到的颜色乘上此材质的颜色
                if (depth < max_scatter_time && record.material.Scatter(ray, record, ref attenuation, ref r))
                {
                    Color c = GetColorForTestMetal(r, hitableList, depth + 1);
                    return new Color(c.r * attenuation.r, c.g * attenuation.g, c.b * attenuation.b);
                }
                else
                {
                    // 假设已经反射了太多次,或者压根就没有发生反射,那么就认为黑了
                    return Color.black;
                }
            }
            
            // 可以当作天空盒
            float t = 0.5f * ray.normalDirection.y + 1f;
            return (1 - t) * new Color(1, 1, 1) + t * new Color(0.5f, 0.7f, 1);
        }

        private Color Diffusing(Ray ray, HitableList hitableList, int depth)
        {
            HitRecord record = new HitRecord();
            // 第一步先得到击中的HitRecord信息,如果击中,得到record(位置,法向量,材质),没击中则是天空盒
            if (hitableList.Hit(ray, 0.0001f, float.MaxValue, ref record))
            {
                //return new Color(record.u, record.v, 0);
                Ray r = new Ray(Vector3.zero, Vector3.zero);
                Color attenuation = Color.black;
                // 第二步根据材质得到发光颜色
                Color emitted = record.material.Emitted(record.u, record.v, record.postion);
                // 如果反射太多次，或者是发光材质，返回emitted
                if (depth >= max_scatter_time || !record.material.Scatter(ray, record, ref attenuation, ref r))
                    return emitted;
                // 第三步根据材质得到散射的光线方向迭代,完成迭代后得到的颜色乘上此材质的颜色和发光颜色
                Color c = Diffusing(r, hitableList, depth + 1);
                return new Color(c.r * attenuation.r, c.g * attenuation.g, c.b * attenuation.b) + emitted;
            }
            
            return CubeMap.Skybox.value(ray.normalDirection);

            /*
            float t = 0.5f * ray.normalDirection.y + 1f;
            // 可以当作天空盒
            return (1 - t) * new Color(1, 1, 1) + t * new Color(0.5f, 0.7f, 1);
            //*/
        }

        bool CreateColorForTestMetal(int width, int height, ref Color[] colors)
        {
            //视锥体的左下角、长宽和起始扫射点设定
            //Vector3 lowLeftCorner = new Vector3(-1.6f, -0.9f, -1f);
            //Vector3 horizontal = new Vector3(3.2f, 0, 0);
            //Vector3 vertical = new Vector3(0, 1.8f, 0);
            //Vector3 original = new Vector3(0, 0, 1);
            //Camera camera = new Camera(original, lowLeftCorner, horizontal, vertical);
            Vector3 from = new Vector3(-0.8f, 1.5f, -0.8f);
            Vector3 to = new Vector3(0, 0.5f, 1);
            Camera camera = new Camera(from, to, Vector3.up, 60, (float)width / height, radius, (from - to).magnitude * 0.8f, 0 ,1);

            //场景内物体
            HitableList hitableList = new HitableList();
            HitableList world = new HitableList();

            //hitableList.list.Add(new Sphere(new Vector3(0, -100f, 1), 100f, new Metal(new Color(0.4f, 0.5f, 0.6f), 0.15f)));
            //hitableList.list.Add(new Sphere(new Vector3(0, -100f, 1), 100f, new Metal(new CheckerTexture(new ConstantTexture(new Color(1, 1, 1)), new ConstantTexture(new Color(0.1f, 0.1f, 0.1f))), 0.15f)));
            hitableList.list.Add(new PlaneXZ(-10, 10, -10, 10, 0f, new Metal(new CheckerTexture(new ConstantColor(new Color(1, 1, 1)), new ConstantColor(new Color(0.1f, 0.1f, 0.1f))), 0.15f)));
            //hitableList.list.Add(new Sphere(new Vector3(0, 0, 1), 0.5f, new Lambertian(new ConstantTexture(new Color(0.8f, 0.3f, 0.3f)))));
            hitableList.list.Add(new Sphere(new Vector3(0, 0.5f, 1), 0.5f, new Lambertian(new ImageTexture("D:/CK/Image/壁纸/【fishman】Tel Aviv-Yafo.jpg", 1, 1, 0.34f))));
            hitableList.list.Add(new Sphere(new Vector3(-1.5f, 0.3f, 3.8f), 0.3f, new Metal(new ConstantColor(new Color(0.7f, 0.2f, 0.7f)), 0.2f)));
            hitableList.list.Add(new Sphere(new Vector3(1f, 0.3f, 1), 0.3f, new Metal(new ConstantColor(new Color(0.2f, 0.8f, 0.8f)), 0.8f)));
            hitableList.list.Add(new Sphere(new Vector3(-0.7f, 0.3f, 0.5f), 0.2f, new Dielectirc(1.5f)));

            hitableList.list.Add(new Sphere(new Vector3(1.8f, 0.1f, 0.9f), 0.1f, new Lambertian(new ConstantColor(new Color(0.3f, 0.5f, 0.8f)))));
            hitableList.list.Add(new Sphere(new Vector3(-0.9f, 0.2f, 3f), 0.2f, new Lambertian(new ConstantColor(new Color(0.4f, 0.8f, 0.6f)))));
            hitableList.list.Add(new Sphere(new Vector3(4.5f, 1f, 3.5f), 1f, new Lambertian(new ConstantColor(new Color(0.4f, 0.8f, 0.6f)))));
            hitableList.list.Add(new Sphere(new Vector3(2.7f, 0.8f, 4.2f), 0.8f, new Lambertian(new ConstantColor(new Color(0.2f, 0.5f, 0.7f)))));
            hitableList.list.Add(new Sphere(new Vector3(4.1f, 0.45f, 1.5f), 0.45f, new Metal(new ConstantColor(new Color(0.95f, 0.95f, 0.95f)), 0)));
            hitableList.list.Add(new Sphere(new Vector3(0.6f, 0.1f, 0f), 0.1f, new Metal(new ConstantColor(new Color(0.9f, 0.65f, 0.4f)), 0.1f)));
            hitableList.list.Add(new Sphere(new Vector3(0f, 0.4f, 4f), 0.4f, new Metal(new ConstantColor(new Color(0.9f, 0.9f, 0.9f)), 0.2f)));
            hitableList.list.Add(new Sphere(new Vector3(-1.8f, 0.6f, 2.1f), 0.2f, new Metal(new ConstantColor(new Color(0.9f, 0.6f, 0.6f)), 0.3f)));
            hitableList.list.Add(new Sphere(new Vector3(-2.1f, 0.15f, 2.6f), 0.15f, new Metal(new ConstantColor(new Color(0.4f, 0.5f, 0.8f)), 0.4f)));
            hitableList.list.Add(new Sphere(new Vector3(1.5f, 0.3f, 0.3f), 0.3f, new Dielectirc(1.5f)));

            Lambertian side = new Lambertian(new ImageTexture(@"C:\迅雷下载\1.7.2原版高清128x VOL.1.1\assets\minecraft\textures\blocks\crafting_table_side.png"));

            hitableList.list.Add(new Cube(new Vector3(-1.2f, 0, 1.1f), new Vector3(-0.6f, 0.6f, 1.7f),
                //new Lambertian(new ConstantColor(new Color(1f, 0.2f, 0.3f)))
                side,
                new Lambertian(new ImageTexture(@"C:\迅雷下载\1.7.2原版高清128x VOL.1.1\assets\minecraft\textures\blocks\crafting_table_front.png")),
                new Lambertian(new ImageTexture(@"C:\迅雷下载\1.7.2原版高清128x VOL.1.1\assets\minecraft\textures\blocks\crafting_table_top.png")),
                side
                ));

            world.list.Add(new BVHNode(hitableList.list.ToArray(), hitableList.list.Count, 0, 1));

            //int l = width * height;
            float recip_width = 1f / width;
            float recip_height = 1f / height;
            for (int j = height - 1; j >= 0; j--)
            {
                // Cancle,可以中断停止生成图片
                //EditorUtility.DisplayProgressBar("Ray Tracing", "Creating..     " + j.ToString(), (float)(height - j) / height);
                if (EditorUtility.DisplayCancelableProgressBar("Ray Tracing", "Creating..     预计剩余时间: " + (sw.ElapsedTicks / (decimal)Stopwatch.Frequency / (height - j) * j).ToString("#0.00"), (float)(height - j) / height))
                {
                    EditorUtility.ClearProgressBar();
                    return false;
                }
                for (int i = 0; i < width; i++)
                {
                    //LowDiscrepancySequence.seed = 0;
                    Color color = new Color(0, 0, 0);
                    for (int s = 0; s < sample; s++)
                    {
                        Ray r = camera.CreateRay((i + Random.value) * recip_width, (j + Random.value) * recip_height);
                        color += Diffusing(r, world, 0);
                    }
                    color *= sample_weight;
                    // 为了使球体看起来更亮,改变gamma值(准确值是pow(color, 1 / 2.2f))
                    color = new Color(Mathf.Sqrt(color.r), Mathf.Sqrt(color.g), Mathf.Sqrt(color.b), 1f);
                    colors[i + j * width] = color;
                }
            }
            EditorUtility.ClearProgressBar();
            return true;
        }
    }
}
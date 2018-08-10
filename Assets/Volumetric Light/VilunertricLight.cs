using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VilunertricLight : MonoBehaviour
{
    public Shader shader;
    [Range(0, 0.999f)]
    public float mieG;
    public float maxLength = 100;
    public int sampleCount = 64;
    public float volumetricIntensity = 0.1f;

    private Camera cam;
    private Material mat;

    // Use this for initialization
    void Awake()
    {
        cam = GetComponent<Camera>();
        mat = new Material(shader);

        //*
        // ViewProjectionMatrix: NDCToViewToWorld
        // on some platforms they have to be transformed a bit to match the native API requirements
        Matrix4x4 inverseViewProjectionMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);

        inverseViewProjectionMatrix *= cam.worldToCameraMatrix;
        inverseViewProjectionMatrix = inverseViewProjectionMatrix.inverse;

        // 近裁面的四个顶点, gl中近裁面的z是1
        Vector3 leftBottom = inverseViewProjectionMatrix.MultiplyPoint(new Vector3(-1, -1, 1));
        Vector3 rightBottom = inverseViewProjectionMatrix.MultiplyPoint(new Vector3(1, -1, 1));
        Vector3 leftTop = inverseViewProjectionMatrix.MultiplyPoint(new Vector3(-1, 1, 1));

        Vector3 test = inverseViewProjectionMatrix.MultiplyPoint(new Vector3(-1, -1, 0));

        //Vector3 rightTop = inverseViewProjectionMatrix.MultiplyPoint(new Vector3(1, 1, 1));

        // 远裁面的四个顶点，gl中远裁面的z是0
        //leftBottom = inverseViewProjectionMatrix.MultiplyPoint(new Vector3(-1, -1, 0));
        //rightBottom = inverseViewProjectionMatrix.MultiplyPoint(new Vector3(1, -1, 0));
        //leftTop = inverseViewProjectionMatrix.MultiplyPoint(new Vector3(-1, 1, 0));
        //rightTop = inverseViewProjectionMatrix.MultiplyPoint(new Vector3(1, 1, 0));

        Vector3 nearU = rightBottom - leftBottom;
        Vector3 nearV = leftTop - leftBottom;
        //*/

        /*
        mat.SetVector("_MieG", new Vector4(1 - (mieG * mieG), 1 + (mieG * mieG), 2 * mieG, 1.0f / (4.0f * Mathf.PI)));
        mat.SetFloat("_MaxLength", maxLength);
        mat.SetFloat("_SampleCount", sampleCount);
        mat.SetFloat("_VolumetricIntensity", volumetricIntensity);

        mat.SetMatrix("_InverseViewProjectionMatrix", inverseViewProjectionMatrix);
        mat.SetVector("_NearLeftBottom", leftBottom);
        mat.SetVector("_NearU", nearU);
        mat.SetVector("_NearV", nearV);
        mat.SetVector("_EyeWorldDir", transform.forward);
        //*/
        LoadNoise3dTexture();
    }

    public Vector2 NoiseVelocity;
    public float NoiseScale = 0.015f;
    public float NoiseIntensity = 1.0f;
    public float NoiseIntensityOffset = 0.3f;
    public TextAsset data;
    private Texture3D _noiseTexture;

    void LoadNoise3dTexture()
    {
        // basic dds loader for 3d texture - !not very robust!

        //TextAsset data = Resources.Load("NoiseVolume") as TextAsset;

        byte[] bytes = data.bytes;

        //print(bytes.Length);

        uint height = BitConverter.ToUInt32(data.bytes, 12);
        uint width = BitConverter.ToUInt32(data.bytes, 16);
        uint pitch = BitConverter.ToUInt32(data.bytes, 20);
        uint depth = BitConverter.ToUInt32(data.bytes, 24);
        uint formatFlags = BitConverter.ToUInt32(data.bytes, 20 * 4);
        //uint fourCC = BitConverter.ToUInt32(data.bytes, 21 * 4);
        uint bitdepth = BitConverter.ToUInt32(data.bytes, 22 * 4);
        if (bitdepth == 0)
            bitdepth = pitch / width * 8;


        // doesn't work with TextureFormat.Alpha8 for some reason
        _noiseTexture = new Texture3D((int)width, (int)height, (int)depth, TextureFormat.RGBA32, false);
        _noiseTexture.name = "3D Noise";

        Color[] c = new Color[width * height * depth];

        uint index = 128;
        if (data.bytes[21 * 4] == 'D' && data.bytes[21 * 4 + 1] == 'X' && data.bytes[21 * 4 + 2] == '1' && data.bytes[21 * 4 + 3] == '0' &&
            (formatFlags & 0x4) != 0)
        {
            uint format = BitConverter.ToUInt32(data.bytes, (int)index);
            if (format >= 60 && format <= 65)
                bitdepth = 8;
            else if (format >= 48 && format <= 52)
                bitdepth = 16;
            else if (format >= 27 && format <= 32)
                bitdepth = 32;

            //Debug.Log("DXGI format: " + format);
            // dx10 format, skip dx10 header
            //Debug.Log("DX10 format");
            index += 20;
        }

        uint byteDepth = bitdepth / 8;
        pitch = (width * bitdepth + 7) / 8;

        for (int d = 0; d < depth; ++d)
        {
            //index = 128;
            for (int h = 0; h < height; ++h)
            {
                for (int w = 0; w < width; ++w)
                {
                    float v = (bytes[index + w * byteDepth] / 255.0f);
                    c[w + h * width + d * width * height] = new Color(v, v, v, v);
                }

                index += pitch;
            }
        }

        _noiseTexture.SetPixels(c);
        _noiseTexture.Apply();

        Shader.SetGlobalTexture("_NoiseTexture", _noiseTexture);
    }
    
    //private Vector3 prePos;
    //private Quaternion preRot;

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (mat != null)
        {
            mat.SetVector("_NoiseVelocity", new Vector4(NoiseVelocity.x, NoiseVelocity.y) * NoiseScale);
            mat.SetVector("_NoiseData", new Vector4(NoiseScale, NoiseIntensity, NoiseIntensityOffset));

            mat.SetVector("_MieG", new Vector4(1 - (mieG * mieG), 1 + (mieG * mieG), 2 * mieG, 1.0f / (4.0f * Mathf.PI)));
            mat.SetFloat("_MaxLength", maxLength);
            mat.SetFloat("_SampleCount", sampleCount);
            mat.SetFloat("_VolumetricIntensity", volumetricIntensity);

            /*
            //if (prePos != transform.localPosition || preRot != transform.localRotation)
            //{
            Matrix4x4 inverseViewProjectionMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);

            inverseViewProjectionMatrix *= cam.worldToCameraMatrix;
            //inverseViewProjectionMatrix = inverseViewProjectionMatrix.inverse;

            Vector3 leftBottom = inverseViewProjectionMatrix.MultiplyPoint(new Vector3(-1, -1, 1));
            Vector3 rightBottom = inverseViewProjectionMatrix.MultiplyPoint(new Vector3(1, -1, 1));
            Vector3 leftTop = inverseViewProjectionMatrix.MultiplyPoint(new Vector3(-1, 1, 1));

            Vector3 nearU = rightBottom - leftBottom;
            Vector3 nearV = leftTop - leftBottom;

            mat.SetMatrix("_InverseViewProjectionMatrix", inverseViewProjectionMatrix);

            mat.SetVector("_NearLeftBottom", leftBottom);
            mat.SetVector("_NearU", nearU);
            mat.SetVector("_NearV", nearV);

            mat.SetVector("_EyeWorldDir", transform.forward);

            //prePos = transform.localPosition;
            //preRot = transform.localRotation;
            //}
            */

            Graphics.Blit(src, dest, mat);
        }
        else
        {
            Graphics.Blit(src, dest);
        }
    }
}
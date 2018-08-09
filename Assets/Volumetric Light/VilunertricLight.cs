using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VilunertricLight : MonoBehaviour
{
    public Shader shader;
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

        //*
        mat.SetVector("_MieG", new Vector4(1 - (mieG * mieG), 1 + (mieG * mieG), 2 * mieG, 1.0f / (4.0f * Mathf.PI)));
        mat.SetFloat("_MaxLength", maxLength);
        mat.SetFloat("_SampleCount", sampleCount);
        mat.SetFloat("_VolumetricIntensity", volumetricIntensity);

        mat.SetMatrix("_InverseViewProjectionMatrix", inverseViewProjectionMatrix);
        mat.SetVector("_NearLeftBottom", leftBottom);
        mat.SetVector("_NearU", nearU);
        mat.SetVector("_NearV", nearV);
        //*/
    }

    private Vector3 prePos;
    private Quaternion preRot;

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (mat != null)
        {
            mat.SetVector("_MieG", new Vector4(1 - (mieG * mieG), 1 + (mieG * mieG), 2 * mieG, 1.0f / (4.0f * Mathf.PI)));
            mat.SetFloat("_MaxLength", maxLength);
            mat.SetFloat("_SampleCount", sampleCount);
            mat.SetFloat("_VolumetricIntensity", volumetricIntensity);

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

                prePos = transform.localPosition;
                preRot = transform.localRotation;
            //}

            Graphics.Blit(src, dest, mat);
        }
        else
        {
            Graphics.Blit(src, dest);
        }
    }
}

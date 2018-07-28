using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VilunertricLight : MonoBehaviour {

    private Camera cam;

    // Use this for initialization
    void Start () {
        cam = GetComponent<Camera>();

        // ViewProjectionMatrix
        // on some platforms they have to be transformed a bit to match the native API requirements
        Matrix4x4 inverseViewProjectionMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);

        inverseViewProjectionMatrix *= cam.worldToCameraMatrix;
        inverseViewProjectionMatrix = inverseViewProjectionMatrix.inverse;

        // 近裁面的四个顶点, gl中近裁面的z是
        Vector3 leftBottom = inverseViewProjectionMatrix.MultiplyPoint(new Vector3(-1, -1, 1));
        Vector3 rightBottom = inverseViewProjectionMatrix.MultiplyPoint(new Vector3(1, -1, 1));
        Vector3 leftTop = inverseViewProjectionMatrix.MultiplyPoint(new Vector3(-1, 1, 1));
        Vector3 rightTop = inverseViewProjectionMatrix.MultiplyPoint(new Vector3(1, 1, 1));

        // 远裁面的四个顶点，gl中远裁面的z是0
        leftBottom = inverseViewProjectionMatrix.MultiplyPoint(new Vector3(-1, -1, 0));
        rightBottom = inverseViewProjectionMatrix.MultiplyPoint(new Vector3(1, -1, 0));
        leftTop = inverseViewProjectionMatrix.MultiplyPoint(new Vector3(-1, 1, 0));
        rightTop = inverseViewProjectionMatrix.MultiplyPoint(new Vector3(1, 1, 0));

    }
	
	// Update is called once per frame
	void Update () {
		
	}
}

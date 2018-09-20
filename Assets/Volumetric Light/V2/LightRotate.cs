using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightRotate : MonoBehaviour
{
    private Vector3 angle = new Vector3(172.5f, 0, 0);
    private int dir = 1;

	// Update is called once per frame
	void Update ()
    {
        if (angle.y > 30)
            dir = -1;
        else if(angle.y < -50)
            dir = 1;

        angle.y += dir * Time.deltaTime * 10;

        transform.rotation = Quaternion.Euler(angle);
    }
}

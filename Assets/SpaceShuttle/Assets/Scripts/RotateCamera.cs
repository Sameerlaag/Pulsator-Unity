using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateCamera : MonoBehaviour
{
    [SerializeField] private float speedRotate;

    void Update()
    {
        transform.Rotate(0f, speedRotate * Time.deltaTime, 0f);
    }
}

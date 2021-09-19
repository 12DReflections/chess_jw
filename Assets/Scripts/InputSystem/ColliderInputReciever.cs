using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColliderInputReciever : InputReciever
{
    private Vector3 clickPosition;

    private void Update()
    {
        // Shoot ray from camera, if you hit a collider record the hit position
        // and call on input recieved method.

        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit))
            {
                clickPosition = hit.point;
                OnInputRecieved();
            }
        }
    }

    // Call on every handler attached to game object
    public override void OnInputRecieved()
    {
        foreach (var handler in inputHandlers)
        {
            handler.ProcessInput(clickPosition, null, null);
        }
    }
}
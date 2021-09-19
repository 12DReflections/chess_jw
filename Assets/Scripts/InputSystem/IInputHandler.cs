using System;
using UnityEngine;

public interface IInputHandler
{
    // Abstract interface with three key ingredients to handle inputs
    void ProcessInput(Vector3 inputPosition, GameObject selectedObject, Action onClick);
}

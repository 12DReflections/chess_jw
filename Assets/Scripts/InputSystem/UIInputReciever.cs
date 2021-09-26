using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class UIInputReciever : InputReciever
{
    [SerializeField] UnityEvent onClick;

    public override void OnInputRecieved()
    {
        // Reference to call back handler in process input method
        foreach (var handler in inputHandlers)
        {
            handler.ProcessInput(Input.mousePosition, gameObject, () => onClick.Invoke());
        }
    }
}
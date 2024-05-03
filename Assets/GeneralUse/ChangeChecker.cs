using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct ChangeChecker<T>
{

    T prev;

    bool isChanged;

    public ChangeChecker(T original)
    {
        isChanged = false;
        prev = original;
    }

    public void Runner(T original)
    {
        if (!prev.Equals(original))
        {
            isChanged = true;
        }
        else if (isChanged)
        {
            isChanged = false;
        }
        prev = original;
    }

    public bool IsChanged()
    {
        return isChanged;
    }


}

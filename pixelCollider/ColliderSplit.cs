using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColliderSplit : MonoBehaviour {
    public int mySize = 1;
    public int minSize = 1;
    public Vector2 myPos;
    public ColliderHandler handler;
    /// <summary>
    /// splits the collider into 4 smaller ones or destroys it if it's too small
    /// </summary>
    /// <param name="col"></param>
    void OnTriggerEnter2D(Collider2D col) {
        if (col.tag == "Destruction") {
            if (mySize == 1) {
                handler.RemoveCollider(GetComponent<Collider2D>());
            } else if ((mySize & (mySize - 1)) == 0) { //check if mySize is power of two, should be always true
                float newExt = (float)mySize / 4;
                //Debug.Log("newExt=" + newExt);
                handler.SummonCollider(myPos.x + newExt, myPos.y + newExt, mySize/2);
                handler.SummonCollider(myPos.x - newExt, myPos.y - newExt, mySize/2);
                handler.SummonCollider(myPos.x - newExt, myPos.y + newExt, mySize/2);
                handler.SummonCollider(myPos.x + newExt, myPos.y - newExt, mySize/2);
                handler.RemoveCollider(GetComponent<Collider2D>());
            } else {
                Debug.LogError("Invalid collider size: " + mySize);
                throw new System.Exception();
            }
            //Debug.Log("mySize: " + mySize + ", minSize:" + minSize);
        }
    }
}

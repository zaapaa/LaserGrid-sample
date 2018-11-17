using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class ColliderHandler : NetworkBehaviour {

    SpriteRenderer sr;
    private float widthWorld, heightWorld;
    private int widthPixel, heightPixel;
    private int resolutionX, resolutionY;
    private Vector2 pixelSize;
    private Vector2 colliderSize;

    [Range(0f, 255f)]
    public int TransparentThreshold;
    [Range(6f, 8f)]
    public int EdgeNeighbourSolidCountThreshold;
    [Tooltip("Multiplier of minColliderSize for maximum collider size")]
    public int MaxColliderSize;
    [Tooltip("Minimum collider size in pixels")]
    public int MinColliderSize;
    public GameObject ColliderPrefab;
    public Transform ColliderEdgeParent;
    public Transform ColliderInnerParent;

    List<Collider2D> colliders;
    Color32[] pixels;

    struct Pixel {
        public bool solid;
        public bool edge;
        public bool edgeOfTexture;
        public bool done;

        public Pixel(bool solid = false, bool edge = false, bool eod = false, bool done = false) {
            this.solid = solid;
            this.edge = edge;
            edgeOfTexture = eod;
            this.done = done;
        }
    }


    void Awake() {
        sr = GetComponentInParent<SpriteRenderer>();
        colliders = new List<Collider2D>();
    }


    void Start() {
        widthWorld = sr.bounds.size.x;
        heightWorld = sr.bounds.size.y;
        widthPixel = sr.sprite.texture.width;
        heightPixel = sr.sprite.texture.height;
        resolutionX = widthPixel / MinColliderSize;
        resolutionY = heightPixel / MinColliderSize;
        pixelSize = new Vector2(widthWorld / widthPixel, heightWorld / heightPixel);
        colliderSize = pixelSize * MinColliderSize;
        Debug.Log("size in world: " + widthWorld + "x" + heightWorld + ", pixels: " + widthPixel + "x" + heightPixel + ", ppu: " + sr.sprite.pixelsPerUnit);
        Debug.Log("pixel size: " + pixelSize.x + ", colliderSize: " + colliderSize.x);
        pixels = sr.sprite.texture.GetPixels32();
        SpawnColliders();

    }

    /// <summary>
    /// spawns colliders around edges of texture.
    /// 1. Loop through texture and save locations of solid pixels (alpha of color above TransparentThreshold) to 2-dimensional array solidPixels
    /// 2. Loop through solidPixels array and for every solid pixel check 3x3 area around it for neighbouring solid pixels and save the amount of them
    /// 3. if amount below EdgeNeighbourSolidCountThreshold, mark the pixel as edge pixel
    /// 4. spawn colliders at every edge pixel's calculated position in the world
    /// 5. Spawn bigger colliders in the non-edge solid area, up to MaxColliderSize times bigger than normal edge collider until every solid area has a collider
    /// </summary>
    private void SpawnColliders() {

        Debug.Log("pixels: " + pixels.Length + ", widthPixel: " + widthPixel + ", heightPixel:" + heightPixel + ", total pixels: " + widthPixel * heightPixel);
        Pixel[,] solidPixels = new Pixel[resolutionX,resolutionY];
        int solidCount = 0, edgeCount = 0;
        int maxN = int.MinValue, minN = int.MaxValue;

        for (int y = 0; y < heightPixel - MinColliderSize; y+=MinColliderSize) {
            for (int x = 0; x < widthPixel - MinColliderSize; x+=MinColliderSize) {
                if (TestSolidBlockPixel(x,y)){
                    Pixel newPixel = new Pixel(true);
                    
                    solidPixels[x/MinColliderSize, y/MinColliderSize] = newPixel;
                    solidCount++;
                }
            }
        }
        Debug.Log("Solid pixels: " + solidCount);
        for (int y = 0; y < resolutionY - 1; y++) {
            for (int x = 0; x < resolutionX - 1; x++) {
                if (solidPixels[x, y].solid == true) {
                    int neighbourSolidCount = 0;
                    if (y == 0 || y > resolutionX - 2 || x == 0 || x > resolutionY - 2) {
                        solidPixels[x, y].edgeOfTexture = true;
                    }
                    for (int j = y - 1; j <= y + 1 && j >= 0 && j < resolutionY - 1; j++) {
                        for (int i = x - 1; i <= x + 1 && i >= 0 && i < resolutionX - 1; i++) {

                            if (solidPixels[i, j].solid) {
                                neighbourSolidCount++;
                            }
                        }
                    }
                    if (neighbourSolidCount <= EdgeNeighbourSolidCountThreshold) {
                        solidPixels[x, y].edge = true;
                        edgeCount++;
                    }
                    if (neighbourSolidCount < minN) {
                        minN = neighbourSolidCount;
                    }
                    if (neighbourSolidCount > maxN) {
                        maxN = neighbourSolidCount;
                    }
                }
            }
        }
        Debug.Log("Edge Pixels: " + edgeCount + ", min:" + minN + ", max:" + maxN);
        //for (int y = 0; y < resolutionY - 1; y++) {
        //    for (int x = 0; x < resolutionX - 1; x++) {
        //        Pixel p = solidPixels[x, y];
        //        if (p.solid && p.edge && !p.edgeOfTexture) {
        //            //SummonCollider(x, y, 1, ColliderEdgeParent);
        //        }
        //    }
        //}
        for (int y = 0; y < resolutionY - 1; y++) {
            for (int x = 0; x < resolutionX - 1; x++) {
                Pixel p = solidPixels[x, y];
                if (p.solid && !p.edgeOfTexture && !p.done) {
                    CalculateInnerCollider(solidPixels, x, y);
                }
            }
        }
        Debug.Log("Total Colliders: " + colliders.Count);
    }
    /// <summary>
    /// calculate x and y in world space and spawn collider there. after that set the collider size
    /// </summary>
    /// <param name="x">pixel's x position in the sprite texture</param>
    /// <param name="y">pixel's y position in the sprite texture</param>
    /// <param name="size">size of pixel, default 1 for edge colliders, bigger for inner colliders</param>
    [Server]
    public void SummonCollider(float x, float y, int size = 0, Transform parent = null) {
        if (size == 0) {
            //size = MinColliderSize;
            return;
        }
        if (parent == null) {
            parent = ColliderInnerParent;
        }
        float yPos = (2 * (heightWorld * y) - heightPixel / MinColliderSize * heightWorld) / (2 * heightPixel / MinColliderSize) + transform.position.y ;
        float xPos = (2 * (widthWorld * x) - widthPixel/MinColliderSize * widthWorld) / (2 * widthPixel/MinColliderSize) + transform.position.x - colliderSize.x*0.5f;
        yPos = heightWorld - yPos-heightWorld;

        GameObject newCollider = Instantiate(ColliderPrefab, new Vector3(xPos, yPos), Quaternion.identity, parent);
        newCollider.transform.localScale = new Vector3(size * colliderSize.x, size * colliderSize.y);
        newCollider.GetComponent<ColliderSplit>().mySize = size;
        newCollider.GetComponent<ColliderSplit>().minSize = MinColliderSize;
        newCollider.GetComponent<ColliderSplit>().myPos = new Vector2(x, y);
        newCollider.GetComponent<ColliderSplit>().handler = this;
		newCollider.layer = 19;
        colliders.Add(newCollider.GetComponent<Collider2D>());
        NetworkServer.Spawn(newCollider);
        //Debug.Log("Total Colliders: " + colliders.Count);
    }
    public void RemoveCollider(Collider2D col) {
        colliders.Remove(col);
        Destroy(col.gameObject);
    }

    void CalculateInnerCollider(Pixel[,] solidPixels, int xStart, int yStart) {
        float size = MinColliderSize/2;
        bool foundColliderBeforeMax = false;
        int x, y;

        float xCenter, yCenter;

        while (!foundColliderBeforeMax || size <= MaxColliderSize) {
            if (TestSolidBlockCollider(solidPixels, xStart, xStart + (int)size, yStart, yStart + (int)size)) {
                //foundColliderBeforeMax = true;
                size /= 4;
                break;
            } else if (size >= MaxColliderSize) {
                size = MaxColliderSize;
                break;
            }
            size *= 2;
        }

        for (y = yStart; y < yStart + size; y++) {
            for (x = xStart; x < xStart + size; x++) {
                solidPixels[x, y].done = true;
            }
        }
        xCenter = xStart + size / 2 - colliderSize.x * MinColliderSize/2;
        yCenter = yStart + size / 2 - colliderSize.y * MinColliderSize/2;

        SummonCollider(xCenter, yCenter, (int)size, ColliderInnerParent);
    }

    /// <summary>
    /// tests if a pixel already has a collider or is on the edge of texture
    /// </summary>
    /// <param name="p">pixel to be tested</param>
    /// <returns>true if pixel has a collider or is on the edge of texture</returns>

    bool TestSolidBlockCollider(Pixel[,] solidPixels, int xStart, int xEnd, int yStart, int yEnd) {
        int x, y;

        if (xEnd > resolutionX - 1 || yEnd > resolutionY - 1) {
            return true;
        }

        for (y = yStart; y < yEnd; y++) {
            for (x = xStart; x < xEnd; x++) {
                Pixel p = solidPixels[x, y];
                if (p.edge || p.done || p.edgeOfTexture) {
                    return true;
                }
            }
        }

        return false;
    }

    bool TestSolidBlockPixel(int x, int y) {
        float total = 0, sum = 0, avg;
        string debug = "Debug for block: " + x + "-"+y;
        if(x>widthPixel-MinColliderSize || y > heightPixel - MinColliderSize) {
            return false;
        }
        for (int i=x; i < x + MinColliderSize && i < widthPixel - 1; i++) {
            for (int j=y; j < y + MinColliderSize && j < heightPixel - 1; j++) {
                total++;
                sum += pixels[x + widthPixel * y].a;
            }
        }
        avg = sum / total;
        debug += " , total: " + total + ", sum: " + sum + ", avg: " + avg;
        //Debug.Log(debug);
        return avg > TransparentThreshold;
    }
}

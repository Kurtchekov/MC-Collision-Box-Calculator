using System;
using System.Collections.Generic;
using UnityEngine;

class AABBGenerator {

    public volatile int collisions, totalCollisions;
    public List<List<Bounds>> allBounds;
    public List<List<Box>> normalizedBoxes;
    public List<Triangle> triangles;

    public volatile int progress;

    internal float x0, y0, z0;
    internal int width, height, length;
    internal bool invertX, invertY, invertZ;
    internal int acceptableAABB;
    internal Order axisOrder;

    public void Reset(Mesh mesh, bool invertX, bool invertY, bool invertZ, Order axisOrder, int acceptableAABB) {
        this.invertX = invertX;
        this.invertY = invertY;
        this.invertZ = invertZ;
        this.axisOrder = axisOrder;
        this.acceptableAABB = acceptableAABB;

        width = (int)Mathf.Ceil(mesh.bounds.size.x);
        height = (int)Mathf.Ceil(mesh.bounds.size.y);
        length = (int)Mathf.Ceil(mesh.bounds.size.z);

        x0 = invertX ? Mathf.Ceil(mesh.bounds.max.x) - 0.5f : Mathf.Floor(mesh.bounds.min.x) + 0.5f;
        y0 = invertY ? Mathf.Ceil(mesh.bounds.max.y) - 0.5f : Mathf.Floor(mesh.bounds.min.y) + 0.5f;
        z0 = invertZ ? Mathf.Ceil(mesh.bounds.max.z) - 0.5f : Mathf.Floor(mesh.bounds.min.z) + 0.5f;

        triangles = new List<Triangle>();
        int a = 0;
        while(a <= mesh.triangles.Length - 3) {
            triangles.Add(new Triangle(
                mesh.vertices[mesh.triangles[a]],
                mesh.vertices[mesh.triangles[a + 1]],
                mesh.vertices[mesh.triangles[a + 2]]));
            a += 3;
        }
        
        totalCollisions = collisions = 0;
    }

    public List<Bounds> CalculateAllBlocks() {
        List<Bounds> boundsList = new List<Bounds>();
        for(int currentPos = 0; currentPos < width * height * length; currentPos++) {
            Vector3 pos = new Vector3(
                x0 + (RelativeX(currentPos, axisOrder, width, height, length) * (invertX ? -1 : 1)),
                y0 + (RelativeY(currentPos, axisOrder, width, height, length) * (invertY ? -1 : 1)),
                z0 + (RelativeZ(currentPos, axisOrder, width, height, length) * (invertZ ? -1 : 1)));
            Bounds bounds = new Bounds(pos, new Vector3(.9999f, .9999f, .9999f));
            if(IsInside(pos, triangles) || Intersects(triangles, bounds))
                boundsList.Add(new Bounds(pos, new Vector3(1, 1, 1)));
        }
        return boundsList;
    }

    public List<Bounds> CalculateSingle(float x, float y, float z, int precision) {
        if (precision == 1) {
            return IsInside(new Vector3(x, y, z), triangles) || 
                Intersects(triangles, new Bounds(new Vector3(x, y, z), new Vector3(.9999f, .9999f, .9999f))) ? 
                new List<Bounds> { new Bounds(new Vector3(x, y, z), new Vector3(1, 1, 1)) } : new List<Bounds>();
        }
        return BruteForceGenerateAABB(x, y, z, precision, triangles);
    }

    public void CalculateAll() {
        progress = 0;
        allBounds = new List<List<Bounds>>();
        normalizedBoxes = new List<List<Box>>();
        totalCollisions = 0;
        for(int currentPos = 0; currentPos < width * height * length; currentPos++) {
            float x1 = x0 + (RelativeX(currentPos, axisOrder, width, height, length) * (invertX ? -1 : 1));
            float y1 = y0 + (RelativeY(currentPos, axisOrder, width, height, length) * (invertY ? -1 : 1));
            float z1 = z0 + (RelativeZ(currentPos, axisOrder, width, height, length) * (invertZ ? -1 : 1));

            List<Bounds> AABBs = CalculateBlockAABB(x1, y1, z1);
            progress++;
            allBounds.Add(AABBs);
            totalCollisions += AABBs.Count;

            List<Box> boxes = new List<Box>();
            foreach(Bounds aabb in AABBs) {
                boxes.Add(new Box(
                    (byte) ConvertRange(-0.5f, 0.5f, 0, 16, aabb.min.x - x1),
                    (byte) ConvertRange(-0.5f, 0.5f, 0, 16, aabb.min.y - y1),
                    (byte) ConvertRange(-0.5f, 0.5f, 0, 16, aabb.min.z - z1),
                    (byte) ConvertRange(-0.5f, 0.5f, 0, 16, aabb.max.x - x1),
                    (byte) ConvertRange(-0.5f, 0.5f, 0, 16, aabb.max.y - y1),
                    (byte) ConvertRange(-0.5f, 0.5f, 0, 16, aabb.max.z - z1)));
            }
            normalizedBoxes.Add(boxes);
        }
    }

    List<Bounds> CalculateBlockAABB(float x, float y, float z) {
        Bounds bounds = new Bounds(new Vector3(x, y, z), new Vector3(.9999f, .9999f, .9999f));
        if(!IsInside(new Vector3(x, y, z), triangles) && !Intersects(triangles, bounds))
            return new List<Bounds>();
        List<Bounds> uniqueBounds = null;
        List<Bounds> previous = new List<Bounds>() { new Bounds(new Vector3(x, y, z), new Vector3(1, 1, 1)) };
        for(var precision = 2; precision <= 16; precision *= 2) {
            uniqueBounds = CalculateSubBlockAABB(x, y, z, precision);
            if(collisions > acceptableAABB)
                return previous;
            previous = uniqueBounds;
        }
        return uniqueBounds;
    }

    List<Bounds> CalculateSubBlockAABB(float x, float y, float z, int precision) {
        List<Bounds> uniqueBounds = BruteForceGenerateAABB(x, y, z, precision, triangles);

        int collisionsAfter;
        do {
        outer:
            collisions = uniqueBounds.Count;
            foreach(Bounds b0 in uniqueBounds) {
                foreach(Bounds b1 in uniqueBounds) {
                    if(b0 == b1)
                        continue;
                    Bounds temp = new Bounds(b0.center, b0.size);
                    temp.Encapsulate(b1);
                    if(CalculateVolume(temp) == CalculateVolume(b0) + CalculateVolume(b1)) {
                        uniqueBounds.Remove(b1);
                        uniqueBounds.Remove(b0);
                        b0.Encapsulate(b1);
                        uniqueBounds.Add(b0);
                        goto outer;
                    }
                }
            }
            collisionsAfter = uniqueBounds.Count;
        } while(collisionsAfter != collisions);
        collisions = uniqueBounds.Count;

        return uniqueBounds;
    }

    public static List<Bounds> BruteForceGenerateAABB(float x, float y, float z, int precision, List<Triangle> triangles) {
        List<Bounds> uniqueBounds = new List<Bounds>();
        float fullSize = 1f / precision;
        float halfSize = .5f / precision;
        float slightFullSize = .9999f / precision;
        for(int i = -precision / 2; i < precision / 2; i++) {
            for(int j = -precision / 2; j < precision / 2; j++) {
                for(int k = -precision / 2; k < precision / 2; k++) {
                    Vector3 tinyPos = new Vector3(x + i * fullSize + halfSize, y + j * fullSize + halfSize, z + k * fullSize + halfSize);
                    Bounds bounds = new Bounds(tinyPos, new Vector3(slightFullSize, slightFullSize, slightFullSize));
                    if(IsInside(tinyPos, triangles) || Intersects(triangles, bounds))
                        uniqueBounds.Add(new Bounds(tinyPos, new Vector3(fullSize, fullSize, fullSize)));
                    
                }
            }
        }
        return uniqueBounds;
    }

    public static float CalculateVolume(Bounds bounds) {
        return bounds.size.x * bounds.size.y * bounds.size.z;
    }

    public static bool IsInside(Vector3 pos, List<Triangle> triangles) {
        List<Vector3> points = new List<Vector3>();
        Vector3 point;
        int up = 0;
        foreach(Triangle tri in triangles) {
            if(Intersect3D_RayTriangle(pos, Vector3.up, tri, out point) == 1) {
                if(!points.Contains(point)) {
                    points.Add(point);
                    up++;
                }
            }
        }
        return up % 2 == 1;
    }

    public static int RelativeX(int position, Order axisOrder, Vector3 size) {
        return RelativeX(position, axisOrder, (int)size.x, (int)size.y, (int)size.z);
    }

    public static int RelativeX(int position, Order axisOrder, int width, int height, int length) {
        switch(axisOrder) {
            case Order.XYZ:
                return position % width;
            case Order.XZY:
                return position % width;
            case Order.YXZ:
                return (position - (position % height)) / width % width;
            case Order.ZXY:
                return (position - (position % length)) / width % width;
            case Order.YZX:
                return position / (height * length);
            case Order.ZYX:
                return position / (height * length);
        }
        return 0;
    }

    public static int RelativeY(int position, Order axisOrder, Vector3 size) {
        return RelativeY(position, axisOrder, (int)size.x, (int)size.y, (int)size.z);
    }

    public static int RelativeY(int position, Order axisOrder, int width, int height, int length) {
        switch(axisOrder) {
            case Order.XYZ:
                return (position - (position % width)) / height % height;
            case Order.XZY:
                return position / (width * length);
            case Order.YXZ:
                return position % height;
            case Order.ZXY:
                return position / (length * width);
            case Order.YZX:
                return position % height;
            case Order.ZYX:
                return (position - (position % length)) / height % height;
        }
        return 0;
    }

    public static int RelativeZ(int position, Order axisOrder, Vector3 size) {
        return RelativeZ(position, axisOrder, (int)size.x, (int)size.y, (int)size.z);
    }

    public static int RelativeZ(int position, Order axisOrder, int width, int height, int length) {
        switch(axisOrder) {
            case Order.XYZ:
                return position / (width * height);
            case Order.XZY:
                return (position - (position % width)) / length % length;
            case Order.YXZ:
                return position / (height * width);
            case Order.ZXY:
                return position % length;
            case Order.YZX:
                return (position - (position % height)) / length % length;
            case Order.ZYX:
                return position % length;
        }
        return 0;
    }

    public static bool Intersects(List<Triangle> triangles, Bounds aabb) {
        foreach(Triangle tri in triangles) {
            if(Intersects(tri, aabb))
                return true;
        }
        return false;
    }

    public static bool Intersects(Triangle tri, Bounds aabb) {
        float p0, p1, p2, r;

        Vector3 extents = aabb.max - aabb.center;

        Vector3 v0 = tri.a - aabb.center,
            v1 = tri.b - aabb.center,
            v2 = tri.c - aabb.center;

        Vector3 f0 = v1 - v0,
            f1 = v2 - v1,
            f2 = v0 - v2;

        Vector3 a00 = new Vector3(0, -f0.z, f0.y),
            a01 = new Vector3(0, -f1.z, f1.y),
            a02 = new Vector3(0, -f2.z, f2.y),
            a10 = new Vector3(f0.z, 0, -f0.x),
            a11 = new Vector3(f1.z, 0, -f1.x),
            a12 = new Vector3(f2.z, 0, -f2.x),
            a20 = new Vector3(-f0.y, f0.x, 0),
            a21 = new Vector3(-f1.y, f1.x, 0),
            a22 = new Vector3(-f2.y, f2.x, 0);

        // Test axis a00
        p0 = Vector3.Dot(v0, a00);
        p1 = Vector3.Dot(v1, a00);
        p2 = Vector3.Dot(v2, a00);
        r = extents.y * Mathf.Abs(f0.z) + extents.z * Mathf.Abs(f0.y);

        if(Mathf.Max(-Mathf.Max(p0, p1, p2), Mathf.Min(p0, p1, p2)) > r) {
            return false;
        }

        // Test axis a01
        p0 = Vector3.Dot(v0, a01);
        p1 = Vector3.Dot(v1, a01);
        p2 = Vector3.Dot(v2, a01);
        r = extents.y * Mathf.Abs(f1.z) + extents.z * Mathf.Abs(f1.y);

        if(Mathf.Max(-Mathf.Max(p0, p1, p2), Mathf.Min(p0, p1, p2)) > r) {
            return false;
        }

        // Test axis a02
        p0 = Vector3.Dot(v0, a02);
        p1 = Vector3.Dot(v1, a02);
        p2 = Vector3.Dot(v2, a02);
        r = extents.y * Mathf.Abs(f2.z) + extents.z * Mathf.Abs(f2.y);

        if(Mathf.Max(-Mathf.Max(p0, p1, p2), Mathf.Min(p0, p1, p2)) > r) {
            return false;
        }

        // Test axis a10
        p0 = Vector3.Dot(v0, a10);
        p1 = Vector3.Dot(v1, a10);
        p2 = Vector3.Dot(v2, a10);
        r = extents.x * Mathf.Abs(f0.z) + extents.z * Mathf.Abs(f0.x);
        if(Mathf.Max(-Mathf.Max(p0, p1, p2), Mathf.Min(p0, p1, p2)) > r) {
            return false;
        }

        // Test axis a11
        p0 = Vector3.Dot(v0, a11);
        p1 = Vector3.Dot(v1, a11);
        p2 = Vector3.Dot(v2, a11);
        r = extents.x * Mathf.Abs(f1.z) + extents.z * Mathf.Abs(f1.x);

        if(Mathf.Max(-Mathf.Max(p0, p1, p2), Mathf.Min(p0, p1, p2)) > r) {
            return false;
        }

        // Test axis a12
        p0 = Vector3.Dot(v0, a12);
        p1 = Vector3.Dot(v1, a12);
        p2 = Vector3.Dot(v2, a12);
        r = extents.x * Mathf.Abs(f2.z) + extents.z * Mathf.Abs(f2.x);

        if(Mathf.Max(-Mathf.Max(p0, p1, p2), Mathf.Min(p0, p1, p2)) > r) {
            return false;
        }

        // Test axis a20
        p0 = Vector3.Dot(v0, a20);
        p1 = Vector3.Dot(v1, a20);
        p2 = Vector3.Dot(v2, a20);
        r = extents.x * Mathf.Abs(f0.y) + extents.y * Mathf.Abs(f0.x);

        if(Mathf.Max(-Mathf.Max(p0, p1, p2), Mathf.Min(p0, p1, p2)) > r) {
            return false;
        }

        // Test axis a21
        p0 = Vector3.Dot(v0, a21);
        p1 = Vector3.Dot(v1, a21);
        p2 = Vector3.Dot(v2, a21);
        r = extents.x * Mathf.Abs(f1.y) + extents.y * Mathf.Abs(f1.x);

        if(Mathf.Max(-Mathf.Max(p0, p1, p2), Mathf.Min(p0, p1, p2)) > r) {
            return false;
        }

        // Test axis a22
        p0 = Vector3.Dot(v0, a22);
        p1 = Vector3.Dot(v1, a22);
        p2 = Vector3.Dot(v2, a22);
        r = extents.x * Mathf.Abs(f2.y) + extents.y * Mathf.Abs(f2.x);

        if(Mathf.Max(-Mathf.Max(p0, p1, p2), Mathf.Min(p0, p1, p2)) > r) {
            return false;
        }

        if(Mathf.Max(v0.x, v1.x, v2.x) < -extents.x || Mathf.Min(v0.x, v1.x, v2.x) > extents.x) {
            return false;
        }

        if(Mathf.Max(v0.y, v1.y, v2.y) < -extents.y || Mathf.Min(v0.y, v1.y, v2.y) > extents.y) {
            return false;
        }

        if(Mathf.Max(v0.z, v1.z, v2.z) < -extents.z || Mathf.Min(v0.z, v1.z, v2.z) > extents.z) {
            return false;
        }

        var normal = Vector3.Cross(f1, f0).normalized;
        var pl = new Plane(normal, Vector3.Dot(normal, tri.a));
        return Intersects(pl, aabb);
    }

    public static bool Intersects(Plane pl, Bounds aabb) {
        Vector3 center = aabb.center;
        var extents = aabb.max - center;

        var r = extents.x * Mathf.Abs(pl.normal.x) + extents.y * Mathf.Abs(pl.normal.y) + extents.z * Mathf.Abs(pl.normal.z);
        var s = Vector3.Dot(pl.normal, center) - pl.distance;

        return Mathf.Abs(s) <= r;
    }

    public static bool Intersects(Vector3 rayOrigin, Vector3 rayVector, Triangle tri) {
        double EPSILON = 0.0000001;
        Vector3 edge1 = tri.b - tri.a;
        Vector3 edge2 = tri.c - tri.a;
        Vector3 h = Vector3.Cross(rayVector, edge2);
        double a = Vector3.Dot(edge1, h);
        if(a > -EPSILON && a < EPSILON) return false;
        double f = 1.0 / a;
        Vector3 s = rayOrigin - tri.a;
        double u = f * (Vector3.Dot(s,h));
        if(u < 0.0 || u > 1.0) return false;
        Vector3 q = Vector3.Cross(s, edge1);
        double v = f * Vector3.Dot(rayVector, q);
        if(v < 0.0 || u + v > 1.0) return false;
        double t = f * Vector3.Dot(edge2, q);
        return (t > EPSILON);
    }

    public static int Intersect3D_RayTriangle(Vector3 origin, Vector3 direction, Triangle T, out Vector3 I) {
        I = Vector3.zero;
        double EPSILON = 0.0000001;
        Vector3 u, v, n;              // triangle vectors
        Vector3 dir, w0, w;           // ray vectors
        float r, a, b;              // params to calc ray-plane intersect

        // get triangle edge vectors and plane normal
        u = T.b - T.a;
        v = T.c - T.a;
        n = Vector3.Cross(u, v);              // cross product
        if(n == Vector3.zero)             // triangle is degenerate
            return -1;                  // do not deal with this case

        dir = direction;              // ray direction vector
        w0 = origin - T.a;
        a = -Vector3.Dot(n, w0);
        b = Vector3.Dot(n, dir);
        if(Math.Abs(b) < EPSILON) {     // ray is  parallel to triangle plane
            if(a == 0)                 // ray lies in triangle plane
                return 2;
            else
                return 0;              // ray disjoint from plane
        }

        // get intersect point of ray with triangle plane
        r = a / b;
        if(r < 0.0)                    // ray goes away from triangle
            return 0;                   // => no intersect
                                        // for a segment, also test if (r > 1.0) => no intersect

        I = origin + r * dir;            // intersect point of ray and plane

        // is I inside T?
        float uu, uv, vv, wu, wv, D;
        uu = Vector3.Dot(u, u);
        uv = Vector3.Dot(u, v);
        vv = Vector3.Dot(v, v);
        w = I - T.a;
        wu = Vector3.Dot(w, u);
        wv = Vector3.Dot(w, v);
        D = uv * uv - uu * vv;

        // get and test parametric coords
        float s, t;
        s = (uv * wv - vv * wu) / D;
        if(s < 0.0 || s > 1.0)         // I is outside T
            return 0;
        t = (uv * wu - uu * wv) / D;
        if(t < 0.0 || (s + t) > 1.0)  // I is outside T
            return 0;

        return 1;                       // I is in T
    }

    public static float ConvertRange(
        float originalStart, float originalEnd, // original range
        float newStart, float newEnd, // desired range
        float value) {
        float scale = (newEnd - newStart) / (originalEnd - originalStart);
        return (newStart + ((value - originalStart) * scale));
    }

    public struct Triangle {
        public Vector3 a, b, c;
        public Triangle(Vector3 a, Vector3 b, Vector3 c) {
            this.a = a;
            this.b = b;
            this.c = c;
        }
    }
}

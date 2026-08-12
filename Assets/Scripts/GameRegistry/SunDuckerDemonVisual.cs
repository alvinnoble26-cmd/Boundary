using UnityEngine;

public static class SunDuckerDemonVisual
{
    public static void Build(Transform root, int layer = 0)
    {
        if (root == null || root.Find("DemonDetails") != null) return;

        DisableOldPart(root, "Sphere");
        DisableOldPart(root, "Sphere (1)");
        DisableOldPart(root, "sword");
        DisableOldPart(root, "Cube");
        DisableOldPart(root, "Cube (1)");
        DisableOldPart(root, "Cube (2)");
        DisableOldPart(root, "Top");
        DisableOldPart(root, "Top Middle");
        DisableOldPart(root, "Top Crown");

        Transform details = new GameObject("DemonDetails").transform;
        details.SetParent(root, false);
        details.gameObject.layer = layer;

        Material skin = Material("Storm Demon Skin", new Color(.58f, .56f, .52f), .48f, .02f);
        Material hair = Material("Layered Black Hair", new Color(.012f, .014f, .02f), .38f, .08f);
        Material eye = Material("Storm Green Eye", new Color(.08f, .9f, .35f), .3f, .08f, true);
        Material pupil = Material("Eye Symbols", new Color(.005f, .002f, .003f), .15f, .1f);
        Material eyeMarking = Material("Black Eye Marking", new Color(.008f, .006f, .012f), .22f, .04f);
        Material tattoo = Material("Black Facial Tattoos", new Color(.012f, .006f, .015f), .28f, .05f);
        Material robe = Material("Black Storm Robe", new Color(.012f, .014f, .022f), .3f, .06f);
        Material whiteCloth = Material("White Robe Trim", new Color(.86f, .85f, .82f), .42f, .02f);
        Material teal = Material("Teal Neck Cord", new Color(.02f, .58f, .62f), .5f, .18f);
        Material rope = Material("Blue Rope Belt", new Color(.16f, .3f, .67f), .44f, .08f);
        Material clothingGold = Material("Gold Robe Motifs", new Color(.94f, .56f, .04f), .52f, .2f);
        Material blade = Material("Storm Katana Blade", new Color(.56f, .62f, .68f), .86f, .8f);
        Material bladeEdge = Material("Storm Katana Edge", new Color(.72f, .78f, .84f), .92f, .9f);
        Material grip = Material("Katana Black Wrap", new Color(.015f, .017f, .022f), .42f, .12f);
        Material gold = Material("Katana Yellow Wrap", new Color(.95f, .68f, .06f), .62f, .45f);

        Renderer bodyRenderer = root.GetComponent<Renderer>();
        if (bodyRenderer == null && root.Find("Body") != null)
            bodyRenderer = root.Find("Body").GetComponent<Renderer>();
        if (bodyRenderer != null) bodyRenderer.sharedMaterial = skin;

        BuildHair(details, hair, layer);
        BuildEyes(details, eye, pupil, eyeMarking, layer);
        BuildTattoos(details, tattoo, layer);
        BuildClothes(details, robe, whiteCloth, teal, rope, clothingGold, layer);
        BuildSword(details, blade, bladeEdge, grip, gold, layer);
    }

    private static void BuildHair(Transform parent, Material material, int layer)
    {
        Transform crest = Child("Layered Black Storm Hair", parent, Vector3.zero, Vector3.zero, layer);
        Vector3[] positions =
        {
            new Vector3(0f, 1.02f, .03f), new Vector3(0f, .97f, -.17f),
            new Vector3(0f, .89f, -.34f), new Vector3(-.2f, .98f, -.04f),
            new Vector3(.2f, .98f, -.04f), new Vector3(-.35f, .83f, .02f),
            new Vector3(.35f, .83f, .02f), new Vector3(-.42f, .65f, -.04f),
            new Vector3(.42f, .65f, -.04f)
        };
        Vector3[] rotations =
        {
            new Vector3(-8, 0, 0), new Vector3(-22, 0, 0), new Vector3(-38, 0, 0),
            new Vector3(-12, 0, 24), new Vector3(-12, 0, -24),
            new Vector3(-8, 0, 52), new Vector3(-8, 0, -52),
            new Vector3(3, 0, 72), new Vector3(3, 0, -72)
        };
        for (int i = 0; i < positions.Length; i++)
            Cone("Layered Hair Spike " + (i + 1), crest, positions[i],
                i > 4 ? new Vector3(.14f, .38f, .14f) : new Vector3(.18f, .42f, .18f),
                rotations[i], material, layer, 9);

        // Curved layered locks form the visible hairstyle. Unlike the crown
        // spikes, these pieces widen at the root and droop around the face.
        HairLock("Left Temple Lock", crest, new Vector3(-.37f, .88f, .34f),
            new Vector2(-.17f, -.48f), .16f, material, layer);
        HairLock("Right Temple Lock", crest, new Vector3(.37f, .88f, .34f),
            new Vector2(.17f, -.48f), .16f, material, layer);
        HairLock("Left Long Side Lock", crest, new Vector3(-.48f, .82f, .12f),
            new Vector2(.08f, -.68f), .19f, material, layer);
        HairLock("Right Long Side Lock", crest, new Vector3(.48f, .82f, .12f),
            new Vector2(-.08f, -.68f), .19f, material, layer);
        HairLock("Left Rear Lock", crest, new Vector3(-.42f, .86f, -.22f),
            new Vector2(-.08f, -.62f), .2f, material, layer);
        HairLock("Right Rear Lock", crest, new Vector3(.42f, .86f, -.22f),
            new Vector2(.08f, -.62f), .2f, material, layer);
        HairLock("Center Fringe", crest, new Vector3(0f, 1.02f, .43f),
            new Vector2(-.09f, -.38f), .18f, material, layer);
        HairLock("Left Fringe", crest, new Vector3(-.18f, 1f, .42f),
            new Vector2(-.12f, -.34f), .16f, material, layer);
        HairLock("Right Fringe", crest, new Vector3(.18f, 1f, .42f),
            new Vector2(.1f, -.33f), .16f, material, layer);
    }

    private static void BuildEyes(Transform parent, Material eyeMaterial, Material pupilMaterial,
        Material markingMaterial, int layer)
    {
        Primitive("Left Sharp Eye Marking", PrimitiveType.Cube, parent,
            new Vector3(-.205f, .51f, .472f), new Vector3(.32f, .16f, .018f),
            new Vector3(0f, 0f, -12f), markingMaterial, layer);
        Primitive("Right Sharp Eye Marking", PrimitiveType.Cube, parent,
            new Vector3(.205f, .51f, .472f), new Vector3(.32f, .16f, .018f),
            new Vector3(0f, 0f, 12f), markingMaterial, layer);
        Vector3[] eyePositions = { new Vector3(-.2f, .5f, .455f), new Vector3(.2f, .5f, .455f) };
        for (int i = 0; i < eyePositions.Length; i++)
        {
            Transform eye = Primitive(i == 0 ? "Left Yellow Eye" : "Right Yellow Eye",
                PrimitiveType.Sphere, parent, eyePositions[i] + Vector3.forward * .04f,
                new Vector3(.13f, .095f, .065f),
                Vector3.zero, eyeMaterial, layer).transform;
            BuildEyeSymbol(eye, i == 0, pupilMaterial, layer);
        }
    }

    private static void BuildTattoos(Transform parent, Material material, int layer)
    {
        Vector2[] left =
        {
            new Vector2(-.42f, .36f), new Vector2(-.29f, .28f), new Vector2(-.39f, .17f),
            new Vector2(-.22f, .08f), new Vector2(-.32f, -.04f)
        };
        Vector2[] right =
        {
            new Vector2(.42f, .36f), new Vector2(.29f, .28f), new Vector2(.39f, .17f),
            new Vector2(.22f, .08f), new Vector2(.32f, -.04f)
        };
        LightningMark("Left Black Lightning Tattoo", parent, left, material, layer);
        LightningMark("Right Black Lightning Tattoo", parent, right, material, layer);
        LightningMark("Forehead Black Mark", parent, new[]
        {
            new Vector2(-.13f, .72f), new Vector2(0f, .62f), new Vector2(.12f, .73f)
        }, material, layer);
        LightningMark("Left Lower Face Mark", parent, new[]
        {
            new Vector2(-.46f, .08f), new Vector2(-.35f, -.08f),
            new Vector2(-.44f, -.22f), new Vector2(-.27f, -.32f)
        }, material, layer);
        LightningMark("Right Lower Face Mark", parent, new[]
        {
            new Vector2(.46f, .08f), new Vector2(.35f, -.08f),
            new Vector2(.44f, -.22f), new Vector2(.27f, -.32f)
        }, material, layer);
        LightningMark("Left Brow Mark", parent, new[]
        {
            new Vector2(-.43f, .64f), new Vector2(-.28f, .7f), new Vector2(-.16f, .64f)
        }, material, layer);
        LightningMark("Right Brow Mark", parent, new[]
        {
            new Vector2(.43f, .64f), new Vector2(.28f, .7f), new Vector2(.16f, .64f)
        }, material, layer);
    }

    private static void BuildClothes(Transform parent, Material robe, Material whiteCloth,
        Material teal, Material rope, Material gold, int layer)
    {
        Transform clothes = Child("Storm Swordsman Clothing", parent, Vector3.zero, Vector3.zero, layer);

        Primitive("Black Robe Torso", PrimitiveType.Cylinder, clothes,
            new Vector3(0f, -.34f, 0f), new Vector3(.52f, .43f, .52f),
            Vector3.zero, robe, layer);
        Primitive("Black Robe Skirt", PrimitiveType.Cylinder, clothes,
            new Vector3(0f, -.77f, 0f), new Vector3(.56f, .3f, .56f),
            Vector3.zero, robe, layer);
        Primitive("Left Wide Sleeve", PrimitiveType.Capsule, clothes,
            new Vector3(-.54f, -.27f, -.02f), new Vector3(.28f, .55f, .24f),
            new Vector3(0f, 0f, -18f), robe, layer);
        Primitive("Right Wide Sleeve", PrimitiveType.Capsule, clothes,
            new Vector3(.54f, -.27f, -.02f), new Vector3(.28f, .55f, .24f),
            new Vector3(0f, 0f, 18f), robe, layer);

        Primitive("Left White Lapel", PrimitiveType.Cube, clothes,
            new Vector3(-.2f, .08f, .505f), new Vector3(.16f, .68f, .035f),
            new Vector3(0f, 0f, -24f), whiteCloth, layer);
        Primitive("Right White Lapel", PrimitiveType.Cube, clothes,
            new Vector3(.2f, .08f, .505f), new Vector3(.16f, .68f, .035f),
            new Vector3(0f, 0f, 24f), whiteCloth, layer);
        Primitive("White Lower Sash", PrimitiveType.Cube, clothes,
            new Vector3(0f, -.25f, .535f), new Vector3(.58f, .13f, .035f),
            Vector3.zero, whiteCloth, layer);

        Torus("Teal Neck Cord", clothes, new Vector3(0f, .29f, .52f),
            new Vector3(.34f, .18f, .06f), teal, layer, 18, 6);
        Primitive("Teal Cord Drop Left", PrimitiveType.Cylinder, clothes,
            new Vector3(-.08f, .05f, .56f), new Vector3(.025f, .22f, .025f),
            new Vector3(0f, 0f, -12f), teal, layer);
        Primitive("Teal Cord Drop Right", PrimitiveType.Cylinder, clothes,
            new Vector3(.08f, .05f, .56f), new Vector3(.025f, .22f, .025f),
            new Vector3(0f, 0f, 12f), teal, layer);

        Torus("Blue Rope Belt", clothes, new Vector3(0f, -.48f, 0f),
            new Vector3(.58f, .13f, .58f), rope, layer, 22, 7);
        Primitive("Blue Rope Drop Left", PrimitiveType.Cylinder, clothes,
            new Vector3(-.13f, -.79f, .48f), new Vector3(.075f, .34f, .075f),
            new Vector3(0f, 0f, -7f), rope, layer);
        Primitive("Blue Rope Drop Right", PrimitiveType.Cylinder, clothes,
            new Vector3(.13f, -.79f, .48f), new Vector3(.075f, .34f, .075f),
            new Vector3(0f, 0f, 7f), rope, layer);

        Primitive("Left Gold Chest Motif", PrimitiveType.Sphere, clothes,
            new Vector3(-.27f, -.08f, .57f), new Vector3(.11f, .2f, .035f),
            new Vector3(0f, 0f, -25f), gold, layer);
        Primitive("Right Gold Chest Motif", PrimitiveType.Sphere, clothes,
            new Vector3(.27f, -.08f, .57f), new Vector3(.11f, .2f, .035f),
            new Vector3(0f, 0f, 25f), gold, layer);
        Primitive("Upper Robe Button", PrimitiveType.Sphere, clothes,
            new Vector3(0f, -.02f, .575f), Vector3.one * .065f,
            Vector3.zero, whiteCloth, layer);
        Primitive("Lower Robe Button", PrimitiveType.Sphere, clothes,
            new Vector3(0f, -.29f, .575f), Vector3.one * .065f,
            Vector3.zero, whiteCloth, layer);
    }

    private static void LightningMark(string name, Transform parent, Vector2[] points,
        Material material, int layer)
    {
        Transform mark = Child(name, parent, Vector3.zero, Vector3.zero, layer);
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector2 delta = points[i + 1] - points[i];
            Vector2 midpoint = (points[i] + points[i + 1]) * .5f;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            Primitive("Tattoo Stroke " + i, PrimitiveType.Cube, mark,
                new Vector3(midpoint.x, midpoint.y, .515f),
                new Vector3(delta.magnitude, .045f, .012f),
                new Vector3(0f, 0f, angle), material, layer);
        }
    }

    private static void BuildSword(Transform parent, Material blade, Material edge,
        Material grip, Material gold, int layer)
    {
        Transform sword = Child("Storm Pattern Katana", parent,
            new Vector3(.05f, .08f, -.59f), new Vector3(6f, 0f, -34f), layer);

        Primitive("Slim Katana Blade", PrimitiveType.Cube, sword, new Vector3(0f, .42f, 0f),
            new Vector3(.105f, 1.42f, .045f), Vector3.zero, blade, layer);
        Primitive("Bright Katana Edge", PrimitiveType.Cube, sword, new Vector3(-.06f, .42f, -.028f),
            new Vector3(.018f, 1.43f, .012f), Vector3.zero, edge, layer);
        Cone("Katana Point", sword, new Vector3(0f, 1.2f, 0f),
            new Vector3(.115f, .28f, .055f), Vector3.zero, blade, layer, 4);

        Primitive("Round Katana Guard", PrimitiveType.Cylinder, sword, new Vector3(0f, -.33f, 0f),
            new Vector3(.27f, .035f, .27f), new Vector3(90f, 0f, 0f), gold, layer);

        Primitive("Long Black Handle", PrimitiveType.Cylinder, sword, new Vector3(0f, -.64f, 0f),
            new Vector3(.085f, .3f, .085f), Vector3.zero, grip, layer);
        for (int i = 0; i < 5; i++)
            Primitive("Yellow Diamond Wrap " + i, PrimitiveType.Cube, sword,
                new Vector3(0f, -.43f - i * .105f, -.09f), new Vector3(.105f, .065f, .018f),
                new Vector3(0f, 0f, 45f), gold, layer);
        Primitive("Katana Pommel", PrimitiveType.Cylinder, sword, new Vector3(0f, -.96f, 0f),
            new Vector3(.11f, .055f, .11f), Vector3.zero, gold, layer);
    }

    private static GameObject Star(string name, Transform parent, Vector3 localPosition,
        float size, Material material, int layer)
    {
        GameObject star = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        star.layer = layer;
        star.transform.SetParent(parent, false);
        star.transform.localPosition = localPosition;
        star.transform.localScale = Vector3.one * size;

        const int points = 5;
        Vector3[] vertices = new Vector3[points * 2 + 1];
        vertices[0] = Vector3.zero;
        for (int i = 0; i < points * 2; i++)
        {
            float radius = i % 2 == 0 ? .5f : .21f;
            float angle = Mathf.Deg2Rad * (90f + i * 180f / points);
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }
        int[] triangles = new int[points * 2 * 3];
        for (int i = 0; i < points * 2; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % (points * 2) + 1;
        }
        Mesh mesh = new Mesh { name = "Five Point Star" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        star.GetComponent<MeshFilter>().sharedMesh = mesh;
        star.GetComponent<MeshRenderer>().sharedMaterial = material;
        return star;
    }

    private static void HairLock(string name, Transform parent, Vector3 position,
        Vector2 end, float width, Material material, int layer)
    {
        const int segments = 6;
        GameObject hair = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        hair.layer = layer;
        hair.transform.SetParent(parent, false);
        hair.transform.localPosition = position;
        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float curve = Mathf.Sin(t * Mathf.PI) * (end.x >= 0f ? .08f : -.08f);
            Vector2 center = new Vector2(end.x * t + curve, end.y * t);
            float halfWidth = Mathf.Lerp(width * .5f, .018f, t);
            vertices[i * 2] = new Vector3(center.x - halfWidth, center.y, 0f);
            vertices[i * 2 + 1] = new Vector3(center.x + halfWidth, center.y, 0f);
        }
        int[] triangles = new int[segments * 12];
        for (int i = 0; i < segments; i++)
        {
            int v = i * 2;
            int t = i * 12;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v + 1; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            triangles[t + 6] = v + 2; triangles[t + 7] = v + 1; triangles[t + 8] = v;
            triangles[t + 9] = v + 2; triangles[t + 10] = v + 3; triangles[t + 11] = v + 1;
        }
        Mesh mesh = new Mesh { name = name + " Mesh", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        hair.GetComponent<MeshFilter>().sharedMesh = mesh;
        hair.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void BuildEyeSymbol(Transform eye, bool left, Material material, int layer)
    {
        Transform symbol = Child(left ? "Left Rank Symbol" : "Right Rank Symbol",
            eye, new Vector3(0f, 0f, .54f), Vector3.zero, layer);
        if (left)
        {
            SymbolStroke(symbol, new Vector2(-.31f, .34f), new Vector2(-.31f, -.34f), material, layer);
            SymbolStroke(symbol, new Vector2(-.31f, .3f), new Vector2(-.05f, .3f), material, layer);
            SymbolStroke(symbol, new Vector2(-.05f, .3f), new Vector2(-.05f, -.05f), material, layer);
            SymbolStroke(symbol, new Vector2(-.31f, .08f), new Vector2(-.08f, .08f), material, layer);
            SymbolStroke(symbol, new Vector2(-.28f, -.08f), new Vector2(-.08f, -.28f), material, layer);
            SymbolStroke(symbol, new Vector2(.05f, .25f), new Vector2(.32f, .25f), material, layer);
            SymbolStroke(symbol, new Vector2(.18f, .34f), new Vector2(.18f, -.32f), material, layer);
            SymbolStroke(symbol, new Vector2(.03f, .02f), new Vector2(.34f, .02f), material, layer);
            SymbolStroke(symbol, new Vector2(.05f, -.28f), new Vector2(.33f, -.28f), material, layer);
        }
        else
        {
            SymbolStroke(symbol, new Vector2(-.29f, .34f), new Vector2(-.29f, -.34f), material, layer);
            SymbolStroke(symbol, new Vector2(-.29f, .29f), new Vector2(-.04f, .29f), material, layer);
            SymbolStroke(symbol, new Vector2(-.29f, .04f), new Vector2(-.07f, .04f), material, layer);
            SymbolStroke(symbol, new Vector2(-.29f, -.25f), new Vector2(-.04f, -.25f), material, layer);
            SymbolStroke(symbol, new Vector2(.06f, .31f), new Vector2(.06f, -.31f), material, layer);
            SymbolStroke(symbol, new Vector2(.06f, .26f), new Vector2(.33f, .26f), material, layer);
            SymbolStroke(symbol, new Vector2(.06f, .02f), new Vector2(.31f, .02f), material, layer);
            SymbolStroke(symbol, new Vector2(.06f, -.28f), new Vector2(.34f, -.28f), material, layer);
            SymbolStroke(symbol, new Vector2(.23f, .25f), new Vector2(.23f, -.27f), material, layer);
        }
    }

    private static void SymbolStroke(Transform parent, Vector2 start, Vector2 end,
        Material material, int layer)
    {
        Vector2 delta = end - start;
        Vector2 midpoint = (start + end) * .5f;
        Primitive("Symbol Stroke", PrimitiveType.Cube, parent,
            new Vector3(midpoint.x, midpoint.y, 0f), new Vector3(delta.magnitude, .055f, .025f),
            new Vector3(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg), material, layer);
    }

    private static void Torus(string name, Transform parent, Vector3 position, Vector3 scale,
        Material material, int layer, int majorSegments, int minorSegments)
    {
        GameObject torus = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        torus.layer = layer;
        torus.transform.SetParent(parent, false);
        torus.transform.localPosition = position;
        torus.transform.localScale = scale;
        bool horizontal = name.Contains("Belt");
        Vector3[] vertices = new Vector3[majorSegments * minorSegments];
        int[] triangles = new int[majorSegments * minorSegments * 6];
        for (int major = 0; major < majorSegments; major++)
        {
            float a = Mathf.PI * 2f * major / majorSegments;
            for (int minor = 0; minor < minorSegments; minor++)
            {
                float b = Mathf.PI * 2f * minor / minorSegments;
                float radial = .5f + .12f * Mathf.Cos(b);
                Vector3 value = horizontal
                    ? new Vector3(Mathf.Cos(a) * radial, .12f * Mathf.Sin(b), Mathf.Sin(a) * radial)
                    : new Vector3(Mathf.Cos(a) * radial, Mathf.Sin(a) * radial, .12f * Mathf.Sin(b));
                vertices[major * minorSegments + minor] = value;
                int nextMajor = (major + 1) % majorSegments;
                int nextMinor = (minor + 1) % minorSegments;
                int t = (major * minorSegments + minor) * 6;
                int current = major * minorSegments + minor;
                int right = nextMajor * minorSegments + minor;
                int up = major * minorSegments + nextMinor;
                int diagonal = nextMajor * minorSegments + nextMinor;
                triangles[t] = current; triangles[t + 1] = right; triangles[t + 2] = up;
                triangles[t + 3] = right; triangles[t + 4] = diagonal; triangles[t + 5] = up;
            }
        }
        Mesh mesh = new Mesh { name = name + " Mesh", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        torus.GetComponent<MeshFilter>().sharedMesh = mesh;
        torus.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static GameObject Cone(string name, Transform parent, Vector3 position,
        Vector3 scale, Vector3 euler, Material material, int layer, int sides)
    {
        GameObject cone = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        cone.layer = layer;
        cone.transform.SetParent(parent, false);
        cone.transform.localPosition = position;
        cone.transform.localScale = scale;
        cone.transform.localEulerAngles = euler;

        Vector3[] vertices = new Vector3[sides + 2];
        vertices[0] = new Vector3(0f, .5f, 0f);
        vertices[1] = new Vector3(0f, -.5f, 0f);
        for (int i = 0; i < sides; i++)
        {
            float angle = Mathf.PI * 2f * i / sides;
            vertices[i + 2] = new Vector3(Mathf.Cos(angle) * .5f, -.5f, Mathf.Sin(angle) * .5f);
        }
        int[] triangles = new int[sides * 6];
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            int t = i * 6;
            triangles[t] = 0; triangles[t + 1] = i + 2; triangles[t + 2] = next + 2;
            triangles[t + 3] = 1; triangles[t + 4] = next + 2; triangles[t + 5] = i + 2;
        }
        Mesh mesh = new Mesh { name = name + " Mesh", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        cone.GetComponent<MeshFilter>().sharedMesh = mesh;
        cone.GetComponent<MeshRenderer>().sharedMaterial = material;
        return cone;
    }

    private static GameObject Primitive(string name, PrimitiveType type, Transform parent,
        Vector3 position, Vector3 scale, Vector3 euler, Material material, int layer)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.layer = layer;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.transform.localEulerAngles = euler;
        Object.Destroy(part.GetComponent<Collider>());
        part.GetComponent<Renderer>().sharedMaterial = material;
        return part;
    }

    private static Transform Child(string name, Transform parent, Vector3 position,
        Vector3 euler, int layer)
    {
        Transform child = new GameObject(name).transform;
        child.gameObject.layer = layer;
        child.SetParent(parent, false);
        child.localPosition = position;
        child.localEulerAngles = euler;
        return child;
    }

    private static Material Material(string name, Color color, float smoothness,
        float metallic, bool emission = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = name, color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (emission && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.4f);
        }
        return material;
    }

    private static void DisableOldPart(Transform root, string name)
    {
        Transform oldPart = root.Find(name);
        if (oldPart != null) oldPart.gameObject.SetActive(false);
    }
}

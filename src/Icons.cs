using UnityEngine;

namespace IslandsOfValheim
{
    // Small icons drawn in code from shapes (signed distances: negative inside), with a soft dark edge like
    // the game's own: the anchor on the map at the sea trader's harbour, the waves of the Seafarer set bonus.
    static class Icons
    {
        const int Size = 64;

        static Sprite anchor, waves, house;

        // A house (its roof, its walls, a door), white as the anchor: the Brim village on the map.
        internal static Sprite House
        {
            get
            {
                if (house == null) house = Draw("IoV_HouseIcon", HouseShape, (x, y) => Color.white);
                return house;
            }
        }

        static float HouseShape(float x, float y)
        {
            float roof = Triangle(x, y, new Vector2(-0.78f, 0.02f), new Vector2(0.78f, 0.02f), new Vector2(0f, 0.72f));
            float walls = Box(x, y + 0.33f, 0.52f, 0.36f);
            float door = Box(x, y + 0.5f, 0.13f, 0.2f);
            return Mathf.Min(roof, Mathf.Max(walls, -door));
        }

        internal static Sprite Anchor
        {
            get
            {
                if (anchor == null) anchor = Draw("IoV_AnchorIcon", AnchorShape, (x, y) => Color.white);
                return anchor;
            }
        }

        // Three waves, white crests over sea blue.
        internal static Sprite Waves
        {
            get
            {
                if (waves == null)
                    waves = Draw("IoV_WavesIcon", WavesShape, (x, y) => Color.Lerp(new Color(0.16f, 0.5f, 0.78f), new Color(0.85f, 0.95f, 1f), Mathf.Clamp01(y * 0.6f + 0.55f)));
                return waves;
            }
        }

        static Sprite Draw(string name, System.Func<float, float, float> shape, System.Func<float, float, Color> colour)
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.name = name;
            Color[] pixels = new Color[Size * Size];
            float pixel = 2f / Size;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size * 2f - 1f, v = (y + 0.5f) / Size * 2f - 1f;
                    float d = shape(u * 1.12f, v * 1.12f) / 1.12f;
                    float fill = Mathf.Clamp01(0.5f - d / pixel);
                    float edge = Mathf.Clamp01(0.5f - (d - 0.08f) / pixel) * 0.7f;
                    float alpha = Mathf.Max(fill, edge);
                    Color c = alpha > 0f ? Color.Lerp(Color.black, colour(u, v), fill / alpha) : Color.clear;
                    c.a = alpha;
                    pixels[y * Size + x] = c;
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            return sprite;
        }

        // A ring, the shank, the stock, the arms and their flukes.
        static float AnchorShape(float x, float y)
        {
            float ring = Mathf.Abs(Length(x, y - 0.66f) - 0.13f) - 0.055f;
            float shank = Box(x, y + 0.02f, 0.06f, 0.56f);
            float stock = Box(x, y - 0.36f, 0.33f, 0.055f);
            float arc = Mathf.Abs(Length(x, y + 0.05f) - 0.55f) - 0.06f;
            arc = Mathf.Max(arc, y + 0.2f); // only the lower part of the circle
            float flukes = Mathf.Min(
                Triangle(x, y, new Vector2(0.68f, 0.02f), new Vector2(0.40f, -0.16f), new Vector2(0.64f, -0.36f)),
                Triangle(x, y, new Vector2(-0.68f, 0.02f), new Vector2(-0.64f, -0.36f), new Vector2(-0.40f, -0.16f)));
            return Mathf.Min(Mathf.Min(ring, shank), Mathf.Min(stock, Mathf.Min(arc, flukes)));
        }

        // Three wavy bands, the middle one a little shifted.
        static float WavesShape(float x, float y)
        {
            float d = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                float row = 0.48f - i * 0.48f;
                float phase = i == 1 ? 1.6f : 0f;
                d = Mathf.Min(d, Wave(x, y - row, phase));
            }
            return d;
        }

        // A sine band from x = -0.75 to 0.75 with rounded ends.
        static float Wave(float x, float y, float phase)
        {
            const float amplitude = 0.11f, k = 7.5f, half = 0.75f, thickness = 0.085f;
            float cx = Mathf.Clamp(x, -half, half);
            float curve = amplitude * Mathf.Sin(k * cx + phase);
            float slope = amplitude * k * Mathf.Cos(k * cx + phase);
            float along = Mathf.Abs(x) > half ? Mathf.Abs(x) - half : 0f;
            float across = (y - curve) / Mathf.Sqrt(1f + slope * slope);
            return Length(along, across) - thickness;
        }

        static float Length(float x, float y) { return Mathf.Sqrt(x * x + y * y); }

        static float Box(float x, float y, float hx, float hy)
        {
            float dx = Mathf.Abs(x) - hx, dy = Mathf.Abs(y) - hy;
            return Length(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)) + Mathf.Min(Mathf.Max(dx, dy), 0f);
        }

        static float Triangle(float x, float y, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            Vector2 p = new Vector2(x, y);
            Vector2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
            Vector2 v0 = p - p0, v1 = p - p1, v2 = p - p2;
            Vector2 q0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 q1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 q2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            float dist = Mathf.Min(Vector2.Dot(q0, q0), Mathf.Min(Vector2.Dot(q1, q1), Vector2.Dot(q2, q2)));
            float side = Mathf.Min(s * (v0.x * e0.y - v0.y * e0.x), Mathf.Min(s * (v1.x * e1.y - v1.y * e1.x), s * (v2.x * e2.y - v2.y * e2.x)));
            return -Mathf.Sqrt(dist) * Mathf.Sign(side);
        }
    }
}

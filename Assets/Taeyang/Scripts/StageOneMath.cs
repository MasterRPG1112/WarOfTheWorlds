using UnityEngine;

namespace Taeyang
{
    public static class StageOneMath
    {
        // Solve |relative + velocity*t| = speed*t, with a bounded fallback.
        public static Vector2 Predict(Vector2 origin, Vector2 target, Vector2 velocity, float speed, float maxLead)
        {
            speed = Mathf.Max(0.1f, speed);
            Vector2 relative = target - origin;
            float a = velocity.sqrMagnitude - speed * speed;
            float b = 2f * Vector2.Dot(relative, velocity);
            float c = relative.sqrMagnitude;
            float t = relative.magnitude / speed;
            if (Mathf.Abs(a) < 0.0001f)
            {
                if (Mathf.Abs(b) > 0.0001f && -c / b > 0) t = -c / b;
            }
            else
            {
                float discriminant = b * b - 4f * a * c;
                if (discriminant >= 0)
                {
                    float root = Mathf.Sqrt(discriminant);
                    float t1 = (-b - root) / (2f * a), t2 = (-b + root) / (2f * a);
                    if (t1 > 0 && t2 > 0) t = Mathf.Min(t1, t2);
                    else if (t1 > 0) t = t1;
                    else if (t2 > 0) t = t2;
                }
            }
            return target + velocity * Mathf.Clamp(t, 0, Mathf.Max(0, maxLead));
        }
    }
}

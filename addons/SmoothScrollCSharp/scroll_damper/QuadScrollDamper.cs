using Godot;
using System;

[Tool]
[GlobalClass, Icon("res://addons/SmoothScrollCSharp/scroll_damper/")]
public partial class QuadScrollDamper : ScrollDamper
{
    float _friction = 4.0f;
    float _factor = 10000.0f;

    /// <summary>
    /// Friction, not physical. 
    ///The higher the value, the more obvious the deceleration. 
    /// </summary>
    [Export(PropertyHint.Range, "0.001,10000.0,0.001,or_greater,hide_slider")]
    public float Friction
    {
        get
        {
            return _friction;
        }
        set
        {
            _friction = Mathf.Max(value, 0.001f);
            Factor = Mathf.Pow(10.0f, Friction) - 1.0f;
        }
    }

    /// <summary>
    /// Factor to use in formula.
    /// </summary>
    protected float Factor
    {
        get
        {
            return _factor;
        }
        set
        {
            _factor = Mathf.Max(value, 0.000000000001f);
        }
    }

    protected override float CalculateVelocityByTime(float time)
    {
        if (time <= 0.0f) return 0.0f;

        return time * time * Factor;
    }

    protected override float CalculateTimeByVelocity(float velocity)
    {
        return Mathf.Sqrt(Mathf.Abs(velocity) / Factor);
    }

    protected override float CalculateOffsetByTime(float time)
    {
        time = Mathf.Max(time, 0.0f);
        return 1.0f / 4.0f * Factor * time * time * time;
    }

    protected override float CalculateTimeByOffset(float offset)
    {
        return Mathf.Pow(Mathf.Abs(offset) * 3.0f / Factor, 1.0f / 3.0f);
    }
}

using Godot;
using System;

[Tool]
[GlobalClass, Icon("res://addons/SmoothScrollCSharp/scroll_damper/")]
public partial class ExpoScrollDamper : ScrollDamper
{
    float _friction = 4.0f;
    float _factor = 10000.0f;
    float _minimumVelocity = 0.4f;

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
            Factor = Mathf.Pow(10.0f, Friction);
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
            _factor = Mathf.Max(value, 1.000000000001f);
        }
    }

    /// <summary>
    /// Minimum velocity duh.
    /// </summary>
    [Export(PropertyHint.Range, "0.001,100000.0,0.001,or_greater,hide_slider")]
    public float MinimumVelocity
    {
        get
        {
            return _minimumVelocity;
        }
        set
        {
            _minimumVelocity = Mathf.Max(value, 0.001f);
        }
    }

    protected override float CalculateVelocityByTime(float time)
    {
        var minimumTime = CalculateTimeByVelocity(MinimumVelocity);
        if (time < minimumTime) return 0.0f;

        return Mathf.Pow(Factor, time);
    }

    protected override float CalculateTimeByVelocity(float velocity)
    {
        return Mathf.Log(Mathf.Abs(velocity)) / Mathf.Log(Factor);
    }

    protected override float CalculateOffsetByTime(float time)
    {
        return Mathf.Pow(Factor,time) / Mathf.Log(Factor);
    }

    protected override float CalculateTimeByOffset(float offset)
    {
        return Mathf.Log(offset * Mathf.Log(Factor)) / Mathf.Log(Factor);
    }

    public override float CalculateVelocityToDest(float from, float to)
    {
        var dist = to - from;
        var minTime = CalculateTimeByVelocity(MinimumVelocity);
        var minOffset = CalculateOffsetByTime(minTime);
        var time = CalculateTimeByOffset(Mathf.Abs(dist) + minOffset);
        var vel = CalculateVelocityByTime(time) * Mathf.Sign((int)dist);
        return vel;
    }
}

using Godot;
using System;
using System.Collections.Generic;

[Tool]
[GlobalClass, Icon("res://addons/SmoothScrollCSharp/scroll_damper/")]
public abstract partial class ScrollDamper : Resource
{

    float _reboundStrength = 7.0f;
    float _attractFactor = 400.0f;

    /// <summary>
    /// Rebound strength. The higher the value, the faster it attracts. 
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1.0,0.001,or_greater,hide_slider")]
    public float ReboundStrength
    {
        get
        {
            return _reboundStrength;
        }
        set
        {
            _reboundStrength = Mathf.Max(0.0f, value);
        }
    }

    /// <summary>
    /// Factor for attracting.
    /// </summary>
    public float AttractFactor
    {
        get
        {
            return _attractFactor;
        }
        set
        {
            _attractFactor = Mathf.Max(0.0f, value);
        }
    }

    protected virtual float CalculateVelocityByTime(float time) => 0.0f;

    protected virtual float CalculateTimeByVelocity(float velocity) => 0.0f;

    protected virtual float CalculateOffsetByTime(float time) => 0.0f;

    protected virtual float CalculateTimeByOffset(float offset) => 0.0f;

    public virtual float CalculateVelocityToDest(float from, float to)
    {
        var dist = to - from;
        var time = CalculateTimeByOffset(Mathf.Abs(dist));
        var vel = CalculateVelocityByTime(time) * Mathf.Sign(dist);
        return vel;
    }

    protected float CalculateNextVelocity(float presentTime, float deltaTime)
    {
        return CalculateVelocityByTime(presentTime - deltaTime);
    }

    protected float CalculateNextOffset(float presentTime, float deltaTime)
    {
        return CalculateOffsetByTime(presentTime)
            - CalculateOffsetByTime(presentTime - deltaTime);
    }

    /// <summary>
    /// Return the result of next velocity and position according to delta time
    /// </summary>
    /// <param name="velocity"></param>
    /// <param name="deltaTime"></param>
    /// <returns></returns>
    public float[] Slide(float velocity, float deltaTime)
    {
        var presentTime = CalculateTimeByVelocity(velocity);
        return new float[] {
            CalculateNextVelocity(presentTime, deltaTime) * Mathf.Sign(velocity),
            CalculateNextOffset(presentTime,deltaTime) * Mathf.Sign(velocity)
        };
    }

    public float Attract(float from, float to, float velocity, float deltaTime)
    {
        var dist = to - from;
        var targetVel = CalculateVelocityToDest(from, to);
        velocity += AttractFactor * dist * deltaTime 
            + CalculateVelocityByTime(deltaTime) * Mathf.Sign(dist);

        if ((dist > 0 && velocity >= targetVel) 
            || (dist < 0 && velocity <= targetVel))
        {
            velocity = targetVel;
        }

        return velocity;
    }

}
